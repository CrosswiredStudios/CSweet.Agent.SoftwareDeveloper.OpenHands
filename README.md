# OpenHands Developer

An isolated OpenHands software developer pilot that plans, implements, tests and repairs authorized
software work while C-Sweet owns tickets, grants, approvals, source publication and delivery.

## Contract

- Agent ID: `com.csweet.software-developer.openhands`
- Version: `0.1.7`
- Provides: `software-development.implement.v1`
- Activation: OnDemand; one concurrent job; requires an assigned project.
- Packages: `CSweet.Agent.Development`, `CSweet.Agent.OpenHands`, and `CSweet.Agent.SoftwareDeveloper.OpenHands`, each 0.1.7.
- Runtime: existing certified Linux development guest, .NET 10 and Python 3.12. OpenHands SDK/tools are pinned to 1.56.0.
- Requested capabilities cover the implemented work, communication, repository, compute, research and private checkpoint workflows in the reviewed manifest. Declarations are not grants.
- Network: reviewed public runtime egress for dependency setup; public page reading follows the separately reviewed web policy.

Daniel and Adrian remain unchanged. This agent is explicitly selected for pilot work. See
[architecture](docs/architecture.md), [feature coverage and live acceptance](docs/pilot-acceptance.md),
and [third-party notices](THIRD-PARTY-NOTICES.md).

Assigned ticket execution can ask a current human manager or authorized agent teammate for
clarification. A human uses the workboard's Reply control to bind an answer to the exact question
revision. The adapter rechecks recipient availability, current management/assignment and unchanged
scope before requesting a governed stage retry. Replies never approve work or expand grants.

Personal-plan authoring also exposes typed ticket reads and factual comments confined to its current
development board. Every target is re-read before use; comment receipts use stable workspace/content
keys. Source notes cannot substitute for requested platform ticket updates.

## Develop

On Linux, prepare the pinned test environment once:

```bash
python3.12 -m venv .test-runtime
.test-runtime/bin/python -m pip install --require-hashes -r src/CSweet.Agent.OpenHands/worker/build-requirements.lock
.test-runtime/bin/python -m pip install --require-hashes --no-build-isolation -r src/CSweet.Agent.OpenHands/worker/requirements-linux.lock
export CSWEET_TEST_OPENHANDS_PYTHON="$PWD/.test-runtime/bin/python"
dotnet test
dotnet run --project src/CSweet.Agent.SoftwareDeveloper.OpenHands -- --self-test
```

The tests need no provider credentials or running C-Sweet instance. The real worker integration
test requires the pinned Python environment and a local scripted model endpoint; it does not skip
silently. On Windows, use an equivalent test virtual environment and set its full Python path in
`CSWEET_TEST_OPENHANDS_PYTHON`. Production execution remains inside the certified Linux guest.

## Install

Publish the SDK/platform prerequisites first. Import the versioned GitHub release through C-Sweet's
normal Installed Agents flow, review its source and grants, choose the provider/model, and assign
it to a disposable pilot project. Runtime dependency setup is automatic; users do not install Python
or configure a second model credential. Research uses C-Sweet's startup-managed SearXNG service.

The worker leaves reasoning effort and reasoning summaries to C-Sweet's selected provider/model
defaults. It passes the full configured context window to OpenHands and preserves the configured
output budget. Worker or provider failures stop as operational blockers without consuming personal
plan code-repair attempts. Diagnostics expose only known failure categories and worker phases;
raw exception messages, credentials and model/tool payloads stay out of ticket activity.

Release assets include independently packaged `.nupkg` files and the verified Linux `.csab`.
Shared-package consumers can use the downloaded packages as a local feed. Never patch approved
package records, stored digests, manifests or installed payloads to apply source changes.

Built with `CSweet.Agent.SDK` 5.1.0.

## Business calendars

Use context.Platform.Calendar.ReadAsync, CreateAsync, UpdateAsync, CancelAsync, or
ScheduleAsync with the typed work-management calendar contracts. Requests are bound to the
runtime business and employee; permission declarations require upgrade review.
Use local wall-clock dates without offsets, an explicit time zone, stable creation keys, and
the last observed revision for edits. Contributors edit their own events; managers can edit all.
Scheduling others follows the reporting hierarchy and never expands execution authority.

Subscribe to com.csweet.calendar.reminder-due.v1 and override HandleCalendarReminderAsync
when role-specific reminder behavior is needed. The default callback reports receipt; scheduled
work is delivered separately through the existing personal work queue.
Calendar.WithToolsAsync(options) adds only approved calendar model tools and operating guidance
to an existing harness. Calendar.GetResponseAsync(client, messages, ...) supplies a bounded
function-invocation loop for simple agents. Preserve all existing execution and approval rules.

## Shared delegation

SDK 5.1.0 supplies typed delegation and work tracking with shared dispatch, durable reporting receipts, current access checks and bounded wake recovery. Follow the SDK docs/delegation-with-tracking.md guide. Delegation does not grant execution authority; memory does not own outstanding obligations.
