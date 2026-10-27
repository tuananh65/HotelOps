> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Task 32 - UC-005 Admin Credential Reset

Status: PARTIAL IMPLEMENTATION; HTTP endpoint BLOCKED by missing trusted reauthentication.
Date: 2026-10-27 (Asia/Saigon). DEV1 / FE-01.
Branch: feature/BE1-database-integration.
Baseline HEAD: 92f14634b92d8fda6e6b044b92bf318e76ee3168; upstream origin/feature/BE1-database-integration. Final local tracking comparison is 0 behind / 0 ahead. This agent performed no fetch/pull/push.

## Approved scope

The developer explicitly approved Task 32 policy in this session: USER_MANAGE with unbound ALL or valid BRANCH plus effective shared memberships; no OWN, self-reset or inactive/deleted targets; backend-verified recent reauthentication bound to actor and current session, up to five minutes; administrator confirmation of employee identity verification; If-Match rowversion; atomic rotation, revoke-all and audit; one-time handoff after commit. This scoped approval supersedes the historical proposal status of Task 21/24 for these decisions. It does not finalize other pending SRS permissions.

## Implemented independent components

- TemporaryCredentialService.ResetAsync via new IConditionalCredentialResetService: transaction-owned conditional reset, active/nondeleted target and self guards, SQL lock/rowversion check, existing hash/update/revoke/audit core, committed rowversion and one-time credential result.
- UserCredentialResetAccess: approved permission/scope helper, separate from profile authority, rejects self/OWN/branch-bound ALL and restricted actors.
- SQL verification harness for conditional reset, real session validation/revocation, faults, concurrency, retry and scope composition.

No controller, endpoint, DTO HTTP contract or new DI registration was added. The new helpers are not invoked by the application. They are low-level building blocks, not an authorized admin workflow. An interface on a concrete credential service is not proof of actor authorization, reauthentication or employee verification.

## Files

Modified:
- src/HotelOps.Api/Services/Authentication/TemporaryCredentialService.cs

Created source:
- src/HotelOps.Api/Services/Authentication/IConditionalCredentialResetService.cs
- src/HotelOps.Api/Services/Users/UserCredentialResetAccess.cs

Created verification:
- scripts/verification/UserCredentialReset/Program.cs
- scripts/verification/UserCredentialReset/UserCredentialReset.Verification.csproj
- README.md, API_CONTRACT.md, AUTHORIZATION.md, TRANSACTION_AND_SESSIONS.md, VERIFICATION_RESULTS.md, OUTSTANDING_ISSUES.md in this directory.

Runtime *.local.json are local evidence only, not source/package material. Existing artifacts and four worktrees are retained. No commit/stage/push/merge, entity/schema/catalog edits or DEV2 changes.

## Reproduce

1. dotnet build HotelOps.slnx
2. dotnet test HotelOps.slnx --no-restore (console suites are not discovered by this command).
3. dotnet build scripts/verification/UserCredentialReset/UserCredentialReset.Verification.csproj
4. Create NEW owned patched fresh/upgrade trials with ../UserProfileUpdate/Verify-Schema.ps1; use its guarded server parameter. Never target HotelOpsDB.
5. Run dotnet scripts/verification/UserCredentialReset/bin/Debug/net10.0/UserCredentialReset.Verification.dll <owned-patched-trial> <NEW-local-json>.
6. For UC-004/read/profile/startup regression, follow ../UserAccountCreation/README.md using a new empty patched trial and a separate new unpatched trial.
7. Run ../UserAccountCreation participant harness and the existing authorization/hardening console suites.

The reset harness uses actual SQL, synthetic owned actors/sessions and grant-path doubles with actual memberships. It does NOT reauthenticate an administrator or exercise an HTTP reset endpoint. It retains fixture users; finally revokes all sessions belonging to its own fixture IDs. No fixture/database deletion.
