# Task 33 - Secure admin reauthentication and credential reset

Task 34 review closes the grant/membership read-to-commit race with retained SQL authorization locks and adds AuthorizationRaceSuite.cs (six DMV-observed revoke races). See ../CredentialResetRelease/SECURITY_REVIEW.md and VERIFICATION_RESULTS.md for current release evidence; original Task 33 results below remain historical.

Date: 2026-10-27 (Asia/Saigon). DEV1 / FE-01, UC-005.
Branch: feature/BE1-database-integration. Baseline HEAD: 92f14634b92d8fda6e6b044b92bf318e76ee3168.
No stage/commit/push/merge or HotelOpsDB changes.

Implemented:
- POST /api/auth/reauthenticate verifies the current password for the exact current SQL-backed session and records a server-only proof.
- POST /api/users/{userId}/reset-credential combines that proof with Task 32 authority, employee identity attestation and If-Match under the reset transaction.
- No schema patch, new login session, JWT, bearer proof or hashing change.

The developer approved Task 32 ALL/BRANCH rules and, during Task 33, the strict identityVerificationConfirmed:true body with backend method INTERNAL_PROCEDURE_CONFIRMATION. This is an administrator's attestation about an offline procedure, not backend employee identity verification and not password reauthentication.

See SECURITY_DESIGN.md, SESSION_PROOF_CONTRACT.md, DATABASE_COMPATIBILITY.md, RESET_INTEGRATION.md and VERIFICATION_RESULTS.md. Task 32 documentation is historical; its missing-reauthentication blocker is resolved for this implementation.

## Files changed for Task 33

Shared wiring/credential implementation:
- src/HotelOps.Api/Program.cs: four registrations; existing scoped credential instance/middleware unchanged.
- src/HotelOps.Api/Services/Authentication/TemporaryCredentialService.cs: internal transaction gate and ordered user locks; audit attestation/outcome metadata; legacy interfaces preserved.
- src/HotelOps.Api/Services/Authentication/IConditionalCredentialResetService.cs: gated-workflow documentation and additional internal outcome statuses (Task 32 file).
- src/HotelOps.Api/Services/Users/UserCredentialResetAccess.cs: documentation reflects integration (Task 32 file).

New application files:
- src/HotelOps.Api/Controllers/AdminReauthenticationController.cs
- src/HotelOps.Api/Controllers/AdminCredentialResetController.cs
- src/HotelOps.Api/Services/Authentication/AdminReauthenticationService.cs
- src/HotelOps.Api/Services/Authentication/ReauthenticationProofStore.cs
- src/HotelOps.Api/Services/Users/AdminCredentialResetWorkflow.cs

Verification:
- AdminReauthentication.Verification.csproj, Program.cs and HttpSuite.cs here.
- ../UserCredentialReset/Program.cs: replace obsolete endpoint-absence assertion with authenticated-controller assertion; full gates are tested here.
- Seven requested documentation files here and supersession notices in the six historical Task 32 documents.
- New *.local.json receipts are local artifacts, never credential material or commit candidates.

No AuthController, generated entities, DbContext, session validator, middleware, RBAC seed, release SQL or DEV2 source changes.

## Reproduce safely

1. dotnet build HotelOps.slnx
2. dotnet test HotelOps.slnx --no-restore (console suites must run separately).
3. dotnet build scripts/verification/AdminReauthentication/AdminReauthentication.Verification.csproj
4. Create NEW guarded fresh/upgrade trials using ../UserProfileUpdate/Verify-Schema.ps1 with the verified local server. Create a separate unpatched trial for inherited startup tests using ../UserDirectory/Prepare-Database.ps1.
5. dotnet scripts/verification/AdminReauthentication/bin/Debug/net10.0/AdminReauthentication.Verification.dll <owned-patched-trial> <NEW-local-json>
6. Rebuild/run Task 32 primitive and UC-004 participant harnesses on the technical trial, and UC-004 HTTP harness on the separate empty patched trial plus negative startup trial.
7. Run the existing CurrentUserAccessor, PermissionService, Authorization, ResourceAuthorization and AuthorizationHardening console projects; git diff --check.

The harness checks server, database name and Task17 ownership marker before any fixture writes. It creates a unique temporary TLS certificate/child API and cleans only those resources. Owned users/fixtures/trials are retained; owned sessions revoked; test-only role/grant fixtures removed. Never reuse/reset a shared database or target HotelOpsDB.
