# Kiểm kê dữ liệu test trong HotelOpsCompactDB

Kiểm tra chỉ đọc SQL thực tế ngày 25/11/2026, Asia/Saigon. Không thay đổi database, tài khoản hay source nghiệp vụ.

## Bằng chứng chắc chắn

- `scripts/database/compact-v2/room-supplies.sql` tạo fixture được duyệt 24/11, lý do ghi DỮ LIỆU KIỂM THỬ, không phải nhận hàng thực tế.
- SQL chính thức hiện có **194** audit `InventoryDocument / AUTHORIZED_ROOM_TEST_DATA`. Audit JSON chứa `authorization=user-approved-20261124-room-supplies`, `executor=maintenance-script`, `synthetic=true`.
- Các chứng từ tương ứng có ID **246–439**: hai RECEIPT 246/247; 96 MATERIAL_REQUEST 248–438 số chẵn; 96 ISSUE 249–439 số lẻ. Phải chọn theo audit + prefix + reason, không chỉ theo dải ID.
- Prefix: `TEST-ROOM-261124-IN-DVBV`, `TEST-ROOM-261124-IN-DVCT`, `TEST-ROOM-261124-MR-<room_id>`, `TEST-ROOM-261124-OUT-<room_id>`.
- 96 phiếu xuất có liên kết phiếu yêu cầu và source line do fixture tạo. Truy vấn source_document/source_line không thấy chứng từ ngoài nhóm tham chiếu trực tiếp nhóm này.

## Xung đột cần giải quyết trước xóa

**Không thể xóa/đảo toàn bộ 194 chứng từ rồi giữ nguyên mọi giao dịch sau đó mà vẫn không âm tồn.**

- Chứng từ ngoài fixture: MR **440**, số `5ac4e9973fbf4548a8adabd14a4b938f`, lý do Yêu kho bổ sung ấm đun nước; ISSUE **441**, số `bdb02db1f2fd4646b38b0a12c2b7f375`, lý do Cấp phát ấm đun. Mỗi phiếu 1 chiếc item222, phòng103.
- Nếu loại tất cả stock movements của 194 chứng từ test, **kho413 / item222 còn -1**. Không có receipt ngoài fixture cho chiếc ấm này. Đây là phụ thuộc tồn kho dù không có FK trực tiếp.
- Không có bằng chứng chắc chắn 440/441 là script test. Giữ nguyên theo yêu cầu Owner; cần quyết định nghiệp vụ về nguồn gốc chiếc ấm trước khi loại nguồn test. Không tự tạo receipt hoặc tự xóa 440/441.
- Đồ bền khác item223/224/225 còn 0 sau loại fixture. Các fill còn số dư từ nguồn khác; không đồng nghĩa mọi nguồn khác là test.

## Không xóa theo prefix danh mục

- `P0-FILL-*` là metadata bootstrap (`p0-supply-defaults.sql`), script nói rõ không tạo stock movements. Owner yêu cầu giữ danh mục; không xóa các item/unit/configuration này.
- `TEST-DURABLE-*` 222–225 là danh mục đồ bền tạo cùng fixture nhưng đã được sử dụng ngoài fixture (ít nhất kettle222). Giữ danh mục theo phạm vi hiện tại.
- Phiếu **243/244** `WAREHOUSE-PLUS100-20261124-DVBV/DVCT` có audit `AUTHORIZED_STOCK_TOPUP`, lý do bổ sung theo yêu cầu Owner, không có marker synthetic/test như room fixture. Chưa đủ bằng chứng để xóa.
- Phiếu **245** SUPPLY_ISSUE thuộc workday33/staff427 và các chứng từ tương tác thực tế sau đó không tự phân loại là test.
- Audit Seed/DEMO_IDENTITIES_CREATED, tài khoản DEMO, mã DEMO-ITEM không cho phép xóa tài khoản/danh mục khi Owner đã yêu cầu giữ.

## Ràng buộc và phương án

- Database có trigger append-only: stock_movements, document_events, audit_logs; posted inventory_documents/lines cũng immutable. Xóa trực tiếp sẽ bị từ chối. Không tắt trigger để né ràng buộc theo mặc định.
- FK cần xét: request_workflows.document_id, request_workflow_lines.document_line_id, request_workflow_events.related_document_id, inventory_documents.source_document_id, inventory_document_lines.source_line_id, stock_movements.line_id/reversal_of_movement_id, document_events, attachments, notifications, supply_stock_documents, staff_supply_ledger.document_line_id.
- Phương án giữ chứng từ thực tế: backup có CHECKSUM/VERIFYONLY, rehearsal clone, manifest ID chứng minh test, dùng linked reversals để vô hiệu hóa tác động test và lưu dấu nguồn gốc; việc ẩn/loại khỏi màn vận hành phải phân biệt lịch sử audit. Chỉ áp dụng nhóm không gây âm tồn hoặc phá liên kết. Không gọi reversal là đã xóa vật lý.
- Nếu Owner yêu cầu xóa vật lý thì cần quy trình ngoại lệ có manifest, archive, transaction, kiểm chứng toàn vẹn và xử lý riêng xung đột kettle; không restore toàn bộ backup cũ vì mất các giao dịch mới.
- **Không chạy `v2-kettle-pending-assets.sql` nguyên bản sau cleanup**: script yêu cầu đúng 200=103 kho+97 cấp phòng, ghi200 asset `MIG222-*`. Sau dọn fixture số nguồn thay đổi; tự tạo200 sẽ đưa test trở lại. Migration foundation cũng cần dựa vào số dư đã dọn, không copy dữ liệu từ clone V2 bị nhiễm test.

## Giới hạn

Đã xác minh nhóm194, số dư sau loại nhóm, chứng từ phụ thuộc kettle, audit và FK/trigger bằng SQL thực. Chưa chứng minh các công việc/phân ca thủ công là test; giữ nguyên. Không build/test ứng dụng vì không thay đổi code. Không có JS mới nên node --check không áp dụng.
