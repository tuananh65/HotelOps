# PermissionService Core — Task03

> Task08: live SQL dùng ConnectionStrings:AuthorizationReadOnly (User Secrets ID HotelOps.Authorization.Verification hoặc env), không fallback HotelOpsDb. Credential có write/DDL sẽ bị từ chối. Xem [setup hiện tại](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md); các kết quả --live bên dưới là lịch sử cho tới khi có principal read-only hợp lệ.

## API và ranh giới tin cậy

`IPermissionService` có ba phép đánh giá scope, không thay thế workflow/SoD validation:

- `EvaluateGlobalAsync`: chỉ một đường cấp quyền ALL với assignment không gắn branch mới có thể cấp quyền. Không dùng để xác nhận quyền trên resource cụ thể.
- `EvaluateBranchAsync`: branch ID được đối chiếu với bảng branches và đường assignment/branch access thực tế. Phép này không chứng minh một resource thuộc branch đó; endpoint có resource phải dùng EvaluateResourceAsync.
- `EvaluateResourceAsync`: chỉ nhận resource type/ID. Backend resolver phải load dữ liệu authoritative, kiểm tra permission/resource-type binding, xác định branch thật và áp dụng ownership rule cụ thể cho current user. Client không thể truyền ownership/trusted flag vào API service này.

`PermissionEvaluation.IsGranted` chỉ true khi quyết định là Granted; giá trị mặc định là Denied. ResourceContextRequired và Unavailable không phải Granted. Caller phải xử lý lỗi, không chuyển failure thành quyền. Chưa có endpoint/policy/attribute được nối vào service ở task này.

`IPermissionResourceResolver` là extension point cho code backend đáng tin cậy, không phải DTO/request binder. `VerifiedPermissionResource` chỉ là kết quả của adapter đó, không tự tạo ra bằng chứng bằng constructor. Không model-bind nó hoặc dùng adapter echo dữ liệu client. Adapter cần xác minh cả loại resource tương ứng permission, sự tồn tại, branch và quy tắc nghiệp vụ đã duyệt. Default resolver trả null, vì vậy **mọi resource check hiện đều fail-closed khi chưa có adapter**, kể cả ALL. Không triển khai ownership thực tế dựa trên created_by hay cùng branch trong task này.

## Đường cấp quyền từ SQL

`EfPermissionDataSource` dùng HotelOpsDbContext hiện có. Mỗi lần đánh giá đều SELECT mới; không cache snapshot/grants qua request hoặc giữa các lần đánh giá.

User phải AccountStatus ACTIVE, DeletedAt null; restricted session bị chặn trước truy vấn. Permission không có cột Active trong schema, vì vậy sự tồn tại được xác định bằng join permissions/role_permissions. Mỗi grant được join vào UserRole bằng chính RoleId đó; path giữ nguyên BranchId, assignment/role flags và AssignedAt/ExpiresAt.

Core chỉ nhận path có assignment/role Active, AssignedAt không ở tương lai, ExpiresAt null hoặc lớn hơn thời điểm UTC hiện tại. Với assignment có branch, branch phải Active và có user_branch_access cùng branch, chưa revoked và GrantedAt không ở tương lai. AccessLevel/primary branch/department không sinh thêm permission. Một branch access độc lập không mở rộng phạm vi role khác.

| Scope | Điều kiện bổ sung |
| --- | --- |
| ALL | Assignment global không cần branch; assignment gắn branch vẫn chỉ được dùng ở branch đó với access hợp lệ |
| BRANCH | Phải có branch trên chính assignment và khớp target branch authoritative |
| OWN | Như BRANCH, cộng resource resolver xác minh đúng ownership/assignment cho user và permission/resource pair |
| DEPARTMENT/unknown/null | Không cấp quyền; không normalize thành scope khác |

Không có bypass theo SYS_ADMIN/OWNER hoặc bất kỳ role name nào; không hard-code role IDs. Không suy luận các scopes thành thứ tự phân cấp. Quyền ALL vẫn phải qua user/role/assignment status và resource resolver nếu đánh giá resource.

DbException, DbUpdateException, TimeoutException và provider wrappers được chuyển thành Unavailable. Cancellation được truyền lên; lỗi lập trình/adapter không phải data failure cũng truyền lên. Không log exception payload, cookie, credentials hoặc session secrets. Không thay authentication middleware hoặc revalidate/recreate session riêng; tái sử dụng CurrentUserAccessor sau SQL cookie validation.

## Verification

Theo console infrastructure đã có, không thêm package test provider hoặc project kiến trúc backend:

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj -- --live
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj
```

Không có --live: 43 core/DI checks bằng test doubles. --live: thêm 7 kiểm tra target/EF SQL resolution, tổng 50. SQL connection lấy từ API User Secrets và environment override, không truyền credentials trên command line. Chỉ SELECT, async/CancellationToken, AsNoTracking; không migrations, fixtures hoặc SaveChanges.

Live test dùng accessor giả lập cho user mẫu đã biết để cô lập core + EF; **không phải bằng chứng cookie authentication hoặc HTTP authorization**. USER/branch IDs mẫu chỉ nằm trong verification, không trong production service. Thay đổi grant giữa hai lần đánh giá được test bằng store double; không sửa production DB để thử revocation. OWN được test bằng resolver double, không chứng nhận một business ownership rule thực tế.

Solution hiện không có framework test project. `dotnet test` exit 0 không chạy các console projects trên; phải chạy lệnh verification riêng. NuGet auditing được giữ nguyên dù môi trường có NU1900.
