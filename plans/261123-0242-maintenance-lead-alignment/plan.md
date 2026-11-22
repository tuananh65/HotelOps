# Maintenance Lead — Project 2

Single agent. CompactDB only; preserve existing UX, schema/entities/grants/Staffing and Project 1. All write validation in backed-up isolated fixture. No commit/push/deploy.

- [x] Source and read-only role audit: role 11, exact 14 grants.
- [x] Backend gaps: review/reopen capability, linked Incident→Maintenance creation, PM edit/history using current scheduler, scoped dashboard/technician/asset context.
- [x] Frontend: eight groups; reuse Work, Assets, Documents, Workday; PM and technician workspace; true dashboard.
- [x] Isolated API/RBAC/workflow/concurrency/PM tests and live browser flow: 33 checks, worker integration 1/1, 27 responsive real-API browser views.
- [x] Build/typecheck/unit/UI and regression validation; inline review/docs/report/journal/process reconciliation. Backend 0 warnings/errors; FE build and targeted formatting pass; .NET 28 pass/3 conditional skips; frontend workflow 5 pass; extended API 128 pass; prior-role UX regressions pass.

| FEATURE | CURRENT_UI | API | PERMISSION | WORKFLOW | GAP | ACTION |
|---|---|---|---|---|---|---|
| Maintenance assignment | Generic work | Work assign/candidates | MAINTENANCE_ASSIGN | PENDING→staff execution | Metadata/modal/coordinator profile | Reuse Work |
| Review | Generic verify | verify COMPLETED→CLOSED, reopen COMPLETED/CLOSED→PENDING | MAINTENANCE_REVIEW | Independent review | FE misses reopen at COMPLETED; assignment ended | Expose existing reopen then reassignment, retain history |
| Incident | Generic work | triage/escalate/link | Incident grants | Independent from Maintenance | No convenient linked task creation/idempotency | Atomic derive through existing Work create |
| PM | Template create/disable | Existing OperationsWorker | PM_MANAGE | Template occurrence→Maintenance | Edit/history/location validation absent | Extend existing template service, no new scheduler |
| Assets | Generic view | asset detail | ASSET_VIEW | Read only | Related maintenance/PM/location absent | Scoped detail context |
| Technician/shift | Missing workspace | Candidate list | MAINTENANCE_ASSIGN | Eligible execution grant | Shift/workload summary absent | Extend candidate service, no USER_VIEW/SHIFT_MANAGE |
| Dashboard | Generic operations | Generic insights | DASHBOARD_VIEW + domain grants | Separate incident/task/PM | Maintenance KPI absent | Scoped domain dashboard |
| Requests/notifications | Generic MR/inbox | Existing endpoints | MR_VIEW / recipient | Read only | Navigation grouping | Reuse components |

Known contract: rejection of maintenance result uses existing `reopen` to PENDING then new assignment; no separate REWORK lifecycle. PM worker remains locally disabled unless explicitly configured; test ticks use isolated fixture only.

Evidence: Maintenance fixture `HotelOpsCompactApiTest3cd42085fa72`; regression fixture `HotelOpsCompactApiTest2354e037944c`. Both newly created; no primary write testing. Report: `plans/reports/implementation-261123-0242-maintenance-lead-alignment.md`.

Review repaired EF DTO ordering translation, duplicate maintenance while pending review, review-filter reset, nested modal Escape, PM UTC/local edit drift, immutable PM identity/lengths, and completed-awaiting-review inclusion in open KPI. No independent-agent review (single agent requested).
