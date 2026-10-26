# Exact file classification — Task27 snapshot

Đối chiếu toàn bộ 197 changed/untracked paths (6tracked modifications,phần còn lại untracked). Task27 không stage bất kỳ file nào;listing này là allowlist review,không phải approval để commit khi release cònBLOCKED. Dev1Packaging Task18 là historical15–17 package đãcommit,không tái stage.

| Category | Count | Review action |
|---|---:|---|
| A | 4 | Selective candidate sau review |
| B | 5 | Selective candidate sau review |
| C | 4 | Selective candidate sau review |
| D | 53 | Selective candidate sau review |
| E | 131 | Exclude;preserve |

A=Task22 detail application;B=profile application;C=rowversion/EF;D=docs/reproducible verification;E=local/out-of-scope artifacts. Category không đồng nghĩa commit boundary;C phải tách SQL artifact khỏi EF+partial gắnatomic với app. Historical planning docs trongD được commit riêng,nội dung pending trong lịch sử không thay approvalTask26.

## A. Task22 — User Account Detail

| Exact path | Status / decision |
|---|---|
| `src/HotelOps.Api/Controllers/UsersController.cs` | Tracked diff;review exact scope |
| `src/HotelOps.Api/DTOs/Users/UserAccountDetailDto.cs` | New file candidate |
| `src/HotelOps.Api/Services/Users/IUserDirectoryService.cs` | Tracked diff;review exact scope |
| `src/HotelOps.Api/Services/Users/UserDirectoryService.cs` | Tracked diff;review exact scope |

## B. Task26 — User Profile application

| Exact path | Status / decision |
|---|---|
| `src/HotelOps.Api/Controllers/UserProfileController.cs` | New file candidate |
| `src/HotelOps.Api/DTOs/Users/UserProfileContracts.cs` | New file candidate |
| `src/HotelOps.Api/Program.cs` | Tracked diff;review exact scope |
| `src/HotelOps.Api/Services/Users/UserProfileAccess.cs` | New file candidate |
| `src/HotelOps.Api/Services/Users/UserProfileService.cs` | New file candidate |

## C. Database rowversion patch + EF

| Exact path | Status / decision |
|---|---|
| `scripts/verification/UserProfileUpdate/rowversion-forward.sql` | New file candidate |
| `scripts/verification/UserProfileUpdate/rowversion-rollback.sql` | New file candidate |
| `src/HotelOps.Api/Data/HotelOpsDbContext.cs` | Tracked diff;review exact scope |
| `src/HotelOps.Api/Entities/User.ProfileConcurrency.cs` | New file candidate |

## D. Documentation and reproducible verification

| Exact path | Status / decision |
|---|---|
| `scripts/verification/EffectiveAccessReview/API_CONTRACT.md` | New file candidate |
| `scripts/verification/EffectiveAccessReview/AUTHORIZATION_DECISION.md` | New file candidate |
| `scripts/verification/EffectiveAccessReview/OUTSTANDING_ISSUES.md` | New file candidate |
| `scripts/verification/EffectiveAccessReview/README.md` | New file candidate |
| `scripts/verification/EffectiveAccessReview/VERIFICATION_RESULTS.md` | New file candidate |
| `scripts/verification/HotelStructureRead/API_CONTRACT_PROPOSAL.md` | New file candidate |
| `scripts/verification/HotelStructureRead/AUTHORIZATION_MATRIX.md` | New file candidate |
| `scripts/verification/HotelStructureRead/IMPLEMENTATION_PLAN.md` | New file candidate |
| `scripts/verification/HotelStructureRead/README.md` | New file candidate |
| `scripts/verification/HotelStructureRead/USE_CASE_MAPPING.md` | New file candidate |
| `scripts/verification/UserAccountDetail/API_CONTRACT.md` | New file candidate |
| `scripts/verification/UserAccountDetail/OUTSTANDING_ISSUES.md` | New file candidate |
| `scripts/verification/UserAccountDetail/Program.cs` | New file candidate |
| `scripts/verification/UserAccountDetail/README.md` | New file candidate |
| `scripts/verification/UserAccountDetail/UserAccountDetail.Verification.csproj` | New file candidate |
| `scripts/verification/UserAccountDetail/VERIFICATION_RESULTS.md` | New file candidate |
| `scripts/verification/UserDirectory/Program.cs` | Tracked diff;review exact scope |
| `scripts/verification/UserManagement/API_CONTRACTS.md` | New file candidate |
| `scripts/verification/UserManagement/AUTHORIZATION_MATRIX.md` | New file candidate |
| `scripts/verification/UserManagement/IMPLEMENTATION_ROADMAP.md` | New file candidate |
| `scripts/verification/UserManagement/OUTSTANDING_DECISIONS.md` | New file candidate |
| `scripts/verification/UserManagement/README.md` | New file candidate |
| `scripts/verification/UserManagement/USE_CASE_MAPPING.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/AUTHORIZATION_VERIFICATION.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/COMMIT_AND_DEPLOY_PLAN.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/DATABASE_COMPATIBILITY.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/FILE_CLASSIFICATION.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/OUTSTANDING_RISKS.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/Program.cs` | New file candidate |
| `scripts/verification/UserManagementIntegration/README.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/REGRESSION_RESULTS.md` | New file candidate |
| `scripts/verification/UserManagementIntegration/UserManagementIntegration.Verification.csproj` | New file candidate |
| `scripts/verification/UserManagementMutation/DECISIONS_REQUIRED.md` | New file candidate |
| `scripts/verification/UserManagementMutation/IDENTITY_PROVISIONING.md` | New file candidate |
| `scripts/verification/UserManagementMutation/IMPLEMENTATION_SEQUENCE.md` | New file candidate |
| `scripts/verification/UserManagementMutation/README.md` | New file candidate |
| `scripts/verification/UserManagementMutation/ROLE_SCOPE_ASSIGNMENT.md` | New file candidate |
| `scripts/verification/UserManagementMutation/SESSION_AND_SECURITY.md` | New file candidate |
| `scripts/verification/UserManagementMutation/TRANSACTION_AND_CONCURRENCY.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/API_CONTRACT.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/AUTHORIZATION.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/DATABASE_CHANGE.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/OUTSTANDING_ISSUES.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/Program.cs` | New file candidate |
| `scripts/verification/UserProfileUpdate/README.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/UserProfileUpdate.Verification.csproj` | New file candidate |
| `scripts/verification/UserProfileUpdate/VERIFICATION_RESULTS.md` | New file candidate |
| `scripts/verification/UserProfileUpdate/Verify-Schema.ps1` | New file candidate |
| `scripts/verification/UserProfileUpdateGate/APPROVAL_REQUIRED.md` | New file candidate |
| `scripts/verification/UserProfileUpdateGate/CONTRACT.md` | New file candidate |
| `scripts/verification/UserProfileUpdateGate/DIFF_PROPOSAL.md` | New file candidate |
| `scripts/verification/UserProfileUpdateGate/README.md` | New file candidate |
| `scripts/verification/UserProfileUpdateGate/VERIFICATION_RESULTS.md` | New file candidate |

## E. Excluded local artifacts — không xóa

| Exact path | Status / decision |
|---|---|
| `scripts/database/release/codex-attention-alert.wav` | EXCLUDE — local/outside package |
| `scripts/verification/Authorization/RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/CREATED_FILES.txt` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff-guards.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_AuthUpgrade-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_Fresh-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_Upgrade-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa-catalog.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa-guards.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_AuthUpgrade-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_Fresh-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_Upgrade-schema.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh-final.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh-relational.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-auth-upgrade.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-fresh.json` | EXCLUDE — local/outside package |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-upgrade.json` | EXCLUDE — local/outside package |
| `scripts/verification/DataScope/RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/HttpSqlIntegration/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/HttpSqlIntegration/RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/PermissionService/RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/BACKUP_RECOVERY_PLAN.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/DEPLOYMENT_READINESS_REPORT.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/EXPECTED_CHANGES_FOR_TASK03B.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/PREFLIGHT_RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/Prepare-Task03A.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/VERIFICATION_PLAN.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/Verify-Rehearsal.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/backup-evidence.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/backup-rehearsal-state.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/deployment-state.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-commit.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-dry-run.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-result.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/postdeployment-readonly.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/preflight-readonly.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/preflight-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/recovery-preflight-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-commit.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-dry-run.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-result.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-verification.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/restore-proof.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03A/source-after-readonly.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/DEPLOYMENT_EXECUTION_REPORT.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/Deploy-Task03B.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/Inspect-Task03B.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/RECOVERY_AND_INCIDENT_NOTES.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/actual-changeset.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/artifact-validation.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/authentication-console-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/backup-verification.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/execution-evidence.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/postdeployment-fingerprints.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/postdeployment-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/predicted-grant-changes.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/preflight-evidence.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/preservation-baseline.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/read-only-harness-error.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/release04-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeDeployment/Task03B/task03a-package-digests.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Run-DepartmentCleanup.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Run-Trial.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/department-cleanup.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/dependency-before.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/dry-run-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/protected-file-hashes.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/result.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/source-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/source-before.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/trial-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/trial-before.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02B/verification-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/Run-Task02C.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/commit-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/department-dry-run-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/dry-run-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/postflight-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/preflight-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/preflight.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/protected-file-hashes.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/recovery-point.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/release-verification-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/resolve-safe-dependencies.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/result.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/source-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/source-before.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/trial-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/trial-before.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/user-reference-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02C/verification-results.txt` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02D/FINAL_RESOLUTION_PLAN.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/EXECUTION_PLAN.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/FINGERPRINT_BASELINES.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/FIXTURE_RESET_MANIFEST.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/FIXTURE_RESET_MANIFEST.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/FK_GRAPH.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Task02E/README.md` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/Verify-Trial.ps1` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/extra-baseline.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/schema-column-differences.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/source-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/trial-after.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/trial-before.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/trial-dry-run.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/trial-state.json` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/trial-upgrade.sql` | EXCLUDE — local/outside package |
| `scripts/verification/RbacUpgradeTrial/verification-result.json` | EXCLUDE — local/outside package |
| `scripts/verification/ResourceAuthorization/RESULTS.md` | EXCLUDE — local/outside package |
| `scripts/verification/ResourceAuthorization/preexisting-file-hashes.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserAccountDetail/task22-final-results.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserAccountDetail/task22-list-final-results.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserDirectory/uc141-results.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserManagementIntegration/integration-results.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserProfileUpdate/profile-final-results.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserProfileUpdate/profile-verified-results.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserProfileUpdate/schema-8be09caeab9e405590d1b0d45cda20a1.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserProfileUpdate/schema-9693edca36684f04a6fd90bde3b8f4b0.local.json` | EXCLUDE — local/outside package |
| `scripts/verification/UserProfileUpdateGate/task25-read-regression.local.json` | EXCLUDE — local/outside package |
| `src/main.cs` | EXCLUDE — local/outside package |

## Sensitive/generated exclusions

Không stage*.local.json,canonical evidence/receipts,old trial/deployment outputs,*.wav,src/main.cs hoặc generatedbin/obj/cert/key/backup/secrets. Ignoredbin/obj và worktrees không xuất hiện trong git ls-files --others nhưng cũng không xóa. .git/worktree metadata không đưa vàopackage. File names/hash receipts không chứng minh secret-free content;tất cả candidate code dùng runtime-generated fixture credentials,in-memory cookies/CSRF,không session values trong docs/evidence.

Shared diff review:Program.cs chỉ2 DI registrations Task26;DbContext chỉ1 RowVersion mapping;Task22 existing4 diffs giữ nguyên. Không application/DEV2/catalog/config/source edits trong Task27. Startup fix vẫn là text proposal trong DATABASE_COMPATIBILITY,không deployable implementation.
