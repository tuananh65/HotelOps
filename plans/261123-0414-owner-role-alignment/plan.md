# Owner role alignment — Project 2 / CompactDB

Single agent. Preserve accepted compact UX. Read-only primary audit; fixture-only writes/tests with backup. No schema/entities/grants/Staffing/Project 1/Git publication changes.

- [x] Verify actual role/user/12 ALL grants and existing global authorization.
- [x] Audit source contracts, domain models and aggregate/report/monitoring gaps.
- [x] Implement permission-gated global read APIs and Owner presentation using existing workspaces where feasible.
- [x] Validate actual fixture API, ALL/permission/read-only boundaries and responsive browser; regression builds/tests.
- [x] Review, report exact status/blockers/files, update UX docs/journal and reconcile local processes.

Actual primary audit: OWNER role 12; user 438 / OWN001 ACTIVE; role assignment branch_id NULL. Twelve ALL grants: ASSET_VIEW, CLEANING_VIEW, DASHBOARD_VIEW, INCIDENT_VIEW, INSPECTION_VIEW, INVENTORY_VIEW, IR_VIEW, MAINTENANCE_VIEW, MR_VIEW, REPORT_VIEW, ROOM_VIEW, TASK_VIEW. No AUDIT_VIEW, PM_MANAGE, staff/admin/attendance or mutation permissions. Branches DVBV 413 / DVCT 414 verified, never hardcode in business queries.

| FEATURE | CURRENT_UI | API | PERMISSION | SCOPE | GAP | ACTION |
|---|---|---|---|---|---|---|
| Global dashboard/comparison | Generic single branch | Dashboard requires branch | DASHBOARD_VIEW + domain VIEW | Actual ALL supported by Access | No aggregate/comparison | Add read-only aggregate with validated ALL and optional branch |
| Branch selector | Existing branch context under logo | auth/me returns all branches | Actual ALL grants | No membership needed | No All choice | Owner filter under logo, preserve auth grants and branch behavior of other roles |
| Rooms | Shared grid/drawer | Single branch list, scoped history | ROOM_VIEW | ALL grants work on detail | Global metadata/filter | Reuse room grid with read scope/options data |
| Work/Incident | Shared table/drawer | Single branch list, detail | Each kind VIEW | ALL detail works | Global list/search/page | Extend read list only; reuse Work with readonly capability |
| Assets/inventory/MR | Shared workspaces | Single branch lists | ASSET/INVENTORY/MR_VIEW | ALL detail works | Global paged monitoring | Aggregate read queries, reuse read detail workflows |
| Reports | Single branch capped arrays | REPORT_VIEW + domain VIEW | ALL | No global aggregates/time comparison | Add paged global reports/trends and minimal staffing aggregates |
| PM | Manager only | PM_MANAGE required for plans | REPORT_VIEW + MAINTENANCE_VIEW | ALL | No read endpoint | Read report over existing PM data, no PM_MANAGE grant or mutation |
| Audit | Guarded existing page | AUDIT_VIEW required | Missing actual grant | None | Permission blocker | Hide audit; retain permitted business history |

Important semantics: distinguish occupancy/readiness/block; COMPLETED Maintenance remains pending review; snapshot KPI are current, date filter describes flow/trend explicitly; no inferred financial data or fabricated stock reservations/low-stock policy.

Final evidence: Owner fixture 33/33 groups; real API browser 45 responsive views with zero business mutations, page/console errors or failed permitted API calls. Extended fixture regression 128/128. Backend build zero warnings/errors; .NET 28 passed, 3 conditional skips. Frontend build/typecheck passed; 7 targeted unit tests and shared compact UX suites passed. Primary metadata/grants unchanged; existing membership preserved, zero-membership fixture proves ALL reads.

Permission blocker retained: AUDIT_VIEW absent, menu hidden/API forbidden. Snapshot history and stable multi-page export unsupported; CSV list export is explicitly current page. No permissions/schema changes to bypass these limits.

Handoff: `plans/reports/implementation-261123-0414-owner-role-alignment.md`; local journal `plans/journals/2026-11-23-owner-role-alignment.md`. Primary API 5768 restored (worker disabled), existing Vite 5186 retained; fixture APIs/browser closed. Unrelated Vite 5173 and Project 1 processes untouched. No commit/push/deploy.
