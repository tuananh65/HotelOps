# FE-04 SRS development acceptance matrix

Internal development assessment against draft SRS use cases 4.1–4.11 and current Change Notes. Not a Tester handover, approved Permission Matrix or accepted shared deployment contract. PASS evidence for one path never grants whole-UC acceptance.

| UC | Required behavior / source priority | Developer implementation/evidence | Remaining acceptance gap |
| --- | --- | --- | --- |
| UC028 | Create one current room cleaning cycle; authorized scope, audit/atomicity and duplicate protection | Preserve FS001–012 source; FS013 real regression runs task/cycle/readiness/audit and fault cases | Shared room status/history contract and final module acceptance |
| UC029 | Eligible staff assignment, authority/ETag, audit and mandatory recipient notification | Existing FS012 SQL boundary preserved; FS013 same candidate opt-in and regression | A01/A02 shared acceptance; provisional publisher is not accepted implementation |
| UC030 | Assigned/In Progress/On Hold reassign, distinct eligible staff, reason, interval closure, audit and both recipients | New backend/frontend paging; SQL module cases, commit/cancellation proof, real lifecycle 3/3 incl privacy/retry/rollback/concurrency | Shared Notification event/recipient contract, full runtime SQL deployment/least privilege acceptance |
| UC031 | Current-owner Start/Resume, Working workday, one open interval, first actual start, room state and audit | Initial ASSIGNED Start verified through React/real API/SQL; effective block preserved | Resume/open-resolved blocker contract; public Workday entry/deployment; legacy room status/history reconcile |
| UC032 | Current-owner progress, snapshot/concurrency, 100% does not implicitly complete | Existing source preserved; units/interception and real regression suite | Final FE04 regression/acceptance and complete lifecycle integration |
| UC033 | In Progress → On Hold, explicit open blocker/reason/note/evidence, close time, audit and mandatory supervisor notification | Readiness review only | Blocker persistence/resolution schema, recipient contract, incident/material request adapter decisions; no invented DEV2 implementations |
| UC034 | Mandatory items/no open blocker, actual end/interval closure, Pending Inspection, evidence/audit/notification | Readiness review only | Completion transaction, inspection integration and required shared contracts |
| UC035 | Receive/Accept/Start assigned rework from failed inspection; workday/interval and audit | Existing entity/read foundation only | Confirm status vocabulary and inspection producer, implement authorized writes and tests |
| UC036 | Remediation per defect, Pending Reinspection, close interval, room/audit and notification | Existing entity/read foundation only | Rework completion transaction and shared reinspection contract |
| UC037 | Scoped monitor/filter/detail plus cancellation only before Start, reason/audit/notification and latest-cycle recalculation | Preserve scoped read monitor, add execution/reassign link | Cancellation write/notification and complete blocker timeline acceptance |
| UC038 | Read-only newest cycles, all owners/times/results, inspection checklist version/defects/rework/attachments, retained history | Preserve current turnover read foundation | Full multi-stage timeline, inactive/history scope and final attachment/inspection evidence |

Unresolved Reception/Operation Manager entitlements and room status transitions remain pending in `SRS_CHANGE_NOTES.md`; no new shared role grants were inferred. Department Head is not silently reduced to a single-department manager. Existing persisted permission paths and branch scope remain authoritative for the implemented requests.

FE-04 remains **IN_DEVELOPMENT / NOT_READY_FOR_MERGE**. No numeric completion percentage, TESTER_READY or TESTER_ACCEPTED claim. Next implementation order and prerequisites are in `UC033_UC038_READINESS.md`.
