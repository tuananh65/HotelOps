# Task45 - UC010 Assign Role v1 controlled release

DEV1 / FE-01, 2026-10-28 Asia/Saigon. Baseline:
dc81915917142de6b640221a1f428aba7e9331fc, feature/BE1-database-integration, upstream 0/0.
Task45 reviews Task44 against the developer's explicit P1-P12 technical approval. No additional
production behavior or permission is introduced. This does not replace formal PO/SRS approval
or finalize the draft Permission Matrix. Verification is in VERIFICATION_RESULTS.md.

## Security review

| Gate | Source and conclusion |
|---|---|
| Actor | Cookie authorization and HotelOpsCookieTicket; AssignAsync matches the SQL-verified current identity to the reference/session. Both Gate calls use SqlSessionService.ValidateSessionAsync with retained User/session locks in the caller transaction. Restricted and self assignment fail closed. |
| Authority | Both EvaluateGlobalAsync(USER_MANAGE) and EvaluateGlobalAsync(ROLE_MANAGE) are required. PermissionService/EfPermissionDataSource requery active/time-valid paths; only unbound ALL grants global authority. No role-name bypass or cached decision. |
| Delegation | UserRoleDelegationPolicy has exactly five ordinal codes/Department pairs. Matches compares the complete sorted permission/scope sequence, including count. Live Role flags and grants are checked twice under catalog locks. SQL seed/trial catalog agree with the approved appendix. |
| Target/scope | ACTIVE/nondeleted target, active primary Branch and matching active Department, existing unrevoked membership granted by current UTC. No membership creation or secondary Branch selection. |
| First role | ANY target UserRole history conflicts, including inactive/expired/future/unbound/OM. Only the own inserted row is excluded at final gate and its complete binding is verified separately. No prior role is updated. |
| HTTP | Strict roleCode/branchId rejects duplicate/unknown fields. Strong If-Match; missing428, stale412, session401, forbidden403, safety409, success201 with committed ETag. HTTPS/CSRF/no-store follow existing conventions. |
| Atomicity | Clean caller-owned ReadCommitted transaction, one scoped DbContext/connection. Conditional User.UpdatedAt update advances rowversion; fresh UserRole INSERT and CREATE audit commit together. Failure/early return disposes the uncommitted transaction. Target credential/status/scope/sessions are preserved. |
| Locks | Global applock FIRST; Users TABLOCKX then UserRoles/Roles/RolePermissions/Permissions retained locks; ordered Users and authority/membership/Branch/Department locks, SQL session validation. No late applock inside Add Branch resource locks. Both race orders were executed. |

No unresolved security finding within approved v1 was identified. This is a second review pass
by the coding agent, not a separate human/external security audit. Table locking increases
contention; external writers, expiry, load/deployment and response-loss limits remain explicit.

## Exact file allowlist

Only these 18 repository-relative paths belong in this controlled commit:

```text
src/HotelOps.Api/Controllers/UserRoleAssignmentController.cs
src/HotelOps.Api/DTOs/Users/AssignUserRoleRequest.cs
src/HotelOps.Api/Services/Users/UserRoleAssignmentService.cs
src/HotelOps.Api/Services/Users/UserRoleDelegationPolicy.cs
src/HotelOps.Api/Program.cs
scripts/verification/UserRoleAssignment/AssignmentHttpSuite.cs
scripts/verification/UserRoleAssignment/Program.cs
scripts/verification/UserRoleAssignment/UserRoleAssignment.Verification.csproj
scripts/verification/UserRoleAssignment/Inspect-TrialSchema.ps1
scripts/verification/UserRoleAssignment/README.md
scripts/verification/UserRoleAssignment/CONTRACT_AND_AUTHORIZATION.md
scripts/verification/UserRoleAssignment/PRIVILEGE_ESCALATION_REVIEW.md
scripts/verification/UserRoleAssignment/TRANSACTION_AND_CONCURRENCY.md
scripts/verification/UserRoleAssignment/VERIFICATION_RESULTS.md
scripts/verification/UserRoleAssignment/OUTSTANDING_BLOCKERS.md
scripts/verification/UserRoleAssignment/RELEASE_SUMMARY.md
scripts/verification/LastAdminProtection/MUTATION_PATH_MATRIX.md
scripts/verification/LastAdminProtection/REMAINING_UNPROTECTED_PATHS.md
```

Excluded: all JSON receipts/preflight snapshots, WAV, bin/obj, certificates, generated secrets,
temporary databases and unrelated artifacts. No DEV2 source, generated entities, DbContext,
schema, RBAC Catalog or shared authentication implementation changes. Program.cs adds only DI;
the two LastAdmin documents describe the additive participant without changing old limitations.

After all required gates PASS: stage explicit paths only, compare all 18 index paths to this
allowlist, inspect the full staged diff, scan credential patterns and check whitespace. Message:

```text
feat(users): implement secure role assignment v1
```

The commit identifies its own hash; no self-referential hash is embedded here. After commit,
verify its exact file set, empty index, clean tracked tree, preserved unrelated artifacts and
unchanged worktrees. No Push/Merge/Rebase/Reset or production deployment.
