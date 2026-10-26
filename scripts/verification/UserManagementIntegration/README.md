# Task27 — User Management Integration & Release Safety

Ngày2026-10-27 (Asia/Saigon). Branch feature/BE1-database-integration;HEAD20c1063e24cf170450d2dffaa81df6c7ef3a896f. origin feature trùngHEAD;origin/develop5299b5e9e8dd917abef327ab9f9bd5494064ccf9,đã kiểm tra trực tiếp bằng ls-remote;không fetch/merge.

**Final verdict: BLOCKED cho release.** Functional regressions tốt trên patched trial nhưng startup schema fail-fast chưa có;API thực tế vẫn listen và health200 trên unpatched DB trong khi new EF User projection lỗi207. Không ghi kết quả này là readiness PASS.

Task27 chỉ thêm integration verification/hồ sơ;không sửa application/SQL patch gốc hoặc sourceTask22/26. Giữ mọi untracked/local/rehearsal/AI-Loop worktrees;index trống. Dev1Packaging Task18 là historical allowlist của commits15–17 đã tồn tại,không dùng lại staging commands đó cho scope mới.

## Hồ sơ

- [Database compatibility và exact proposed startup diff](DATABASE_COMPATIBILITY.md)
- [Authorization doubles/runtime grants](AUTHORIZATION_VERIFICATION.md)
- [Regression thực tế](REGRESSION_RESULTS.md)
- [Exact changed/untracked file classification](FILE_CLASSIFICATION.md)
- [Commit/deploy sequence](COMMIT_AND_DEPLOY_PLAN.md)
- [Outstanding risks](OUTSTANDING_RISKS.md)

## Reproduce integration

Build solution và UserManagementIntegration.Verification.csproj. Verify-Schema.ps1 của Task26 tạo fresh/upgrade mới và receipts;fresh patched trial phải còn empty personnel. Tạo thêm canonical trial chưa patch qua existing UserDirectory/Prepare-Database.ps1. Không reuse/reset trials cũ.

```powershell
dotnet scripts/verification/UserManagementIntegration/bin/Debug/net10.0/UserManagementIntegration.Verification.dll <new-patched-empty-trial> <new-local-json> <new-unpatched-trial>
dotnet scripts/verification/UserManagementIntegration/bin/Debug/net10.0/UserManagementIntegration.Verification.dll --entity-reads <patched-trial>
```

Harness giữ existing329 profile/list/detail assertions,thêm actual old-schema startup diagnostic và real HTTP BRANCH/OWN bằng2 custom test-only roles. Finally revoke owned sessions và xóa test-role assignments/grants/roles do chính lượt tạo;không xóa official catalog rows. PFX tạm riêng được dọn,không xóa untracked files. Evidence local có success=true nghĩa assertions/observations đã hoàn tất,**releaseReady=false** là kết luận triển khai.

Không chạy migration/DDL/DML trên HotelOpsDB;không stage/commit/push/merge. Exact proposed source fix chưa được áp dụng theo yêu cầu report diff của task.

Final workspace verification:617/617 files có trước Task27 giữ nguyên SHA-256;chỉ thêm7 reports,project+harness,1local integration receipt tại folder này và1schema receipt mới do existing Task26 verifier sinh. Không application/source/schema patch edits. git diff --check/cached check PASS,index trống. Classification bao phủ197changed/untracked paths:66candidates,131exclude;pattern scan66candidates không phát hiện literal secret theo rule đã dùng (không phải chứng nhận DLP đầy đủ).
