# Developer adapter and OpenHands execution

`DevelopmentAgentBase` implements C-Sweet callbacks. `AssignedDevelopmentService`,
`PersonalDevelopmentService`, `DevelopmentIntakeService`, `ProjectIntakeService`, and
`DevelopmentCoordinationService` retain platform workflows. `IDevelopmentExecutionEngine`
is the engine-neutral seam. `OpenHandsDeveloperProfile` supplies identity and role instructions.

`OpenHandsExecutionEngine` launches a pinned Python worker inside the existing certified Linux
development guest. The subprocess terminal uses OpenHands' terminal action contract with C-Sweet's
guarded, stateless subprocess executor. This preserves deadlines, actual exit-code receipts,
resource handoff and publication evidence without requiring tmux. File editing and task tracking
use upstream OpenHands executors. A clean conversation working directory and private home avoid
ambient repository/user plugin loading; source access is explicit and confined by tool adapters.

`ModelBridge` binds an ephemeral loopback endpoint to the originating .NET execution context.
It translates Chat Completions to the SDK's `IChatClient`, including streaming/tool identities,
reasoning, generation bounds and usage. Only a temporary local bridge credential crosses to Python.
No provider, workload, session or lease credential is copied. C-Sweet's inference cancellation and
acknowledged waiting remain authoritative; the worker has no separate model HTTP deadline or fallback.

The stdio protocol has a version and frame bound. SDK capabilities use typed .NET functions; Python
never receives private MCP transport. Upstream tool execution is serialized. Each persisted event
waits for the parent's durable checkpoint acknowledgement before execution advances.
The callback also captures `conversation.state.model_dump_json()` through OpenHands' public
serialization API. Upstream batches its base-state write until a step exits; this paired snapshot
retains the matching branch head when an action is interrupted inside that step. Restoration
materializes that snapshot before constructing the conversation and rejecting pending actions.

`PlatformExecutionCheckpointClient` stores engine state separately from source. Core retains
protected immutable blobs and atomically advances metadata in its existing revisioned operating-state
table. Larger transfers use bounded chunks within Office's existing 16 MiB frame limit. Partial
uploads are not resume points. The SDK, Core and adapter verify versions, revisions, archive safety
and content digests. Restoring requires current work/assignment authorization.

OpenHands' public `reject_pending_actions` reconciles interrupted actions before continuation;
unconfirmed shell commands are never replayed automatically. A durable turn receipt avoids repeating
completed inference when a later platform mutation is retried. Source and private metadata restore
together. `.csweet` state, transcripts and bridge credentials cannot enter source publication.

`ManagerResponseRecovery` correlates a persisted question with the exact authenticated answer,
current ticket/stage and unchanged planning revision. Resolution decisions are persisted before
governed retry, so interrupted retry responses do not repeat inference. Scope revisions and approvals
remain explicit manager decisions. Discussion events are wake hints; bounded attention recovery
handles missed notifications. Source publication still requires current-source validation receipts.

Questions retain either an agent installation identity or a human employee identity. Human answers
must have `OrganizationUser` authorship and the exact question/revision correlation. Agent recipients
are also re-read from the current available roster, including assigned specialists. The workboard
Reply control records this correlation through the existing authorized comment mutation and durable
discussion events. Edited/deleted questions, edited answers, changed managers, revoked availability,
scope changes and unresolved decisions cannot trigger automatic stage retry.

When a tool requests a wait, `OpenHandsExecutionEngine` acknowledges the completed tool observation's
checkpoint before stopping at the following progress or action boundary. An unresolved pause blocks
worker startup on duplicate callbacks. The real worker regression restores that observation after
discarding its local runtime, without posting the question again.

Research uses `PlatformResearchClient` and the platform's startup-managed SearXNG service.
Page reading reuses public address, connection pinning and redirect policy checks. No paid search
service or provider key is required. Existing model and infrastructure costs remain separate.
