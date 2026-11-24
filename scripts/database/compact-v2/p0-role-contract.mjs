// Explicit, reviewable P0 grant contract. Run with --write to regenerate migration/matrix.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = fileURLToPath(new URL('../../../', import.meta.url));
const grants = {
  SYS_ADMIN: 'AUDIT_VIEW MASTER_DATA_MANAGE ROLE_MANAGE USER_MANAGE USER_VIEW',
  OWNER: 'ASSET_VIEW CLEANING_VIEW DASHBOARD_VIEW INCIDENT_VIEW INSPECTION_VIEW INVENTORY_VIEW IR_VIEW MAINTENANCE_VIEW MR_VIEW REPORT_VIEW ROOM_VIEW TASK_VIEW REPLENISHMENT_VIEW STAFF_SUPPLY_VIEW',
  OPERATION_MANAGER: 'ASSET_MANAGE ASSET_RETIRE ASSET_TRANSFER ASSET_VIEW AUDIT_VIEW ATTENDANCE_RECORD CLEANING_VIEW DASHBOARD_VIEW INCIDENT_VIEW INSPECTION_VIEW INVENTORY_VIEW IR_VIEW MAINTENANCE_VIEW MR_VIEW REPORT_VIEW ROOM_BLOCK_MANAGE ROOM_VIEW SHIFT_MANAGE TASK_MANAGE TASK_EXECUTE TASK_VIEW USER_VIEW ROOM_SUPPLY_CONFIG STAFF_SUPPLY_VIEW REPLENISHMENT_VIEW REPLENISHMENT_APPROVE TRANSFER_REQUEST_CREATE TRANSFER_REQUEST_APPROVE',
  HOUSEKEEPING_SUPERVISOR: 'ATTENDANCE_RECORD CLEANING_ASSIGN CLEANING_VIEW DASHBOARD_VIEW DEFECT_RESOLVE INCIDENT_CREATE INCIDENT_VIEW INSPECTION_PERFORM INSPECTION_VIEW ROOM_VIEW IR_CREATE IR_PROCESS IR_VIEW STAFF_SUPPLY_VIEW',
  HK_STAFF: 'ATTENDANCE_RECORD CLEANING_EXECUTE CLEANING_VIEW IR_CREATE IR_PROCESS IR_VIEW MR_CREATE MR_VIEW ROOM_VIEW TASK_EXECUTE TASK_VIEW STAFF_SUPPLY_VIEW',
  MAINTENANCE_SUPERVISOR: 'ASSET_VIEW ATTENDANCE_RECORD DASHBOARD_VIEW INCIDENT_MANAGE INCIDENT_VIEW MAINTENANCE_EXECUTE MAINTENANCE_REVIEW MAINTENANCE_VIEW ROOM_VIEW IR_CREATE IR_PROCESS IR_VIEW',
  RECEPTION: 'ATTENDANCE_RECORD IR_CREATE IR_VIEW ROOM_CHECKIN ROOM_CHECKOUT ROOM_OCCUPANCY_MOVE ROOM_VIEW',
  STOREKEEPER: 'ATTENDANCE_RECORD INVENTORY_VIEW ITEM_MASTER_MANAGE MR_APPROVE_L1 MR_FULFILL MR_VIEW REPLENISHMENT_CLOSE REPLENISHMENT_CREATE REPLENISHMENT_RECEIVE REPLENISHMENT_VIEW STOCK_ADJUST_POST STOCK_ISSUE_POST STOCK_RECEIPT_POST STOCK_RETURN_POST STOCK_REVERSE STOCK_TRANSFER_POST STOCKTAKE_PERFORM IR_CREATE IR_PROCESS IR_VIEW STAFF_SUPPLY_VIEW',
};
const scope = (role, permission) => ['OWNER', 'SYS_ADMIN'].includes(role) ? 'ALL' : permission === 'ATTENDANCE_RECORD' || (role === 'HK_STAFF' && permission === 'STAFF_SUPPLY_VIEW') ? 'OWN' : 'BRANCH';
const extra = ['ROOM_SUPPLY_CONFIG', 'STAFF_SUPPLY_VIEW', 'REPLENISHMENT_APPROVE', 'TRANSFER_REQUEST_CREATE', 'TRANSFER_REQUEST_APPROVE'];
const tuples = Object.entries(grants).flatMap(([role, text]) => text.split(' ').map(permission => `('${role}','${permission}','${scope(role, permission)}')`));
const insert = 'INSERT @grants VALUES\n ' + tuples.join(',\n ') + ';';
const sqlPath = root + 'scripts/database/compact-v2/p0-role-migration.sql';
if (process.argv.includes('--write')) {
  const sql = readFileSync(sqlPath, 'utf8').replace(/\/\* TARGET_GRANTS \*\/(?:[\s\S]*?\/\* END_TARGET_GRANTS \*\/)?/, `/* TARGET_GRANTS */\n ${insert}\n /* END_TARGET_GRANTS */`);
  writeFileSync(sqlPath, sql);
  const catalogPath = root + 'src/HotelOps.Api/Compact/Security/PermissionCatalog.cs';
  let catalog = readFileSync(catalogPath, 'utf8');
  for (const code of extra) if (!catalog.includes(code)) catalog = catalog.replace('REPLENISHMENT_VIEW', `${code} REPLENISHMENT_VIEW`);
  writeFileSync(catalogPath, catalog);
  const rows = Object.entries(grants).map(([role, text]) => `| ${role} | ${text.split(' ').map(p => `${p} (${scope(role, p)})`).join(', ')} |`).join('\n');
  writeFileSync(root + 'docs/requirements/p0-261124/permission-matrix.md', `# P0 target permission matrix\n\nExplicit allowlist; not a union of retired role grants. B §3 overrides A where they conflict. Only TEST cutover is authorized.\n\n| Role | Permissions and scope |\n|---|---|\n${rows}\n\n## Required service restrictions\n\n- OM has no FE06 mutation, ordinary internal-request mutation, warehouse posting, receipt, stock adjustment or fulfillment grant. IR_VIEW is oversight only. Replenishment approval uses REPLENISHMENT_APPROVE on the named workflow only. TASK_EXECUTE and TASK_MANAGE cover operational tasks only, including approved own-task completion.\n- User/role creation remains SYS_ADMIN-only. USER_VIEW gives OM branch-scoped staffing visibility, not account editing.\n- HK staff cannot report incidents. HK supervisor records incidents and performs paper-checklist inspection. No electronic CHECKLIST_MANAGE grant.\n- MAINTENANCE_SUPERVISOR tracks external repairs directly. No MAINTENANCE_ASSIGN grant, no internal technician workforce. Existing execute/review codes must be restricted to the new repair workflow by services.\n- IR_CREATE/IR_PROCESS are route-level prerequisites, not authority to bypass participant/kind/state rules. Ordinary employee requests remain creator/recipient-scoped in services.\n- Storekeeper remains the only operational stock poster; supply automation uses transactional service rules and does not grant HK stock posting.\n- STAFF_SUPPLY_VIEW is OWN for HK staff, BRANCH for operational supervisors/storekeeper/OM, ALL read-only for Owner.\n- TRANSFER_REQUEST_CREATE and TRANSFER_REQUEST_APPROVE are BRANCH-scoped for OM. Services must validate both source and destination responsibility and the approved transfer lifecycle.\n\n## Migration and recovery\n\nThree mappings: DEPT_MANAGER → OPERATION_MANAGER; MAINTENANCE_LEAD → MAINTENANCE_SUPERVISOR; MAINTENANCE_STAFF → HK_STAFF. Preserve IDs, credentials, account status, existing valid target roles and history. Old roles become inactive and all nonrevoked old assignments are revoked. Future role assignments retain effective dates. Sessions for all target-contract roles are revoked because grants change.\n\nSplit ENG employment at migration time and create/reuse HK membership in the same branch. Clone/reuse target shift templates, move only future unstarted assignments; past shift FK/history remain unchanged. Active old-role shifts block cutover. Inactive target templates, duplicate schedules, capacity overflow, conflicting effective periods and unresolved open maintenance responsibility abort the transaction with actionable data. Unstarted maintenance with one eligible supervisor can be reassigned with immutable audit/event history. Never convert maintenance to cleaning.\n\nThe SQL refuses Production; coordinator must verify a fresh backup restore before execution. A single transaction rolls back any failure. Successful application is idempotent through an immutable migration audit marker. Recovery after a committed cutover uses the verified pre-migration TEST backup while P0 runtime is stopped; never delete audit history or reactivate old sessions in-place. Audit includes old role/grant/assignment/membership/shift snapshots and changed future schedule mapping, excluding credentials and session tokens.\n\n## Decisions still pending\n\nOrdinary material-request approval has no new assignment: MR_APPROVE_L1/L2/FALLBACK are intentionally absent from the target roles pending the user's decision. The named replenishment approval is separate and authorized. Existing ordinary requests must remain readable; do not mark them approved by migration.\n`);
}
const sql = readFileSync(sqlPath, 'utf8');
assert.equal(Object.keys(grants).length, 8);
assert(sql.includes(insert));
for (const forbidden of ['IR_CREATE','IR_PROCESS','IR_OVERDUE_HANDLE','INCIDENT_CREATE','INCIDENT_TRIAGE','INCIDENT_MANAGE','INCIDENT_ESCALATION_HANDLE','STOCK_ISSUE_POST','STOCK_RECEIPT_POST','MR_APPROVE_L1']) assert(!grants.OPERATION_MANAGER.split(' ').includes(forbidden));
for (const old of ['DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF']) assert(!Object.hasOwn(grants, old));
assert(!grants.HK_STAFF.includes('INCIDENT_CREATE'));
assert(!grants.MAINTENANCE_SUPERVISOR.includes('MAINTENANCE_ASSIGN'));
assert(sql.includes("DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestP0%'"));
assert(sql.includes('BEGIN TRANSACTION') && sql.includes('ROLLBACK'));
assert(!/\b(DROP|TRUNCATE)\s+TABLE/i.test(sql));
assert(!/UPDATE\s+(?:dbo\.)?users\b/i.test(sql));
const access = readFileSync(root + 'src/HotelOps.Api/Compact/Security/Access.cs', 'utf8');
const catalog = readFileSync(root + 'src/HotelOps.Api/Compact/Security/PermissionCatalog.cs', 'utf8');
for (const code of extra) assert(catalog.includes(code));
assert(access.slice(access.indexOf('HasRole'), access.indexOf('RequireRolePermission')).includes('ur.AssignedAt<=now'));
console.log('PASS: explicit 8-role contract, forbidden OM writes, HK incident denial, future-role guard, test-only transactional migration. SQL execution is coordinated separately.');
