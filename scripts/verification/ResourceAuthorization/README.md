# Task05 — Cleaning Task resource authorization

> Task08: live SQL dùng ConnectionStrings:AuthorizationReadOnly (User Secrets ID HotelOps.Authorization.Verification hoặc env), không fallback HotelOpsDb. Credential có write/DDL sẽ bị từ chối. Xem [setup hiện tại](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md); các kết quả --live bên dưới là lịch sử cho tới khi có principal read-only hợp lệ.

## Resource → Branch → Current Assignee

Adapter đầu tiên: `CleaningTaskResourceResolver`, chỉ hỗ trợ cặp:

| Thành phần | Mapping |
| --- | --- |
| Resource type | `cleaning_task`, trùng resource_code hiện có |
| Identifier | cleaning_tasks.cleaning_task_id |
| Permission | CLEANING_EXECUTE, catalog hiện cấp OWN cho HK_STAFF |
| Branch | cleaning_tasks.room_id → rooms.room_id → rooms.branch_id |
| Current assignment | cleaning_assignments.cleaning_task_id; assignee_user_id; assignment_status ACTIVE; ended_at NULL; assigned_at không ở tương lai |
| Supported task state | IN_PROGRESS; room còn Active |

UC-030 xác định sau reassign chỉ người mới được làm việc; UC-031/032 xác định thực hiện công việc phải là người được giao hiện tại. GBR-047/049 và phần FE-04 Cleaning Task Detail/Execution củng cố quan hệ này. Các quy tắc assignment trên được kiểm tra lại trực tiếp từ SQL schema và dữ liệu hiện có.

Không dùng created_by, cùng department, primary branch hoặc owner ID từ client. Schema không có unique-current-assignee constraint; nếu có nhiều assignment ACTIVE/chưa kết thúc/đã tới thời điểm giao, adapter từ chối vì không xác định được một assignee duy nhất. Assignment REASSIGNED/CANCELLED/COMPLETED hoặc có ended_at không còn là bằng chứng OWN; lịch sử được giữ nguyên.

Chỉ current user trùng assignee duy nhất mới nhận VerifiedPermissionResource(branch từ room, HasOwnership=true). Với CLEANING_EXECUTE, current assignment là ràng buộc của resource/permission pair, không thể được một ALL grant giả lập bỏ qua. Sau đó PermissionService tiếp tục kiểm tra account/restricted/role/grant/branch assignment/branch access như trước. Adapter không tự trả Granted.

## Giới hạn trạng thái và workflow

SRS draft dùng Pending Assignment/Assigned/On Hold/Pending Inspection, trong khi SQL CHECK của cleaning_tasks chỉ cho phép PENDING/IN_PROGRESS/COMPLETED/CANCELLED. Task này không tự ánh xạ PENDING sang Assigned và không thay status/SQL. Adapter chỉ mở đường xác minh assignment cho task **IN_PROGRESS**, phù hợp UC-032 và phần execution của UC-034. Start/Resume/Completed history chưa được adapter hỗ trợ; các trạng thái khác trả null/fail-closed.

Đây là scope/assignment authorization, không phải xác nhận hoàn tất toàn bộ business preconditions. Workday, work interval, checklist, blocker, room lifecycle transitions và audit của command phải được kiểm tra riêng trong workflow tương ứng. Trước rollout write endpoints cần chốt status mapping và revalidate assignment/preconditions trong transaction thực thi để tránh race với reassignment. Adapter hiện chỉ SELECT, không giữ lock để bảo đảm một thao tác ghi tương lai.

`CLEANING_VIEW`, `CLEANING_ASSIGN`, rework, inspection, operational tasks, maintenance, incidents, material requests và inventory records chưa có adapter trong task này. Không tái sử dụng current-assignee rule này cho lịch sử xem/approval/review khi chưa có quy tắc riêng.

## Tích hợp

Program.cs đăng ký scoped `ICleaningTaskAccessReader → EfCleaningTaskAccessReader` và thay resolver mặc định bằng CleaningTaskResourceResolver. UnresolvedPermissionResourceResolver vẫn tồn tại cho những host/test cần explicit default-deny. Không sửa PermissionService/handler/policies.

Backend có thể đi qua handler hiện có:

```csharp
var decision = await authorization.AuthorizeAsync(
    HttpContext.User,
    new PermissionResource(CleaningTaskResourceResolver.ResourceType, cleaningTaskId),
    PermissionPolicy.Name(CleaningTaskResourceResolver.PermissionCode));
```

Route ID chỉ là lookup key. Không truyền branch/owner từ request vào verified facts. Resource mode của Task04 gọi PermissionService, rồi resolver load branch/assignment từ DB. Endpoint middleware muốn dùng resource policy phải có resource handle backend đặt trong HttpContext.Features trước UseAuthorization; không thay bằng Branch policy để bỏ qua resource verification.

Unknown resource type, incompatible permission, missing resource, invalid ID/user, room inactive, branch không xác định, unsupported status hoặc assignment không hợp lệ đều trả null. PermissionService/handler deny và không tiết lộ sự tồn tại hay chi tiết resource. SQL exceptions được truyền về cơ chế failure handling hiện có; không cache facts giữa các lần evaluate/request.

## Verification

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet run --project scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj
dotnet run --project scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj -- --live
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj -- --live
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj
```

Không --live: 33 adapter/core checks với reader/data doubles và 16 HTTP assertions. HTTP host loopback dùng adapter, PermissionService, handler, cookie events/middleware thật; session store, permission data và cleaning reader là doubles. Ephemeral keys và synthetic tickets chỉ nằm trong test host; không đăng ký test endpoint ở app chính, không xuất credentials/cookies.

--live: thêm 10 SELECT checks trên HotelOpsDB, gồm reader EF thật và adapter+PermissionService với grants thật. Current-user accessor cho phần live core được giả lập để cô lập SQL/resource authorization; không xác nhận một HTTP cookie/SQL session thật cho user mẫu. Không thay đổi assignments để thử revocation: test doubles chịu trách nhiệm tình huống lỗi/thay đổi; SQL thật kiểm tra dữ liệu reassign đã tồn tại.

Host test không kiểm chứng HTTPS/browser cookie behavior hoặc login/password workflow. User Secrets/env dùng cấu hình local của API; không hard-code connection string. Tất cả operations DB async với CancellationToken, AsNoTracking; không migrations/seed/SaveChanges/fixtures. Console projects theo infrastructure hiện có; dotnet test hiện chưa có framework test project để thực thi các checks này.

`preexisting-file-hashes.json` là baseline 237 source/verification files trước Task05, chỉ dùng kiểm tra bảo toàn thay đổi có sẵn, không phải dữ liệu authorization hoặc baseline SQL.
