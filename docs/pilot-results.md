# OpenHands pilot evidence

Status on 2026-10-20: releases and normal installation verified; live development acceptance remains
pending the existing C-Sweet Linux compute host permission prerequisite. No broader adoption decision
has been made.

## Released and installed

- SDK 5.1.0 published to NuGet and GitHub; 538 SDK, two sample and seven generated-template tests passed.
- All three pilot packages released at 0.1.0, followed by maintenance release 0.1.1. Linux/Python 3.12 CI
  passed 232 tests, including actual OpenHands 1.56.0 execution, research tool invocation, persistence,
  restoration and interrupted-action recovery. Windows verification also passed 232 tests.
- Normal Installed Agents import of 0.1.0 used the existing source-build fallback because the release
  lacked the installer's exact bundle checksum sidecar. Historical release assets were preserved.
  Release 0.1.1 added that sidecar; normal update downloaded, verified and sealed the prebuilt release
  on attempt one. No approved package records, artifact digests or installed payloads were patched.
- Maintenance release 0.1.2 published all three packages and the verified Linux bundle with its exact
  checksum sidecar. All 234 agent tests passed locally and in CI, including operational startup/exit
  regression tests. The independent package consumer and agent self-test passed. The normal targeted
  Installed Agents update then installed 0.1.2 on attempt one, with download, digest verification and
  immutable package sealing all complete (2026-10-20 01:05 America/Los_Angeles).
- The shared adapter now also carries the subsequently published Daniel 2.1.2 shell-policy recovery
  behavior. Four additional tests cover denied-command feedback, absent validation receipts, bounded
  escalation, source preservation and permitted continuation, including actual pinned worker execution.
  This closes a workflow difference before any harness performance comparison.
- Release 0.1.3 passed all 238 tests locally and in Linux/Python 3.12 CI, package verification,
  independent consumer and self-test. Its normal targeted update installed the prebuilt bundle on
  attempt one with download, digest verification and sealing complete (2026-10-20 01:16
  America/Los_Angeles). This is the current installed OpenHands pilot version.
- OpenHands Developer 0.1.1 was hired through the normal catalog review with Local vLLM qwen3.8-27b,
  256,000 context tokens and 128,000 maximum output tokens. Its reviewed public internet grant is Allowed.

## Live references

- Disposable business: `bff47f33-93d4-437f-9042-d7322fbec22c` (OpenHands Developer Pilot).
- OpenHands employee: `d6f25ba7-eab2-4d4f-88de-889cf9eb1834`.
- Disposable project: `d8784bf6-4876-49e9-9ded-5510ae972c4a` (Slug Utility — OpenHands Pilot).
- Approved hire workflow: `b633a604-b27f-47d3-8fee-bc86b5f54024`.
- Retained direct intake: `ee613655-863c-4e91-92f6-c66823fe2b8b` (Slug Normalizer Module with Tests).
- Informational initiative: `PD8784BF648-1`; no execution authority was inferred from its creation.
- Direct-work personal ticket prefix: `7B3755E2`; visible Ready and unclaimed while compute waits.
- Baseline project: `743d0b9a-81ce-45ee-be99-e48c4ada54ce` (Slug Utility — Daniel Baseline).
- Baseline direct intake: `636cda27-c0a3-48a4-b41c-e7a6d70e05e6` (Implement normalizeSlug module with tests).
- Published Daniel 2.1.2, commit `38eb2d1ca426328931f950e287cf0d4acdd5cb50`, installed normally for
  a separate baseline employee with matching model/token/repair settings. Daniel and Adrian source,
  versions, historical releases and existing installations were not modified.

Onboarding completed and direct chat successfully retained the request, discovered the existing
project, verified assignment and started the intake. The Compute page then reported “Administrator
permission is needed” and “Waiting agents remain queued until Linux preparation recovers.” No manual
Python configuration or Office image modification has been performed.

## Directed discussion and review feedback

Two owner-directed requests on initiative `PD8784BF648-1` produced two correlated agent replies,
with four comments total after explicit refresh. Request identities were
`f851852a-0c6f-42ec-9e95-43f7be82ddef` revision 1 and
`f43cacf4-f76c-4ea2-ab72-6c2af83f3a9f` revision 1.

The first reply proposed useful NFKD examples but incorrectly treated existing capability grants
as proof of runtime readiness and limited combining-mark removal to U+0300–U+036F. Review feedback
corrected both points and reiterated that comments cannot grant execution authority. The second
reply explicitly retracted the readiness claim, acknowledged the administrator prerequisite, and
changed the test plan to Unicode `\\p{M}` including a U+1AB0 case. Source work remained unclaimed.
This verifies directed discussion and response to review feedback, not source implementation,
source reviewer rework or manager-question governed resume.

## Acceptance and comparison

Actual published-agent coding, Python bootstrap in the live VM, source publication/independent review,
test repair, reviewer rework, runtime recovery, manager-question/resume, deployment, reminders and
delegation still require live completion. Automated coverage is not substituted for these scenarios.
Completion rate, review outcomes, intervention counts, duration and authoritative token usage are not
yet available for a fair Daniel comparison. No performance advantage is claimed.

The module-only live request explicitly excludes deployment. Existing platform
`PersonalWorkPlanning.ValidateRequest` and shared `DevelopmentPlanningService.ValidateDraft` require
a final validation/deployment pair for personal plans. The live evaluation must verify that this scope
conflict becomes a manager decision rather than silently adding a deployment or Docker deliverable.
No scope expansion or deployment has been approved or performed for the module request.

Platform PR: https://github.com/CrosswiredStudios/C-Sweet/pull/11. Local isolated platform verification
passed, including 91 protocol/schema/research/checkpoint cases and six approved-definition hiring
cases; its CI restore is independently blocked by the baseline pin to unpublished
`CSweet.Memory.Broker` 0.5.1 (NuGet currently supplies 0.5.0). Local verification packed the unchanged
0.5.1 source without publishing or modifying it.

## Compute preparation recovery (2026-10-20)

The owner approved the initial Windows elevation prompt. Linux image preparation completed,
including Ubuntu installation and reusable-image finalization. Setup then stopped at
`compute_checks_failed`: `ComputeResultDispatcherTests.Cursor_passes_a_full_page_of_expired_results_to_deliver_fresh_evidence`
encountered Windows access denied while atomically replacing its temporary replay journal.
All 625 existing compute tests passed on rerun, consistent with an intermittent file-access failure.

Platform commit `d842db79` adds bounded retries for Windows rename access/sharing failures while
retaining the journal lock, rechecking protected paths and preserving the previous journal.
It never retries a physical effect. Persistent lock and cancellation still fail closed. All
628 compute tests passed from the clean isolated checkout, including three new lock scenarios.
The normal Compute UI retry reused the completed image and finished at 08:39:50 America/Los_Angeles.
Its own installer acceptance report passed all 628 tests, and the business-scoped compute service
is Running. The Compute page showed Ready and automatically requested separate 2-CPU/2-GiB
instances for OpenHands and Daniel. Live source execution remains unverified.

## First live planning attempts after compute readiness

Both queued requests were claimed automatically after their separate compute instances became
ready. Daniel's first attempt stopped safely during planning after three invalid-JSON responses
(the final parser diagnostic reported an unclosed object/array). No Daniel source or package was
changed. OpenHands completed a 26-ticket personal plan, then stopped at the first child's source
workspace preparation with an authorization denial. It had not executed the OpenHands coding
worker or published source at this point.

Both retained intake descriptions still narrowed combining marks to U+0300–U+036F. Matching owner
clarifications were posted on the actual execution tickets, requiring all Unicode Mark characters
and an additional U+1AB0 test. OpenHands task descriptions subsequently included that correction,
although one earlier story outline retained the older range. This intake inconsistency is a live
limitation, not a completed correctness check.

Platform commit `c5b2cc6d` routes project personal-plan children using their server-written
`PersonalPlan` metadata through the existing personal-assignment checks. Children use the live claim on their coordinator; their retained chat references alone do not identify that lease. The previous router
mistook them for orchestration tasks. All 33 related Git/planning tests passed; regressions cover
personal and project boards, distinct branches, current child/coordinator assignment, expired
claims, revoked grants and disabled installations. The isolated AgentHost build was applied through
an Aspire-controlled stop/restart with the prior assembly retained. Normal workboard Retry actions
returned the child and coordinator to Ready. No grants, approved payloads or package records were
patched.

The OpenHands plan acknowledges the no-deployment conflict but includes a final task named
`[BLOCKED-PENDING-RESOLUTION] Deployment per planning contract`. Its name is not proof of a platform
block or approval. No deployment has run, and this scenario remains unaccepted pending verification
that the adapter stops for an authorized manager decision before expanding the requested scope.

The first normal child retry changed its board position. The adapter then selected a later task by
board rank, and C-Sweet correctly rejected skipping unfinished dependencies. Platform commit
`91aa0b6c` makes plan response ranks reflect server-written `PersonalPlan.Order` while preserving
board ranks. All 112 related personal-work service, planning and Git tests passed, including a
reordered-first-task regression and continued rejection of skipping it. The isolated planning
service and AgentHost assemblies were applied through another controlled Aspire restart; the
OpenHands coordinator was retried normally again. These platform restarts are pilot interventions,
not evidence of successful OpenHands conversation restoration.

Daniel's normal planning retry created a plan, then stopped at workspace synchronization after the
platform restart with a current-claim authorization denial. Source delivery remains incomplete for
both agents. No completion-rate or performance advantage is claimed.
