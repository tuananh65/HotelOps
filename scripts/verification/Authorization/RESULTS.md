# Task04 — Authorization Policies & Handler results

**PASS WITH NOTES** cho hạ tầng đã triển khai và các kiểm tra đã thực hiện.

## A. Context Verification

Đã đọc CurrentUserAccessor, PermissionService/interface/data source/resource resolver, Cookie Infrastructure/Events, middleware, AuthController và báo cáo Task03. Dùng đúng EvaluateGlobalAsync, EvaluateBranchAsync, EvaluateResourceAsync hiện có; không viết lại PermissionService hoặc thêm RBAC từ claims. Pipeline hiện tại phù hợp, giữ nguyên thứ tự.

## B. Implementation

Files mới:

- `Security/Authorization/PermissionRequirement.cs`: requirement, check mode, policy-name helper và branch-level context.
- `Security/Authorization/PermissionAuthorizationHandler.cs`: scoped handler gọi PermissionService theo đúng context.
- `Security/Authorization/PermissionPolicyProvider.cs`: provider + EF catalog reader.
- `Security/Authorization/PermissionAuthorizationRegistration.cs`: registration extension.
- `scripts/verification/Authorization/Authorization.Verification.csproj`, `Program.cs`, `README.md`, `RESULTS.md`.

File sửa trong task: `src/HotelOps.Api/Program.cs`, thêm một lệnh AddHotelOpsPermissionAuthorization. Các diff mapping/DI cũ thuộc Task01–03 được giữ nguyên.

## C. Security Design

Provider chỉ tạo permission policy khi code canonical và tồn tại trong SQL permissions; giữ Default/Fallback/External policies. Không cache permission policies hoặc thêm fallback permit-all. Unknown policy trả null, endpoint dùng nó nhận generic configuration-error 500, không được chạy tiếp.

Handler yêu cầu chính request principal/scheme và validated CurrentUserAccessor; restricted bị fail. Branch/resource target chỉ nhận typed backend context, không đọc client query/route/header. Branch được PermissionService đối chiếu SQL; resource tiếp tục qua resolver. Không có default ownership adapter hoặc role-name bypass. Handler chỉ Succeed requirement đang xử lý khi kết quả thực sự Granted; Fail không làm requirements khác biến mất. Data exceptions/Unavailable không bao giờ trở thành Authorized.

## D. Tests & Verification

| Nhóm | Kết quả thực tế |
| --- | --- |
| dotnet build HotelOps.slnx | PASS, 0 warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit0, solution chưa có framework test project |
| Authorization verification --live | **65/65 assertions PASS** |
| Provider/handler/HTTP/doubles | 61 assertions trong host loopback cô lập |
| EF catalog reader trên HotelOpsDB | 4 assertions: target DB, valid code, missing code, ordinal casing |
| PermissionService regression --live | **50/50 PASS**, gồm 7 SQL checks |
| CurrentUserAccessor regression | **14/14 PASS** |
| DataScope regression | **15/15 PASS**, SQL read-only |
| git diff --check | PASS |

HTTP kiểm tra thực tế qua Kestrel, real cookie handler/events/middleware: anonymous401/AUTHENTICATION_REQUIRED; valid permission/context200; thiếu quyền403/FORBIDDEN; inactive account/role; cross-branch; role/branch mixing; SYS_ADMIN/OWNER không bypass; OWN true/false/không adapter; restricted403/PASSWORD_CHANGE_REQUIRED; invalid cookie401; CSRF thiếu token400; multiple requirements; default/fallback/external policy preservation. Exception từ service/catalog nhận generic500, không xuất chi tiết provider. Service trả Unavailable nhận forbid403 qua cookie events. Test store không ghi SQL.

Imperative authorization kiểm tra đúng typed backend branch target, từ chối principal khác mượn identity của request và snapshot bị thiếu. Catalog double bị xóa code giữa hai lookup chứng minh provider không lưu policy mặc định cho code đã bị thu hồi. Grant/status logic được kiểm tra qua PermissionService thật với data doubles, và regression SQL riêng.

Lần test host đầu tiên lỗi DI vì CookieSessionValidator concrete vẫn đăng ký trong host không có DbContext. Đã loại đăng ký SQL validator khỏi **test host** và thay bằng session validator/toucher doubles. Không sửa source Authentication hoặc tắt DI validation. Sau sửa, toàn bộ test nêu trên PASS.

NuGet NU1900 vẫn xuất hiện ở console projects vì không tải được vulnerability metadata api.nuget.org. Không tắt Security Audit. Solution build cuối không có warnings/errors.

## E. Compatibility

FE-01 Authentication, CookieSessionValidator, CurrentUserAccessor, PermissionService, EF mappings và controller source không đổi. Cookie scheme, middleware order và 401/403 problem contract giữ nguyên. Scoped handler không giữ DbContext trong singleton; provider singleton mở async scope cho catalog SELECT. Không hard-code connection strings hoặc permission matrix.

## F. Outstanding Work

- Resource adapters authoritative theo từng nghiệp vụ; default resolver vẫn từ chối.
- Rollout policy/controller protection theo từng API; task này không bảo vệ hàng loạt các API nghiệp vụ.
- SoD, state transitions, ownership rules và trusted feature loader trước authorization cần thiết kế/duyệt theo từng feature.
- HTTP integration với SQL sessions/credential workflows thật, HTTPS/browser behavior và resource adapters thật chưa kiểm chứng. HTTP test ở đây dùng session/data doubles và ephemeral keys trong isolated host; không được gọi là production end-to-end Authentication/Authorization PASS.
- Department Retirement là task riêng, không xử lý.

## G. Git Status

Chỉ thêm 8 files của Task04 và thêm một DI call trong Program.cs. Giữ nguyên các thay đổi có sẵn, bao gồm files untracked. Không SQL writes/schema/migration/scaffold, không sửa catalog/config/FE-01, không commit/push/merge/PR.

## H. Final Verdict

**PASS WITH NOTES**: policies/handler và các trường hợp bảo mật quan trọng đã được kiểm chứng trong phạm vi mô tả. Production resource authorization vẫn fail-closed tới khi có adapters và API integration được triển khai; còn NU1900 và giới hạn HTTP/SQL-session integration nêu trên.
