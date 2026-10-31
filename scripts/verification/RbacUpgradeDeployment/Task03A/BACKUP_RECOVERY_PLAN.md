# Backup & Recovery Plan — Task03A/03B boundary

## Đã thực hiện an toàn trong Task03A

- Source HotelOpsDB (DB_ID22) chỉ được SELECT/IF-THROW read preflight và một BACKUP COPY_ONLY CHECKSUM mới. Không main DML, DDL, source Dry Run, RESTORE, SINGLE_USER, detach hoặc COMMIT Upgrade.
- Backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\RBAC03A_20261021_24e14e196c6a.bak`.
- msdb start/finish server-local:2026-10-21 19:49:54.000; 17,711,104 bytes theo backup_size/compressed_backup_size. File length không đọc được do ACL; không thay ACL. Backup nằm ngoài Git.
- VERIFYONLY CHECKSUM PASS; RESTORE thực tế PASS vào `HotelOps_RBAC03A_Rehearsal_20261021_a236462a264f` (DB_ID28), physical `.mdf`/`_log.ldf` riêng, không WITH REPLACE.
- Clone trước upgrade khớp72 table hashes và columns/FK/CHECK/default/index metadata của nguồn; clone sau rehearsal integrity PASS. Không tự drop clone hoặc overwrite backup.
- Chỉ có một backup mới cho Task03A. Lần resume không backup lại: msdb/VERIFYONLY được xác minh lại rồi mới restore target chưa tồn tại.

Xem `backup-evidence.json`, `backup-rehearsal-state.json`, `restore-proof.json`, `recovery-preflight-results.txt`. SQL backup history/restore history trong msdb có thay đổi dự kiến; 72 application tables của nguồn không đổi.

## Recovery model thực tế và giới hạn

SELECT lại xác nhận HotelOpsDB đang **SIMPLE**, snapshot isolation OFF, RCSI OFF. Lịch sử backup được kiểm tra không thể hiện log backup chain đủ dùng; với recovery model hiện tại **không được hứa point-in-time/tail-log recovery cho thay đổi sau backup**. Không đổi recovery model ở Task03A.

VERIFYONLY không bằng restore test; actual restore đã được làm để bổ sung bằng chứng. Actual restore test không phải diễn tập cutover application, không chứng nhận mất dữ liệu sau backup sẽ tự phục hồi được.

## Điều kiện trước khi duyệt/chạy Task03B

1. DEV1 phê duyệt exact main candidate SHA256 và expected changeset, gồm6 migration audit records. Không dùng SQL trong `initial-rehearsal-evidence/`.
2. Chọn maintenance window để dừng mọi writer nguồn, gồm login/logout/change-password/reset/temp-credential, SQL session touch (kể cả authenticated GET), role/personnel updates và background jobs. Chỉ tạm dừng app traffic/writers khi có phê duyệt; không cần/không tự dùng SINGLE_USER.
3. Revalidate identity22/createStamp, source fingerprints/schema, personnel scope và backup availability. Main script strict baseline; bất kỳ row thay đổi từ Task03A đều phải stop/reassess. Không cập nhật hash để ép script chạy.
4. Nếu đã có changes sau backup hoặc backup cũ không đạt recovery objective: tạo backup checkpoint mới được duyệt, kiểm chứng/rehearsal phù hợp và refresh approved package. Không mặc định backup Task03A bảo vệ những writes chưa có trong nó.
5. Ghi nhận before snapshots/SQL audit existing count và digests; bảo vệ users/password/session fields, departments và business tables. Receipt metadata nằm trong Git được; dữ liệu backup/full sensitive preimages phải ngoài repo.

## Những trường hợp phải dừng

- Chưa có phê duyệt Task03B/session gate; wrong server/database identity; fingerprint/schema/file digest drift.
- Personnel selections không còn phù hợp, aliases old/new cùng tồn tại, last role state khác baseline, data_scope mất hoặc khác type/nullability.
- Backup/VERIFYONLY/actual recovery evidence không còn phù hợp; không giữ được writer quiescence; SQL lock timeout/deadlock/permission error.
- Catalog tuple/metadata, fixed personnel, audit scope, protected data hoặc constraints không đạt postconditions.
- Department retirement warnings được đánh dấu là migration riêng, **không** sửa/xóa fixture để “làm xanh” Task03B.

## Rollback trong transaction

Release02 TRY/CATCH/XACT_ABORT/ROLLBACK giữ nguyên; các conflict resolutions và6 audit inserts nằm trong cùng transaction. Audit trước rollback và identity increments có thể để lại identity gaps nhưng không được reseed để che. Với target này data_scope đã có; strengthened guard chặn schema thiếu cột để không chạy additive DDL ngoài main transaction.

Nếu lỗi trước COMMIT, kiểm tra transaction đã rollback và xác minh source against before snapshot, không chạy tiếp batch để bypass guard. Identity/approval/drift checks lặp ở cả4 GO batch nhằm ngăn trường hợp client tiếp tục sau lỗi. Không dùng source Dry Run trong Task03A; rehearsal rollback chỉ diễn ra trên clone.

## Recovery sau COMMIT (chỉ là kế hoạch, chưa được thực thi)

Không có automatic down-migration. Role aliases/grants/personnel thay đổi có thể được API khác sử dụng sau COMMIT, nên không ghi đè bằng snapshot cũ thiếu review.

1. Giữ/dừng writers theo phê duyệt, ghi current state và các writes phát sinh sau backup/COMMIT; giữ audit mới. Không tự reconnect backend sang clone.
2. Chọn reviewed compensating changeset theo exact before-images và thực trạng hoặc restore checkpoint vào **DB tên mới** với files riêng để phân tích/reconcile.
3. Với SIMPLE, writes sau backup phải được bảo vệ bằng checkpoint mới/archive/explicit reconcile; không tuyên bố tail-log có thể cứu tự động. Không restore backup cũ lên main để làm mất các writes đó.
4. Actual restore vào DB mới có thể xác minh credential/user/session history trước cutover; không đồng nghĩa được phép cutover, rename DB, đổi connection string hoặc restore đè main. Những thao tác đó cần kế hoạch và approval riêng. **Không WITH REPLACE trên HotelOpsDB.**
5. Authentication verification sau recovery: original password hashes/temp/lockout/account state, session history và revoked state được giữ đúng security policy; không tự reset password/unrevoke sessions để “đăng nhập được”. User/group scope được verify lại, không chỉ counts.
6. Catalog/assignment metadata và business data đối chiếu baseline đúng giai đoạn; DBCC CHECKDB/CHECKCONSTRAINTS, schema/FK/index checks. Chỉ reopen writers sau review và smoke test được phép.

Task03A kết thúc ở backup/rehearsal và tài liệu này. Không tự restore main, không cutover hoặc dừng dịch vụ hiện tại.
