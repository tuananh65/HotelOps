# Task44 - UC-010 Assign Role v1

Task45 security re-review, rerun evidence and controlled commit manifest are recorded in
RELEASE_SUMMARY.md and the Task45 section of VERIFICATION_RESULTS.md. Task44's no-commit
statements below describe its implementation stage; Task45 separately authorizes one bounded
release commit after verification.

Implemented under the developer's explicit P1-P12 technical approval, 2026-10-28 Asia/Saigon.
This approval does not claim to replace formal SRS/PO approval or finalize the Permission Matrix.

POST /api/users/{userId}/roles accepts strict JSON {roleCode, branchId} and a strong User.RowVersion
If-Match. It requires a full SQL-backed session, CSRF, effective unbound USER_MANAGE/ALL AND
ROLE_MANAGE/ALL, and a different target. No role-name bypass or runtime permission-subset shortcut.

Delegation is limited to exact approved envelopes: HK_STAFF/HK, HK_SUPERVISOR/HK,
MAINTENANCE_STAFF/MT, MAINTENANCE_LEAD/MT and STOREKEEPER/WH. Target is ACTIVE/nondeleted,
with active matching primary Branch/Department and existing valid membership. Any historical,
dormant, expired, future, unbound or OM role path is a conflict. No second role, secondary-Branch
role, protected/global/custom role, regrant, membership creation, Role Revoke or Deactivate.

UserRole INSERT, User aggregate version and CREATE audit commit atomically. Credentials,
status, PrimaryBranch, Department, memberships and target sessions are preserved. Success is
201 with committed DTO/ETag; missing tag428, malformed400, stale412, session401, forbidden403,
safety/duplicate409 and global serialization timeout503. Audit/provider/deadlock failures use
existing sanitized middleware and roll back. No automatic retry/regrant is installed.

Documentation:

- CONTRACT_AND_AUTHORIZATION.md: current approved contract, followed by historical proposal.
- PRIVILEGE_ESCALATION_REVIEW.md: exact five grant/scope envelopes and deliberate exclusions.
- TRANSACTION_AND_CONCURRENCY.md: implemented global-first lock order and schema basis.
- VERIFICATION_RESULTS.md: executed SQL/HTTP tests, regressions and limitations.
- OUTSTANDING_BLOCKERS.md: boundaries outside v1 and formal/deployment approvals.

Production files: Controllers/UserRoleAssignmentController.cs, DTOs/Users/AssignUserRoleRequest.cs,
Services/Users/UserRoleAssignmentService.cs, Services/Users/UserRoleDelegationPolicy.cs, and one
scoped registration in Program.cs. No entity, DbContext, existing controller/service, permission
catalog, authentication semantics or DEV2 source edits.

Verification source: UserRoleAssignment.Verification.csproj, Program.cs and AssignmentHttpSuite.cs.
Inspect-TrialSchema.ps1 remains the earlier SELECT-only schema/catalog inspector. Earlier review
artifacts and receipts are preserved; no result is overwritten.

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx
dotnet build scripts/verification/UserRoleAssignment/UserRoleAssignment.Verification.csproj
dotnet scripts/verification/UserRoleAssignment/bin/Debug/net10.0/UserRoleAssignment.Verification.dll <owned-patched-trial> <NEW-local-receipt>
```

The console harness is not discovered by dotnet test. It verifies exact SQL server/database,
canonical trial name, Task17 marker and compatible schema before fixture writes. Actual HTTPS
uses an ephemeral pinned certificate, real login/cookies/CSRF/SQL sessions and production service.
Test-only catalog/master drift and audit faults occur exclusively in disposable trials, are
restored in finally, and catalog/master equality is checked after cleanup. Existing trials,
owned users/audits and receipts remain; only owned temporary role/grant/session/fault/TLS/process
resources are cleaned. No HotelOpsDB access, production catalog/DEV2 edit, Commit/Push/Merge,
artifact deletion or worktree changes. JSON receipts, binaries and secrets are not deliverables.
