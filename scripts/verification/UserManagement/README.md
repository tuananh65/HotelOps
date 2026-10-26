# Task21 — User Management Contract Review

Ngày: 2026-10-26. DEV1 / FE-01. Hồ sơ phân tích, không phải phê duyệt business rules hoặc API mới.

## Baseline

Branch feature/BE1-database-integration; HEAD 20c1063 (UC-141). Tracked tree/index sạch trước task. origin tracking local không báo ahead/behind; task này không fetch, không xác minh remote trực tiếp và không merge. Hai rehearsal worktrees được giữ nguyên.

Đã đọc AGENTS.md; SRS_CHANGE_NOTES.md; SRS sections01 (UC inventory và draft Permission Matrix),02 (UC-004–011/141),03 (User Detail),05 (GBR); Task16 coverage/backlog; UsersController, UserDirectory contracts/service/query; PermissionService/data source; User/Role/UserRole/RolePermission/UserBranchAccess/AuditLog và DbContext; credential/reset/session primitives; RBAC catalog/release verification và canonical seed manifest.

## Hồ sơ

- [Mapping và business rules](USE_CASE_MAPPING.md)
- [HTTP và DTO đề xuất](API_CONTRACTS.md)
- [Quyền và security](AUTHORIZATION_MATRIX.md)
- [Dependencies, trình tự và task đầu tiên](IMPLEMENTATION_ROADMAP.md)
- [Quyết định còn thiếu](OUTSTANDING_DECISIONS.md)

## Kết luận

READY WITH NOTES cho review; mutation implementation còn các blockers riêng.

GET /api/users đã có Global:USER_VIEW, SQL-backed cookie/session infrastructure, no-store, pagination20/max100. Chưa có detail/access-review/admin mutation APIs. Task16 là snapshot cũ trước UC-141, không dùng dòng “chưa có list” làm hiện trạng.

Các gaps lớn: chưa chốt quyền detail/access review/UC-007 và tổ hợp quyền create-with-roles; không có row_version User; multi-department scope không được model biểu diễn; provisioning primitive không composable transaction; last-admin/open-work/SoD/concurrent grant changes cần contract và protocol có kiểm chứng.

Chỉ thêm sáu Markdown trong UserManagement. Không sửa Task20A, source, Permission Matrix, database hoặc modules DEV2; không stage/commit/push/merge. Build/tests/API/DB runtime: Not Run vì documentation-only. Không dùng kết quả UC-141 trước đây để tuyên bố kiểm thử các API chưa có.
