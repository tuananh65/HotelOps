# Task26 — UC005 Profile-only Update

Ngày2026-10-27 (Asia/Saigon). Branch feature/BE1-database-integration;baseline20c1063. Task22/local hồ sơ20A–25 giữ nguyên.

Triển khai PATCH /api/users/{userId} và read-token GET /api/users/{userId}/profile. Chỉ fullName/email/phone. Không credential/status/scope/role writes, không UC011.

## Approval áp dụng

- User đã duyệt BRANCH/OWN rồi làm rõ:BRANCH phải có membership actor/target cùng active branch và USER_MANAGE/BRANCH trong chính branch đó;OWN self+valid USER_MANAGE/OWN;ALL chỉ unbound.
- Rowversion+read-token/If-Match;strict PATCH,audit atomic,không revoke thường.
- Shared schema/EF/DI/audit tối thiểu đã duyệt. Không sửa foundation evaluator hoặc RBAC catalog;2 scoped DI registrations cho access composition và profile service.
- Scope approvals không cấp thêm grant cho role. Current catalog chỉ SYS_ADMIN USER_MANAGE/ALL; scoped tests không sửa grants.

[API contract](API_CONTRACT.md) · [Authorization](AUTHORIZATION.md) · [Database](DATABASE_CHANGE.md) · [Verification](VERIFICATION_RESULTS.md) · [Limits](OUTSTANDING_ISSUES.md).

## Architecture

New UserProfileController,UserProfileService,UserProfileAccess và DTO/parser trong project ASP.NET hiện tại. User concurrency property nằm partial User.ProfileConcurrency.cs; DbContext thêm đúng1 mapping IsRowVersion. Program.cs thêm2 scoped registrations, không thay middleware/cookie/DEV2 registrations. UsersController/DirectoryService Task22 không sửa trong Task26.

## Reproduce safely

Build solution/harness; Verify-Schema.ps1 -Server <reviewed-local-instance> tạo2 DB mới qua guarded existing setup, verify forward/rollback/fresh/upgrade. Setup hiện có dùng reviewed local instance; verify Server phải đúng instance đó. Evidence schema-*.local.json cho biết fresh trial rỗng đã patch.

```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/UserProfileUpdate/UserProfileUpdate.Verification.csproj
& ./scripts/verification/UserProfileUpdate/Verify-Schema.ps1 -Server '<reviewed instance>'
dotnet scripts/verification/UserProfileUpdate/bin/Debug/net10.0/UserProfileUpdate.Verification.dll <new-empty-patched-fresh-trial> <new-local-receipt>
```

Không dùng lại trial đã có fixtures. Không chạy binary mới lên schema chưa có row_version. Forward/rollback hiện **trial-only**, không phải authorization để migrate HotelOpsDB hoặc deploy. Không thay release SQL gốc/canonical seed; không EF migration; không commit/push/merge.
