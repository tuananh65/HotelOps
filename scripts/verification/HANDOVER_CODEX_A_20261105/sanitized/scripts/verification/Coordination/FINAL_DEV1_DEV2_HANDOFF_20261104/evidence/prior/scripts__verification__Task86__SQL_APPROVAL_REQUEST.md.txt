# Task86 — phạm vi quyền SQL riêng đã được duyệt

Approval thực tế: SQL_APPROVAL.json. Trial đã tạo thành công, không reset; xem sql-prepare-approved.log. Phần dưới giữ exact scope để review. Fixture bổ sung có approval riêng tại SQL_RESUME_APPROVAL.json.

Chỉ một database mới: `HotelOps_Canonical_20261104_223000_c01286a1_Fresh` trên `REDACTED_LOCAL_SQL_HOST\SQLEXPRESS`. Chưa kết nối instance, chưa xác minh database có tồn tại. Khi được duyệt, bắt buộc kiểm tra @@SERVERNAME và nonexistence trước CREATE; trùng tên thì dừng, không reset/xóa/đổi tên tự động.

## Gói có thể review

- `Prepare-Task86Trial.ps1.proposal`: gọi Prepare-Database hiện hữu, kiểm hash prerequisite; source template chỉ thay tên database trong memory. Chỉ dùng master để đọc instance/nonexistence và CREATE đúng trial; không sửa cấu hình server. Các batch USE sau đó chỉ tới trial mới.
- `SQL_SCOPE_REVIEW.proposal.json`: 12 prerequisite script khớp SHA256 bản Task85 đã review; thêm script Branch rowversion đã được **duyệt thiết kế**.
- `branch-rowversion-forward.sql.proposal`: duy nhất thêm dbo.branches.row_version ROWVERSION NOT NULL. Guard exact name + marker, kiểm cột rowversion khác/kiểu không tương thích. Prepare kiểm schema legacy, CI collation và dữ liệu canonical, thử rollback DDL trong outer transaction, sau đó chạy forward hai lần để kiểm idempotency. Không sửa EF entities/mappings. Nếu precondition không đạt: dừng.
- `HttpsProgram.cs` / `Task86.HttpsVerification.csproj`: fixtures và acceptance qua production Program build từ current source, localhost HTTPS pin certificate, credential login + SQL sessions + CSRF thật. Không tạo synthetic identity để vượt authorization.
- `AuditFailureConstraint.sql.proposal`: CHECK constraint chỉ trên audit_logs trong trial mới, chỉ từ chối hai marker Task86 để tạo SQL audit failure thật. Harness gỡ đúng constraint do nó vừa tạo trong finally. Không sửa audit production hoặc mapping.

## Phạm vi ghi fixtures và API

Chín user t86.*, chín UserRole từ catalog hiện hữu (SYS_ADMIN / RECEPTION), một UserBranchAccess branch-bound; không thêm/sửa permissions, roles, role_permissions. Một floor, một legacy room type, một room, một cleaning task PENDING để regression. Auth tạo SQL sessions/audit; test revoke session/role assignment, expired session, inactive account/employment chỉ trên các users t86.* vừa tạo.

Create/Update các branches T86_* qua HTTPS; concurrency duplicate và stale update; no-op; audit failure/no partial commit; fail-closed deactivate không chuyển branch Active→Inactive. Existing canonical BR001/BR002 trong **trial mới** được đọc và dùng fixture scope; không truy cập canonical/shared DB bên ngoài.

Harness chưa bao phủ successful deactivate / deactivate-create race / full stock-task-request fixture matrix / revoke during observed lock wait. Các case đó vẫn NOT RUN hoặc BLOCKED; không dùng số checks còn lại để công nhận UC012 complete.

## Điều kiện giữ an toàn

Không kết nối HotelOpsDB, không truy cập trial Task82/84/85, không sửa database tồn tại trước lần tạo Task86. Giữ lại trial mới sau test và giữ dữ liệu khi failure để điều tra. Nếu preparation hoặc fixtures thất bại, không tự reset/rerun ghi đè; báo điểm dừng. Không commit/push/merge.

Approval A đã áp dụng một dòng DI. B được duyệt thiết kế trước; sau đó user duyệt quyền SQL riêng cho đúng gói này theo Task86 §G2. Không suy rộng sang shared/protected DB.
