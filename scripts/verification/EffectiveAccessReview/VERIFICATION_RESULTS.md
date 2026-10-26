# Verification Task23

## Thực hiện

- Contract/source inventory: đọc UC011, draft matrix, catalog và Task16/21/22; gate FAIL do chưa xác nhận permission/visibility.
- Remote develop:5299b5e9e8dd917abef327ab9f9bd5494064ccf9, không thay đổi baseline.
- dotnet build HotelOps.slnx: PASS,0 warnings/0 errors trong Task23.
- Source không có effective-access action/controller/service; không tạo endpoint để “test” thiếu contract.
- git diff --check và git diff --cached --check: PASS; index vẫn trống. Năm Markdown mới không trailing whitespace/dòng trống thừa cuối file.
- SHA-256:580/580 file có trước task giữ nguyên, gồm source chưa commit Task22 và mọi local artifacts. Chỉ thêm5 file EffectiveAccessReview; không stage/commit/push/merge.

## Not Run — không được báo PASS

Mọi functional/security HTTP test UC011: existing/no-role/multi-role/duplicate grants/inactive/ALL-BRANCH-OWN/missing-softdeleted/cookie/session/reviewer/crossbranch/disclosure/resource context **Not Run**, vì endpoint chưa được phép triển khai.

Không rerun UC141/Task22 SQL/HTTP harness; không tạo trial hoặc personnel/session fixture trong task analysis. Các kết quả234/144/167 ghi trong Task22 là lịch sử, không phải kiểm thử UC011.

Regression foundations chạy lại trong Task23, không --live: PermissionService43/43, CurrentUserAccessor14/14, Authorization61/61, ResourceAuthorization49/49; tổng167 PASS, command exit0. Các HTTP suites này dùng cookie/session/data doubles và production handlers/adapters; không chứng minh SQL target access-review behavior. Không báo các checks này là tests endpoint UC011.

## Test plan sau approval

Phải có reviewer allow/deny, self/crossbranch theo approved visibility, anonymous/invalid/revoked session; target no roles/multi roles/inactive/expired/future/deleted; path duplicates và provenance; ALL null/bound, BRANCH, conditional OWN, unknown scopes; no secrets và no false CanAccess. SQL test trên trial riêng xác minh query count/projection/cancellation và no mutations ngoài existing session touch.

Build/list/detail/auth regressions phải chạy lại khi thực sự sửa C#. Không lấy việc docs không đổi source làm bằng chứng runtime UC011 đã có.
