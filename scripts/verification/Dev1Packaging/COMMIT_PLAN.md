# Proposed logical commits — không chạy commit trong Task18

Index chung của Git không có ba vùng staging độc lập. Để tránh developer commit nhầm cả ba nhóm, Task18 chỉ selective stage **A (15 files)**. B/C đã audit và có exact commands bên dưới để dùng **sau khi** developer review/commit nhóm trước. Không có git reset/restore/unstage/delete.

| Thứ tự | Commit title đề xuất | Scope | File count |
|---|---|---|---:|
| 1 | feat(database): add guarded canonical setup and verification | Task15 source/verification/docs | 15 |
| 2 | docs(dev1): audit backend gaps and document packaging review | Task16 + Task18 reports | 9 |
| 3 | feat(users): add read-only UC-141 directory with global USER_VIEW | Task17 application/verification/docs | 13 |

Dependency: A trước C vì UserDirectory Prepare-Database đọc canonical scripts. B là historical audit baseline trước Task17; không đồng nghĩa API còn thiếu sau C. Không bundle raw evidence để thỏa dependency: teammate tự sinh receipts bằng harness.

## Group A — 15 files

```powershell
git add -- 'scripts/database/release/canonical/00_Canonical_Reference_Contract.sql' `
  'scripts/database/release/canonical/01_Canonical_Seed.sql' `
  'scripts/database/release/canonical/02_Canonical_Verify.sql' `
  'scripts/database/release/canonical/Run-Canonical.ps1' `
  'scripts/verification/CanonicalDatabase/CANONICAL_SEED_MANIFEST.md' `
  'scripts/verification/CanonicalDatabase/DATABASE_TRIAL_RESULTS.md' `
  'scripts/verification/CanonicalDatabase/DEMO_FIXTURES.md' `
  'scripts/verification/CanonicalDatabase/INSTALLATION_PLAN.md' `
  'scripts/verification/CanonicalDatabase/ModelProbe/ModelProbe.csproj' `
  'scripts/verification/CanonicalDatabase/ModelProbe/Program.cs' `
  'scripts/verification/CanonicalDatabase/OUTSTANDING_ISSUES.md' `
  'scripts/verification/CanonicalDatabase/README.md' `
  'scripts/verification/CanonicalDatabase/SCHEMA_COMPARISON.md' `
  'scripts/verification/CanonicalDatabase/Test-Catalog.ps1' `
  'scripts/verification/CanonicalDatabase/Test-Guards.ps1'
```

Task18 thực hiện đúng allowlist này nếu validation/index audit pass. Developer review `git diff --cached` trước khi commit riêng A.

## Group B — 9 files

```powershell
git add -- 'scripts/verification/Dev1BackendScope/FIRST_IMPLEMENTATION_PLAN.md' `
  'scripts/verification/Dev1BackendScope/IMPLEMENTATION_BACKLOG.md' `
  'scripts/verification/Dev1BackendScope/MODULE_OWNERSHIP.md' `
  'scripts/verification/Dev1BackendScope/README.md' `
  'scripts/verification/Dev1BackendScope/USE_CASE_COVERAGE_MATRIX.md' `
  'scripts/verification/Dev1Packaging/README.md' `
  'scripts/verification/Dev1Packaging/FILE_CLASSIFICATION.md' `
  'scripts/verification/Dev1Packaging/SECURITY_AUDIT.md' `
  'scripts/verification/Dev1Packaging/COMMIT_PLAN.md'
```

Chỉ stage nhóm này sau khi commit trước đã được developer xử lý và index sạch. Không chạy lệnh nếu index đang chứa changes của người khác.

## Group C — 13 files

```powershell
git add -- 'src/HotelOps.Api/Program.cs' `
  'scripts/verification/UserDirectory/API_CONTRACT.md' `
  'scripts/verification/UserDirectory/OUTSTANDING_ISSUES.md' `
  'scripts/verification/UserDirectory/Prepare-Database.ps1' `
  'scripts/verification/UserDirectory/Program.cs' `
  'scripts/verification/UserDirectory/README.md' `
  'scripts/verification/UserDirectory/UserDirectory.Verification.csproj' `
  'scripts/verification/UserDirectory/VERIFICATION_RESULTS.md' `
  'src/HotelOps.Api/Controllers/UsersController.cs' `
  'src/HotelOps.Api/DTOs/Users/UserDirectoryContracts.cs' `
  'src/HotelOps.Api/Services/Users/IUserDirectoryService.cs' `
  'src/HotelOps.Api/Services/Users/UserDirectoryQueries.cs' `
  'src/HotelOps.Api/Services/Users/UserDirectoryService.cs'
```

Chỉ stage nhóm này sau khi commit trước đã được developer xử lý và index sạch. Không chạy lệnh nếu index đang chứa changes của người khác.

## Review gates trước từng commit

1. So sánh `git diff --cached --name-only` với đúng allowlist group đang xử lý.
2. `git diff --cached --check`; review staged content, không chỉ working tree.
3. Không có JSON receipts, WAV, backup/recovery files, PFX, secrets, bin/obj/generated artifacts hoặc src/main.cs trong index.
4. Build và explicit regression evidence phải phù hợp source đang commit. C gồm đúng một Program.cs DI insertion; auth/middleware order và DEV2 source không đổi.
5. Developer tự quyết định commit message/commit/push/PR. Task18 không thực hiện các Git operations đó.

Raw evidence/old local files: xem denylist đầy đủ trong FILE_CLASSIFICATION.md. Không xóa chúng và không dùng blanket add. Cả bốn báo cáo Task18 thuộc B, do đó không stage chung A.
