# Exact commit allowlist — Task 29

Baseline: 20c1063; branch feature/BE1-database-integration. No push/merge/PR.

Order differs from the suggested order: startup gate precedes profile/EF so the first binary with the new EF mapping already has the guard. EF partial/mapping are atomic with profile. Startup runtime harness is deferred to the profile commit because it references profile types.

74 unique reviewed paths: original 66 candidates plus 8 Task 28 paths. Program.cs appears in two groups: only validator DI/startup in group 3; remaining two approved profile DI lines in group 4. Historical Task 20A–27 reports remain historical; use Task 28 harness for current startup expectations.

## Detail

- `src/HotelOps.Api/Controllers/UsersController.cs`
- `src/HotelOps.Api/DTOs/Users/UserAccountDetailDto.cs`
- `src/HotelOps.Api/Services/Users/IUserDirectoryService.cs`
- `src/HotelOps.Api/Services/Users/UserDirectoryService.cs`
- `scripts/verification/UserDirectory/Program.cs`
- `scripts/verification/UserAccountDetail/API_CONTRACT.md`
- `scripts/verification/UserAccountDetail/OUTSTANDING_ISSUES.md`
- `scripts/verification/UserAccountDetail/Program.cs`
- `scripts/verification/UserAccountDetail/README.md`
- `scripts/verification/UserAccountDetail/UserAccountDetail.Verification.csproj`
- `scripts/verification/UserAccountDetail/VERIFICATION_RESULTS.md`

## Database artifacts

- `scripts/verification/UserProfileUpdate/rowversion-forward.sql`
- `scripts/verification/UserProfileUpdate/rowversion-rollback.sql`
- `scripts/verification/UserProfileUpdate/Verify-Schema.ps1`
- `scripts/verification/UserProfileUpdate/DATABASE_CHANGE.md`

## Startup gate

- `src/HotelOps.Api/Data/StartupSchemaValidator.cs`
- `src/HotelOps.Api/Program.cs`
- `scripts/verification/StartupSchemaGate/README.md`
- `scripts/verification/StartupSchemaGate/SCHEMA_CONTRACT.md`
- `scripts/verification/StartupSchemaGate/IMPLEMENTATION.md`
- `scripts/verification/StartupSchemaGate/VERIFICATION_RESULTS.md`
- `scripts/verification/StartupSchemaGate/DEPLOYMENT_NOTES.md`

## Profile and approved planning/verification

- `src/HotelOps.Api/Controllers/UserProfileController.cs`
- `src/HotelOps.Api/DTOs/Users/UserProfileContracts.cs`
- `src/HotelOps.Api/Program.cs`
- `src/HotelOps.Api/Services/Users/UserProfileAccess.cs`
- `src/HotelOps.Api/Services/Users/UserProfileService.cs`
- `src/HotelOps.Api/Data/HotelOpsDbContext.cs`
- `src/HotelOps.Api/Entities/User.ProfileConcurrency.cs`
- `scripts/verification/EffectiveAccessReview/API_CONTRACT.md`
- `scripts/verification/EffectiveAccessReview/AUTHORIZATION_DECISION.md`
- `scripts/verification/EffectiveAccessReview/OUTSTANDING_ISSUES.md`
- `scripts/verification/EffectiveAccessReview/README.md`
- `scripts/verification/EffectiveAccessReview/VERIFICATION_RESULTS.md`
- `scripts/verification/HotelStructureRead/API_CONTRACT_PROPOSAL.md`
- `scripts/verification/HotelStructureRead/AUTHORIZATION_MATRIX.md`
- `scripts/verification/HotelStructureRead/IMPLEMENTATION_PLAN.md`
- `scripts/verification/HotelStructureRead/README.md`
- `scripts/verification/HotelStructureRead/USE_CASE_MAPPING.md`
- `scripts/verification/UserManagement/API_CONTRACTS.md`
- `scripts/verification/UserManagement/AUTHORIZATION_MATRIX.md`
- `scripts/verification/UserManagement/IMPLEMENTATION_ROADMAP.md`
- `scripts/verification/UserManagement/OUTSTANDING_DECISIONS.md`
- `scripts/verification/UserManagement/README.md`
- `scripts/verification/UserManagement/USE_CASE_MAPPING.md`
- `scripts/verification/UserManagementIntegration/AUTHORIZATION_VERIFICATION.md`
- `scripts/verification/UserManagementIntegration/COMMIT_AND_DEPLOY_PLAN.md`
- `scripts/verification/UserManagementIntegration/DATABASE_COMPATIBILITY.md`
- `scripts/verification/UserManagementIntegration/FILE_CLASSIFICATION.md`
- `scripts/verification/UserManagementIntegration/OUTSTANDING_RISKS.md`
- `scripts/verification/UserManagementIntegration/Program.cs`
- `scripts/verification/UserManagementIntegration/README.md`
- `scripts/verification/UserManagementIntegration/REGRESSION_RESULTS.md`
- `scripts/verification/UserManagementIntegration/UserManagementIntegration.Verification.csproj`
- `scripts/verification/UserManagementMutation/DECISIONS_REQUIRED.md`
- `scripts/verification/UserManagementMutation/IDENTITY_PROVISIONING.md`
- `scripts/verification/UserManagementMutation/IMPLEMENTATION_SEQUENCE.md`
- `scripts/verification/UserManagementMutation/README.md`
- `scripts/verification/UserManagementMutation/ROLE_SCOPE_ASSIGNMENT.md`
- `scripts/verification/UserManagementMutation/SESSION_AND_SECURITY.md`
- `scripts/verification/UserManagementMutation/TRANSACTION_AND_CONCURRENCY.md`
- `scripts/verification/UserProfileUpdate/API_CONTRACT.md`
- `scripts/verification/UserProfileUpdate/AUTHORIZATION.md`
- `scripts/verification/UserProfileUpdate/OUTSTANDING_ISSUES.md`
- `scripts/verification/UserProfileUpdate/Program.cs`
- `scripts/verification/UserProfileUpdate/README.md`
- `scripts/verification/UserProfileUpdate/UserProfileUpdate.Verification.csproj`
- `scripts/verification/UserProfileUpdate/VERIFICATION_RESULTS.md`
- `scripts/verification/UserProfileUpdateGate/APPROVAL_REQUIRED.md`
- `scripts/verification/UserProfileUpdateGate/CONTRACT.md`
- `scripts/verification/UserProfileUpdateGate/DIFF_PROPOSAL.md`
- `scripts/verification/UserProfileUpdateGate/README.md`
- `scripts/verification/UserProfileUpdateGate/VERIFICATION_RESULTS.md`
- `scripts/verification/StartupSchemaGate/Program.cs`
- `scripts/verification/StartupSchemaGate/StartupSchemaGate.Verification.csproj`

## Task 29 reports — local only

README.md, COMMIT_MANIFEST.md, SECURITY_REVIEW.md, VERIFICATION_RESULTS.md and RELEASE_RESTRICTIONS.md in this directory are deliberately not staged. Final hashes and post-commit verification will be recorded here. All *.local.json receipts remain excluded.

## Completed commits and exact snapshot proofs

| Order | Commit | Message | Files | Independently built tree |
|---|---|---|---:|---|
| 1 | efc83a11c156dcf0d35d007a540149d144269410 | feat(users): add user account detail endpoint | 11 | d76de2f0172560b676f188cf45cab98c387128cb |
| 2 | 2fa07fbbdcc2cad0c283d9833c4910f4895dbcf8 | feat(db): add user rowversion compatibility patch | 4 | 80b2e98c898cc5a615f8b1a42da0b86f82b5cec5 |
| 3 | 0b9df80178a798f72e473269bac6c774392dfae3 | fix(api): enforce startup database schema compatibility | 7 | 5c555634c101b3c8344ce1314d4bd4a55fd27071 |
| 4 | dda26e4648027a004bca1a2865e0302f0b063122 | feat(users): add concurrency-safe profile update | 53 | cc34f0893268ec77a89e949e3cda363200a85881 |

All four exact index trees built successfully with 0 warnings/errors before their commits. Each commit tree hash matches its validated snapshot. File lists for each commit match the corresponding exact group above; total 74 unique paths (Program.cs intentionally appears in groups 3 and 4). No excluded JSON/WAV/binary/credential artifact was committed. Cached whitespace checks passed for every group.

After each commit, all 638 pre-existing tracked/untracked files retained identical SHA-256 content. Original source, local artifacts and receipts were not rewritten; only Git tracking/history advanced. Final tracked working tree/index are clean; 140 untracked files remain, including the five local Task 29 reports and new receipts.

Remote feature remains 20c1063, remote develop remains 5299b5e, confirmed again after local commits. Current feature is ahead 4. No push/merge/PR/amend/rebase performed.

All worktrees still exist. External-state observation: AI-Loop worktree HEAD moved from 20c1063 to ba50d1b during this task; no commands modified that worktree. A read-only git log attempt there was refused by sandbox ownership checks; no safe.directory configuration was changed. Rehearsal worktrees remain at 3688ba9 and d6ef057. This external movement did not alter the verified DEV1 source/index.
