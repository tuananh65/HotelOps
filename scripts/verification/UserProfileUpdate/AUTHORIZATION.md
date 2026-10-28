# USER_MANAGE scope enforcement

Actor lấy từ CurrentUserAccessor dựa validated cookie+SQL SessionSnapshot;không role-name bypass,không client actor/branch proof. UserProfileController yêu cầu authenticated;service/access composition áp dụng USER_MANAGE trước trả profile. Update recheck scope trong transaction.

## Approved rules

- ALL: PermissionService.EvaluateGlobalAsync(USER_MANAGE),chỉ eligible unbound ALL.
- BRANCH: actor và target có UserBranchAccess GrantedAt<=now,RevokedAt=null,cùng branch active;grant USER_MANAGE/BRANCH hợp lệ phải nằm chính branch đó. Ít nhất1 common branch đủ. Không dùng PrimaryBranchId hoặc mọi actor branches như authority.
- OWN: targetId==actorId và eligible USER_MANAGE/OWN;không self-update mặc định. Valid OWN theo foundation vẫn cần branch-bound assignment/actor branch access.
- Branch-bound ALL không được widen thành scoped management.
- Active/nondeleted actor,valid role/assignment/time/access và unknown-scope denial do foundation quyết định. Target có thể inactive nhưng không softdeleted.

## Implementation không nhân đôi RBAC

UserProfileAccess lấy common memberships authoritative,sort branchIds;mọi decision dùng instance PermissionService với **real CurrentUserAccessor**,source adapter chỉ lọc DataScope chính xác BRANCH hoặc OWN và private resolver xác nhận user_profile/targetId/caller/proven branch. Không giả claims/identity,không thêm grants,không copy eligibility predicates. Global check dùng existing injected service. CleaningTaskResourceResolver đăng ký dùng chung không bị thay thế.

Service scoped policy không bị class Global USER_VIEW của UsersController AND vào vì controller mới. New read-token và PATCH dùng cùng management rules;không mở List/Detail BRANCH visibility.

## Catalog và tests

Current seed03:SYS_ADMIN USER_MANAGE/ALL,không role nào USER_MANAGE/BRANCH/OWN. Đây không phải permission mặc định cho bất kỳ user mang tên role;assignment/user/session vẫn cần valid. OWNER không có quyền này.

Positive/negative BRANCH/OWN tests dùng real SQL memberships + synthetic grant-path data source/core,không sửa catalog để tạo artificial roles/grants. Real HTTP tests dùng catalog hiện có và Cookie/SQL sessions;không tuyên bố scoped grants đã được production provisioned.

Recheck không phải global serialization với concurrent membership/grant revocation ở writer khác. Next request đọc quyền mới;request đang in-flight có race boundary như foundation hiện có. Không tự đổi UserRole/ROLE_MANAGE infrastructure để hứa stronger invariant ngoài profile task.
