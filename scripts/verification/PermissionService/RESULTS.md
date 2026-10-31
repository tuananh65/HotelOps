# Task03 — PermissionService Core results

**PASS WITH NOTES** cho phạm vi core; chưa chứng nhận toàn bộ HTTP/resource-specific Authorization.

## A. Context Verification

Đã kiểm tra User, Role, Permission, RolePermission/DataScope, UserRole, UserBranchAccess, Branch và Fluent mappings. SQL SELECT xác nhận role/user/branch flags, assigned_at/expires_at, branch_id nullable, granted_at/revoked_at; Permission không có cột trạng thái Active. Catalog vẫn 12 roles/10 Active, 62 permissions/140 grants; scopes ALL8/BRANCH81/OWN51, không DEPARTMENT (DataScope regression trên DB thật PASS).

Các assignment hiện tại gồm SYS_ADMIN global; OM Branch1; DH Branch1/2; các role vận hành theo branch. Có STAFF assignment còn active nhưng role STAFF inactive, không được dùng cấp quyền. Không thay catalog hay dữ liệu để làm test pass.

## B–C. Implementation và scopes

Tạo IPermissionService.cs (contract/result/resource resolver), PermissionDataSource.cs (EF read model/query), PermissionService.cs (evaluation) trong Security/Authorization. Sửa Program.cs thêm ba scoped registrations cho data source, resolver mặc định và service; dùng lại TimeProvider/DbContext/CurrentUserAccessor đã có.

Mỗi path giữ đúng assignment của role sở hữu grant. Global/branch/resource APIs phân biệt phạm vi rõ ràng. Branch access không mở rộng role sang branch khác; ALL không tự bỏ qua branch-bound assignment; OWN cần authoritative adapter. Mặc định thiếu resource adapter luôn trả ResourceContextRequired, không Authorized. Chưa có business adapter vì chưa có ownership rule/resource-specific binding được duyệt cho task này.

## D. Security Verification

Test doubles kiểm tra user/session invalid/restricted, inactive user/role, missing grant/permission, nhiều role, expired/revoked/future assignments, branch isolation/access revocation, null branch, ownership true/false/wrong branch/missing context, ALL, unknown scopes, không role-name bypass, không ghép grant/branch từ hai role, store changes giữa lần evaluate, database/timeout/provider errors, programming error propagation, cancellation, default decision Denied và scoped DI.

Production queries chỉ đọc. No cache, no credentials logging. PermissionService không là đường miễn restricted authentication. SoD/workflow restrictions vẫn là trách nhiệm của task nghiệp vụ sau.

## E. Testing thực tế

| Kiểm tra | Kết quả |
| --- | --- |
| dotnet build HotelOps.slnx (sau code cuối) | PASS, 0 warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; solution chưa có framework test project |
| PermissionService --live | **50/50 PASS: 43 core/DI doubles + 7 live SQL checks** |
| Live OM user1 ROOM_VIEW Branch1 / Branch2 | Granted / Denied |
| Live SYS_ADMIN user16 USER_VIEW global / ROOM_VIEW Branch1 | Granted / Denied |
| Live missing permission / no tracking | Denied / PASS |
| CurrentUserAccessor regression | **14/14 PASS** |
| DataScope regression trên HotelOpsDB | **15/15 PASS** |
| git diff --check | PASS |
| HTTP Authorization / ownership adapter nghiệp vụ | Not Run / chưa triển khai |

Live core verification dùng accessor double cùng EF queries thực tế; không đăng nhập HTTP hoặc xác nhận phiên thật cho các user mẫu. Test thu hồi grant dùng test store thay đổi giữa hai lần evaluate, không thực hiện UPDATE vào HotelOpsDB. Test lỗi SQL dùng exception doubles, không làm gián đoạn server thật. Không diễn giải các test này thành chứng minh mọi workflow/resource ownership.

Console projects vẫn có warning NU1900 do không truy cập vulnerability metadata tại api.nuget.org. Không tắt audit. Không có warning/error trong solution build cuối.

## F–G. Compatibility và Git

FE-01, CurrentUserAccessor, Task02 mappings và middleware pipeline giữ nguyên; không policy/handler/attribute mới. Service đọc matrix thực tế, thay đổi role/grants được đọc lại mỗi lần, unknown scope fail-closed. Resource adapters tương lai phải kiểm tra binding và ownership từ backend, tuyệt đối không echo client data.

Files mới của task: ba source files nêu trên và bốn files tại thư mục verification này (csproj, Program.cs, README.md, RESULTS.md). File source sửa trong task: Program.cs thêm DI. Những diff DbContext/RolePermission có sẵn thuộc Task02, giữ nguyên; các file Task03B/untracked có sẵn được bảo toàn. Không commit/push/merge/PR, không SQL writes/schema/migration/scaffold.

## H. Final Verdict

**PASS WITH NOTES.** Core và EF read-only resolution đạt kiểm tra đã chạy. OWN hiện chỉ có extension contract/default-deny và test-double evidence; cần adapter + integration tests theo từng resource ở task sau. Chưa bảo vệ controllers hoặc thực hiện HTTP authorization; còn giới hạn NuGet vulnerability metadata nêu trên.
