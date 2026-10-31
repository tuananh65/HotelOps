# Preflight Results — HotelOpsDB, Task03A

Nguồn bằng chứng: `preflight-results.txt`, `deployment-state.json`, `restore-proof.json`, `rehearsal-result.json`. Tất cả đọc database chính, ngoại trừ COPY_ONLY backup được phép. Không có source DML/DDL/Dry Run.

- Exact identity: DESKTOP-JI8U50T\SQLEXPRESS / HotelOpsDB / DB_ID22 / createStamp2026-10-18 02:41:14.627 / compatibility160.
- 72 source tables khớp count/SHA-256 với Task02 original sourceBefore; không có drift phải suy đoán. Đây là Source→Source Baseline, không phải Source=Trial đã upgrade.
- Roles11, active11, permissions47, grants182. Scopes ALL27/BRANCH57/OWN53/DEPARTMENT45.
- DH selections4/B1,19/B2 vẫn Active đúng primary branch. Các extra DH:5/17/18/23 B1 và20/25 B2. User1 General Manager role branchNULL/primary1, còn MANAGER access1/2. User5/19 còn Storekeeper assignments nguồn. Không thay selections.
- users26, SQL sessions10. Chỉ IDs/states/session counts xuất ra report, không credential/hash mật khẩu/user profile đầy đủ.
- data_scope đã tồn tại varchar20 NOT NULL; CHECK đúng schema đang dùng và trusted/enabled. failed_login_count, locked_until, temporary_password_expires_at, session_key_hash tồn tại.
- Snapshot schema riêng cho columns/FK/CHECK/default/index. Restore clone khớp toàn bộ metadata hashes và table hashes, sau upgrade cũng giữ nguyên schema hashes.
- Runtime source không đọc legacy role codes/permission grants ở Authentication/CurrentUserAccessor; Auth DTO không yêu cầu department_id. RolePermission chưa DataScope mapping: note cho task authorization sau, không blocker FE-01.
- Giữ raw preflight report không nhạy cảm để DEV1 review actual user_role IDs/scope; environment-specific employee choices không đưa vào shared Release.

Fingerprints là row content theo PK, không gồm identity last_value hoặc trạng thái engine. Các phép đọc là sequential ReadCommitted, không phải distributed snapshot; nếu concurrent activity xuất hiện trước03B phải revalidate, không tự dùng lại kết luận cũ. Rehearsal/current source sau cùng đều khớp baseline đã lưu; backup msdb history không nằm trong72 application tables.
