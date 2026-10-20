"""Versioned stdio adapter for the unmodified OpenHands Software Agent SDK.

stdout is exclusively protocol traffic. SDK logging and third-party prints go to
stderr, which the parent drains without copying raw content into activity logs.
"""
from __future__ import annotations

import hashlib
import json
import os
import sys
import threading
import uuid
from pathlib import Path

PROTOCOL = 1
MAXIMUM_FRAME = 2 * 1024 * 1024
_output = sys.stdout
sys.stdout = sys.stderr
_gate = threading.RLock()
_phase = "startup"

from openhands.sdk.tool import Observation


class BridgeObservation(Observation):
    """A module-level schema so persisted OpenHands events can be deserialized."""


def rpc(kind: str, **payload):
    with _gate:
        line = json.dumps({"protocol": PROTOCOL, "kind": kind, **payload}, ensure_ascii=False)
        if len(line) > MAXIMUM_FRAME:
            raise RuntimeError("Worker frame exceeds its bound")
        _output.write(line + "\n")
        _output.flush()
        raw = sys.stdin.readline(MAXIMUM_FRAME + 1)
        if not raw or len(raw) > MAXIMUM_FRAME:
            raise RuntimeError("Missing or oversized adapter response")
        response = json.loads(raw)
        if response.get("protocol") != PROTOCOL or not response.get("ok"):
            raise RuntimeError("C-Sweet stopped this worker action")
        return response.get("result")


def main():
    global _phase
    raw = sys.stdin.readline(MAXIMUM_FRAME + 1)
    if not raw or len(raw) > MAXIMUM_FRAME:
        raise RuntimeError("Missing or oversized worker request")
    request = json.loads(raw)
    if request.get("protocol") != PROTOCOL:
        raise RuntimeError("Unsupported protocol")
    import importlib.metadata
    if any(importlib.metadata.version(package) != "1.56.0" for package in ("openhands-sdk", "openhands-tools")):
        raise RuntimeError("Incompatible OpenHands runtime")

    _phase = "imports"
    from pydantic import SecretStr
    from openhands.sdk import LLM, Agent, Conversation
    from openhands.sdk.context import AgentContext
    from openhands.sdk.context.condenser import LLMSummarizingCondenser
    from openhands.sdk.event import ActionEvent, MessageEvent, ObservationEvent
    from openhands.sdk.conversation.persistence_const import BASE_STATE
    from openhands.sdk.security import NeverConfirm
    from openhands.sdk.tool import Action, Observation, Tool, ToolDefinition, ToolExecutor, register_tool
    from openhands.tools.file_editor.definition import FileEditorAction, FileEditorObservation, TOOL_DESCRIPTION
    from openhands.tools.file_editor.impl import FileEditorExecutor
    from openhands.tools.terminal.definition import TerminalAction
    from openhands.tools.task_tracker import TaskTrackerTool

    source = Path(request["workspace"]).resolve()
    directory = Path(request["directory"]).resolve()
    persistence = directory / "engine"
    scratch = directory / "runtime"
    scratch.mkdir(parents=True, exist_ok=True)

    class PlatformExecutor(ToolExecutor):
        def __init__(self, name):
            self.name = name

        def __call__(self, action, conversation=None):
            # Drop SDK-internal discriminator/risk fields, retaining only declared arguments.
            args = action.model_dump(mode="json", by_alias=True, exclude={"kind", "security_risk", "summary"}, exclude_none=True)
            value = rpc("tool", name=self.name, arguments=args)
            return BridgeObservation.from_text(json.dumps(value, ensure_ascii=False))

    specs = []
    for spec in request["tools"]:
        if spec["name"] == "execute_workspace_command":
            continue
        action_type = Action.from_mcp_schema("CSweetAction_" + spec["name"], spec["schema"])

        def create_tool(cls, conv_state, _spec=spec, _action=action_type, **params):
            return [cls(description=_spec["description"], action_type=_action,
                        observation_type=BridgeObservation, executor=PlatformExecutor(_spec["name"]))]

        tool_class = type("CSweetTool_" + spec["name"], (ToolDefinition,),
                          {"name": spec["name"], "__module__": __name__, "create": classmethod(create_tool)})
        register_tool(spec["name"], tool_class)
        specs.append(Tool(name=spec["name"]))

    class TerminalExecutor(ToolExecutor):
        def __call__(self, action, conversation=None):
            if action.is_input or action.reset or not action.command.strip():
                return BridgeObservation.from_text("Use one standalone command. Interactive sessions and terminal reset are unavailable.", is_error=True)
            value = rpc("tool", name="execute_workspace_command", arguments={"command": action.command})
            return BridgeObservation.from_text(value if isinstance(value, str) else json.dumps(value))

    class CSweetTerminalTool(ToolDefinition[TerminalAction, BridgeObservation]):
        name = "terminal"

        @classmethod
        def create(cls, conv_state, **params):
            return [cls(description="Run one command in a fresh subprocess rooted at " + str(source) +
                        ". Five-minute hard deadline; no interactive input. C-Sweet enforces policy and records real validation receipts.",
                        action_type=TerminalAction, observation_type=BridgeObservation, executor=TerminalExecutor())]

    class ConfinedEditorExecutor(FileEditorExecutor):
        def __call__(self, action, conversation=None):
            rpc("gate")
            path = Path(action.path)
            resolved = (path if path.is_absolute() else source / path).resolve()
            if not resolved.is_relative_to(source) or ".git" in resolved.relative_to(source).parts:
                return FileEditorObservation.from_text("File access must remain inside the source workspace.", command=action.command, is_error=True)
            return super().__call__(action.model_copy(update={"path": str(resolved)}), conversation)

    class CSweetFileEditorTool(ToolDefinition[FileEditorAction, FileEditorObservation]):
        name = "file_editor"

        @classmethod
        def create(cls, conv_state, **params):
            return [cls(description=TOOL_DESCRIPTION + "\nSource root: " + str(source),
                        action_type=FileEditorAction, observation_type=FileEditorObservation,
                        executor=ConfinedEditorExecutor(workspace_root=str(source)))]

    register_tool("csweet_terminal", CSweetTerminalTool)
    register_tool("csweet_file_editor", CSweetFileEditorTool)
    specs += [Tool(name="csweet_terminal"), Tool(name="csweet_file_editor"), Tool(name=TaskTrackerTool.name)]
    _phase = "model_setup"
    llm = LLM(model="openai/csweet-broker", base_url=request["baseUrl"], api_key=SecretStr(request["bridgeToken"]),
              usage_id="development", max_input_tokens=request["contextTokens"],
              max_output_tokens=request["outputTokens"], num_retries=0, log_completions=False,
              reasoning_effort=None, reasoning_summary=None,
              timeout=None, stream_idle_timeout=None,
              api_mode="chat", disable_vision=True, caching_prompt=False,
              capability_overrides={"supports_vision": False, "supports_prompt_cache": False, "supports_responses_api": False})
    agent = Agent(llm=llm, tools=specs, tool_concurrency_limit=1,
                  persona=request.get("instructions") or "Implement only C-Sweet's authorized assignment.",
                  agent_context=AgentContext(load_user_skills=False, load_public_skills=False, load_project_skills=False),
                  condenser=LLMSummarizingCondenser(llm=llm.model_copy(update={"usage_id": "condenser"}),
                                                  max_size=120, max_tokens=request["contextTokens"] - request["outputTokens"], keep_first=2))
    receipt_path = directory / "adapter-turn.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8")) if receipt_path.exists() else {}
    turn = hashlib.sha256(request["prompt"].encode()).hexdigest()
    if receipt.get("turn") == turn and receipt.get("completed"):
        _output.write(json.dumps({"protocol": PROTOCOL, "kind": "completed", "text": receipt["text"]}) + "\n")
        _output.flush()
        return

    final = {"text": ""}
    state_snapshot = directory / "conversation-state.json"
    conversation = None

    def save_current_state():
        # The public callback runs after the event is persisted, but upstream batches
        # its base-state write until the surrounding step exits. Capture its public
        # serialization too, so a crash retains the matching branch HEAD and event.
        state_snapshot.write_text(conversation.state.model_dump_json(exclude_none=True), encoding="utf-8")

    def checkpoint(event):
        if isinstance(event, ActionEvent) and event.tool_name == "finish":
            final["text"] = event.action.message
        elif isinstance(event, MessageEvent) and event.source == "agent":
            final["text"] = "".join(getattr(part, "text", "") for part in event.llm_message.content)
        save_current_state()
        # This synchronous acknowledgement prevents the next action/inference
        # from escaping before the event and matching state are durable.
        rpc("checkpoint")
        if isinstance(event, ObservationEvent):
            rpc("progress")

    conversation_id = uuid.uuid5(uuid.NAMESPACE_URL, request["session"])
    had_state = (persistence / conversation_id.hex).exists()
    if had_state and state_snapshot.exists():
        (persistence / conversation_id.hex / BASE_STATE).write_text(state_snapshot.read_text(encoding="utf-8"), encoding="utf-8")
    _phase = "conversation_setup"
    conversation = Conversation(agent=agent, workspace=str(scratch), persistence_dir=str(persistence),
                                conversation_id=conversation_id, callbacks=[checkpoint],
                                max_iteration_per_run=request["maximumIterations"], visualizer=None,
                                plugins=[])
    conversation.set_confirmation_policy(NeverConfirm())
    if had_state:
        conversation.reject_pending_actions("Interrupted action: do not replay it. Reinspect current source and reconcile external effects through C-Sweet receipts.")
    receipt_path.write_text(json.dumps({"turn": turn, "completed": False}), encoding="utf-8")
    conversation.send_message(request["prompt"])
    _phase = "execution"
    conversation.run()
    if not final["text"].strip():
        raise RuntimeError("OpenHands ended without a final implementation report")
    receipt_path.write_text(json.dumps({"turn": turn, "completed": True, "text": final["text"]}), encoding="utf-8")
    save_current_state()
    rpc("checkpoint")
    _output.write(json.dumps({"protocol": PROTOCOL, "kind": "completed", "text": final["text"]}, ensure_ascii=False) + "\n")
    _output.flush()


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # Errors are deliberately generic at this boundary; neither credentials nor
        # model/tool payloads belong in process logs or ticket activity.
        if "--diagnostics" in sys.argv:
            import traceback
            traceback.print_exc(file=sys.stderr)
        _output.write(json.dumps({"protocol": PROTOCOL, "kind": "failed", "code": type(error).__name__, "phase": _phase}) + "\n")
        _output.flush()
        sys.exit(1)
