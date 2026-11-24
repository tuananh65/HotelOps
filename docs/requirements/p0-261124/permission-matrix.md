# P0 target permission matrix

Explicit allowlist; not a union of retired role grants. B §3 overrides A where they conflict. Only TEST cutover is authorized.

| Role | Permissions and scope |
|---|---|
| SYS_ADMIN | AUDIT_VIEW (ALL), MASTER_DATA_MANAGE (ALL), ROLE_MANAGE (ALL), USER_MANAGE (ALL), USER_VIEW (ALL) |
| OWNER | ASSET_VIEW (ALL), CLEANING_VIEW (ALL), DASHBOARD_VIEW (ALL), INCIDENT_VIEW (ALL), INSPECTION_VIEW (ALL), INVENTORY_VIEW (ALL), IR_VIEW (ALL), MAINTENANCE_VIEW (ALL), MR_VIEW (ALL), REPORT_VIEW (ALL), ROOM_VIEW (ALL), TASK_VIEW (ALL), REPLENISHMENT_VIEW (ALL), STAFF_SUPPLY_VIEW (ALL) |
| OPERATION_MANAGER | ASSET_MANAGE (BRANCH), ASSET_RETIRE (BRANCH), ASSET_TRANSFER (BRANCH), ASSET_VIEW (BRANCH), AUDIT_VIEW (BRANCH), ATTENDANCE_RECORD (OWN), CLEANING_VIEW (BRANCH), DASHBOARD_VIEW (BRANCH), INCIDENT_VIEW (BRANCH), INSPECTION_VIEW (BRANCH), INVENTORY_VIEW (BRANCH), IR_VIEW (BRANCH), MAINTENANCE_VIEW (BRANCH), MR_VIEW (BRANCH), REPORT_VIEW (BRANCH), ROOM_BLOCK_MANAGE (BRANCH), ROOM_VIEW (BRANCH), SHIFT_MANAGE (BRANCH), TASK_MANAGE (BRANCH), TASK_EXECUTE (BRANCH), TASK_VIEW (BRANCH), USER_VIEW (BRANCH), ROOM_SUPPLY_CONFIG (BRANCH), STAFF_SUPPLY_VIEW (BRANCH), REPLENISHMENT_VIEW (BRANCH), REPLENISHMENT_APPROVE (BRANCH), TRANSFER_REQUEST_CREATE (BRANCH), TRANSFER_REQUEST_APPROVE (BRANCH) |
| HOUSEKEEPING_SUPERVISOR | ATTENDANCE_RECORD (OWN), CLEANING_ASSIGN (BRANCH), CLEANING_VIEW (BRANCH), DASHBOARD_VIEW (BRANCH), DEFECT_RESOLVE (BRANCH), INCIDENT_CREATE (BRANCH), INCIDENT_VIEW (BRANCH), INSPECTION_PERFORM (BRANCH), INSPECTION_VIEW (BRANCH), ROOM_VIEW (BRANCH), IR_CREATE (BRANCH), IR_PROCESS (BRANCH), IR_VIEW (BRANCH), STAFF_SUPPLY_VIEW (BRANCH) |
| HK_STAFF | ATTENDANCE_RECORD (OWN), CLEANING_EXECUTE (BRANCH), CLEANING_VIEW (BRANCH), IR_CREATE (BRANCH), IR_PROCESS (BRANCH), IR_VIEW (BRANCH), MR_CREATE (BRANCH), MR_VIEW (BRANCH), ROOM_VIEW (BRANCH), TASK_EXECUTE (BRANCH), TASK_VIEW (BRANCH), STAFF_SUPPLY_VIEW (OWN) |
| MAINTENANCE_SUPERVISOR | ASSET_VIEW (BRANCH), ATTENDANCE_RECORD (OWN), DASHBOARD_VIEW (BRANCH), INCIDENT_MANAGE (BRANCH), INCIDENT_VIEW (BRANCH), MAINTENANCE_EXECUTE (BRANCH), MAINTENANCE_REVIEW (BRANCH), MAINTENANCE_VIEW (BRANCH), ROOM_VIEW (BRANCH), IR_CREATE (BRANCH), IR_PROCESS (BRANCH), IR_VIEW (BRANCH) |
| RECEPTION | ATTENDANCE_RECORD (OWN), IR_CREATE (BRANCH), IR_VIEW (BRANCH), ROOM_CHECKIN (BRANCH), ROOM_CHECKOUT (BRANCH), ROOM_OCCUPANCY_MOVE (BRANCH), ROOM_VIEW (BRANCH) |
| STOREKEEPER | ATTENDANCE_RECORD (OWN), INVENTORY_VIEW (BRANCH), ITEM_MASTER_MANAGE (BRANCH), MR_APPROVE_L1 (BRANCH), MR_FULFILL (BRANCH), MR_VIEW (BRANCH), REPLENISHMENT_CLOSE (BRANCH), REPLENISHMENT_CREATE (BRANCH), REPLENISHMENT_RECEIVE (BRANCH), REPLENISHMENT_VIEW (BRANCH), STOCK_ADJUST_POST (BRANCH), STOCK_ISSUE_POST (BRANCH), STOCK_RECEIPT_POST (BRANCH), STOCK_RETURN_POST (BRANCH), STOCK_REVERSE (BRANCH), STOCK_TRANSFER_POST (BRANCH), STOCKTAKE_PERFORM (BRANCH), IR_CREATE (BRANCH), IR_PROCESS (BRANCH), IR_VIEW (BRANCH), STAFF_SUPPLY_VIEW (BRANCH) |

## Required service restrictions

- OM has no FE06 mutation, ordinary internal-request mutation, warehouse posting, receipt, stock adjustment or fulfillment grant. IR_VIEW is oversight only. Replenishment approval uses REPLENISHMENT_APPROVE on the named workflow only. TASK_EXECUTE and TASK_MANAGE cover operational tasks only, including approved own-task completion.
- User/role creation remains SYS_ADMIN-only. USER_VIEW gives OM branch-scoped staffing visibility, not account editing.
- P0 explicitly supersedes the earlier ban on HK/Reception room reporting: HK staff may report assigned-room missing durable objects or damaged equipment; Reception may report damaged room equipment. These specialized routes require IR_CREATE and service-level role/room/assignment/workday checks. Neither role gains generic INCIDENT_CREATE. HK supervisor verifies reports and performs paper-checklist inspection. No electronic CHECKLIST_MANAGE grant.
- MAINTENANCE_SUPERVISOR tracks external repairs directly. No MAINTENANCE_ASSIGN grant, no internal technician workforce. Existing execute/review codes must be restricted to the new repair workflow by services.
- IR_CREATE/IR_PROCESS are route-level prerequisites, not authority to bypass participant/kind/state rules. Ordinary employee requests remain creator/recipient-scoped in services.
- Storekeeper remains the only operational stock poster; supply automation uses transactional service rules and does not grant HK stock posting.
- STAFF_SUPPLY_VIEW is OWN for HK staff, BRANCH for operational supervisors/storekeeper/OM, ALL read-only for Owner.
- TRANSFER_REQUEST_CREATE and TRANSFER_REQUEST_APPROVE are BRANCH-scoped for OM. Services must validate both source and destination responsibility and the approved transfer lifecycle.

## Migration and recovery

Three mappings: DEPT_MANAGER → OPERATION_MANAGER; MAINTENANCE_LEAD → MAINTENANCE_SUPERVISOR; MAINTENANCE_STAFF → HK_STAFF. Preserve IDs, credentials, account status, existing valid target roles and history. Old roles become inactive and all nonrevoked old assignments are revoked. Future role assignments retain effective dates. Sessions for all target-contract roles are revoked because grants change.

Split ENG employment at migration time and create/reuse HK membership in the same branch. Clone/reuse target shift templates, move only future unstarted assignments; past shift FK/history remain unchanged. Active old-role shifts block cutover. Inactive target templates, duplicate schedules, capacity overflow, conflicting effective periods and unresolved open maintenance responsibility abort the transaction with actionable data. Unstarted maintenance with one eligible supervisor can be reassigned with immutable audit/event history. Never convert maintenance to cleaning.

The SQL refuses Production; coordinator must verify a fresh backup restore before execution. A single transaction rolls back any failure. Successful application is idempotent through an immutable migration audit marker. Recovery after a committed cutover uses the verified pre-migration TEST backup while P0 runtime is stopped; never delete audit history or reactivate old sessions in-place. Audit includes old role/grant/assignment/membership/shift snapshots and changed future schedule mapping, excluding credentials and session tokens.

## Confirmed ordinary-material decision

The user confirmed on 24 November 2026 that material requests remaining within the hotel are processed by Storekeeper. The companion `p0-store-policy.sql` grants existing MR_APPROVE_L1 at BRANCH scope to STOREKEEPER; services also require the Storekeeper role. OM/DH approval and fallback paths are revoked. Ordinary creators cannot self-approve; Storekeeper may approve another employee's request and fulfill it within approved quantities. The narrow room-remediation confirmation path remains separate. Migration does not mark pending ordinary requests approved.

The effective target matrix is the composition of `p0-role-migration.sql` and `p0-store-policy.sql`, not the first script alone. Runtime denial/approval tests remain required; a grant listing alone does not prove enforcement.
