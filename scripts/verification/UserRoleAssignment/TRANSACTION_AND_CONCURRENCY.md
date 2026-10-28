# Task44 implemented transaction and locking

Technical approval P1-P12 is now explicit. UserRoleAssignmentService.AssignAsync implements:

1. Validate trusted actor/session reference, deny restricted/self/nonallowlisted role, copy
   expected version and begin caller-owned local ReadCommitted transaction on a clean context.
2. GlobalAdminSerialization.AcquireAsync FIRST, then ReadSnapshotAsync: retained Users TABLOCKX,
   UserRoles -> Roles -> RolePermissions -> Permissions. Same EF connection/DbTransaction and
   transaction-owned applock; no early release or cached eligibility count.
3. Ordered actor/target Users UPDLOCK/HOLDLOCK; reuse existing authority lock acquisition only
   (not reset permission decisions) for actor paths, membership ranges and Branches. Snapshot
   also retains ROLE_MANAGE/catalog ranges. Retain requested Branch and target Department.
4. Initial Gate checks live SQL session, both effective unbound ALL permissions, target ACTIVE/
   nondeleted and original RowVersion, active primary Branch/Department and matching valid
   membership, exact live Role flags/envelope and absence of ANY target role path.
5. Conditional update only User.UpdatedAt to advance aggregate rowversion, fresh branch-bound
   UserRole INSERT, then narrow CREATE audit. Server actor/time/IsActive/ExpiresAt values. Save
   and detach assignment/audit; no credential, scope, membership or session mutation.
6. Read own updated User version. For a nonzero durable baseline, EnsurePreservedAsync rereads
   poststate. Zero-baseline pure additive workforce grants cannot remove an existing durable
   admin and do not turn this endpoint into global-admin bootstrap. The primitive is unchanged.
7. Final Gate rechecks session/permissions/time-sensitive authority, target/current version,
   scope/membership and exact envelope; ignore only the newly inserted row for conflict checking.
   Separately verify its complete intended binding/server metadata. Commit, then return DTO/ETag.
   Early outcome or exception disposes uncommitted transaction and rolls back version/role/audit.

The target cannot authorize the actor: self-grant is denied and inserted target role never
enters actor authority. Retained SQL locks prevent ordinary role/grant/master revocation or
insertion from overtaking this transaction; final checks also cover time passing while locked.

Actual HTTP/SQL tests cover Assign/Assign, both Add-first and Assign-first schedules, authority
assignment/permission revocation, candidate Role/grant drift before and after retained locks,
expiry at final gate, audit rollback, global timeout, cancellation and intentional deadlock/retry.
DMV checks use real blocking SPIDs/LCK_M waits. Trial drift uses controlled SQL writers because
no production Role/Grant management writer exists; this is not authorization for such an API.

Add Branch remains unchanged: it takes User locks before catalog/membership work and advances
the same aggregate token. Assign acquires global BEFORE its Users X snapshot, never between
Add's resource locks. If Add already holds User locks, Assign waits before taking catalog locks;
if Assign holds Users X, Add waits before taking its own authority/resource locks. Observed
controlled schedules produce one201/one412; no claim covers every external inverse order.

Conservative table locking increases contention. Global timeout503 and provider/deadlock500
fail closed with whole rollback; cancellation uses async transaction disposal. No automatic
retry. External writer/expiry, load and production deployment limits remain documented.

The earlier schema review/proposed sequence below is retained as historical design evidence.
Current source and executed tests above supersede its NOT IMPLEMENTED / NOT VERIFIED labels
only within the technically approved v1; formal SRS/PO approval is not claimed.

---
# Actual schema and proposed transaction plan

**No UC-010 mutation is implemented.** The sequence below is a design for approval and future
SQL/HTTP verification, not an executed Assign Role workflow.

## Schema evidence

Inspected generated UserRole, Role, RolePermission and UserBranchAccess entities,
`HotelOpsDbContext` mappings, and actual SQL metadata through Inspect-TrialSchema.ps1.
Authoritative local receipt for this review: `task44-schema-final.local.json`.

| Observed SQL structure | Implication / limit |
|---|---|
| user_roles: user_role_id, user_id, role_id, nullable branch_id, assigned_by, assigned_at, expires_at, is_active | Fresh branch-bound INSERT is representable. No delegation, protected/assignable, RevokedAt or per-assignment rowversion column. IsActive=false is not proof of a specific revocation procedure. |
| Unique user_roles_index_39(user_id,role_id,branch_id), unfiltered | Historical/inactive/expired tuples remain covered. A future handler must explicitly reject or implement approved regrant; duplicate INSERT is not a recovery policy. |
| Separate FKs from UserRole to Users, Roles, Branches and AssignedBy user | No FK to (user_id,branch_id) membership; no database proof of active/time-valid membership, role/Branch consistency, delegation or OM cardinality. |
| Unique user_branch_access_index_41(user_id,branch_id), unfiltered | Revoked membership tuple still exists; no implicit regrant. |
| Filtered UX_user_branch_access_primary(user_id) WHERE is_primary=1 AND revoked_at IS NULL | At most one unrevoked primary membership represented by this index; does not enforce User.PrimaryBranchId/DepartmentId alignment or OM single-Branch. |
| RolePermission CK_role_permissions_data_scope allows ALL/BRANCH/DEPARTMENT/OWN | SQL scope domain is broader than current evaluator, which does not grant DEPARTMENT. This is not delegation authority. |
| No other checks and no triggers on the four inspected tables in final receipt | No schema-enforced SoD, assignability or OM cardinality policy was found. Metadata evidence, not a mutation behavior test. |
| users.row_version: nonnullable SQL timestamp/rowversion | Existing aggregate If-Match pattern can be reused by a newly approved writer; no new schema is required just to advance User aggregate version. No automatic coverage of external writers is implied. |

The schema is sufficient to represent a narrow new assignment, but does not settle policy.
No schema change is requested or implemented. Physical duplicate/expired/regrant writes are
NOT tested as UC-010 operations; the metadata inspection is SELECT-only.

## Proposed sequence after policy approval

1. Parse strict `{roleCode, branchId}` and strong eight-byte If-Match; use actual SQL-backed full
   session/CSRF and server actor. No client DataScope/permissions/AssignedBy/IsActive/dates.
2. Begin local caller-owned ReadCommitted transaction with a clean HotelOpsDbContext. If using
   GlobalAdminSerialization, acquire its applock FIRST, then baseline snapshot (Users TABLOCKX,
   followed by retained UserRoles/Role/RolePermission/Permission locks). Never acquire it after
   Add Branch's resource/User locks. The guard does not authorize the request.
3. Retain actor/target User locks in ascending ID order; lock all existing and missing target
   UserRole tuple ranges, relevant authority paths and catalog/envelope rows. Retain membership
   ranges and destination Branch/Department rows in the existing compatible order. Recheck
   SQL session, pre-existing actor authority and approved delegation, target ACTIVE/nondeleted,
   rowversion, Role active, exact envelope, membership/primary/Department, tuple/SoD/OM rules.
4. Conditional User aggregate update, new UserRole INSERT and narrow CREATE audit in the same
   transaction. Do not modify previous UserRoles, credentials, status, PrimaryBranch, Department,
   memberships or sessions. Server supplies actor/time. Detach flushed entries before snapshot.
5. Recheck session/authority, catalog/envelope/target state and final effective capabilities at
   commit boundary. For a nonzero baseline, existing EnsurePreservedAsync can verify survivors;
   do not change the primitive's empty-baseline rule. Pure branch-bound insertion with no other
   changes has no admin-decreasing effect, so zero-baseline treatment must be specified in the
   approved caller contract rather than misusing the guard as a bootstrap endpoint.
6. Commit and return only committed DTO/version; on failure rollback token, UserRole and audit.
   Preserve stale If-Match semantics; retry requires fresh SQL state. No automatic role regrant.

## Add Branch / LastAdmin interaction

Existing `UserBranchAssignmentService.AddAsync` locks actor/target Users first, authority rows,
target UserRoles (including absent ranges), roles/grants, membership ranges and destination.
It refuses all existing destination role rows, including dormant ones, and unsafe OM expansion.
It never inserts UserRole or approves UC-010 delegation.

A future Assign writer must retain the same target aggregate lock/version and target role /
membership ranges; Add-first and Assign-first schedules both need real DMV observations and
final source/target checks. V1 proposal allows roles only in existing primary membership,
while Add creates secondary membership, so the race also needs different-destination aggregate
token tests. Neither workflow may bootstrap its own authority from newly created data.

Task43 global-first writers can block this schedule via Users X locks; adding an applock late
would invert ordering. Exact mixed schedules, lock escalation, cancellation/timeouts/deadlock
rollback and retry remain NOT VERIFIED for UC-010. Do not claim deadlock freedom from a sketch.

A pure new approved assignment is additive to effective capabilities. Updating old BranchId,
IsActive, expiry or role binding is a different reducing/regrant workflow and is excluded.
External SQL/catalog writers and time-driven expiry retain the published LastAdmin limitations.
