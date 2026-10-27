# Explicit selective staging allowlists

33 distinct paths total. Program.cs is intentionally split between the two commits; no broad directory staging.

## Commit 1 - reauthentication (5 paths)

- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Controllers/AdminReauthenticationController.cs
- src/HotelOps.Api/Services/Authentication/AdminReauthenticationService.cs
- src/HotelOps.Api/Services/Authentication/ReauthenticationProofStore.cs
- scripts/verification/AdminReauthentication/SESSION_PROOF_CONTRACT.md

Program.cs index patch includes only ReauthenticationProofStore singleton and AdminReauthenticationService scoped registration.

## Commit 2 - authorized reset (29 paths)

### A/C - Credential primitive and reset orchestration

- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Services/Authentication/TemporaryCredentialService.cs
- src/HotelOps.Api/Services/Authentication/IConditionalCredentialResetService.cs
- src/HotelOps.Api/Services/Users/UserCredentialResetAccess.cs
- src/HotelOps.Api/Services/Users/AdminCredentialResetWorkflow.cs
- src/HotelOps.Api/Controllers/AdminCredentialResetController.cs

Program.cs adds only the remaining reset-access/workflow registrations relative to commit 1.

### D - Task 32 reproducible harness and historical documentation

- scripts/verification/UserCredentialReset/Program.cs
- scripts/verification/UserCredentialReset/UserCredentialReset.Verification.csproj
- scripts/verification/UserCredentialReset/README.md
- scripts/verification/UserCredentialReset/API_CONTRACT.md
- scripts/verification/UserCredentialReset/AUTHORIZATION.md
- scripts/verification/UserCredentialReset/TRANSACTION_AND_SESSIONS.md
- scripts/verification/UserCredentialReset/VERIFICATION_RESULTS.md
- scripts/verification/UserCredentialReset/OUTSTANDING_ISSUES.md

### D - Task 33 integrated verification and documentation

- scripts/verification/AdminReauthentication/Program.cs
- scripts/verification/AdminReauthentication/HttpSuite.cs
- scripts/verification/AdminReauthentication/AuthorizationRaceSuite.cs
- scripts/verification/AdminReauthentication/AdminReauthentication.Verification.csproj
- scripts/verification/AdminReauthentication/README.md
- scripts/verification/AdminReauthentication/SECURITY_DESIGN.md
- scripts/verification/AdminReauthentication/DATABASE_COMPATIBILITY.md
- scripts/verification/AdminReauthentication/VERIFICATION_RESULTS.md
- scripts/verification/AdminReauthentication/RESET_INTEGRATION.md
- scripts/verification/AdminReauthentication/OUTSTANDING_ISSUES.md

SESSION_PROOF_CONTRACT.md is already in commit 1.

### D - Task 34 release dossier

- scripts/verification/CredentialResetRelease/README.md
- scripts/verification/CredentialResetRelease/FILE_MANIFEST.md
- scripts/verification/CredentialResetRelease/SECURITY_REVIEW.md
- scripts/verification/CredentialResetRelease/VERIFICATION_RESULTS.md
- scripts/verification/CredentialResetRelease/RELEASE_LIMITATIONS.md

## E - Excluded and preserved

- All runtime JSON, including *.local.json receipts from Task 32/33/34 and schema receipts.
- WAV, backups, PFX/certificates, binaries, bin/obj, exported snapshot directories and staging patch files.
- Existing Authorization/CanonicalDatabase/DataScope/HttpSqlIntegration/RbacUpgrade*/UserManagementCommit artifacts and other unrelated reports.
- Task 20A-31 documentation/source outside this feature; already tracked dependencies are not restaged.
- Secrets, connection credentials and local configuration.

No deletion, all-files staging, branch switch, worktree mutation, amend, merge or push is part of the release.
