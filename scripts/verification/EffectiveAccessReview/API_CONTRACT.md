# Conditional API contract — chưa triển khai

Route candidate: GET /api/users/{userId}/effective-access, phù hợp prefix/conventions UsersController và UC011 read-only. Task21 /access là proposal cũ; Task23 cho review /effective-access, không cần tạo hai aliases. Route chỉ được dùng sau gate approval.

## Proposed request / response

userId positive Int64; optional feature (đối chiếu Permission.ModuleCode) và permissionCode exact catalog filter theo UC011. Chốt validation/length/case và paging/bounds trước code. Missing/soft-deleted404 chung chỉ sau reviewer authorization; anonymous401, thiếu quyền403 trước target lookup; invalid input400. Đây là proposed semantics, chưa runtime verified.

DTO đề xuất có điều kiện:
- userId, evaluatedAtUtc, accountStatus.
- roleAssignments: assignmentId, roleId/code/name, branchId?, assignedAt/expiresAt, eligibility. Chốt có chỉ hiển thị eligible hay cả inactive/expired paths.
- primaryDepartment: chỉ một User.DepartmentId nếu được phép hiển thị. Không tự tạo department memberships array theo nhu cầu SRS vì chưa có multi-department mapping.
- branchAccess: scope membership tối thiểu đã được reviewer visibility cho phép, không credentials.
- grants: permissionCode, feature/moduleCode, role/assignment provenance, dataScope và branchId?.
- effectiveScopes nếu được tính đúng: permissionCode + scope + branchId + source paths; không flat canAccess=true.

Không sessions/session IDs/tokens/hash/password/credential timestamps, grantor personnel profile hoặc arbitrary EF graph. Không thêm field chỉ vì entity có sẵn. No-store; server derives caller/time, không actor từ client.

## Bốn lớp dữ liệu

1. Assigned roles: bản ghi UserRole, kể cả record có thể không eligible; cần label rõ nếu show.
2. Granted permissions: RolePermission gắn role, không đồng nghĩa target đang có quyền.
3. Eligible effective scope paths: chỉ paths qua các điều kiện User/assignment/role/time/branch/membership; union không bỏ branch binding.
4. Resource decision: phải có verified context/resolver và business checks. Không có context thì không kết luận Allow/CanAccess.

## Các quy tắc source hiện chứng minh

PermissionService dùng current authenticated user, không nhận arbitrary targetId. EfPermissionDataSource kiểm tra ACTIVE/nondeleted user; assignment IsActive/AssignedAt/ExpiresAt; Role.IsActive; branch-bound paths cần active branch và access chưa revoked với GrantedAt<=now.

| DataScope | Diễn giải được phép báo cáo sau approval |
|---|---|
| ALL, assignment.BranchId=null | Eligible grant có phạm vi không bound branch, không phải bypass nghiệp vụ/SoD |
| ALL, BranchId!=null | Chỉ branch assignment đó, không được nâng thành Global |
| BRANCH | Cần assignment branch hợp lệ; không ghép membership ở branch khác |
| OWN | Resource ownership còn phải resolver xác minh; chỉ báo conditional scope, không arbitrary room/task access |
| DEPARTMENT / unknown | Current evaluator không grant; không “normalize” thành BRANCH |
| Inactive user/deleted, role/assignment inactive, expired/future | Không effective; chốt visibility của raw assignments/reasons cho reviewer trước |

Inactive branch hoặc revoked branch access làm branch-bound path không hợp lệ. Current global-null ALL không tự lấy scope từ PrimaryBranchId. Account lock/restricted temporary session là authentication constraint khác grant eligibility; grant snapshot không chứng minh target có session valid. Không suy ra department authorization từ DepartmentId hoặc ManagerUserId.

## Kiến trúc nếu được duyệt

Authorize reviewer bằng foundation trước lookup. Không thay HttpContext.User, cookie claims, CurrentUserAccessor hoặc SQL session snapshot thành target. Không gọi EvaluateGlobalAsync rồi gắn kết quả caller cho target.

Cần thiết kế reuse cùng evaluation predicates/logic qua abstraction target-review được duyệt; current service caller-bound chưa đủ để thực hiện một target snapshot bằng API có sẵn. Không sao chép evaluator thành hệ RBAC thứ hai. Shared change/DI phải giải thích impact trước theo AGENTS; task này không sửa.

Query async/no-tracking, projection whitelist; bounded number queries, không query từng role/permission N+1; evaluatedAt thống nhất và snapshot-consistency strategy cần chốt. Deterministic order đề xuất roleCode/assignmentId, permissionCode/dataScope/branchId/assignmentId. Dedupe chỉ khi không mất provenance/scopes; OWN và ALL/BRANCH không gộp thành một scalar capability.
