# Implementation sequence / verification

## Recommended first mutation coding task

**UC005 profile-only update**, chưa reset, status, scope hoặc roles. Tên/email/phone theo SRS, USER_MANAGE mapping; Global-only reviewer phase/target visibility phải được phê duyệt riêng, không mượn Global USER_VIEW detail.

Trước code: duyệt D1/D2 (permission/visibility và stale-client semantics). Nếu không chấp thuận snapshot/CAS amendment thay row_version thì còn BLOCKED, không ship version giả.

Minimal candidate:
- User admin service riêng trong src/HotelOps.Api; controller HTTP concerns; no architectural project split.
- Update only fullName/email/phone; model storage limits200/150/20, uniqueness email toàn hệ thống, soft-deleted not found, unknown privileged fields reject.
- Dedicated transaction/user lock, baseline comparison + conditional update và affected=1; updatedAt và audit whitelist; no credential/status/role/scope writes.
- Actor revalidation và approved scope; existing cookie/SQL session/CSRF; no-store.
- Source-only no schema option cần approval GBR005 limitations. DI nếu cần phải proposal+approval, Task22 không cho general shared change.
- Tests SQL trial: stale form, same user concurrent edits, nullphone, duplicate email concurrent writes, actor revoked permission, audit failure rollback, cancel/commit-unknown; no hash/status/roles mutation. Snapshot ABA test phải ghi limitation hoặc reject design nếu business không chấp thuận.
- Acceptance: no lost updates theo approved contract, audit atomic, no secrets, build/regressions PASS; không claim monotonic version nếu không có.

## Trình tự tiếp

1. D1/D2 và profile baseline protocol review; implementation nhỏ theo scope trên.
2. Approval transaction participant D3 và test extract/core compatibility độc lập, không mutation endpoint mới ở bước infrastructure chưa được giao.
3. Reset UC005 sau D4 identity verification/delivery/stale check, SQL reset-vs-login/change race suite.
4. Create UC004 sau D5 employeeCode và initial role/scope delegation; one transaction account+assignments+credential+audit.
5. UC006 status sau D7 last-admin/open work; no self deactivation, lock preservation/reactivation.
6. UC007/010 sau D6 scope model/delegation/regrant/expiry, D7 shared guard/DEV2 coordination và D8 invalidation contract.
7. UC011 vẫn blocked riêng; không cần mở UC011 API để dùng internal authorization foundations.

## Verification Task24

- dotnet build HotelOps.slnx: PASS,0 warnings/0 errors,2026-10-27.
- Regression foundations không --live chạy trong Task24: PermissionService43/43,CurrentUserAccessor14/14,Authorization61/61,ResourceAuthorization49/49;tổng167 PASS,command exit0. Các HTTP suites dùng synthetic cookie/session data và production handlers, không chứng minh SQL mutation workflow.
- Transaction/concurrency evidence là source analysis, không runtime proof. Không tạo harness mô phỏng transaction bằng in-memory rồi gọi SQL PASS.
- SQL mutation/rollback/last-admin races/reset-vs-login/assignment races: Not Run. No database connections/fixtures/migrations/reset/revoke.
- UC141/Task22 SQL HTTP harness không chạy lại vì không code/source changes; kết quả Task22 là historical, không tests UC004–010.
- Final SHA-256:585/585 files có trước Task24 nguyên vẹn; chỉ thêm7 Markdown ở UserManagementMutation. git diff --check/cached check PASS; index trống. Kiểm tra riêng7 file mới không trailing whitespace hoặc dòng trống thừa EOF. Không source/schema/service edits, không stage/commit/push/merge/delete.

## Runtime test prerequisites khi được giao

Disposable trial identity guards và non-sensitive personnel fixtures, permission gates đã duyệt; không production user/session. Fault injection trước/sau từng insert/hash/update/revoke/audit/commit; confirm rollback với context mới. Hai connections cho thật SQL blocking/races, không Task.WhenAll cùng DbContext. Unknown commit recovery test riêng; no blind retry. Revoke owned fixture sessions cleanup, retain evidence không secrets, không auto drop/reseed database.
