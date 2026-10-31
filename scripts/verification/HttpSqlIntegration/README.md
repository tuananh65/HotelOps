# HTTPS + real SQL session integration

Quy trình hiện tại được harden ở Task08: [TEST_ENVIRONMENT_SETUP.md](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md). [RESULTS.md](RESULTS.md) giữ báo cáo Task06 lịch sử; xem [kết quả Task08](../AuthorizationHardening/VERIFICATION_RESULTS.md) cho lượt chạy mới.

## Configuration và prerequisites

Windows Development, .NET SDK theo global.json, SQL Server 2022+; target cố định HotelOps_Authorization_Test. Server lấy từ Integration configuration riêng, không hard-code instance của DEV1 và không đọc ConnectionStrings:HotelOpsDb.

User Secrets ID HotelOps.Authorization.Integration.Task06 hoặc environment: ConnectionStrings:Integration / ConnectionStrings__Integration. ExpectedServer: Integration:ExpectedServer hoặc HotelOpsIntegration__ExpectedServer. Credentials ứng dụng it06.* được sinh riêng vào local User Secrets; không copy password/session từ database chính.

Setup và test runner không cần package RbacUpgradeTrial/Deployment hoặc deployment-state. Source/helpers phải được commit cùng nhau; private receipts, fixture manifest, credentials và logs giữ local.

## Chạy

Từ repository root sau khi cấu hình local và được phép dùng database test:

```powershell
./scripts/verification/HttpSqlIntegration/Prepare-Secrets.ps1
# Bỏ flag khi chỉ muốn kiểm tra DB đã tồn tại có receipt đúng.
./scripts/verification/HttpSqlIntegration/Prepare-Database.ps1 -CreateIfMissing
dotnet restore scripts/verification/HttpSqlIntegration/HttpSqlIntegration.csproj
dotnet run --project scripts/verification/HttpSqlIntegration/HttpSqlIntegration.csproj --no-restore
./scripts/verification/HttpSqlIntegration/Verify-Preservation.ps1
```

Fresh database chỉ dùng Full Schema 01 và canonical RBAC Seed 03, không Upgrade. Existing database thiếu receipt/marker hoặc identity/date/hash không khớp phải dừng, không adopt/reset/reseed/restore đè. Partial checkpoint không replay. Không xóa receipt/manifest để ép setup tiếp tục.

Prepare-Secrets giữ credentials có sẵn, không reset password của fixture. Restricted credential hết hạn sau 7 ngày phải báo blocker để review, không tự gia hạn/reset.

## Security boundary và cleanup

Kestrel chỉ HTTPS loopback, certificate riêng được client pin; Data Protection ephemeral chỉ test host. Cookie scheme/events, CSRF, SQL validation/touch/revocation, login/workflow, CurrentUserAccessor, permission service/handler/catalog/resolver đều là implementation production. Login decorator chỉ ghi journal session đã commit, không fake result.

Guard trước mọi EF command xác minh server, DB_NAME, DB_ID, create_date và provenance; connection sai target bị từ chối trước SQL. Metadata timestamp parse chặt. File lock ngăn runner cùng package đồng thời, không khóa external application.

Các route /api/integration/* chỉ có trong executable test. POST probe trả 204, không triển khai workflow Cleaning. Controlled SQL resource-read THROW kiểm thử fail-closed, không phải SQL outage/failover certification.

Cleanup dùng FE-01 logout cho chính session tạo trong journal, giữ audit/history/fixtures và trả inactive fixture về INACTIVE. Process kill hoặc mất SQL có thể ngăn finally; kiểm tra sessions với quyền test phù hợp, không reset DB.

Task08 đã chạy lại 102 assertions; test state sau chạy có 0 session chưa revoke. Verify-Preservation entry point hiện chỉ SELECT state database test; không đọc main configuration hay chạy main fingerprint. Kiểm chứng main data của Task08 là one-time review SELECT audit riêng.

## Evidence và validation khác

environment.json, fixtures.json, provisioning checkpoint, results.json, test-final-state.json và các main-before/after/preexisting/preservation artifacts lịch sử đều local-only. Không commit hoặc chia sẻ secret-bearing logs/cookies/session material.

Run-Regressions.ps1 cũ yêu cầu approved read-only configuration cho live suites và sẽ dừng nếu thiếu. Dùng [Run-Validation.ps1](../AuthorizationHardening/Run-Validation.ps1) để phân biệt offline/doubles và BLOCKED live. Console executables chưa được dotnet test solution discover.
