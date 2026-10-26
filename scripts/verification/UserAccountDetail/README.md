# Task22 — User Account Detail

Triển khai GET /api/users/{userId}, read-only slice UC-141/UC-005, không phải UC mới.

Dùng UsersController và IUserDirectoryService/UserDirectoryService hiện có; không sửa Program.cs/DI/middleware/catalog/schema/generated entities hoặc module DEV2. Task22 phê duyệt Global USER_VIEW detail-only, giải quyết O1 cho slice này trong Task21; không phê duyệt UC011/scoped visibility hoặc mutations.

[API contract](API_CONTRACT.md) · [Verification](VERIFICATION_RESULTS.md) · [Remaining issues](OUTSTANDING_ISSUES.md).

Baseline: feature/BE1-database-integration,20c1063. origin/develop kiểm tra trực tiếp vẫn5299b5e9e8dd917abef327ab9f9bd5494064ccf9; không merge/fetch. Tracked/index sạch trước task. Local Task20A/Task21 được bảo toàn.

## Reproduce

Build solution và harness:
```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/UserAccountDetail/UserAccountDetail.Verification.csproj
```

Dùng quy trình Prepare-Database.ps1 đã có tại UserDirectory với tên trial mới đúng pattern HotelOps_Canonical_yyyyMMdd_HHmmss_<8hex>_Fresh; script từ chối database tồn tại và không sửa SQL sources. Sau setup:

```powershell
dotnet scripts/verification/UserAccountDetail/bin/Debug/net10.0/UserAccountDetail.Verification.dll <new-trial-name> <new-local-json-path>
```

Harness mới dựa trên UC141 harness và giữ các list assertions, thêm detail SQL/HTTP tests. UC141 harness cũ cũng được chạy riêng; chỉ readiness được đổi từ CSRF thành anonymous protected-route401 để warm catalog/EF model, không thay expected permissions. Không chạy lại trên trial đã có personnel; dùng database mới, không reset/delete.

Fixture passwords/cookies/CSRF ở memory; TLS pin certificate riêng; child API chỉ trỏ trial. Finally revoke owned sessions, stop child và xóa đúng PFX tạm. Giữ trial fixtures cho điều tra. File *.local.json là evidence local, không đưa credential vào output và không tự stage.
