# Session, security và audit

## Khi nào revoke?

| Mutation | SRS rule | Source đã có / gap | Đề xuất |
|---|---|---|---|
| Create UC004 | Temporary first-login restricted | Primitive initial provisioning revoke-all và temp expiry; chưa full create orchestration | Reuse participant sau approval; no cookie auto-login |
| Reset UC005 | Revoke tất cả sessions, temp/force-change | ApplyAsync đã revoke trong transaction; caller auth/stale verification chưa có | Keep exact behavior, one-time handoff sau commit |
| Deactivate UC006 | Revoke all sessions; preserve history | SessionRevocationService composable, status workflow chưa có | Status+revoke+audit atomic; no session real writes Task24 |
| Reactivate UC006 | LockedUntil giữ tới expiry | No workflow | Không restore revoked sessions; không clear lock vì activate |
| Profile UC005 | Không bắt buộc logout-all trong SRS | No mutation | Đề xuất không revoke chỉ vì tên/email/phone đổi, cần approve scope |
| Scope UC007 / roles UC010 | Effective access thay đổi next request | PermissionService đọc grant mới mỗi evaluate, không permission cache | Revoke-all bổ sung cho security-sensitive removal là lựa chọn cần duyệt, không giả SRS bắt buộc logout mọi grant |
| RolePermission UC009 | Next request toàn assignees | Deferred ngoài task | Không mở role-catalog management |

SessionRevocationService tham gia caller transaction/context, user lock trước session update, UTC và clean/no-tracked User/Session guard. Không tự commit; audit của business operation do caller chịu trách nhiệm. Rollback transaction rollback revoke timestamps. SessionTouch/Validate vẫn recheck SQL; không lưu authorization cố định trong cookie.

In-flight operation đã qua gate không tự rollback khi revoke xảy ra sau đó. Cần mutation reauthorization và coordination; revoke-all không thay invariant locking. Restricted temporary session không được admin business operations.

## Security rules matrix

| Vấn đề | SRS/explicit rule | Implemented hiện tại | Gap / cần duyệt |
|---|---|---|---|
| Last-admin |026 giữ ít nhất1 active admin có permission administration | Chưa có shared mutation guard | Define effective protected set/locked/temp/global/expiry và concurrent removals |
| Self-deactivate |027 cấm | Không có deactivate endpoint | Server actor target compare trong workflow, không userId body |
| Self-revoke admin |027 cấm | Không có revoke endpoint | Define protected role/capability và multiple assignments; không chặn mọi self-profile edit theo suy đoán |
| Escalation |002 scope/permission,030 không bypass SoD; user task explicit | PermissionService no role-name bypass | Delegation ceiling/self-grant/crossbranch/global assignment policy chưa chốt; “chỉ grant quyền mình có” là option, chưa SRS rule |
| Open work |028 gồm tasks/cleaning/rework/incidents/internal requests;010 removed executor role guard | Không unified user-admin dependency guard | Chốt authoritative status/assignment/handover và lock protocol; DEV2 phối hợp, không sửa họ |
| Audit |007/008 same transaction; actor/time UTC/entity/action/before-after; no credential | Auth workflows có metadata audit | Chưa general user-admin before/after builder; helper không auto sanitize |
| CSRF/actor | Existing infrastructure + task explicit | ApiCsrfMiddleware unsafe /api; CurrentUserAccessor từ validated cookie+SQL snapshot | Reuse, không client ActorUserId/role/branch proof; gate+transaction recheck |
| Deactivation reason |012 >=10 chars;006 | No endpoint | Validate/trim approved reason; activation/revoke reason không tự áp012 |

Last-admin role code là business invariant cần định danh được duyệt, không được hardcode numeric RoleId hoặc dùng role name làm bypass authorization. Count admin dưới ReadCommitted ngoài lock có thể cho hai admin đồng thời xóa lẫn nhau. Giới hạn role hết hạn còn cần quyết định continuous vs point-in-time invariant.

## Audit transaction order và sensitive protection

Capture whitelist before/after từ locked server state; chỉ field được mutation cho phép. Execute mutation/revoke rồi add audit và SaveChanges/commit cùng transaction; nếu revocation guard cần tracker sạch thì không add pending audit trước revoke. Audit actor lỗi FK phải rollback; không log full request/entity.

Không password/plaintext/hash/token/cookie/session-key hash trong audit hoặc response thông thường. Chỉ create/reset one-time plaintext sau confirmed commit theo approved delivery. Error409/business conflicts không leak work details ngoài actor scope. SQL availability không convert thành account missing/allow.

DEV2 Audit/reporting ownership không có nghĩa DEV1 được sửa reporting modules. Service-local AuditLog records theo existing SQL contract có thể cần phối hợp format, không thay audit schema.
