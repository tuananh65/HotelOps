# GET /api/users/{userId}

Căn cứ: Task22 explicit instructions; Task21 UserManagement/API_CONTRACTS và AUTHORIZATION_MATRIX; SRS02 UC005/141 và SRS03 User Detail. Global visibility đã được duyệt trong Task22, không áp dụng cho APIs khác.

- userId: positive Int64 theo User.UserId. Không dùng route constraint long để tránh invalid ID rơi sang404; model binding/Range trả400 cho nonnumeric/overflow/zero/negative sau authorization.
-200: account tồn tại và DeletedAt=null, kể cả ACTIVE/INACTIVE; đây là trạng thái target, khác trạng thái caller phải active.
-404: absent hoặc soft-deleted, cùng generic ProblemDetails title; không query credentials để phân biệt.
-401: anonymous/invalid/expired/revoked session theo Cookie+SQL validation.
-403: caller authenticated không có eligible Global USER_VIEW; cả target tồn tại/không tồn tại đều bị gate trước target query.
- No-store theo UsersController; không hỗ trợ client actor/branch làm authorization evidence.

DTO whitelist:
```json
{
  "id": 123,
  "employeeCode": "example",
  "username": "example",
  "fullName": "Example",
  "email": "example@example.invalid",
  "phone": null,
  "accountStatus": "ACTIVE",
  "primaryBranch": { "id": 1, "code": "BR001", "name": "HotelOps Branch 1" },
  "department": { "id": 1, "code": "HK", "name": "Housekeeping" }
}
```

IDs trên chỉ minh họa shape, không business constants. Branch/department nullable. EmployeeCode là identifier đã có UC141 và được Task21 minimal detail contract chọn; không mở employment/manager/avatar/audit/session fields. Không roles/effective permissions/branch membership arrays hoặc rowversion giả.

Global policy class-level hiện có gọi PermissionService: chỉ ALL trên active eligible assignment không bound branch. BRANCH/OWN/branch-bound ALL fail-closed; không bypass SYS_ADMIN/OWNER hoặc matching target branch. Global grant hợp lệ được xem target ở branch khác theo contract, không phải cross-branch escalation.

Service validate positive ID/cancellation; AsNoTracking, direct scalar/reference DTO projection, một SingleOrDefaultAsync query có tag UserAccountDetail:Get. Không Include hoặc lazy per-user queries, không credential SELECT, không SaveChanges. Session-touch middleware hiện có vẫn có thể ghi activity của caller; “read-only” nghĩa endpoint không thay account/master/business data, không tắt session infrastructure.
