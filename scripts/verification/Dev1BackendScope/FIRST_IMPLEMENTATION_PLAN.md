# Task coding đầu tiên đề xuất — UC-141 User Directory (read slice)

Đây là contract đề xuất để review cho task kế tiếp. Chưa tạo endpoint/DTO/service. Giữ single ASP.NET project; không đổi schema, generated entities hoặc Permission Matrix.

## Scope và ranh giới

Triển khai phần danh sách của UC-141: tìm kiếm users, lọc branch/department/role/account status, phân trang và sắp xếp xác định. Actor SRS: Administrator; kiểm tra bằng permission **USER_VIEW**, không hard-code role name/ID.

Chưa gồm create/update/deactivate/reset-password, scope/role assignment, target-user effective permission review, hoặc detail editor. UC-141 bước chọn account để xem effective permissions liên quan UC-005/011; sau task list slice **không được tuyên bố toàn bộ UC-141 hoàn tất**. Không tạo UC hoặc permission mới cho endpoint.

## API contract đề xuất

`GET /api/users`

| Query | Contract |
|---|---|
| search | Optional, trim; full name/username/email theo UC-141; dùng EF parameterized expressions. Giới hạn kỹ thuật độ dài phải thống nhất/validate trong task coding, không coi là rule SRS đã chốt |
| branchId | Optional positive long; lọc primary branch của target account theo User.PrimaryBranchId; không dùng làm bằng chứng quyền caller |
| departmentId | Optional positive long; nếu kèm branch phải thuộc branch đó; không tồn tại/out-of-scope không được làm lộ record |
| roleId | Optional positive long từ catalog; lọc theo assignment còn hiệu lực và role active, không nhân đôi user khi có nhiều roles. Cần chốt có thêm chế độ historical role membership trong task sau; không âm thầm trộn |
| accountStatus | Optional; external Active/Inactive map rõ ràng sang giá trị DB đã xác minh trước implementation; không coi Locked là AccountStatus mới |
| page | Default 1, >=1; kiểm tra overflow khi tính offset |
| pageSize | Default 20, 1–100 theo SYS-PAR-11/GBR-015 |

Sort cố định `FullName`, rồi `UserId` để tie-break theo UC-141 và GBR-015. Không thêm unrestricted sort expression. Search collation semantics dựa SQL hiện có, không hứa accent sensitivity khi chưa kiểm chứng.

Response `200`: DTO đề xuất `{ items, page, pageSize, totalCount }`. Mỗi item gồm `id`, `employeeCode`, `username`, `fullName`, `email`, `primaryBranch` (id/code/name hoặc null), `department` (id/code/name hoặc null), `roles` (role id/code/name và assignment branch), `accountStatus`. Tổng count áp dụng cùng authorization/filter predicates, trước paging; không lấy số lượng toàn hệ thống khi scope bị giới hạn. Không trả EF entity.

Không đưa password/hash, temporary credential/expiry, failed-login counters, lock internals, session IDs/hashes, CSRF/session credentials hoặc raw effective-grant debugging vào user-list response/logs. Khả năng hiển thị temporary-lock indicator chỉ thêm khi có contract tối thiểu được thống nhất.

HTTP: 200 kể cả empty list; 400 cho query không hợp lệ; 401 khi không có validated session; 403 khi restricted hoặc thiếu quyền. DB/catalog unavailable không bao giờ trở thành 200/allow: giữ fail-closed error behavior hiện có và thống nhất ProblemDetails với shared auth owner nếu muốn thay đổi. Không thêm fallback auth policy toàn ứng dụng vì ảnh hưởng DEV2.

## Security và scope

Catalog hiện có SYS_ADMIN → USER_VIEW:ALL với null-branch assignment phù hợp. Scope list đầu tiên dùng **existing Global USER_VIEW policy** (`HotelOps.Permission:Global:USER_VIEW`), bắt buộc SQL-validated cookie, active user/role/assignment. Có role name Administrator không tự cấp quyền; client không được tự chọn scope.

Global policy chỉ nhận grant ALL không gắn branch theo core hiện tại; caller chỉ có branch-bound/OWN grant phải bị từ chối cho contract global này. Không silently nâng branch-bound grant thành global. Nếu team cần branch-scoped user administration, đó là scope mở rộng cần xác nhận mapping và thiết kế scoped query riêng, không implement bằng role check.

UC-141 yêu cầu nằm trong scope của administrator; chọn global endpoint làm slice giới hạn cho grant hiện có, **không tuyên bố hỗ trợ mọi scoped administrator trong SRS**. Branch/department query chỉ thu hẹp target rows sau authorization. Target DeletedAt != null được loại khỏi list; inactive chưa deleted vẫn có thể hiện khi filter phù hợp. Không dùng `UserBranchAccess.IsPrimary` làm giới hạn quyền.

## Thành phần đề xuất và dependencies

- Controller mới trong Controllers: HTTP/query/response only; service trong Services/Users và DTOs/Users. Tên class chính thức thống nhất theo naming repo lúc coding; các thành phần này chưa tồn tại.
- Reads: User, Branch, Department, UserRole, Role. Authorization tái dùng Permission/RolePermission/UserBranchAccess/UserSession qua core, không tự rewrite query security.
- AsNoTracking, EF async với CancellationToken trên mọi truy vấn DB. Project đúng trường DTO, tránh N+1 và collection-join nhân bản ảnh hưởng pagination. Không include toàn bộ User navigation graph.
- Không cần transaction ghi, rowversion mới, temporary credential issuance hoặc audit mutation cho read này. Không gọi TemporaryCredentialService. Không thêm Employee table.
- Program.cs DI là shared component; giải thích chính xác registration mới và lấy approval theo AGENTS.md trước khi chỉnh trong task coding. Task 16 không thay DI.

## Business rules và các quyết định còn cần xác nhận

Nguồn: UC-141, functional User List screen, GBR-001/002/015 và SYS-PAR-11. Snapshot catalog và Task 15 không thay thế toàn bộ matrix pending.

- Trước coding xác nhận contract global-only phù hợp đợt đầu, semantics filter primary branch vs any branch membership, chỉ active/effective role filter và mapping Active/Inactive. Đây là đề xuất có phạm vi rõ, không phải quyết định nghiệp vụ được tự phê duyệt.
- User.AccountStatus là string hiện không có CHECK enum đầy đủ trong model; không tự thêm status values. AccountStatus và EmploymentStatus là hai khái niệm khác nhau.
- Không dùng IPermissionService hiện tại để evaluate quyền của target user; đó thuộc UC-011 task sau.

## Test strategy

1. Query/service cases: keyword blank/trim/name/username/email; combined filters; role nhiều assignment không duplicate; target soft-deleted bị loại, inactive được lọc đúng; null branch/department; deterministic FullName/UserId order; empty list và count sau filtering.
2. Boundary cases: page/pageSize invalid, max100, very large offsets, long input, SQL-special characters được parameterized, cancellation propagation. Không dùng EF InMemory thay cho xác minh SQL collation/query behavior.
3. HTTP/security cases: anonymous401, validated USER_VIEW allow200, authenticated no permission403, restricted403, expired/revoked session401, revoked grant phản ánh ở request sau; denied action không gọi business query. Check count/result không bị leak khi denied.
4. SQL integration opt-in trên DB trial độc lập: fixture identity/credentials theo hướng dẫn Task 15, nguồn grant hợp lệ và negative roles; không dùng HotelOpsDB, không tắt guard của runner cũ. Fixture target users ở hai branch, nhiều role và >20 rows. Kiểm tra response không chứa secrets hoặc EF navigation graph.
5. Scope tests: global ALL với assignment null-branch được phép; branch-bound/OWN grants bị từ chối theo contract này; branch filter không nâng quyền. Khác biệt scoped admin SRS được ghi là chưa hỗ trợ.

## Acceptance criteria

- Routes/DTO/status/filter/sort/paging đúng contract đã xác nhận; không endpoint mutation hoặc API DEV2 bị sửa.
- Mọi request đều qua session + USER_VIEW; không role/branch ID hard-code, no unauthorized query/count exposure.
- Async/AsNoTracking/DTO projection và cancellation được kiểm chứng; không trả credential material.
- Tests query, HTTP deny/allow và SQL behavior trên trial đã chạy với evidence; nếu chưa có fixtures thì ghi testing blocked, không claim fully tested.
- `dotnet build HotelOps.slnx` pass; test suite thực sự discover hoặc console assertions được gọi rõ ràng, không coi `dotnet test` zero-discovery là passed functionality.
- Không sửa schema/main DB/generated entities/matrix; shared DI registration chỉ sau approval. Báo cáo hoàn tất phân biệt list slice với toàn bộ UC-141 và UC-011.
