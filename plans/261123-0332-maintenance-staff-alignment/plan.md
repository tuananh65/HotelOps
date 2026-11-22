# Maintenance Staff alignment — Project 2

Single agent; retain compact UX. No primary writes, grant/schema/entity/Staffing changes, Project 1 changes, or Git publication. Test writes only in freshly created backed-up fixture.

- [x] Read-only role audit: role 5, 13 actual grants; no INCIDENT_CREATE/IR_CREATE; INCIDENT_MANAGE/IR_PROCESS/TASK_EXECUTE exist.
- [x] Inspect assignment/execution/review/rework, Incident authorization, evidence, requests, Workday and notification contracts.
- [x] Implement personal maintenance workspace/dashboard/history and scope/ownership/capability fixes without new permissions or lifecycle.
- [x] Isolated API/RBAC/concurrency/workflow and real browser validation; regression builds/tests.
- [x] Inline review, docs/report/journal and restore needed local servers.

| FEATURE | CURRENT_UI | API | PERMISSION | OWNERSHIP | WORKFLOW | GAP | ACTION |
|---|---|---|---|---|---|---|---|
| Personal dashboard | Generic work cards | Personal only cleaning/task | MAINTENANCE_VIEW, ATTENDANCE_RECORD | Assigned only | Pending/executing/completed/closed | Maintenance KPI absent | Extend personal query with explicit maintenance mode |
| Maintenance tasks | Generic Work | Full execution | MAINTENANCE_EXECUTE | Current assignee | Accept/start/pause/progress/complete | Personal filters/modal/review context | Reuse Work, no parallel lifecycle |
| Decline | Absent | No transition | No separate contract | N/A | Assignment ACTIVE/COMPLETED/REASSIGNED/CANCELLED | No supported decline | BLOCKED; do not create status/action |
| Incident | Generic Work | INCIDENT_MANAGE overly broad actions | Actual MANAGE/VIEW, no CREATE/TRIAGE | Execution assigned only | Incident independent | Staff could coordinate under coarse grant | Require existing triage capability for coordination; preserve assigned execution |
| Review/rework | Lead workspace added | verify/reopen | Staff read only | Reopened work assigned | COMPLETED→reopen PENDING→execute | Staff notification/history/filter | Reuse events, notify actual assignee |
| Assets | Generic view | Scoped asset history | ASSET_VIEW | Branch read | Read only | Personal drawer profile | Reuse assets |
| MR / IR / tasks | Shared workspaces | Existing services | MR_CREATE/VIEW, IR_PROCESS/VIEW, TASK_EXECUTE/VIEW | Owner/assignee for writes | Separate lifecycles | Navigation/work-source selection | Shared tabs and bounded source lookup |
| Workday / inbox | Shared | OWN / recipient | ATTENDANCE_RECORD OWN | Actor | Existing | No specialist route | Reuse, no schedule/admin grant |

Contract blockers: Incident reporting and Internal Request creation lack grants; assignment decline unsupported. All independent permitted features continue.

## Final evidence

- Staff API: 27 grouped checks; final fixture HotelOpsCompactApiTest4eabd21e611e. Browser: 30 real-API responsive views, 10 successful browser mutations plus Lead reopen through fixture API, 0 page errors/failed API.
- Extended API regression 128; Lead regression 33 with worker integration 1; solution tests 28 pass/3 conditional skip; frontend workflow 5 pass; shared UX 48 route/viewport plus settings/Reception/Workday/dashboard suites and four prior role regressions passed.
- Frontend typecheck/build/targeted formatting passed. Backend build 0 warnings/errors. Primary API restored at 5768 with worker=false; existing Vite 5186 retained, fixture APIs stopped.
- Inline review fixed source-linked MR ownership/retry key, Incident candidate/coordination/handover capability, IR routing authority, evidence assignment, pending-review labeling, work history labels and nested modal errors.
- Overall PARTIAL against the entire proposed capability list: Decline, Incident Create and IR Create remain explicitly blocked by contract/grants. This checked plan means authorized independent implementation and validation are complete; it does not represent those blocked features as implemented.
- Full Vietnamese handoff: [report](../reports/implementation-261123-0332-maintenance-staff-alignment.md).
