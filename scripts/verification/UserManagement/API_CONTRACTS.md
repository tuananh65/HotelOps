# API contracts — đề xuất để duyệt

Tên route/DTO và HTTP semantics dưới đây là thiết kế kỹ thuật, không phải nội dung đã được SRS phê duyệt. O1–O9 trong OUTSTANDING_DECISIONS là gates. Không API nào ngoài GET /api/users hiện có được triển khai.

## Common contract

Cookie Authentication + SQL Session Validation; không bearer/role bypass. Unsafe methods dùng existing CSRF token/cookie flow. No-store mọi response chứa personnel/credential/access. TargetId là selector; authorize trước khi trả target/filter/error details.

Đề xuất:401 invalid session;403 thiếu endpoint permission;404 chung cho target absent/deleted/out-of-scope sau endpoint gate;400 field validation;409 duplicate/business conflict/stale state (nếu chọn ETag chuẩn thì dùng412/428, cần chốt O3);503 availability theo infrastructure, không giả missing user. Không phản hồi SQL exception/hash/token.

Mọi mutation nhận opaque expectedVersion/concurrency token sau khi O3 được duyệt; chưa có row_version property để map. Không dùng UpdatedAt như bảo đảm concurrency vì credential/grant writers không cập nhật thống nhất. Actor/time lấy từ validated server context, không nhận actorId/grantedBy/isSystemRole/dataScope/passwordHash từ client.

## Read endpoints

| Route đề xuất | Request | Response DTO / giới hạn |
|---|---|---|
| GET /api/users/{userId} | positive long targetId | UserAccountDetailDto { id,employeeCode,username,fullName,email,phone,accountStatus,primaryBranch:{id,code,name}?,department:{id,code,name}? }. Version chỉ thêm khi O3 chốt; không lồng effective permissions mặc định |
| GET /api/users/{userId}/access | feature?, permissionCode? (catalog code, exact filter đề xuất) | UserAccessReviewDto { userId,evaluatedAt,accountStatus,roles,branchAccess,permissions }. Chi tiết paths bên dưới; shape/paging limits cần chốt O2 |

Roles của access response đề xuất gồm userRoleId, role code/name, branchId?, assignedAt/expiresAt,isActive và eligibility. BranchAccess gồm branchId,active/revoked membership; không trình bày department array như dữ liệu thật khi model chỉ có primary DepartmentId. permissions grouped feature với permissionCode, dataScope, branchId?, grantingRole và assignmentId. Giữ từng path, không cross-product role scopes. OWN ghi “requires resource ownership”, không effective=true cho mọi resource. ALL bound branch không hiển thị global. Inactive/expired/revoked paths nếu hiển thị phải có exclusion reason, không nằm trong effective set.

Đánh giá target grant eligibility không chứng minh target có valid session hoặc có thể approve mọi business operation; runtime SoD/resource rules vẫn áp dụng. Không đổi HttpContext.User/claims/accessor sang target. Read model/service riêng, authorize reviewer trước; reuse verified evaluator rules qua thiết kế được review, không sao chép query rồi gọi là đầy đủ effective access.

## Create — UC-004

POST /api/users

CreateUserAccountRequest candidate:
{ employeeCode, username, fullName, email, phone?, primaryBranchId, departmentId?, branchAssignments:[{branchId,departmentIds:[]}], roleAssignments:[{roleId,branchId?,expiresAt?}] }.

Đây là target contract cần O4/O5/O6: employeeCode required/unique trong DB nhưng không có trên SRS form; cách cấp mã chưa chốt. departmentIds[] mô tả nhu cầu SRS, chưa persist được đầy đủ. Không được ship DTO này rồi âm thầm chỉ lưu department đầu tiên. Quyền USER_MANAGE cộng quyền gán role/scope chưa chốt; không cho create-with-roles bypass ROLE_MANAGE.

Response201 Location /api/users/{id}, body { user:UserAccountDetailDto, temporaryCredential:{password,expiresAt} }. Chỉ sau full commit; no-store; credential không có retrieval/replay endpoint. SQL/HTTP không atomic: response thất lạc không được truy xuất plaintext từ DB; recovery/reset và idempotency O6 cần duyệt. Không tự tạo default admin role/password hoặc insert default identity.

Validation: theo UC004, max lengths từ model fullName200/username100/email150/phone20/employeeCode40 là giới hạn lưu trữ, không business rule mới. Email format theo screen; uniqueness username/email và DB employeeCode, kể cả concurrent inserts. Reuse collation/normalization được duyệt; scope/roles active, duplicate IDs/date bounds xử lý rõ. O4 chốt null branch trên ALL assignment, role-specific OM/DH rules.

## Update / reset — UC-005

PUT /api/users/{userId}/profile
Request { fullName,email,phone?,expectedVersion }; response200 UserAccountDetailDto với new version khi có protocol.
Không nhận username,roles,branchId,departmentId,accountStatus,employmentStatus,managerUserId/avatarUrl để tránh overposting. Unknown security fields phải reject bằng validation đã chốt, không silently apply.

POST /api/users/{userId}/credential-reset
Request { expectedVersion, confirmation } candidate; confirmation chỉ xác nhận thao tác, không là proof identity verification. Quy trình người quản trị xác minh danh tính phải chốt O6. Response200 { temporaryCredential:{password,expiresAt} }, không secret ở URL/log. Revoke all sessions+credential/audit atomic; reset existing primitive có24h, reset lockout state; primitive effects phải được chấp thuận ở admin contract.

## Activate/deactivate — UC-006

PUT /api/users/{userId}/account-status
Request { accountStatus:"ACTIVE"|"INACTIVE", reason?,expectedVersion }; response200 minimal { id,accountStatus,newVersion? }.

Reason bắt buộc >=10 cho deactivate; activation reason requirement/no-op semantics cần chốt. Không sửa EmploymentStatus hoặc DeletedAt, không hard delete. Deactivate revoke all sessions trong transaction; activate giữ LockedUntil, không restore sessions/roles. Last-admin/self/open-work guard ở server trước commit; race guards O3/O7/O8. Trả conflict summary tối thiểu nếu open work, không lộ DEV2 task details ngoài reviewer scope.

## Branch/department và role assignment — tách UC-007/010

PUT /api/users/{userId}/scope
Request { primaryBranchId,departmentId?,branches:[{branchId,departmentIds:[]}],expectedVersion }.
Response200 scoped assignment snapshot; O4 chưa cho implement multi-department shape. Full replacement là proposal cần xác nhận; không xóa membership/history thật, revoked_at cho removal. Scope removal phải xử lý role assignments còn bound branch tương ứng trong cùng approved workflow, không để silent inconsistent grants.

POST /api/users/{userId}/role-assignments
Request { roleId,branchId?,expiresAt?,expectedVersion }; response201 { assignmentId,role:{id,code,name},branchId,assignedAt,expiresAt,isActive,newVersion? }.

POST /api/users/{userId}/role-assignments/{assignmentId}/revoke
Request { expectedVersion,reason? }; response200 updated assignment snapshot.
Assignment phải thuộc target; isActive=false thay hard delete; UserRole chưa có revokedAt/revokedBy nên audit giữ actor/time/reason. Không tự thêm cột. Reason mandatory/minimum cho role revoke chưa được GBR012 nêu rõ, để O9 quyết định.

Grant/revoke routes là slice UC010 (SRS UI batch Save); nếu cần atomic multi-role transfer thì chốt batch contract riêng, không hứa hai calls là atomic. Không nhận RolePermission.DataScope để sửa trong user assignment. UC008/009 catalog writes là task khác, chưa đề xuất endpoint triển khai.

## Audit / concurrency cho mọi mutation

GBR007/008: account/scope/roles/credential/session changes liên quan và AuditLog cùng transaction; actor UTC entity/action before-after whitelist. Reason location và action code phải khớp constraints AuditLog, không tự đặt enum SQL mới. Không password/hash/token trong old/new/request logs. Không audit GET như một mutation bắt buộc.

O3 cần stale-client check trên aggregate User+roles+memberships; per-user lock không bảo vệ hai admin xóa lẫn nhau hay concurrent work assignment. Chốt shared last-admin guard và work-assignment locking với mọi writers. Không retry mù khi commit outcome unknown, không trả secret/success trước commit.
