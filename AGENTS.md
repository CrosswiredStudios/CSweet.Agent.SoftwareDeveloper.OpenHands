# OpenHands Developer repository instructions

This repository contains one standalone C-Sweet protocol-v2 agent. Its purpose is:

> Implements authorized software work through OpenHands while C-Sweet owns assignments, grants, approvals and delivery.

## Invariants

- Keep `com.csweet.software-developer.openhands` and version `0.1.4` synchronized between agent code,
  `csweet-plugin.json`, tests, and releases.
- The root manifest is the reviewed authority request. Keep `provides`, `requires`, events,
  configuration, credentials, web access, and UI contributions synchronized with implementation
  and tests.
- Request the minimum authority needed. Manifest declarations never grant access.
- Use typed callbacks and `AgentRuntimeContext.Platform`. Do not implement MCP/JSON-RPC, access
  runtime/workload/lease tokens, connect directly to databases or Docker, or handle provider
  credentials.
- Agent work is delivered at least once. Honor cancellation and use stable domain idempotency keys
  for external mutations.
- Keep workflow code in `CSweet.Agent.Development`, engine code in `CSweet.Agent.OpenHands`, and role identity in the executable agent. Never add OpenHands dependencies to C-Sweet's SDK or reference sibling agent source.
- Pin upstream SDK/tools and every Python dependency with hashes. Runtime setup may use the existing approved guest tools and public egress; never bypass Office or introduce Docker execution into the agent.
- Private checkpoint archives never belong in source snapshots or published code. Bind recovery to current work and assignment authority. Stop on checkpoint failure, cancellation, pending manager questions and uncertain command cleanup.
- Publish agent changes through versioned releases and Installed Agents. Do not patch approved records, manifests, digests or installed payloads. Bump versions before writing matching release notes.
- Unknown capabilities and events must fail or be ignored safely without leaking sensitive data.

## Verification

Run from the repository root:

```powershell
python3.12 -m venv .test-runtime
.test-runtime/bin/python -m pip install --require-hashes -r src/CSweet.Agent.OpenHands/worker/build-requirements.lock
.test-runtime/bin/python -m pip install --require-hashes --no-build-isolation -r src/CSweet.Agent.OpenHands/worker/requirements-linux.lock
export CSWEET_TEST_OPENHANDS_PYTHON="$PWD/.test-runtime/bin/python"
dotnet test
dotnet run --project src/CSweet.Agent.SoftwareDeveloper.OpenHands -- --self-test
```

Any new capability, grant, event, configuration field, credential, or network rule requires a
manifest update, a README explanation, and tests.
