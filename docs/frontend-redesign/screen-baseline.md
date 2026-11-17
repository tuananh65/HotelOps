# Frontend redesign: screen baseline

Reference: feature/react-frontend at
9d220a94509e650464406a9b83099d24cb568220

80 required screens. Utility routes are not part of the count. This inventory does not certify implementation or API coverage.

Preserve integrated authentication, authorization, session recovery, API contracts, and all existing workflows. Prototype roles are not permission specifications.

| ID | Screen | Reference route | Reference component exists locally | Exact route declaration exists locally |
|---|---|---|---|---|
| 1 | Sign In | `/login` | True | True |
| 2 | Forgot Password | `/forgot-password` | True | True |
| 3 | Change Password | `/change-password` | True | True |
| 4 | Profile | `/profile` | True | True |
| 5 | Dashboard | `/dashboard` | False | True |
| 6 | Notification Center | `/notifications` | False | True |
| 7 | Notification Detail | `/notifications/:id` | False | False |
| 8 | User List | `/users` | False | True |
| 9 | User Detail | `/users/:id` | False | True |
| 10 | Role List | `/roles` | False | True |
| 11 | Role Detail | `/roles/:id` | False | False |
| 12 | Permission Matrix | `/permissions` | False | True |
| 13 | Branch List | `/branches` | False | True |
| 14 | Branch Detail | `/branches/:id` | False | False |
| 15 | Floor/Area List | `/locations` | False | True |
| 16 | Floor/Area Detail | `/locations/:id` | False | False |
| 17 | Room Type List | `/room-types` | False | True |
| 18 | Room Type Detail | `/room-types/:id` | False | True |
| 19 | Room List | `/rooms` | False | True |
| 20 | Room Detail | `/rooms/:id` | False | True |
| 21 | Department List | `/departments` | False | True |
| 22 | Department Detail | `/departments/:id` | False | True |
| 23 | Room Readiness Board | `/room-readiness` | False | False |
| 24 | Cleaning Task List | `/cleaning/tasks` | True | True |
| 25 | Create Cleaning Task | `/cleaning/tasks/new` | True | True |
| 26 | Cleaning Task Detail | `/cleaning/tasks/:id` | True | True |
| 27 | Cleaning Assignment | `/cleaning/assignment` | True | True |
| 28 | Rework Task List | `/rework` | True | True |
| 29 | Rework Task Detail | `/rework/:id` | True | True |
| 30 | Room Turnover History | `/rooms/turnover` | True | True |
| 31 | Inspection Checklist List | `/checklists` | False | False |
| 32 | Inspection Checklist Detail | `/checklists/:id` | False | False |
| 33 | Inspection Queue | `/inspections/queue` | False | False |
| 34 | Room Inspection | `/inspections/:id/perform` | False | False |
| 35 | Inspection Result | `/inspections/:id/result` | False | False |
| 36 | Request Rework | `/inspections/:id/rework` | False | False |
| 37 | Reinspection | `/inspections/:id/reinspect` | False | False |
| 38 | Inspection History | `/inspections/history` | False | False |
| 39 | Incident List | `/incidents` | False | False |
| 40 | Report Incident | `/incidents/new` | False | True |
| 41 | Incident Detail | `/incidents/:id` | False | False |
| 42 | Maintenance Assignment | `/maintenance/assignment` | False | False |
| 43 | Maintenance Task List | `/maintenance/tasks` | False | False |
| 44 | Maintenance Task Detail | `/maintenance/tasks/:id` | False | False |
| 45 | Preventive Maintenance Schedule List | `/maintenance/schedules` | False | False |
| 46 | Preventive Maintenance Schedule Detail | `/maintenance/schedules/:id` | False | False |
| 47 | Reference Data | `/reference-data` | False | False |
| 48 | Asset List | `/assets` | False | True |
| 49 | Asset Detail | `/assets/:id` | False | True |
| 50 | Asset Maintenance History | `/assets/:id/history` | False | False |
| 51 | Warehouse List | `/inventory/warehouses` | False | True |
| 52 | Warehouse Detail | `/inventory/warehouses/:id` | False | False |
| 53 | Inventory Dashboard | `/inventory/dashboard` | False | False |
| 54 | Item List | `/inventory/items` | False | True |
| 55 | Item Detail | `/inventory/items/:id` | False | True |
| 56 | Unit of Measurement | `/inventory/units` | False | True |
| 57 | Stock Transaction List | `/inventory/transactions` | False | False |
| 58 | Stock Transaction Detail | `/inventory/transactions/:id` | False | False |
| 59 | Stocktake List | `/inventory/stocktakes` | False | True |
| 60 | Stocktake Detail | `/inventory/stocktakes/:id` | False | True |
| 61 | Stock Balance & Ledger | `/inventory/ledger` | False | False |
| 62 | Material Request List | `/material-requests` | False | False |
| 63 | Material Request Detail | `/material-requests/:id` | False | False |
| 64 | Replenishment List | `/replenishments` | False | False |
| 65 | Replenishment Detail | `/replenishments/:id` | False | False |
| 66 | Task List | `/tasks` | False | False |
| 67 | Task Detail | `/tasks/:id` | False | False |
| 68 | Task Assignment | `/tasks/assignment` | False | False |
| 69 | Workload & Time Logs | `/workload` | False | False |
| 70 | Overdue Work | `/overdue` | False | False |
| 71 | Shift Management | `/shifts` | False | False |
| 72 | Shift Detail | `/shifts/:id` | False | False |
| 73 | Workday Record | `/workdays` | False | False |
| 74 | Shift Handover List | `/handovers` | False | False |
| 75 | Shift Handover Detail | `/handovers/:id` | False | False |
| 76 | Internal Request List | `/internal-requests` | False | True |
| 77 | Internal Request Detail | `/internal-requests/:id` | False | False |
| 78 | Operational Reports | `/reports` | False | True |
| 79 | Audit Log | `/audit-logs` | False | False |
| 80 | Audit Log Detail | `/audit-logs/:id` | False | False |

A missing exact declaration may be a renamed route, a query-driven detail, or a nested route. A present component may be disconnected or legacy. Each requires semantic and browser verification before acceptance.

## Acceptance checks

- Map every reference screen to an integrated destination and its actions.
- Preserve any extra integrated screens beyond the reference 80.
- Verify loading, empty, error, unauthorized, session-expired, validation and mutation states.
- Verify keyboard operation and desktop/mobile layouts.
- Run relevant frontend tests, typecheck and production build; report real API/database limitations separately.
