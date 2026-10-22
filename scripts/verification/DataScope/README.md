# EF Core DataScope verification

> Task08: live SQL dùng ConnectionStrings:AuthorizationReadOnly (User Secrets ID HotelOps.Authorization.Verification hoặc env), không fallback HotelOpsDb. Credential có write/DDL sẽ bị từ chối. Xem [setup hiện tại](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md); các kết quả --live bên dưới là lịch sử cho tới khi có principal read-only hợp lệ.

Mapping Database First: `RolePermission.DataScope` là string không nullable, ánh xạ `role_permissions.data_scope`, varchar(20), required, default SQL `('BRANCH')`. Property không khởi tạo thành quyền ALL/BRANCH trong CLR; default mô tả database hiện có, không quyết định authorization.

Schema SQL CHECK vẫn chấp nhận ALL/BRANCH/DEPARTMENT/OWN; RBAC Release hiện dùng ALL=8, BRANCH=81, OWN=51 và không có grant DEPARTMENT. Mapping giữ nguyên chuỗi đọc được, không chuyển đổi scope legacy hoặc suy luận quyền từ role name, department, branch assignment hay ownership.

Hai generated files được chỉnh tối thiểu theo conventions hiện tại: `Entities/RolePermission.cs` và `Data/HotelOpsDbContext.cs`. Không thay PK, index hoặc relationships. Scaffold tương lai có thể ghi đè các chỉnh sửa: scaffold từ schema đã nâng cấp phải sinh lại DataScope và default tương đương; review diff và chạy verification này trước khi chấp nhận. Không thêm partial property trùng với property do scaffold sinh.

## Chạy

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj
```

Console project theo infrastructure verification hiện có; không thêm package hoặc project kiến trúc backend. Solution hiện không chứa test project; `dotnet test` thành công không đồng nghĩa đã chạy các console checks.

Connection lấy từ User Secrets của API, rồi environment override `ConnectionStrings__HotelOpsDb`. Không truyền secrets qua command line hoặc commit cấu hình. SQL checks bắt buộc kết nối thực tế đến HotelOpsDB; không tự bỏ qua nếu mất kết nối. Không thay cấu hình encryption để chạy test.

15 checks gồm EF metadata, default/nullability/type, PK/index/relationships, actual DB/schema, catalog counts, đọc scope qua EF và đối chiếu từng PK với SQL, full entity materialization và không tracking. Tất cả database operations dùng async/CancellationToken; chỉ SELECT, không SaveChanges/migration/EnsureCreated/fixtures. Counts được chốt theo RBAC Release của task này, phải review lại khi có release được duyệt khác.

Verification không bao gồm HTTP authentication integration hoặc PermissionService. Authentication/accessor source được giữ nguyên; console accessor regression chạy riêng.
