# File classification — Tasks 15–17 / Task18 packaging

Baseline `5299b5e`, branch `feature/BE1-database-integration`. Ban đầu: một tracked modification (Program.cs), index trống và 155 untracked files. Task18 chỉ thêm bốn báo cáo; không sửa source Tasks15–17.

| Nhóm | Số file | Package action |
|---|---:|---|
| A — Canonical Database source/config + verification/docs | 15 | Selective stage trước, commit logic đầu tiên |
| B — DEV1 scope/planning + packaging review | 9 | Đã audit; chưa stage để không trộn commit A |
| C — UC-141 application + reproducible verification/docs | 13 | Đã audit; chưa stage để không trộn commit A |
| X — local/out-of-scope artifacts | 123 | Không stage; không xóa hoặc ghi đè |

Các nhóm A/B/C có 37 file: 36 new và một modification Program.cs. Không có schema/model/API DEV2 hoặc shared config change ngoài DI một dòng đã duyệt.

## A — exact allowlist

| File | Task/provenance | Quyết định |
|---|---|---|
| `scripts/database/release/canonical/00_Canonical_Reference_Contract.sql` | Task15 | Stage A |
| `scripts/database/release/canonical/01_Canonical_Seed.sql` | Task15 | Stage A |
| `scripts/database/release/canonical/02_Canonical_Verify.sql` | Task15 | Stage A |
| `scripts/database/release/canonical/Run-Canonical.ps1` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/CANONICAL_SEED_MANIFEST.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/DATABASE_TRIAL_RESULTS.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/DEMO_FIXTURES.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/INSTALLATION_PLAN.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/ModelProbe/ModelProbe.csproj` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/ModelProbe/Program.cs` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/OUTSTANDING_ISSUES.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/README.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/SCHEMA_COMPARISON.md` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/Test-Catalog.ps1` | Task15 | Stage A |
| `scripts/verification/CanonicalDatabase/Test-Guards.ps1` | Task15 | Stage A |

## B — exact allowlist

| File | Task/provenance | Quyết định |
|---|---|---|
| `scripts/verification/Dev1BackendScope/FIRST_IMPLEMENTATION_PLAN.md` | Task16 | Audited; giữ cho commit B |
| `scripts/verification/Dev1BackendScope/IMPLEMENTATION_BACKLOG.md` | Task16 | Audited; giữ cho commit B |
| `scripts/verification/Dev1BackendScope/MODULE_OWNERSHIP.md` | Task16 | Audited; giữ cho commit B |
| `scripts/verification/Dev1BackendScope/README.md` | Task16 | Audited; giữ cho commit B |
| `scripts/verification/Dev1BackendScope/USE_CASE_COVERAGE_MATRIX.md` | Task16 | Audited; giữ cho commit B |
| `scripts/verification/Dev1Packaging/README.md` | Task18 | Audited; giữ cho commit B |
| `scripts/verification/Dev1Packaging/FILE_CLASSIFICATION.md` | Task18 | Audited; giữ cho commit B |
| `scripts/verification/Dev1Packaging/SECURITY_AUDIT.md` | Task18 | Audited; giữ cho commit B |
| `scripts/verification/Dev1Packaging/COMMIT_PLAN.md` | Task18 | Audited; giữ cho commit B |

## C — exact allowlist

| File | Task/provenance | Quyết định |
|---|---|---|
| `src/HotelOps.Api/Program.cs` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/API_CONTRACT.md` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/OUTSTANDING_ISSUES.md` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/Prepare-Database.ps1` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/Program.cs` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/README.md` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/UserDirectory.Verification.csproj` | Task17 | Audited; giữ cho commit C |
| `scripts/verification/UserDirectory/VERIFICATION_RESULTS.md` | Task17 | Audited; giữ cho commit C |
| `src/HotelOps.Api/Controllers/UsersController.cs` | Task17 | Audited; giữ cho commit C |
| `src/HotelOps.Api/DTOs/Users/UserDirectoryContracts.cs` | Task17 | Audited; giữ cho commit C |
| `src/HotelOps.Api/Services/Users/IUserDirectoryService.cs` | Task17 | Audited; giữ cho commit C |
| `src/HotelOps.Api/Services/Users/UserDirectoryQueries.cs` | Task17 | Audited; giữ cho commit C |
| `src/HotelOps.Api/Services/Users/UserDirectoryService.cs` | Task17 | Audited; giữ cho commit C |

## X — toàn bộ untracked files bị loại

Không khẳng định mọi file bên dưới có secret. Đây là denylist do provenance/phạm vi, local receipt hoặc dữ liệu phục hồi chưa được phép đưa vào package. Không đọc rồi sao chép nội dung nhạy cảm vào báo cáo.

| File | Lý do loại |
|---|---|
| `scripts/database/release/codex-attention-alert.wav` | WAV local — không source package |
| `scripts/verification/Authorization/RESULTS.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/CanonicalDatabase/CREATED_FILES.txt` | Manifest local liệt kê cả artifacts, không phải commit allowlist |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff-guards.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_AuthUpgrade-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_Fresh-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_123559_826e9eff_Upgrade-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa-catalog.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa-guards.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_AuthUpgrade-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_Fresh-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/HotelOps_Canonical_20261026_124702_3bcc1bfa_Upgrade-schema.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh-final.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh-relational.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/ef-fresh.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-auth-upgrade.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-fresh.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/CanonicalDatabase/evidence/final-ef-upgrade.json` | Raw local trial/EF/catalog/schema evidence, gồm kết quả superseded; không commit |
| `scripts/verification/DataScope/RESULTS.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/HttpSqlIntegration/README.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/HttpSqlIntegration/RESULTS.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/PermissionService/RESULTS.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/RbacUpgradeDeployment/Task03A/BACKUP_RECOVERY_PLAN.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/DEPLOYMENT_READINESS_REPORT.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/EXPECTED_CHANGES_FOR_TASK03B.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/PREFLIGHT_RESULTS.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/Prepare-Task03A.ps1` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/README.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/VERIFICATION_PLAN.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/Verify-Rehearsal.ps1` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/backup-evidence.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/backup-rehearsal-state.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/deployment-state.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-commit.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-dry-run.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/initial-rehearsal-evidence/rehearsal-result.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/postdeployment-readonly.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/preflight-readonly.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/preflight-results.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/recovery-preflight-results.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-after.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-commit.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-dry-run.sql` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-result.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/rehearsal-verification.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/restore-proof.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03A/source-after-readonly.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/DEPLOYMENT_EXECUTION_REPORT.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/Deploy-Task03B.ps1` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/Inspect-Task03B.ps1` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/README.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/RECOVERY_AND_INCIDENT_NOTES.md` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/actual-changeset.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/artifact-validation.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/authentication-console-results.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/backup-verification.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/execution-evidence.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/postdeployment-fingerprints.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/postdeployment-results.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/predicted-grant-changes.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/preflight-evidence.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/preservation-baseline.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/read-only-harness-error.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/release04-results.txt` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeDeployment/Task03B/task03a-package-digests.json` | Deployment/recovery/backup package cũ; có khả năng chứa local state/fingerprints, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/README.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Run-DepartmentCleanup.ps1` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Run-Trial.ps1` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/README.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/department-cleanup.sql` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/dependency-before.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/dry-run-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/protected-file-hashes.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/result.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/source-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/source-before.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/trial-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/trial-before.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02B/verification-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/README.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/Run-Task02C.ps1` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/commit-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/department-dry-run-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/dry-run-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/postflight-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/preflight-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/preflight.sql` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/protected-file-hashes.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/recovery-point.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/release-verification-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/resolve-safe-dependencies.sql` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/result.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/source-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/source-before.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/trial-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/trial-before.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/user-reference-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02C/verification-results.txt` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02D/FINAL_RESOLUTION_PLAN.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/EXECUTION_PLAN.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/FINGERPRINT_BASELINES.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/FIXTURE_RESET_MANIFEST.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/FIXTURE_RESET_MANIFEST.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/FK_GRAPH.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Task02E/README.md` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/Verify-Trial.ps1` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/extra-baseline.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/schema-column-differences.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/source-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/trial-after.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/trial-before.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/trial-dry-run.sql` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/trial-state.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/trial-upgrade.sql` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/RbacUpgradeTrial/verification-result.json` | Rehearsal/migration/dependency/data snapshots cũ, ngoài phạm vi |
| `scripts/verification/ResourceAuthorization/RESULTS.md` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/ResourceAuthorization/preexisting-file-hashes.json` | Verification result/receipt trước Task15 hoặc file ngoài scope; giữ local |
| `scripts/verification/UserDirectory/uc141-results.json` | Raw local execution receipt; dùng Markdown summary đã audit |
| `src/main.cs` | File IDE/untracked của developer, ngoài Task15–17 |

## Ignored artifacts không thuộc Git change set

`bin/`, `obj/`, generated DLL/PDB/runtime assets và raw Task18 receipt `obj/Dev1Packaging/uc141-task18-results.json` không stage. Temporary PFX của harness nằm ngoài repository và bị xóa sau khi dừng đúng API child process. Không stage secrets.json, connection credentials, cookie/session material, backup/MDF/LDF/PFX hoặc local recovery receipts. Không dùng `git add .`, `git add -A` hoặc stage cả thư mục verification.

## Trạng thái index theo kế hoạch

Index chỉ chứa 15 file A; B/C và bốn báo cáo này chưa stage. Program.cs giữ tracked modification chưa stage. Untracked còn lại dự kiến 144 file (21 file package B/C mới + 123 exclusions). Kết quả index thực tế được kiểm tra bằng exact allowlist trước completion.
