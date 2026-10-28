# Task44 technical approval and implemented escalation controls

P1-P12 and the five permission/scope envelopes below have now been explicitly approved for
Task44 technical implementation by the developer. Historical proposal/not-approved labels
below describe the earlier review stage and are superseded within this bounded v1 only.
No formal SRS/PO replacement, general role hierarchy, new catalog grant or DEV2 SoD certification
is claimed. Global/system roles, self-grant, secondary-Branch assignment, all existing role
combinations and dormant/regrant paths remain refused. No public Revoke/Deactivate route exists.

Implementation: UserRoleDelegationPolicy uses a fixed ordinal five-code Department map and exact
permission/scope set equality. UserRoleAssignmentService checks live SQL Role flags/grants under
retained locks at initial and final gates, rejects ANY pre-existing target role path, validates
primary scope/membership and both actor ALL permissions/session. It creates one new branch-bound
assignment and audit only. GlobalAdminSerialization is an invariant/locking tool, not authority
to grant roles. Test-only drift injections run exclusively in guarded disposable databases and
are restored; catalog/master before/after equality is asserted by the harness.

---
# UC-010 privilege escalation and SoD proposal

Status: **proposal only; no assignment implementation or business approval**.
Read P1-P12 in CONTRACT_AND_AUTHORIZATION.md as a package.

## Observed catalog and scope

Source: existing RBAC seed and SELECT-only disposable SQL receipt `task44-schema-final.local.json`.
Roles have no BranchId, assignable flag, delegation ceiling or protected-assignment flag.
RolePermission contains permission/DataScope; UserRole contains nullable BranchId. Actual
permission evaluation in `Security/Authorization/PermissionService.cs` keeps grants attached
to their own assignment: unbound ALL can be global; bound ALL is not unbound global authority;
BRANCH/OWN need the matching active Branch/membership, and OWN requires verified resource context.
SQL accepts DEPARTMENT in a check constraint, but the current evaluator does not grant it.

| Observed role(s) | SQL IsSystemRole / IsActive | Observed capability | Proposed UC-010 decision |
|---|---|---|---|
| SYS_ADMIN | true / true | 6 ALL grants including USER_MANAGE and ROLE_MANAGE | Protected: deny v1, no bootstrap/recovery through this endpoint |
| OWNER | true / true | DASHBOARD_VIEW/ALL and REPORT_VIEW/ALL | Deny v1 despite not being an eligible global administrator |
| OPERATIONS_MANAGER | true / true | 20 BRANCH grants; no ROLE_MANAGE | Defer; preserve single-Branch requirement and resolve draft conflict |
| HK_STAFF, HK_SUPERVISOR | false / true | 13 grants each, BRANCH/OWN | P2/P4 envelope candidates, first role only |
| MAINTENANCE_STAFF, MAINTENANCE_LEAD | false / true | 13 / 18 grants, BRANCH/OWN | P2/P4 envelope candidates, first role only |
| STOREKEEPER | false / true | 15 grants, BRANCH/OWN | P2/P4 envelope candidate, first role only |
| DEPT_MANAGER | false / true | 18 grants, BRANCH/OWN; no ROLE_MANAGE | Defer; not safe merely because non-system |
| RECEPTION | false / true | 12 grants, BRANCH/OWN | Defer UC-010 approval; UC-004 approval does not transfer |
| HK_INSPECTOR, STAFF | false / false | Inactive legacy roles | Deny |

IsSystemRole means neither "all roles with this flag are assignable" nor "only flagged roles
need protection". GBR-029 addresses deletion of system roles, not delegation. Effective global
administration is the Task41 USER_MANAGE/ALL + ROLE_MANAGE/ALL intersection, not a SYS_ADMIN name
check. A custom/mutated role could hold that intersection; unknown roles or unexpected grants
must therefore fail closed. Client must never supply PermissionId, DataScope or AssignedBy.

## Proposed role-combination matrix (requires business approval)

| Current/scheduled role set + requested role | Proposed v1 behavior | Evidence / unresolved point |
|---|---|---|
| No effective/scheduled role + one of the five candidates | Allow only after P1-P12 approval | Concrete first-grant slice; not yet approved |
| Same exact tuple, even inactive/expired/future | 409; no update/regrant | Unique SQL tuple includes historical rows; no RevokedAt field |
| HK_STAFF + HK_SUPERVISOR | Defer/deny v1 | Execution plus verification requires per-work SoD; role union alone cannot prove independent inspection |
| MAINTENANCE_STAFF + MAINTENANCE_LEAD | Defer/deny v1 | Execution plus independent verification; no complete cross-workflow SoD certification in this review |
| STOREKEEPER + another distinct role | Defer/deny v1 | Proposed narrow release restriction; SRS does NOT establish a universal prohibition of all Storekeeper combinations |
| Roles spanning HK/MT/WH Departments or secondary Branch role | Defer/deny v1 | No per-Branch multi-Department membership relation in current User schema |
| DEPT_MANAGER + HK_SUPERVISOR or MAINTENANCE_LEAD | Defer | Draft GBR-018 explicitly permits these subject to SoD; do not permanently outlaw them. Revised DH scope/SoD/delegation still needs approval |
| Existing OM + new role, or any target + OM | Defer/deny v1 | Avoid bypassing single-Branch and operational authority restrictions; role-assignment writer must own inverse safety |
| Existing privileged/global role + another role | Defer/deny v1 | Increased combined authority is outside the first-grant envelope |

V1's refusal to add a distinct second role is a proposed scope limit. SRS GBR-002 allows several
roles; future approved combinations can broaden the slice without turning deny-by-default into
an invented permanent business rule. Scheduled/latent paths must be checked, not just roles
effective at the instant of the request. Regrant never happens implicitly.

## Specific escalation scenarios to reject after approval

1. Actor has ROLE_MANAGE but no approved role envelope; granting an operational/admin role must
   not be authorized by the permission gate alone.
2. An allowlisted role code acquires new grants, ALL scope or administrative authority externally;
   do not continue using the old delegation approval without envelope review.
3. Client sends null BranchId to convert an operational grant into global authority, or borrows
   a membership/permission path from another Branch; strict branch-bound request and same-path
   checks must reject it.
4. Self-grant bootstraps the grantor's authority, or the newly inserted assignment authorizes
   its own final check. P5 forbids self-grant; final checks must evaluate pre-existing authority.
5. Adding a role silently revives an inactive/expired tuple or makes an old role effective through
   a new membership. V1 performs only a fresh INSERT after conflict checks, never reconciliation.
6. Count of eligible global admins increases but actor lacks delegation authority. LastAdmin
   serialization protects an invariant and cannot authorize any role grant.

Catalog runtime grants are evidence of effects, not business authorization to delegate them.
No DEV2 workflow changes or SoD-completeness claim is made. Any later expansion to combined
roles must verify independent-work approval rules in every affected workflow before release.

## Observed candidate envelopes for review (not delegation approval)

These are the permission/scope pairs observed in the disposable canonical catalog.
They define what P2 would need to authorize explicitly; no grant is edited or applied here.

| Candidate role | Observed permission/scope pairs |
|---|---|
| HK_STAFF | ATTENDANCE_RECORD/OWN, CLEANING_EXECUTE/OWN, CLEANING_VIEW/OWN, DEFECT_RESOLVE/OWN, INCIDENT_CREATE/BRANCH, INCIDENT_VIEW/OWN, IR_CREATE/OWN, IR_PROCESS/OWN, MR_CREATE/OWN, ROOM_VIEW/BRANCH, SHIFT_VIEW/OWN, TASK_EXECUTE/OWN, TASK_VIEW/OWN |
| HK_SUPERVISOR | ATTENDANCE_RECORD/OWN, CLEANING_ASSIGN/BRANCH, CLEANING_VIEW/BRANCH, INCIDENT_CREATE/BRANCH, INCIDENT_VIEW/BRANCH, INSPECTION_PERFORM/BRANCH, INSPECTION_VIEW/BRANCH, IR_CREATE/OWN, MR_CREATE/OWN, ROOM_VIEW/BRANCH, SHIFT_VIEW/BRANCH, TASK_EXECUTE/OWN, TASK_VIEW/BRANCH |
| MAINTENANCE_STAFF | ASSET_VIEW/BRANCH, ATTENDANCE_RECORD/OWN, INCIDENT_CREATE/BRANCH, INCIDENT_VIEW/BRANCH, IR_CREATE/OWN, IR_PROCESS/OWN, MAINTENANCE_EXECUTE/OWN, MAINTENANCE_VIEW/OWN, MR_CREATE/OWN, ROOM_VIEW/BRANCH, SHIFT_VIEW/OWN, TASK_EXECUTE/OWN, TASK_VIEW/OWN |
| MAINTENANCE_LEAD | ASSET_VIEW/BRANCH, ATTENDANCE_RECORD/OWN, INCIDENT_CREATE/BRANCH, INCIDENT_TRIAGE/BRANCH, INCIDENT_VIEW/BRANCH, IR_CREATE/OWN, IR_PROCESS/BRANCH, MAINTENANCE_ASSIGN/BRANCH, MAINTENANCE_EXECUTE/OWN, MAINTENANCE_REVIEW/BRANCH, MAINTENANCE_VIEW/BRANCH, MR_CREATE/OWN, PM_MANAGE/BRANCH, ROOM_BLOCK_MANAGE/BRANCH, ROOM_VIEW/BRANCH, SHIFT_VIEW/BRANCH, TASK_EXECUTE/OWN, TASK_VIEW/BRANCH |
| STOREKEEPER | ASSET_VIEW/BRANCH, ATTENDANCE_RECORD/OWN, INVENTORY_VIEW/BRANCH, IR_CREATE/OWN, IR_PROCESS/OWN, MR_CREATE/OWN, MR_FULFILL/BRANCH, SHIFT_VIEW/OWN, STOCK_ISSUE_POST/BRANCH, STOCK_RECEIPT_POST/BRANCH, STOCK_RETURN_POST/BRANCH, STOCK_TRANSFER_POST/BRANCH, STOCKTAKE_PERFORM/BRANCH, TASK_EXECUTE/OWN, TASK_VIEW/OWN |
