# Pilot acceptance and comparison

The pilot is isolated from Daniel and Adrian. It is not eligible for a broad replacement decision
until released-package installation and the live scenarios below have retained evidence.

| Feature | Implementation | Automated evidence |
|---|---|---|
| Assigned implementation and validation | `AssignedDevelopmentService`, `WorkspaceValidation` | Outcome, dependency, rework and validation regressions; real OpenHands worker test |
| Review, publication and restart recovery | `AssignedDevelopmentService`, `PersonalDevelopmentService` | ReviewDelivery, NoChangeRework, PlanCompletionRecovery tests |
| Planning and personal delivery | `DevelopmentPlanningService`, `PersonalDevelopmentService` | StagedPlanning, PlanningExecution, deployment recovery tests |
| Direct chat and project intake | `DevelopmentIntakeService`, `ProjectIntakeService` | ComputeChat and onboarding fixtures; live direct-chat journey required |
| Directed discussion and manager escalation | `TicketDiscussionTools`, `Discussion`, `ManagerResponseRecovery`, `DevelopmentCoordinationService` | ManagerResponseRecovery tests; live manager/specialist round trip required |
| Build readiness | `DeliveryScopeExecution` | BuildReadiness tests |
| Progress and command deadlines | `WorkspaceProgress`, `BoundedWorkspaceShell` | WorkspaceShell tests; real worker tool test |
| Durable engine sessions | `OpenHandsExecutionEngine`, Core `ExecutionCheckpointStore` | Real worker restoration; protected storage, chunking, authorization and cleanup tests |
| Free research | `PlatformResearchClient`, Core `ResearchCapabilityHandler` | Real worker typed search; parsing/extraction/policy and grant tests |
| Calendar, delegation and tracking | SDK callback/tool composition | SDK suites; live onboarding/delegation/reminder scenario required |

Run the following in a disposable C-Sweet business/project using the published agent and normal
grant/configuration/assignment flows. Save ticket, stage/attempt, checkpoint, source candidate and
independent review references for each result:

1. Clean startup and installation; automatic Python environment preparation without manual setup.
2. Implement a small approved feature, research an unfamiliar documented API, run focused tests,
   publish through C-Sweet and obtain independent review of the exact candidate.
3. Repair a real failing test and reviewer-requested change, preserving acceptance criteria.
4. Interrupt the runtime after edits and after a tool/model result; replace/reconnect and recover
   without duplicate comments, publication, deployment or unconfirmed command replay.
5. Ask an assigned manager/specialist a blocking question. Verify visible discussion and governed
   resume after a current authorized answer. Reject stale replies, changed scope and revoked grants.
6. Complete direct-chat intake and a personal plan through task reviews, integration validation,
   preview/deployment readiness and a running test link, with no self-approved QA claims.
7. Exercise calendar reminders, delegation/tracking and aggregate build readiness.

Compare equivalent tasks with Daniel using the same provider/model/settings. Record completion,
independent review outcomes, interventions, duration, SDK/provider token usage and recovery results.
OpenHands' local cost estimate for the broker alias is not authoritative billing evidence.
Do not assign both agents to the same mutable workspace. Adopt only by explicit assignment;
rollback directs future work to the existing developer and preserves unfinished pilot evidence.

Live results belong in `docs/pilot-results.md` after execution. Automated tests alone are not live acceptance.
