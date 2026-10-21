# Task04 — Permission Authorization infrastructure

> Task08: live SQL dùng ConnectionStrings:AuthorizationReadOnly (User Secrets ID HotelOps.Authorization.Verification hoặc env), không fallback HotelOpsDb. Credential có write/DDL sẽ bị từ chối. Xem [setup hiện tại](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md); các kết quả --live bên dưới là lịch sử cho tới khi có principal read-only hợp lệ.

## Production contracts

`AddHotelOpsPermissionAuthorization()` được gọi sau Cookie Infrastructure và PermissionService DI. Scoped handler dùng IPermissionService/ICurrentUserAccessor của request; singleton policy provider chỉ giữ options và IServiceScopeFactory, mở scope riêng cho catalog query. Không sửa middleware order, authentication schemes, FE-01 error contract hoặc các controller.

Policy name có dạng:

```text
HotelOps.Permission:Global:USER_VIEW
HotelOps.Permission:Branch:ROOM_VIEW
HotelOps.Permission:Resource:TASK_EXECUTE
```

`PermissionPolicy.Name(code, check)` tạo tên canonical và kiểm tra syntax; mặc định là Resource. Helper không chứng minh permission tồn tại. Provider xác minh code bằng EF SELECT từ permissions, đối chiếu ordinal trước khi tạo policy. Không có whitelist role hoặc permission matrix được copy vào code. Không cache catalog/endpoint permission policies; PermissionService cũng đọc lại grants mỗi lần evaluate.

Tiền tố trên được dành riêng cho provider; không tự đăng ký policy khác trùng namespace đó trong AuthorizationOptions. Prefix phân biệt hoa/thường, mode và code phải canonical. Unknown/malformed permission policies trả null. Theo ASP.NET Core, endpoint trỏ đến policy không tồn tại là lỗi cấu hình, không phải permit-all: middleware exception hiện có trả generic 500. Named policies ngoài namespace được chuyển cho DefaultAuthorizationPolicyProvider; default/fallback policy giữ nguyên options hiện có.

Policy tạo bởi provider gồm RequireAuthenticatedUser, HotelOpsCookie scheme và PermissionRequirement. Handler chỉ gọi Succeed cho đúng requirement sau khi PermissionService.IsGranted. Denied, Restricted, Unavailable, InvalidRequest và ResourceContextRequired đều không cấp quyền. Exception không được chuyển thành Granted; framework/middleware giữ lỗi generic. Các requirements khác vẫn phải thỏa mãn.

## Resource và branch context

Handler yêu cầu principal đúng với HttpContext.User của request và CurrentUserAccessor đọc được snapshot đã xác minh. Không lấy role/permission claims, branchId/ownerId từ route/query/header.

Backend có thể dùng resource-based authorization sau khi xác định loại phép kiểm tra:

```csharp
var policy = PermissionPolicy.Name("ROOM_VIEW", PermissionCheck.Branch);
var result = await authorization.AuthorizeAsync(
    HttpContext.User, new PermissionBranchContext(branchId), policy);
```

Đây chỉ là phép branch-level check; PermissionService đối chiếu branch Active, role assignment và branch access trên DB. Không dùng kết quả để suy ra branch/ownership của một bản ghi chưa được xác minh.

```csharp
var policy = PermissionPolicy.Name("TASK_EXECUTE"); // Resource mode
var result = await authorization.AuthorizeAsync(
    HttpContext.User, new PermissionResource(resourceType, resourceId), policy);
```

Resource type/ID là handle, không phải ownership proof. PermissionService tiếp tục gọi resolver backend để xác minh permission/resource binding, branch thật và ownership. Default resolver hiện trả null: OWN/resource authorization tiếp tục fail-closed tới khi có adapter đã duyệt.

Với endpoint policy qua middleware, AuthorizationHandlerContext.Resource là HttpContext. Code backend chạy trước UseAuthorization có thể cung cấp `PermissionBranchContext` hoặc `PermissionResource` qua HttpContext.Features. Không model-bind feature từ request hoặc cho phép client truyền HasOwnership/trusted flag. Task04 không thêm middleware load resource hoặc business adapter nào vào app chính. Endpoint thiếu feature phù hợp sẽ bị deny, kể cả client gửi branch/owner trong query.

Global mode gọi EvaluateGlobalAsync, không kiểm tra quyền trên một bản ghi cụ thể. Không dùng Global policy để thay cho resource authorization chỉ vì user có ALL. SoD và state transitions vẫn phải do nghiệp vụ kiểm tra.

## Verification và giới hạn

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj -- --live
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj -- --live
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj
```

Console verification theo infrastructure hiện có, không thêm TestHost/framework packages hoặc projects kiến trúc backend. `dotnet test` hiện không có framework test project để chạy; không thay thế các lệnh console.

Test host dùng Kestrel loopback port ngẫu nhiên, đăng ký Cookie Infrastructure/HotelOpsCookieEvents và các middleware thật. IDataProtectionProvider được thay bằng ephemeral provider trong test, validator/toucher/catalog/permission data/resource adapter là doubles. Credentials/cookies chỉ tồn tại trong bộ nhớ, không xuất ra logs/Git. Host kiểm thử độc lập, không mở thêm endpoint ở ứng dụng chính.

Cookie header được gửi thủ công qua HTTP loopback trong test; production SecurePolicy.Always không bị sửa. Vì thế kết quả không chứng nhận HTTPS deployment, browser cookie behavior, SQL session validation hoặc login/password workflow thực tế. Test issue endpoint chỉ tạo synthetic ticket trong isolated host, không phải production login.

Không --live: 61 assertions hỗn hợp provider/imperative handler/HTTP/problem contract và synthetic cookie issuance. --live thêm 4 SQL catalog checks, tổng 65. Không gọi đây là 65 HTTP request scenarios hoặc unit tests độc lập. SQL checks đọc cấu hình local User Secrets/env của API và chỉ SELECT; không tạo fixtures/session trong HotelOpsDB.
