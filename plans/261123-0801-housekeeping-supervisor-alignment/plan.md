# HK Supervisor — full role alignment

Single agent. Preserve existing compact UX and the pending-room intake from the previous task. Primary inspection is read-only; all test writes use backed-up isolated fixtures. No schema/grants/Staffing/Project 1/publication changes.

Baseline verified: role HOUSEKEEPING_SUPERVISOR id 10, 11 grants: ATTENDANCE_RECORD OWN; CHECKLIST_MANAGE, CLEANING_ASSIGN, CLEANING_VIEW, DASHBOARD_VIEW, DEFECT_RESOLVE, INCIDENT_CREATE, INCIDENT_VIEW, INSPECTION_PERFORM, INSPECTION_VIEW, ROOM_VIEW BRANCH. No RAW room mutation, USER/SHIFT, stock or maintenance privileges. No role IDs in application logic.

- [x] Read request, requirements/change notes, relevant entities/mappings/services/UI and actual role/grants.
- [x] Complete scoped backend dashboard, staffing/read queues, checklist reads/version contract and lifecycle/defect gaps.
- [x] Align seven permission-gated UI groups; reuse Work/Rooms/Workday/notifications, preserve creation intake and hidden filters.
- [x] Verify API workflow/RBAC/branches on isolated fixtures, builds/unit/browser/regressions; review and report exact coverage/blockers.

| FEATURE | CURRENT_UI | API | PERMISSION | WORKFLOW | GAP | ACTION |
|---|---|---|---|---|---|---|
| Dashboard | Generic branch dashboard | Generic counts | DASHBOARD + domain VIEW | Current snapshot | No HK workload/queues | Add domain-gated HK read summary and actionable lists |
| Sidebar | Generic menu | Existing routes | Actual branch grants | Presentation only | No seven-group workspace | Dedicated single-role navigation; multi-role retains existing shell |
| Cleaning | Intake and existing task queues | CleaningRooms/Queue/Create/Act | CLEANING_ASSIGN/VIEW | Existing assign/execute | Sorting/filter and modal consistency | Reuse intake, add sort; modal assignment, keep ownership |
| Inspection | Existing Work creation/checklist/pass/fail | WorkService | INSPECTION_VIEW/PERFORM | Assignment -> accept/start -> conclusion | Waiting room roots absent, duplicate/rework admission unclear | Add waiting-root read queue and explicit lifecycle guards |
| Rework/defects | Existing Work/findings | Findings/resolve | CLEANING_ASSIGN + DEFECT_RESOLVE | Preserve failed inspection, reinspection after rework | No defect queue; resolve lacks explicit DEFECT_RESOLVE gate | Add scoped defect list, enforce grant, keep independence |
| Rooms | Grid and history | Room/Work APIs | ROOM + domain VIEW | Separate occupancy/readiness/block | No current cleaning/inspection links, floor only Owner | Add scoped room workflow context, table option/floor filters |
| Staff/shift | None for HKS | CandidateRows exists | CLEANING_ASSIGN | Candidates not User Management | No HK workload/shift read | Add minimal HK staff read based on existing candidates |
| Checklist | Create only, published immutable | TemplateService | CHECKLIST_MANAGE | Published snapshots immutable | No detail/version editor; cannot disable published | Add detail/version reuse; report activation limitation, no trigger/schema bypass |
| Incident/inbox | Existing Work/Inbox | Scoped existing services | INCIDENT_CREATE/VIEW, recipient | No triage or maintenance authority | Not grouped in HK workspace | Reuse permission-gated tabs and safe links |

## Final validation

- Backend build passed with zero warnings/errors. Unit suite: 28 passed, three conditional integration tests skipped (fixture environment not configured for those separate worker/account tests).
- HKS browser: 24 groups at 1440/768/375; pending-room intake browser passed. HK Staff 27 responsive views and OM 24 responsive views passed. Shared UX: 48 navbar checks plus settings/Reception/Workday/dashboard checks passed. Browser tests use mocked APIs; backend workflow tests use SQL fixtures.
- Final SQL fixture `HotelOpsCompactApiTest7aeefa946273`: 51/51 groups PASS, including repeated failed reinspection, no reuse of outdated rework, and independent cleaning/inspection/block/occupancy room fields. Earlier 47/50-group runs are superseded.
- HKS browser final 24 groups PASS including combined cleaning/block filters and loaded screenshots; Reception regression PASS. No pageerrors. API local restored at port 5768, PID 3144, health ok; fixture API stopped. Vite 5186 HTTP 200.
- Published checklist activation/deactivation remains a database-contract blocker; UI creates new immutable versions. No trigger/schema/grant bypass.

Report: [implementation and verification](../reports/implementation-261123-0850-housekeeping-supervisor-alignment.md). Scoped single-agent review completed; no further code edits after final build/browser validation.
