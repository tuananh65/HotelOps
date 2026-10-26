# API / ETag contract

## GET /api/users/{userId}/profile

Management read-token endpoint cùng USER_MANAGE scope với PATCH, không phụ thuộc USER_VIEW. Không đổi GET list/detail. Positive Int64; authorized existing/nondeleted ACTIVE hoặc INACTIVE target→200,no-store:
```json
{"id":123,"fullName":"Example","email":"example@example.invalid","phone":null}
```
Header ETag là strong quoted16uppercasehex chars biểu diễn8bytes SQL rowversion. Không rowversion/credential/roles/grants trong body. Missing/deleted→404 sau global authorization; scoped target không chứng minh visibility→403 chung như target không có shared membership, không leak existence.

## PATCH /api/users/{userId}

Content-Type application/json. Header If-Match lấy nguyên strong ETag vừa đọc. Body object chỉ các keys case-sensitive fullName/email/phone;ít nhất1 key. Không JSON Patch library:
```json
{"fullName":"New Name","phone":null}
```
Omitted giữ nguyên;phone:null clear;fullName/email:null invalid;trim strings,phone blank→null. fullName required nếu present,max200;email valid,max150;phone string/null,max20,không parse numeric. Unknown/duplicate keys/type invalid400,không mass assignment. Service normalize/validate lại DTO.

Results:
-200 snapshot4fields+ETag mới sau commit;no-op200 ETag giữ nguyên,không audit/update.
-400 invalid ID/type/body/fields,invalid CSRF theo middleware hiện có,noncanonical/weak/wildcard/multiple/malformed If-Match.
-401 anonymous/invalid/expired/revoked session.
-403 không management scope/restricted;client actor/branch fields không tạo permission.
-404 global-authorized missing/deleted.
-409 duplicate email toàn users,kể cảinactive/deleted;generic title,no other account/provider details.
-412 stale If-Match (HTTP precondition conflict),khác409 email collision.
-428 missing If-Match. Không dùng body expectedVersion hoặc CAS.
-Other failures generic existing middleware500/503;không retry ambiguous commit.

Email error translation: catch2601/2627 chỉ quanh users profile UPDATE,rollback trước,confirm collision bằng SQL equality query sau rollback. Không dựa scaffold index name vì fresh schema tự đặt tên;không catch audit failures như email duplicate.

## Audit / session

UPDATE3 fields và UpdatedAt;AuditLog users targetId,real actorId,UTC,UPDATE,sorted changed fields và before/after actual delta. Cùng transaction;no password/hash/token/session data;no success trướccommit. Audit failure→rollback cả profile/token/audit.

Không revoke/update credential/status/roles/scope. Existing session activity touch vẫn chạy. No-op/stale results xác nhận snapshot tại lúc đọc/check;không hứa resource/version bất biến sau response.

If-Match equality được thi hành bởi atomic WHERE row_version=@expected trên UPDATE và affected-row check,không chỉ check rồi SaveChanges snapshot cũ. Token thay đổi do bất kỳ users UPDATE,bao gồm auth activity trên User. Rollback/schema rollback invalidates/retires token context;client phải re-read sau schema operation.
