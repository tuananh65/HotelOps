# UC028 contract — BLOCKED_CONTRACT

| Dimension | Current evidence / decision |
| --- | --- |
| Existing endpoint / active | None in current Controllers; no create method inferred from prototype or archival source |
| HTTP route / method | Not defined; do not invent a production route |
| Request / response DTO | No current CreateCleaningTask request/response contract; no EF entity response proposed |
| Permission / ownership | SQL catalog CLEANING_ASSIGN description includes create; SRS supervisor. Exact source/room binding must be reviewed; no CLEANING_CREATE invented |
| Branch | Task→Room→Branch and active hierarchy must be authoritative; query-string branch does not grant scope |
| State | SRS new→PENDING_ASSIGNMENT; legacy generated Status default PENDING is not an approved mapping by itself |
| Concurrency | SRS one open task/room; no reviewed duplicate/current-cycle lock+predicate, idempotency or creation If-Match contract |
| CSRF | Any future unsafe /api action must use unchanged cookie/session + fresh X-CSRF-TOKEN, not a cached/prototype token |
| Transaction | Task, frozen items where required, room recalculation/history and audit must succeed/fail together using same HotelOpsDbContext; no implementation yet |
| Notification | UC028 main flow does not cite GBR010; do not invent a mandatory event. Save+Assign separately invokes UC029 and its dependencies |
| Room effects | Pending Cleaning unless effective Blocked; missing authoritative GBR043/044 current-cycle/block protocol |
| SQL | cleaning_tasks, rooms, room_status_histories, audit_logs, reference_values; approved room-type/item schema as applicable. No schema/mapping edit |
| Frontend | CreateCleaningTaskScreen /cleaning/tasks/new, preserved PageHeader/panel and monitor return link; save unavailable, mock persistence removed |
| Missing implementation | Server creation/resource gate, actual request fields/validation, duplicate prevention, room lifecycle/history/transaction + approved integration |
| Approval readiness | BLOCKED_CONTRACT; task explicitly allows contract tests/preparation instead of guessing shared rules |
| Live verification | LIVE_WRITE_BLOCKED_APPROVAL; no new Create write executed |

Confirmed requirements are documented, not implemented: room/type/priority/instructions/due, room-type cleaning items, active room, duplicate existing open-task response, atomic room history/audit. Missing exact API means there is no honest client submit/409/success/refresh integration to test yet. The unavailable UI and no-mutation browser test prevent mock success. No fake503 endpoint or client-minted task IDs.

Next approval package must specify exact route/DTO, permission/resource gate, current-cycle/open predicate, reference IDs, rowversion/lock protocol, room-state history and rollback behavior. Cross-owner/shared schema/security/DI changes need exact reviewed diff before application. SQL write scope must be separately approved after contracts exist.
