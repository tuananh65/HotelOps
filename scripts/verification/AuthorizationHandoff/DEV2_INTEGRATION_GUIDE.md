# DEV2 integration contract

> Task08: live read verification dùng ConnectionStrings:AuthorizationReadOnly trong User Secrets riêng, kiểm tra quyền và từ chối principal ghi/DDL; không dùng fallback ConnectionStrings:HotelOpsDb. Policy catalog hiện theo RequestAborted và budget 5 giây. Xem [setup](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md).

Namespace của toàn bộ core và Cleaning adapters: `HotelOps.Api.Security.Authorization`, kể cả files nằm trong Resources/. Authentication types ở `HotelOps.Api.Security` và `HotelOps.Api.Services.Authentication`.

**Có Permission Grant không đồng nghĩa có quyền trên mọi resource.** Resource-specific authorization phải kiểm chứng nghiệp vụ và dữ liệu phía server. Mọi ví dụ Cleaning dưới đây chỉ minh họa adapter đã có, không tạo hay phê duyệt workflow/controller mới.

## 1. Identity theo request

Contract hiện tại:

```csharp
public readonly record struct CurrentUserInfo(long UserId, long SessionId, bool IsRestricted);
public interface ICurrentUserAccessor
{
    bool TryGetCurrentUser(out CurrentUserInfo user);
}
```

Inject scoped accessor, gọi trong request HTTP sau authentication. False nghĩa là không có validated identity để sử dụng; không lấy UserId từ DTO/header/query thay thế. True chỉ là identity, chưa phải quyền. IsRestricted=true vẫn là identity hợp lệ nhưng bị giới hạn: không cho nghiệp vụ đi tiếp, không tự clear temporary credential marker. Không đưa SessionId thành bằng chứng permission và không trả session credential ra DTO/log.

Accessor yêu cầu ticket đúng scheme/fields và SQL SessionSnapshot của chính request. Không cache user hiện tại vào singleton, không tái sử dụng snapshot ở request khác, không dùng accessor trong background job như một danh tính hệ thống giả.

## 2. PermissionService và scope

Các chữ ký đúng source:

```csharp
Task<PermissionEvaluation> EvaluateGlobalAsync(string permissionCode, CancellationToken cancellationToken);
Task<PermissionEvaluation> EvaluateBranchAsync(string permissionCode, long branchId, CancellationToken cancellationToken);
Task<PermissionEvaluation> EvaluateResourceAsync(string permissionCode, PermissionResource resource, CancellationToken cancellationToken);

public readonly record struct PermissionEvaluation(PermissionDecision Decision)
{
    public bool IsGranted => Decision == PermissionDecision.Granted;
}
public sealed record PermissionResource(string ResourceType, long ResourceId);
public sealed record VerifiedPermissionResource(long BranchId, bool HasOwnership);
```

Các decision: Denied, Granted, Unauthenticated, Restricted, ResourceContextRequired, Unavailable, InvalidRequest. Chỉ `IsGranted` được đi tiếp. Không coi Unavailable/exception là success; không fallback sang role name. Core không tự map mọi decision sang HTTP; policy handler hiện map mọi non-grant sau authentication thành forbid. SQL session store unavailable có thể trả 503 qua middleware; lỗi programming/catalog có thể thành generic 500. Caller imperative cần thống nhất error contract, không biến resource/context error thành allow.

| Check | Khi dùng | Giới hạn |
|---|---|---|
| Global | Hành động thật sự không có target branch/resource | Chỉ path ALL với assignment branch null cấp được global; không dùng để bypass resource |
| Branch | Chức năng đã xác nhận chỉ cần branch-level authorization | BRANCH hoặc ALL thích hợp; branch target vẫn phải được xác minh; OWN không đủ |
| Resource | Xem/ghi một resource có rule riêng | Luôn cần resolver xác minh resource, kể cả khi có ALL |

ALL/BRANCH/OWN là scope trong SQL grant, không phải scope client tự chọn để nâng quyền. ALL gắn assignment có branch vẫn bị giới hạn đúng branch và branch access còn hiệu lực. Null-branch BRANCH/OWN không tự được rộng ra. OWN không đồng nghĩa CreatedBy hoặc có mặt cùng branch: ý nghĩa do adapter nghiệp vụ quyết định.

Ví dụ helper dùng service đã có, không đọc client branch/owner:

```csharp
using HotelOps.Api.Security.Authorization;

static async Task<bool> CanExecuteCurrentCleaningAsync(
    IPermissionService permissions, long cleaningTaskId, CancellationToken ct)
{
    var result = await permissions.EvaluateResourceAsync(
        CleaningTaskResourceResolver.PermissionCode,
        new PermissionResource(CleaningTaskResourceResolver.ResourceType, cleaningTaskId), ct);
    return result.IsGranted;
}
```

Truyền RequestAborted hoặc CancellationToken do ASP.NET cung cấp. Không swallow cancellation. Không trả entity trực tiếp; DTO, workflow và business rules vẫn thuộc service của module.

## 3. Resource Resolver

```csharp
public interface IPermissionResourceResolver
{
    Task<VerifiedPermissionResource?> ResolveAsync(
        string permissionCode, PermissionResource resource,
        long currentUserId, CancellationToken cancellationToken);
}
```

Resolver kiểm tra permission/resource type đúng cặp, load record theo ID, xác minh quan hệ thật và rule riêng, trả branch từ SQL và ownership đã chứng minh. Thiếu/không tồn tại/không hỗ trợ/không chứng minh được: trả null. Dùng EF async/AsNoTracking cho reads, không invent schema/status. Không dựng VerifiedPermissionResource trực tiếp từ DTO.

**Hiện DI chỉ có một IPermissionResourceResolver: CleaningTaskResourceResolver.** Core không tự enumerate nhiều resolvers. DEV2 không đăng ký resolver thứ hai bằng cách AddScoped cùng interface rồi vô tình để last-registration-wins thay thế Cleaning. Cần phối hợp shared change để dispatch theo permission/type một cách fail-closed, giữ adapter hiện có và regression. Task07 không triển khai dispatcher.

Adapter Cleaning chỉ chấp nhận CLEANING_EXECUTE + cleaning_task, IN_PROGRESS, room active và đúng một current assignment ACTIVE, EndedAt null, AssignedAt không ở tương lai, assignee là current user. Kể cả ALL vẫn phải là current assignee. **Không sử dụng cho Start/Resume**, view, reassign, complete workflow hoặc module DEV2 khi chưa có rule riêng được phê duyệt.

## 4. Policy và trusted context

```csharp
public enum PermissionCheck { Resource, Branch, Global }
public sealed record PermissionBranchContext(long BranchId);
```

`PermissionPolicy.Name(code, check)` sinh tên `HotelOps.Permission:{Resource|Branch|Global}:{CODE}`. Code phải canonical uppercase và tồn tại trong SQL catalog. Unknown policy không có permit-all fallback; cấu hình endpoint sai có thể trả generic 500. Không sửa Permission Matrix để làm endpoint test Passed.

Có hai cách tích hợp vào endpoint **đã tồn tại và đủ business rules**:

### A. Imperative resource authorization

Endpoint giữ cookie authentication và các security middleware; sau đó gọi IAuthorizationService với principal của chính HttpContext và resource typed. Ví dụ helper tương thích API thực tế:

```csharp
using HotelOps.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

static Task<AuthorizationResult> AuthorizeCurrentCleaningAsync(
    HttpContext http, IAuthorizationService authorization, long cleaningTaskId)
    => authorization.AuthorizeAsync(
        http.User,
        new PermissionResource(CleaningTaskResourceResolver.ResourceType, cleaningTaskId),
        PermissionPolicy.Name(CleaningTaskResourceResolver.PermissionCode));
```

Trước khi chạy nghiệp vụ phải kiểm tra result.Succeeded; false thì forbid theo HotelOpsCookie scheme. Endpoint/action chưa xác thực phải challenge trước, ví dụ authentication attribute dùng `AuthenticationSchemes = HotelOpsCookieTicket.Scheme`. Không truyền principal tự xây vào helper: handler yêu cầu chính principal của request đã validate.

### B. Declarative policy với binding trước UseAuthorization

Attribute cần compile-time constant, không thể gọi PermissionPolicy.Name trong argument attribute:

```csharp
[Authorize(Policy = "HotelOps.Permission:Resource:CLEANING_EXECUTE")]
```

Trước UseAuthorization, middleware/binder dành riêng endpoint lấy route ID đã parse, rồi đặt:

```csharp
httpContext.Features.Set(new PermissionResource(
    CleaningTaskResourceResolver.ResourceType, cleaningTaskId));
```

ID chỉ là lookup key; adapter tiếp tục xác minh SQL. Đặt Feature trong action hoặc action filter là **quá muộn** cho endpoint authorization middleware. Binder phải giới hạn đúng endpoint/permission/resource contract, không biến mọi route id thành CleaningTask. Thiếu Feature sẽ deny, không tự đọc query/body để bù.

Branch policy tương tự dùng Feature `new PermissionBranchContext(verifiedBranchId)` hoặc resource typed khi gọi AuthorizeAsync. Phải xác minh branch target/quan hệ của resource ở server; branch-level check không thay resource check cho item endpoints.

## 5. DI và pipeline phải giữ

Program hiện đăng ký scoped CurrentUserAccessor, EfPermissionDataSource, EfCleaningTaskAccessReader, CleaningTaskResourceResolver và PermissionService; AddHotelOpsPermissionAuthorization đăng ký scoped handler/catalog và singleton policy provider. Provider tạo scope riêng khi đọc catalog; không capture scoped DbContext trong singleton. Extension không thay thế việc đăng ký permission data source/service/resolver.

Pipeline production: exception wrapper → HTTPS redirection → Authentication → InvalidCookie → CSRF → RestrictedSession → Authorization → SessionTouch → controllers. Nếu cần declarative resource binder, đặt sau routing và trước authorization; không di chuyển/bỏ security middleware. Không thay provider chung mà làm mất default/fallback/external policies. Không giả lập role claims hoặc SessionSnapshot để gọi handler.

## 6. HTTP và Restricted Session

- Không có cookie hợp lệ: 401; invalid/revoked cookie có thể SESSION_INVALID, anonymous challenge AUTHENTICATION_REQUIRED.
- Cookie hợp lệ nhưng policy không grant: 403 FORBIDDEN.
- Restricted session: 403 PASSWORD_CHANGE_REQUIRED cho business APIs; chỉ csrf/session/change-password/logout theo allowlist hiện tại. Không thêm ngoại lệ để test pass.
- Unsafe /api request thiếu/sai CSRF: 400 CSRF_VALIDATION_FAILED, có thể xảy ra trước authorization; không hứa mọi anonymous POST luôn trả 401.
- Không trả SQL/provider exception, stack trace, cookie/session credential hoặc connection string cho client.

FE-01 login sau commit phát cookie và CSRF token bound identity mới. Mutation phải gửi cả cookie và X-CSRF-TOKEN. Không tái sử dụng token anonymous sau login hoặc token thuộc session trước change-password.

## 7. Endpoint verification checklist

Test anonymous/invalid/expired/revoked cookie, inactive account/role/assignment, restricted session; valid grant, missing grant, cross-branch, đúng/sai OWN, former/ambiguous assignment, role mixing, missing resource/context, unknown permission, additional requirements, SQL failure/cancellation, và CSRF missing/invalid/valid cho unsafe method. Chứng minh action/service không được chạy khi security check fail.

Phân biệt doubles, live SELECT và real SQL sessions. Main HotelOpsDB chỉ SELECT cho verification. HTTP session mutations chỉ dùng integration database được cấp phép, guarded riêng, credentials mới ngoài Git và cleanup exact sessions; không chạy deployment/trial scripts như tests. Guard code không thay SQL least privilege: account local hiện có quyền ghi main DB (finding F01).

## 8. Những việc không được làm

Không role-name/ID bypass; không client branch/owner evidence; không dùng global/branch grant để chứng nhận mọi resource; không bỏ qua decision/exception; không cache quyền xuyên request; không ghi main DB để test; không tự thay matrix/status/schema; không override resolver chung làm mất adapter khác; không gắn policy rồi mới tạo context trong action.

Permission check chỉ là một bước. Business-specific SoD, workday/checklist, state transition, audit và transaction phải được thực thi trong service. Revalidate assignment/branch/user/grant phù hợp trong transaction hoặc dùng concurrency control trước write; SessionTouch không khóa permission/resource cho toàn workflow.
