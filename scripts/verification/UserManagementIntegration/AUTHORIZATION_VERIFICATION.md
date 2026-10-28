# Authorization integration — doubles vs runtime

## Foundation / source findings

USER_VIEW Global gate chỉ cho list/detail;new profile controller/service dùng USER_MANAGE và không bị class-level USER_VIEW AND. No role-name bypass;actor từvalidated Cookie/SQL snapshot. Mutation service BeginTransaction trước CanManageAsync:reauthorization diễn ra trong transaction,trước target snapshot/UPDATE.

ALL chỉ unbound;BRANCH actual membership actor+target cùng active branch và eligible grant trong chínhbranch;OWN self+valid OWN grant;branch-boundALL không widen. Không PrimaryBranchId shortcut. Next-request invalidation đã test;transaction không serialize tuyệt đối với concurrent grant revocation ở workflow khác,không claim stronger guarantee.

## Tests tách biệt

1.Foundation167 checks:43 PermissionService+14 CurrentUserAccessor+61 Authorization+49 ResourceAuthorization;core/HTTP doubles,kèm production handlers/adapters. Không live production SQL.
2.Inherited profile harness:real memberships + grant-path doubles chứng minh ALL/BRANCH/OWN eligibility,expiry/inactive/branch isolation;không coi là runtime catalog grant.
3.**Task27 real runtime grants:**chỉ trên mới patched trial,custom roles T27_PROFILE_BRANCH và T27_PROFILE_OWN,RolePermission USER_MANAGE scopes BRANCH/OWN và UserRole branchA;không sửa official SYS_ADMIN/OWNER grants hoặc62 permission records. Actual API Cookie/SQL sessions và EfPermissionDataSource/evaluator.

Runtime đã PASS:
- BRANCH read-token200/PATCH200 cho common membershipA dù target primaryB.
- No common branch403;grant ởbranchB không borrowmembershipA;inactive assignment/role403.
- Branch-boundALL403 dù common branch.
- USER_MANAGE không tạo USER_VIEW:list/detail403.
- OWN self200/PATCH200;other-user403 trong cùngbranch.
- Revoked actor membership403 với cookie còn trướcđó.
- Normal seeded OWNER không management trước testgrant;chỉexplicit custom OWN mới cho self. Không bypass role-name.

Sau tests,gỡ đúngtest-role UserRoles/RolePermissions/Roles. Catalog fingerprint cuối lượt khớp trước,toàn bộ official rows giữ nguyên. SQL hậu kiểm0 test roles và0 unrevoked sessions trên3 trials. Grant cleanup không áp vào official IDs hoặc main DB.

Không cho test-only roles/SQL receipts vào canonical seed;harness tạo runtime ephemeral fixtures sau khi xác minh target receipt. Permissions là existingUSER_MANAGE,không permission63.
