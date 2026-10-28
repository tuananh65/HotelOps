# POST /api/users — UC-004

Cookie authentication plus valid SQL session; existing CSRF header/cookie flow. Both Global USER_MANAGE and Global ROLE_MANAGE are required, independently, without branch binding. No USER_VIEW prerequisite is introduced. Success has Cache-Control no-store and Location /api/users/{id}.

```json
{
  "employeeCode": "HR-provided-code",
  "username": "employee.username",
  "fullName": "Employee Name",
  "email": "employee@example.invalid",
  "phone": null,
  "branchId": 1,
  "departmentId": 1,
  "roleCode": "HK_STAFF"
}
```

Example IDs are selectors, not authorization evidence or hard-coded business IDs. Exactly one branch/department/role. Unknown, duplicate or case-mismatched JSON keys are rejected. Required strings are trimmed/nonempty; storage maxima employeeCode40, username100, fullName200, email150, phone20, roleCode60. Email format follows existing EmailAddressAttribute convention. Phone may be omitted/null/blank (normalizes null), never a numeric JSON value. Reference IDs must be positive Int64. No new employee-code regex is invented; SQL varchar fields are checked for lossless representability/byte length under the actual database collation.

Client cannot set password/hash, account/employment status, actor, grants, scope, expiry, manager, role arrays or assignment metadata. Account starts ACTIVE; employment starts at the existing ACTIVE schema default value. Branch/department must be active, department must belong to branch and match the approved role code mapping.

201 body has exactly user and temporaryCredential. user fields: id,employeeCode,username,fullName,email,phone,branchId,departmentId,roleCode,accountStatus. temporaryCredential has password and expiresAtUtc, and exists only in this success response. No EF entity, hash, session key, token or effective permission graph is returned.

- 400: invalid/duplicate/unknown input; invalid/inactive/mismatched masters; role outside approved initial allowlist. Existing CSRF errors also use 400.
- 401: anonymous/invalid/expired/revoked SQL session.
- 403: restricted session or missing/ineligible either grant, including branch-bound ALL/BRANCH/OWN.
- 409: employeeCode/username/email uniqueness collision, including inactive/deleted records. duplicateFields reports confirmed field names using database equality/collation, without exposing another account's ID/data. Database constraints are the concurrency backstop, not prechecks.
- Generic 500/503: existing exception/session-store convention; no credential handoff on failure or commit uncertainty. Unsupported media types follow existing framework 415 behavior.

No If-Match is required for a new resource. There is no plaintext retrieval/replay endpoint or automatic password rotation on retry. If commit succeeded but the response is lost, repeating the same identity conflicts; use separately authorized reset workflow when available. Reset is not implemented here.
