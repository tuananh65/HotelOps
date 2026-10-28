# Identity provisioning

## Có sẵn

User là account+employee record hiện tại, không có entity Employee riêng. SQL users.user_id identity; EmployeeCode unique/required max40, Username100 unique, Email150 unique, FullName200, Phone20 optional. SRS form không cho chính sách cấp EmployeeCode. Không tự sinh mã từ MAX+1 hoặc dùng ID4/19.

PasswordCredentialService dùng ASP.NET Identity V3; baseline100000 iterations, giới hạn maximum1000000 và stored hash255; verify bảo toàn SuccessRehashNeeded, không tự write. Không tự dùng hash string từ client.

TemporaryCredentialService.Prepare(userId>0) sinh24 ký tự crypto-random từ alphabet64, có uppercase/lowercase/digit/-_; hash trong memory, prepared consume/dispose một lần. Validity24h; plaintext không persistent. Consume sau rollback không reuse. Managed strings không bảo đảm zeroization, phải giảm lifetime/log exposure.

ApplyAsync đặt TemporaryPasswordExpiresAt, reset FailedLoginCount/LockedUntil, revoke all sessions và audit metadata không secret, rồi commit mới phát one-time IssuedTemporaryCredential. Primitive không là public admin workflow, không kiểm permission/last-admin/identity verification.

## Minimal UC004 proposed sequence — cần approval participant

1. Authorize operation/role/scope quyền đã duyệt; validate required info/master data, employee code source và username/email uniqueness.
2. Begin dedicated SQL transaction; revalidate actor quyền/master/status với approved locking protocol.
3. Insert User/SaveChanges trong transaction để lấy identity, **chưa commit**.
4. Prepare(actualUserId); current interface cần ID thật. Hash lúc này kéo dài transaction nhưng không cần giả identity hoặc đổi hasher.
5. Insert UserBranchAccess/UserRole và flush/detach workflow-owned entries đúng participant guards; chỉ mappings đã được duyệt.
6. Participant apply credential/revokes/audit trong cùng transaction; add account/scope/role before-after audit cùng transaction.
7. Confirm commit rồi handoff một lần/no-store. Không trả plaintext trước commit; delivery failure dùng explicit authorized recovery/reset.

Optimization hash trước transaction cần redesign preparation identity binding, không giả userId hoặc mặc định custom hasher bỏ qua identity; defer cho tới có nhu cầu và approval. DB uniqueness constraints là backstop chống concurrent duplicates, không chỉ AnyAsync precheck. Case/collation/Unicode normalization cần contract; không tự lower mọi input.

## Reset UC005

SRS yêu cầu admin xác minh người dùng, confirm reset, temp24h/force-change và revoke all sessions/audit same transaction. Existing primitive đã xử lý writes này, nhưng authorize target, identity-verification procedure, stale-client/current-state checks và one-time HTTP handoff chưa có.

Lựa chọn giữ ApplyAsync nguyên và wrapper authorize trước: ít diff, nhưng stale check và permission recheck bên ngoài transaction có TOCTOU. Đề xuất dùng participant cùng admin orchestrator sau approval; giữ old ApplyAsync behavior cho callers hiện có. Không cấp reset route chỉ vì primitive đã có.

## Các giới hạn phải giữ

- Không gán default role/admin khi tạo user; selected roles trong UC004 cần authority riêng, không bypass ROLE_MANAGE bằng USER_MANAGE.
- Account ACTIVE ban đầu theo UC004; EmploymentStatus defaultACTIVE là schema behavior, không tự đồng nhất employment lifecycle/account lifecycle.
- Không gửi password vào audit, URL, telemetry, fixtures receipts hoặc plaintext outbox.
- Idempotency/recovery sau network failure phải duyệt; không replay one-time password bằng cached ordinary DTO. First-admin bootstrap ngoài anonymous/public API.
- Transaction participant cần approval cross-cutting nhưng không cần schema; employee-code và multi-dept scope cần business decision. Có thể test độc lập bằng guarded trial fixtures, không production personnel.

## Audit gap hiện tại

AuthenticationAudit helper chỉ hỗ trợ LOGIN/LOGOUT/UPDATE, entity users, NewValues/ChangedFields. Nó không tự thu OldValues hoặc sanitize arbitrary object. Không truyền full User chứa PasswordHash. User create/assignment audit cần whitelist builder service-local và SQL-supported actions, không mở rộng helper chung chỉ để tiện.

SQL release01 CK_audit_logs_action cho phép CREATE,UPDATE,DELETE,APPROVE,REJECT,CANCEL,LOGIN,LOGOUT; old_values/new_values/changed_fields phải null hoặc JSON hợp lệ. Đề xuất CREATE cho account mới và UPDATE cho profile/status/scope/role-assignment changes, kèm operation metadata whitelist. Không dùng INSERT/ROLE_REVOKE là action enum mới. AuthenticationAudit hiện không nhận CREATE dù SQL cho phép; service-local builder tránh sửa helper auth chỉ để hỗ trợ account creation.
