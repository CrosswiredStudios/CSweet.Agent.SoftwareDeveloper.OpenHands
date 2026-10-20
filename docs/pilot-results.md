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

Platform PR: https://github.com/CrosswiredStudios/C-Sweet/pull/11. Local isolated platform verification
passed, including 91 protocol/schema/research/checkpoint cases and six approved-definition hiring
cases; its CI restore is independently blocked by the baseline pin to unpublished
`CSweet.Memory.Broker` 0.5.1 (NuGet currently supplies 0.5.0). Local verification packed the unchanged
0.5.1 source without publishing or modifying it.
