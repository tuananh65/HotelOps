# Exact staging allowlist

Only these 21 files are eligible for the UC-004 commit.

## Application source (6)

- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Services/Authentication/TemporaryCredentialService.cs
- src/HotelOps.Api/Controllers/UserAccountCreationController.cs
- src/HotelOps.Api/DTOs/Users/CreateEmployeeAccountRequest.cs
- src/HotelOps.Api/Services/Authentication/IInitialCredentialTransactionParticipant.cs
- src/HotelOps.Api/Services/Users/UserAccountCreationService.cs

Program.cs adds scoped creation service and aliases the transaction participant to the existing credential service instance. TemporaryCredentialService extracts common write/audit logic while preserving the legacy transaction-owning API. These existing Task 30 shared edits are within the explicit Task 31 review/commit scope; no new shared infrastructure edits were needed.

## Reproducible verification (4)

- scripts/verification/UserAccountCreation/Program.cs
- scripts/verification/UserAccountCreation/UserAccountCreation.Verification.csproj
- scripts/verification/UserAccountCreation/Http/Program.cs
- scripts/verification/UserAccountCreation/Http/UserAccountCreation.Http.Verification.csproj

## UC-004 historical contract and evidence summaries (6)

- scripts/verification/UserAccountCreation/README.md
- scripts/verification/UserAccountCreation/API_CONTRACT.md
- scripts/verification/UserAccountCreation/AUTHORIZATION.md
- scripts/verification/UserAccountCreation/TRANSACTION_DESIGN.md
- scripts/verification/UserAccountCreation/VERIFICATION_RESULTS.md
- scripts/verification/UserAccountCreation/OUTSTANDING_ISSUES.md

## Task 31 release documentation (5)

- scripts/verification/UserAccountCreationRelease/README.md
- scripts/verification/UserAccountCreationRelease/FILE_MANIFEST.md
- scripts/verification/UserAccountCreationRelease/SECURITY_REVIEW.md
- scripts/verification/UserAccountCreationRelease/VERIFICATION_RESULTS.md
- scripts/verification/UserAccountCreationRelease/REMAINING_RISKS.md

## Excluded and retained

- All *.local.json and other runtime JSON receipts, including Task 31 output.
- scripts/database/release/codex-attention-alert.wav.
- CanonicalDatabase evidence/ and CREATED_FILES.txt; unrelated RESULTS.md reports.
- RbacUpgradeTrial/, RbacUpgradeDeployment/, UserManagementCommit/ and unrelated analysis.
- Backups, secrets, PFX, logs, bin/, obj/ and other generated/local artifacts.

No broad directory staging is permitted. Baseline tracked dependencies are referenced, not restaged. No file or worktree deletion is part of this task.
