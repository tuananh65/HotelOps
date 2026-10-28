# GET /api/users

Implemented scope: **UC-141 list/search/filter/pagination only**, per Task16 plan and Task17. This does not finish the UC-141 account-detail/effective-permissions step (UC-005/011).

## Authentication and scope

Existing cookie/SQL session validation and `HotelOps.Permission:Global:USER_VIEW` policy are mandatory. The existing PermissionService evaluates the validated caller; no role-name bypass, new grants or permission matrix changes.

- Effective ALL with null branch assignment: permitted.
- BRANCH, OWN, legacy/unknown scope or branch-bound ALL: denied for this endpoint, even when branchId matches. Scoped-administrator directory behavior has no approved contract here; it is not widened to global access.
- Inactive/deleted caller, inactive role/assignment, future/expired assignment, invalid/revoked session or restricted temporary-password session cannot obtain list data.
- Branch/department filters only narrow target users after authorization. Branch filter means **User.PrimaryBranchId**, not membership in any UserBranchAccess row. Department is the target User.DepartmentId. No client-supplied identity is used for authorization.

## Query

| Parameter | Behavior |
|---|---|
| search | Optional, trimmed substring of FullName/Username/Email; blank ignored. Maximum raw length 200 characters is a technical input bound aligned with FullName storage, not a new SRS business rule. SQL Server collation controls case/accent behavior. SQL-special characters are literal/parameterized |
| branchId | Optional positive Int64; primary branch filter. Unknown ID returns empty results |
| departmentId | Optional positive Int64; department filter. When branchId is supplied, department must exist in that branch or 400 |
| roleId | Optional positive Int64; at least one active assignment of an active role, AssignedAt <= captured UTC now, ExpiresAt null or > now. Unknown ID returns empty results |
| accountStatus | Optional Active/Inactive, case-insensitive and trimmed, mapped to existing ACTIVE/INACTIVE database values; blank ignored. Other values, including Locked, rejected |
| page | Int32 >=1; default 1. Offset must fit Int32 |
| pageSize | 1–100; default 20 (SYS-PAR-11) |

No user-controlled sort expression. Order is FullName ascending, then UserId ascending. Non-deleted inactive target users are included by default and may be filtered; DeletedAt != null is always excluded. AccountStatus is independent of EmploymentStatus.

Role filter and displayed roles use the same active/time-valid assignment predicate. Displayed role assignments include their own branch; this is assignment information, **not a computed effective-permissions response**. It does not claim that a target with inactive account/branch or missing branch access may execute those roles' permissions.

## Response

`200 application/json`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

Each item exposes only `id`, `employeeCode`, `username`, `fullName`, `email`, `primaryBranch`, `department`, `roles`, `accountStatus`. Branch/department values are null or `{ id, code, name }`. Each role is `{ id, code, name, branch }`. IDs are Int64 JSON numbers. Roles are ordered by code and assignment ID. No EF entity/navigation graph is serialized.

Password/hash/reset material, temporary credentials/expiry, session data, auth tokens, failed-login counters and lock internals are absent. Responses are no-store. TotalCount uses the same filtered query before paging, not page length. No matches and valid pages beyond the last return 200 with an empty items array.

HTTP statuses: **200** allowed list; **400** invalid query or mismatched branch/department; **401** missing/invalid/revoked session; **403** insufficient permission or restricted session. Existing authentication failure/error contracts remain in effect; provider/catalog failure never yields permission or a successful list. No new global error middleware or fallback policy was introduced.

## Persistence behavior

UserDirectoryService performs only SELECTs: normally count + projected page (two queries), plus one branch/department consistency check when both IDs are present. Filtering/pagination occur in SQL; no per-user query and no full-table materialization before filters. Count and page capture the same assignment UTC timestamp, but are separate read-committed queries, not a transactionally frozen snapshot during unrelated concurrent writes.

No user/assignment/catalog/audit mutation is added by this service. Existing authentication middleware may touch the validated session's last-used/expiry values; this behavior is preserved, not disabled to make a GET test appear completely write-free. No new session is created by the directory endpoint.
