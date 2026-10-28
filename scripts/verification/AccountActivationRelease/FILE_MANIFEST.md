# Exact commit allowlist — Task37

32 unique paths:6 API source/DI files,8 verification source/project/scripts,18 related Markdown documents. This is the complete allowlist; no directory-wide staging. Runtime evidence is never implicitly included.

## A. API source and DI (Task35–36)

- `src/HotelOps.Api/Program.cs`
- `src/HotelOps.Api/Controllers/UserAccountActivationController.cs`
- `src/HotelOps.Api/DTOs/Users/UserAccountActivationRequest.cs`
- `src/HotelOps.Api/DTOs/Users/UserAccountStatusChange.cs`
- `src/HotelOps.Api/Services/Users/UserAccountActivationService.cs`
- `src/HotelOps.Api/Services/Users/UserAccountStatusAccess.cs`

## B. Task35 reproducibility and historical decision record

- `scripts/verification/UserAccountStatus/UserAccountStatus.Verification.csproj`
- `scripts/verification/UserAccountStatus/Program.cs`
- `scripts/verification/UserAccountStatus/ActivationSqlSuite.cs`
- `scripts/verification/UserAccountStatus/Inspect-Dependencies.ps1`
- `scripts/verification/UserAccountStatus/README.md`
- `scripts/verification/UserAccountStatus/API_CONTRACT.md`
- `scripts/verification/UserAccountStatus/AUTHORIZATION.md`
- `scripts/verification/UserAccountStatus/BUSINESS_RULES.md`
- `scripts/verification/UserAccountStatus/TRANSACTION_AND_CONCURRENCY.md`
- `scripts/verification/UserAccountStatus/VERIFICATION_RESULTS.md`
- `scripts/verification/UserAccountStatus/OUTSTANDING_ISSUES.md`

## C. Task36 HTTP/race verification and current activation contract

- `scripts/verification/UserAccountActivation/UserAccountActivation.Verification.csproj`
- `scripts/verification/UserAccountActivation/Program.cs`
- `scripts/verification/UserAccountActivation/ActivationRaceSuite.cs`
- `scripts/verification/UserAccountActivation/ActivationHttpSuite.cs`
- `scripts/verification/UserAccountActivation/README.md`
- `scripts/verification/UserAccountActivation/API_CONTRACT.md`
- `scripts/verification/UserAccountActivation/AUTHORIZATION_AND_RACE.md`
- `scripts/verification/UserAccountActivation/VERIFICATION_RESULTS.md`
- `scripts/verification/UserAccountActivation/DEV2_DEACTIVATION_HANDOFF.md`

## D. Task37 release review and handoff

- `scripts/verification/AccountActivationRelease/README.md`
- `scripts/verification/AccountActivationRelease/FILE_MANIFEST.md`
- `scripts/verification/AccountActivationRelease/SECURITY_REVIEW.md`
- `scripts/verification/AccountActivationRelease/VERIFICATION_RESULTS.md`
- `scripts/verification/AccountActivationRelease/DEV2_HANDOFF_MESSAGE.md`
- `scripts/verification/AccountActivationRelease/REMAINING_RISKS.md`

## Excluded and preserved

Every *.json runtime receipt (including task37-*.local.json and schema receipts), WAV, backup, binary/certificate, bin/obj export/build output, secrets/local configuration, unrelated Task20A–31 analysis/release artifacts and all other untracked files/worktrees. No new schema patch, migration, RBAC seed, DEV2 source or generated entity/mapping is included. Task35 documents describe a historical blocked snapshot; Task36 and this release supersede activation-only blockers. Deactivation business/last-admin/open-work proposals remain unimplemented.

Existing verification/schema/UC004/UC005/foundation dependencies already tracked at baseline HEAD 1a969e3731d12d430a250e219caa1e1fbfeb52b9 remain dependencies, not additional staged files. The tested API source/index blob identity and exact file list are checked before commit; exact resulting hash/list are reported after commit without amend.
