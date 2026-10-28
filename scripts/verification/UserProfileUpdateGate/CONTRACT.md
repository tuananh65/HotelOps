# Exact candidate contract — chưa approved

## Gate results

| Decision | Classification | Evidence / outcome |
|---|---|---|
| PATCH /api/users/{userId} | CONFIRMED BY SRS/SOURCE cho task explicit route, không phải permission approval | Task25 chọn PATCH; UC005 có profile update |
| Editable fullName,email,phone only | CONFIRMED BY SRS/SOURCE | UC005 flow3; roles/status ở UC010/006. Không employeeCode/username/avatar/manager/department/branch/password/status/role fields |
| USER_MANAGE code tồn tại | CONFIRMED BY SRS/SOURCE | Seed03 user/manage; SYS_ADMIN được grant ALL nhưng không bypass |
| Global unbound ALL management scope, crossbranch/self/inactive-target | PROPOSED — NEEDS APPROVAL; implementation BLOCKED | Task22 chỉ USER_VIEW detail; không được suy diễn scope quyền ghi |
| Loaded version conflict detection | CONFIRMED BY SRS/SOURCE | GBR005 row_version; model User chưa có concurrency token |
| CAS original-values thay rowversion và chấp nhận ABA | PROPOSED — NEEDS APPROVAL; implementation BLOCKED | Task24 D2 chưa approval, không đánh dấu đề xuất là approved |
| Email unique toàn hệ thống | CONFIRMED BY SRS/SOURCE | GBR025; unique users.Email trong SQL/EF |
|400/409 error details, PATCH/no-op/normalization | PROPOSED — NEEDS APPROVAL | Cần chốt application semantics bên dưới |
| Audit actor/time/entity/action/before-after cùng transaction, no secrets | CONFIRMED BY SRS/SOURCE |007/008; AuditLog fields/action UPDATE/JSON constraints có sẵn |
| Exact audit whitelist/no-op behavior | PROPOSED — NEEDS APPROVAL | Không serialise full entity; source chưa user-profile workflow |
| Không revoke sessions cho fullName/email/phone | PROPOSED — NEEDS APPROVAL | SRS không yêu cầu revoke profile; email không tự thay Username/password; reset/status ngoài scope |

## Request candidate — CAS branch, Content-Type application/json

Không JSON Patch operations hay JSON Merge Patch library; typed partial object, property presence được giữ:
```json
{
  "expectedProfile": { "fullName": "Original Name", "email": "old@example.invalid", "phone": null },
  "fullName": "New Name",
  "phone": null
}
```

- userId positive Int64; zero/negative/overflow/nonnumeric400 sau authorization.
- expectedProfile bắt buộc đủ3 properties (phone có thể null). Đây là client baseline, **không proof đã đọc/không authorization evidence**.
- fullName/email/phone top-level: ít nhất1 property. Omitted = giữ nguyên. phone:null = clear; fullName/email:null hoặc whitespace-only400.
- Unknown hoặc duplicate JSON properties400 ở cả top-level và expectedProfile; case-sensitive camelCase canonical. Tránh parser last-value-wins/overposting.
- fullName trim,max200;email trim,max150 và valid format;phone optional trim,max20,empty thành null là proposal. Không parse phone numeric, không tự đổi case email/username.
- expectedProfile giữ chính xác values read từ GET detail (không trim/lower baseline). Compare Ordinal/null-aware để phát hiện khác biệt case/space, không dùng SQL CI collation để xác nhận snapshot.
- Chỉ proposed profile CAS dùng data GET detail hiện có, không thay Task22 DTO. Caller USER_MANAGE không có USER_VIEW cần GET-authority riêng để lấy baseline; không tự cấp USER_VIEW hoặc bắt writer phải có USER_VIEW. Client không bị buộc phải có read permission bởi write gate, nhưng không có baseline thì400; vấn đề UX/read-authority phải được chốt nếu muốn hỗ trợ manage-only actor.

## Proposed management scope

Dùng Global:USER_MANAGE qua infrastructure, chỉ effective ALL trên assignment không branch-bound. Không SYS_ADMIN/OWNER role bypass. BRANCH/OWN/branchboundALL failclosed. Cho authorized global manager cập nhật target nondeleted ở branch khác, targetACTIVE/INACTIVE và self-profile (không self-status/role). Tất cả các visibility choices này cần approval, không suy từ tên ALL hay authority của detail.

Current UsersController có class Global:USER_VIEW; thêm method USER_MANAGE vào đó sẽ thành **AND hai policies**. Đề xuất controller mutation riêng cùng routeprefix để không vô tình đòi USER_VIEW và không sửa read gates. Actor dùng CurrentUserAccessor và SQL validated session; client actor/branch không phải proof. Transaction cần recheck actor permission; không hứa instantaneous revocation cả in-flight requests chỉ vì recheck gần commit.

## HTTP outcomes / conflict precedence

| HTTP | Candidate code | Semantics |
|---|---|---|
|200 | — | {id,fullName,email,phone}; no-store; committed hoặc no-op đúng contract |
|400 | PROFILE_VALIDATION_FAILED | Invalid body/presence/type/field/ID/missing expectedProfile; CSRF invalid vẫn dùng existing CSRF_VALIDATION_FAILED |
|401 | Existing auth convention | Anonymous/invalid/expired/revoked session; không lookup target |
|403 | Existing auth convention | Không management permission hoặc restricted session; target có/không đều không lộ existence |
|404 | USER_NOT_FOUND | Sau gate, missing/softdeleted; không để distinguish qua email precheck |
|409 | PROFILE_CHANGED | Current profile khác expectedProfile dưới lock, yêu cầu reload; kiểm stale trước duplicate |
|409 | EMAIL_IN_USE | Email đã được dùng trong toàn users, kể cả inactive/softdeleted vì SQL unique không filtered; không trả ownerID/email/request SQL |
|409 | PROFILE_STATE_CHANGED | Target bị delete/status thay đổi theo conditional current-state check trong transaction, không credential disclosure |

No-op: baseline phải match trước; patch sau normalization không đổi3 fields →200 snapshot, không audit/UpdatedAt write (proposal). Không return mutation success trước commit. Không dùng ETag header nên không pretend If-Match semantics412/428. Generic500/503 vẫn theo existing error infrastructure khi lỗi ngoài domain; không map mọi SqlException thành409.

Email duplicate precheck giúp UX nhưng DB uniqueness là authority. Chi tiết translation có trong diff proposal.

## Session / audit

Profile fields không dùng để cấp role/scope hoặc verify password. Đề xuất không revoke/reset sessions/FailedLoginCount/LockedUntil/temp-expiry; không đổi Username. Email update không tự trở thành credential reset. No-store và không log raw request/body personnel. Audit3-field whitelist theo old/new actual delta, transaction actor/time từ server.
