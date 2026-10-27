# Authorization and approved initial roles

SRS UC-004 identifies Administrator and requires account/selected scope/roles plus temporary credential in one transaction. Catalog has USER_MANAGE and ROLE_MANAGE as distinct permissions; currently SYS_ADMIN has both ALL paths, but the role name is never used as an actor bypass. USER_MANAGE does not imply ROLE_MANAGE.

Task 30 user approval explicitly resolves the previous assignment ambiguity:

| Initial role | Required Department code |
|---|---|
| HK_STAFF | HK |
| HK_SUPERVISOR | HK |
| MAINTENANCE_STAFF | MT |
| MAINTENANCE_LEAD | MT |
| STOREKEEPER | WH |
| RECEPTION | FO |

Exactly one active catalog role, one active branch, one active department in that branch. SYS_ADMIN, OWNER, OPERATIONS_MANAGER, DEPT_MANAGER, HK_INSPECTOR, STAFF, unknown/inactive roles and roleless accounts are not accepted. The map is a UC-004 admission policy, not a role-permission grant change. No role/branch IDs are hard-coded. The explicit approval takes precedence over the historical matrix's pending Reception/DH wording; no room-operation behavior or catalog record changes.

The controller requires both existing Global policies. Service re-evaluates both permissions inside its own transaction and resolves the actor from validated CurrentUserAccessor. Only eligible branch-unbound ALL grants pass; no client actor/scope fields, claims-only role checks or PrimaryBranchId shortcut. A valid global actor may create in any active branch; a branch-bound actor cannot create even within its branch under this contract.

Role assignment BranchId is always the validated target branch, never null. PrimaryBranchId/DepartmentId and exactly one UserBranchAccess are written consistently, with server actor/time, STANDARD access level and IsPrimary=true. New target receives no SYS_ADMIN/OWNER or extra grant. Role/branch/department changes after creation remain UC-007/010, outside this task.

Authorization is rechecked in the mutation transaction; this does not introduce global serialization against concurrent revocation by another authorization writer. It preserves the current foundation's in-flight boundary. Master active-state checks are protected by SQL locks until commit.
