# Task 16 — Fallback Authorization Hardening

Ngày thực hiện: 03/11/2026, Asia/Saigon. DEV1. Đây là task hardening được giao trong phiên hiện tại; tài liệu Dev1BackendScope có nhãn Task 16 trước đây là một audit lịch sử khác.

## A. Preflight Git Status

- Đã đọc AGENTS.md; branch `feature/BE1-database-integration`, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`.
- Trước sửa: working tree/index sạch. Sáu commit ahead upstream: `01bceb8`, `71a17f9`, `a2e6d68`, `afd2c11`, `51df1c9`, `4a18732`; là coordination, Inventory authorization và merge DEV2 đã audit.
- Program, permission registration/provider, cookie events/validator, CurrentUserAccessor, public endpoints và middleware pipeline đã được đối chiếu lại với audit trước; không có source drift.
- Không checkout/merge/reset/clean/commit/push; không sửa worktree khác, đặc biệt Task 11. Không kết nối database.

## B. Root Cause

`AddAuthorization()` trước đây không cấu hình fallback. `UseAuthentication()` xác lập identity nhưng không tự yêu cầu đăng nhập trên endpoint không có authorization metadata. Các endpoint nghiệp vụ chưa được DEV2 gắn permission policies có thể đi qua khi anonymous.

## C. FallbackPolicy Implementation

`AddHotelOpsPermissionAuthorization()` cấu hình `FallbackPolicy` bằng `AuthorizationPolicyBuilder(HotelOpsCookieTicket.Scheme).RequireAuthenticatedUser().Build()`.

Fallback chỉ bảo vệ authentication. Identity phải được cookie events xác minh qua validator hiện có; trong ứng dụng chính validator dùng SQL-backed session service. Không thay DefaultPolicy, PermissionService, grant/scope evaluation hoặc role behavior. User đã authenticated có thể qua fallback dù không có permission: DEV2 vẫn phải thêm kiểm tra quyền nghiệp vụ.

Giữ nguyên pipeline: AuthenticationException → HTTPS redirection → Authentication → InvalidCookie → CSRF → RestrictedSession → Authorization → SessionTouch → controller. OpenAPI chỉ được map trong Development như trước.

Unsafe `/api` request thiếu CSRF trả 400 trước authorization; request anonymous có CSRF hợp lệ vẫn bị fallback trả 401. Đây là thứ tự bảo vệ hiện có. AllowAnonymous cũng không bỏ qua InvalidCookie hoặc RestrictedSession middleware.

## D. Custom Policy Provider Compatibility

`PermissionPolicyProvider` đã kế thừa `DefaultAuthorizationPolicyProvider.GetFallbackPolicyAsync()` và `GetDefaultPolicyAsync()`; không cần override mới. HTTP/provider checks xác nhận cùng policy instance từ options, đúng cookie scheme, dynamic policy vẫn tạo PermissionRequirement, catalog changes/failures không cấp quyền. Chế độ regression cũ vẫn cấu hình fallback có claim bổ sung để chứng minh explicit default/named policies không bị fallback ghi đè.

## E. Public Endpoint Classification

| Endpoint | Phân loại sau thay đổi | Bằng chứng/quyết định |
|---|---|---|
| POST /api/auth/login | A — public, bắt buộc CSRF | AllowAnonymous hiện có |
| POST /api/auth/logout | A — public, bắt buộc CSRF | AllowAnonymous hiện có; workflow logout giữ nguyên |
| GET /api/auth/csrf | A — public | AllowAnonymous hiện có |
| GET /api/health/database | A — public readiness | Thêm AllowAnonymous ở action; body/status contract giữ nguyên |
| /openapi/{documentName}.json | A — public chỉ trong Development | Quyết định developer; MapOpenApi().AllowAnonymous() nằm trong IsDevelopment() |
| /WeatherForecast | Đã loại bỏ mã mẫu | Developer cho phép xóa sau dependency review |
| Endpoint nghiệp vụ không có metadata | B — cần validated cookie session | Fallback mới |
| Endpoint có explicit authorization | B — theo policy/access checks hiện có | Không thay bằng fallback |

Không còn quyết định public endpoint pending trong task này. `git grep` và tìm kiếm scripts/tests/docs xác nhận WeatherForecast chỉ được dùng bởi controller/model mẫu và file HotelOps.Api.http chứa một request mẫu; không có dependency nghiệp vụ hoặc verification bắt buộc. Xóa cả ba file. Hai báo cáo audit lịch sử vẫn nhắc đến template; không phải executable references và không sửa lịch sử audit.

## F. Verification Results

Lệnh đã chạy trên source sau khi xóa WeatherForecast:

```powershell
dotnet build HotelOps.slnx
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj --no-restore -- --production-fallback
dotnet scripts/verification/Authorization/bin/Debug/net10.0/Authorization.Verification.dll
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj --no-restore
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj --no-restore
dotnet scripts/verification/DataScope/bin/Debug/net10.0/DataScope.Verification.dll --model-only
dotnet run --project scripts/verification/AuthorizationHardening/AuthorizationHardening.Verification.csproj --no-restore
dotnet run --project scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj --no-restore
dotnet test HotelOps.slnx
git diff --check
```

| Kiểm tra | Kết quả thực chạy |
|---|---|
| Solution build | PASS, 0 errors; 2 NU1900 warning entries |
| Production fallback mode | PASS, 93 assertions |
| Existing/custom fallback mode | PASS, 61 assertions |
| PermissionService | PASS, 43 checks |
| CurrentUserAccessor | PASS, 14 checks |
| DataScope model-only | PASS, 7 checks; không mở kết nối |
| AuthorizationHardening | PASS, 21 checks |
| ResourceAuthorization | PASS, 49 checks |
| dotnet test HotelOps.slnx | Exit 0; solution chỉ có API, không discover automated test cases |
| git diff --check | PASS |
| Live SQL DataScope | BLOCKED/NOT RUN: thiếu AuthorizationReadOnly; setup guard dừng trước kết nối |
| SQL sessions / production auth business workflows | NOT RUN |

93 assertions gồm anonymous 401, authenticated/no-permission 200 trên endpoint không metadata, malformed/revoked cookie 401, revocation giữa validate và touch 401, restricted session 403, restricted CSRF bootstrap 200, actual Health/Csrf controllers, Development OpenAPI 200, assembly không còn template, named permission denial 403, scheme/default/provider checks và CSRF round trips. Các assertion counts bao gồm setup/provider/problem-contract checks, không phải số HTTP scenarios độc lập.

Test host dùng cookie events, middleware, CurrentUserAccessor, permission engine/provider/handler thật với ephemeral Data Protection, session/catalog/permission data doubles và readiness database double. Login/logout là endpoint stubs chỉ kiểm tra anonymous + CSRF gate; production actions được kiểm tra metadata, không chạy credential mutations. Cookie headers được gửi thủ công qua HTTP loopback; request scheme được đặt HTTPS chỉ trong isolated runner để kiểm tra Secure antiforgery round trips, không đổi production TLS/forwarded-header handling. Không chứng nhận browser HTTPS deployment hoặc SQL revocation implementation.

## G. Regression Results

Default policy, explicit named policies, additional requirements, ALL/BRANCH/OWN, role/assignment revocation, no SYS_ADMIN/OWNER bypass và resource ownership checks đều qua các runners nêu trên. CSRF vẫn chạy trước authorization, token anonymous không dùng được cho authenticated session, valid CSRF không thay thế authentication/permission. Middleware ordering production không đổi.

Các lỗi trung gian được ghi rõ: build đầu chạy trùng output API bị CS2012; chạy lại tuần tự PASS. Khi bổ sung OpenAPI vào verification host, build runner báo CS9137; đã thêm InterceptorsNamespaces cho generated namespace trong csproj runner và chạy lại PASS. NU1900 còn tồn tại vì không truy cập được https://api.nuget.org/v3/index.json để lấy vulnerability data; không tắt audit hoặc che cảnh báo. DataScope mặc định bị thiếu read-only setup; model-only chạy riêng PASS, không thay cấu hình để vượt guard.

## H. Files Changed

Modified:

- src/HotelOps.Api/Security/Authorization/PermissionAuthorizationRegistration.cs
- src/HotelOps.Api/Security/HotelOpsCookieInfrastructure.cs (comment đồng bộ với cấu hình mới)
- src/HotelOps.Api/Controllers/HealthController.cs
- src/HotelOps.Api/Program.cs
- scripts/verification/Authorization/Program.cs
- scripts/verification/Authorization/Authorization.Verification.csproj
- scripts/verification/Authorization/README.md

Added: scripts/verification/FallbackAuthorization/README.md (báo cáo này).

Deleted:

- src/HotelOps.Api/Controllers/WeatherForecastController.cs
- src/HotelOps.Api/WeatherForecast.cs
- src/HotelOps.Api/HotelOps.Api.http

## I. Remaining DEV2 Responsibilities

Gắn permission/resource authorization cho các business APIs, xác định target branch/resource authoritative, lấy actor từ authenticated context và ngăn cross-branch access; kiểm chứng Inventory và các workflow liên quan. Không coi authenticated-only fallback là hoàn tất authorization coverage. Canonical fixtures/rehearsal và database patches DEV2 giữ nguyên.

## J. Risks and Final Verdict

Implementation hoàn thành trong phạm vi được duyệt; build và các isolated verification/regression checks PASS. Chờ developer review, chưa commit/push/merge.

Live SQL/session lifecycle và API persistence không được kiểm chứng trong task này. Production cookie infrastructure vẫn giới hạn Windows Development. OpenAPI non-Development được kiểm tra qua source guard không đổi, chưa khởi chạy host production (cookie deployment configuration chưa được hỗ trợ). NU1900 và read-only SQL setup limitation còn mở. Không thay database, RBAC Matrix, DataScope, entities, DEV2 controllers/services hoặc scripts release/seed.
