# Use case mapping và requirements có căn cứ

Tất cả use cases dưới đây ghi actor Administrator trong SRS draft. Đây không phải chỉ thị hard-code SYS_ADMIN hoặc cho OWNER bypass. Mã quyền là catalog hiện có; các mapping còn thiếu được nêu rõ.

| Chức năng | UC chính xác | Permission mapping | Hiện trạng / dependencies |
|---|---|---|---|
| User Account Detail | Bước mở account trong UC-141; hiển thị trước edit UC-005 và access UC-011. Không có UC detail riêng | USER_VIEW candidate cho read-only; chưa duyệt quyền thấy toàn role/scope | Chưa endpoint. User, Branch, Department; assignments qua API riêng |
| Effective Roles & Permissions | UC-011 Review User Access Permissions | USER_VIEW hoặc ROLE_MANAGE cần chốt, không tự OR | Caller evaluator có; target-user review chưa có. UserRole/RolePermission/Permission/UserBranchAccess |
| Create User/Employee Account | UC-004 Create User Account | USER_MANAGE; nếu gán roles trong create cần chốt ROLE_MANAGE cùng operation | Primitive credential có; chưa provisioning orchestration. Không có entity Employee riêng |
| Update Account Information + admin reset | UC-005 Update User Account | USER_MANAGE | Chưa endpoint; update chỉ fullName/email/phone; reset là alternative flow riêng |
| Activate/Deactivate | UC-006 Activate/Deactivate User Account | USER_MANAGE | Chưa endpoint; User/UserSession/AuditLog + open-work dependencies |
| Branch/Department Assignment | UC-007 Assign User to Branch/Department | USER_MANAGE vs ROLE_MANAGE chưa xác nhận | Chưa endpoint; User primary scope + UserBranchAccess; thiếu multi-department relationship |
| Assign/Revoke roles | UC-010 Assign/Revoke User Roles | ROLE_MANAGE | Chưa endpoint; assignment branch/expiry, last-admin, open work, audit |
| Role definition / role permissions | UC-008 Create/Update User Role; UC-009 Configure Role Permissions | ROLE_MANAGE | Liên quan nhưng không đồng nghĩa role assignment. Dynamic roles xung đột fixed canonical catalog; defer riêng |
| Existing list | UC-141 View User List | Global USER_VIEW đã triển khai | GET /api/users; branch filter hiện dựa PrimaryBranchId, không phải toàn memberships |

## Rules theo nguồn

Nguồn: docs/requirements/srs_sections/02_use_case_specifications.md §1.4–1.11; §1.12 UC-141; 03_functional_requirements.md §1.2.2; 05_requirement_appendix.md.

| Chức năng | Business rules / validation / scope | Audit / concurrency / credential |
|---|---|---|
| Detail | Account trong scope; read-only; không suy ra quyền từ targetId. SRS không có full detail field whitelist | Không có yêu cầu audit read trong UC này; không tự tạo mutation audit cho GET. Version cần cho subsequent edit nhưng chưa có model |
| Access review UC-011 | Roles, branches, departments, effective permission list theo feature; filter feature/permission; từng permission có granting role; empty list hợp lệ | Không thay đổi dữ liệu. Một evaluatedAt nhất quán; snapshot consistency cần chốt; không giả danh target để gọi evaluator caller |
| Create UC-004 | GBR003/004/025/031: required fullName/username/email, phone optional; active branch/dept/roles, dept thuộc branch, username/email unique toàn hệ thống; Active account; roles và scope được chọn | GBR007/008/023: một transaction account/roles/scope/hashed credential/audit; random temp password24h hiển thị một lần, restricted first login |
| Update UC-005 | Chỉ fullName/email/phone; duplicate email bị từ chối; role/status đổi ở UC010/006. Username post-create editability SRS chưa quy định, không tự mở | GBR005 loaded row_version;007/008 same transaction. Admin reset sau xác minh danh tính+confirm, random temp, force-change, revoke all sessions, audit không password |
| Activate/deactivate UC-006 | GBR026 last active Administrator có permission administration phải được giữ;027 cấm tự deactivate;028 open work phải reassign/handover trước. Deactivate reason >=10 theo012. Reactivate giữ locked_until đến khi hết hạn | Status+session revoke+audit atomic. Activation không khôi phục session đã revoked.005 concurrency. Flow activation có reason nhưng min10 được xác nhận cho deactivation, không tự mở rộng012 sang mọi activation |
| Scope UC-007 | Target ACTIVE; một/nhiều branches và departments theo031; master active và dept cùng branch; removed scope có open work thì từ chối | Scope+audit atomic; quyền mới áp dụng next request.005 stale state guard; không copy primary branch thành toàn bộ access |
| Roles UC-010 | Target ACTIVE; role active; referenced scope hợp lệ;027 không tự revoke Administrator;026 không mất last admin;018 SoD; executor-role removal cần open-work check028 | Assignment+audit atomic; next request reflects grants.005; session invalidation strategy xem matrix, không tự tuyên bố mọi role change phải logout theo SRS |
| Role config UC-008/009 |029 system roles bảo vệ;030 chỉ permission catalog, không bypass scope/SoD;026 last admin |007/008 before/after grants;005. Canonical12 roles/62 permissions khác draft “nine actors/one-per-UC” |

GBR018 nguyên bản mô tả DH từng department bị Change Notes sửa: DH là branch-wide, không dùng Department.ManagerUserId để hạn chế role. SRS031 cho OM nhiều branches cũng cần reconcile canonical manifest “OM scoped to one branch”; không triển khai multi-branch OM theo draft.

## Data protection áp dụng từng chức năng

Detail/access/update/status/assignment responses chỉ DTO whitelist; không trả hash, raw session credential, session token/hash, cookie, failed-login internals hoặc navigation graphs. Credential create/reset là ngoại lệ rõ trong UC004/005: plaintext chỉ trả một lần sau commit, không có GET đọc lại, không audit/log/cache. Email/phone là personnel data, chỉ hiện cho caller đã được phép; assignment/grant details không mặc nhiên public vì list đã có email.
