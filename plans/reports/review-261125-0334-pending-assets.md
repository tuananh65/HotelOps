# Review hồ sơ ấm đun chờ kiểm kê — D11

Ngày 25/11/2026, Asia/Saigon. Review tĩnh; không chạy SQL, API, server hoặc test. Chỉ tạo báo cáo này.

## Kết luận

Không thấy blocker gây mất dữ liệu hoặc tự kích hoạt 200 tài sản trong đường thực thi hiện tại. Hồ sơ inactive PENDING_INSPECTION, không gán vị trí thực tế/số sê-ri/tình trạng vật lý và giữ sổ kho phù hợp D11. Đây là ánh xạ sổ sách, chưa phải hoàn tất chuyển số dư hay kiểm kê thực tế.

## Findings

1. **P2 — Nội dung giải thích bị hỏng Unicode.** `frontend/src/compact/assets.tsx:301` chứa dấu hỏi thật trong chuỗi “H? s? chuy?n…”, không chỉ lỗi terminal. Thông tin rằng hồ sơ không phải tài sản nhập thêm trở nên khó đọc. Sửa literal UTF-8. Dòng 228/299 còn ghi “Đã ngừng”/“Đã ngừng sử dụng” cho pending; nên ghi “Chờ kiểm kê thực tế” để không ngụ ý đã ngừng dùng sau kiểm tra.

2. **P2 — ALREADY_APPLIED đối soát chưa đủ.** `scripts/database/compact-v2/v2-kettle-pending-assets.sql:6` chỉ kiểm tra COUNT mã MIG222 là 200 rồi trả thành công. Chưa kiểm tra 200 hồ sơ vẫn inactive/PENDING_INSPECTION, chưa có vị trí thực tế/retired_at, và mỗi asset có đúng một ánh xạ MIGRATION_PENDING_INSPECTION cùng operation_key/source. Dữ liệu lệch nhưng số lượng giữ nguyên sẽ báo ALREADY_APPLIED. Đề nghị kiểm tra các bất biến và tổng theo chi nhánh/audit trước trả thành công. Không có bằng chứng dữ liệu hiện đã lệch.

## Các đường đã kiểm tra

- Script guard tên TEST, XACT_ABORT, transaction/rollback; không sửa stock_movements hoặc chứng từ cũ. Nguồn phải 200, kho 103, cấp phòng 97; từ chối số lượng phân số. Mã ổn định theo branch/nguồn/thứ tự; audit giữ bookWarehouseId/bookRoomId/sourceLineId và NOT_ADDITIONAL_STOCK.
- Artifact `asset-constraints.json` cho phép room/location cùng null; không có CHECK condition_code hoặc trigger assets chặn insert trong artifact đã đọc. Chưa xác minh trực tiếp database hiện tại.
- AssetService.Act chặn inactive trước move/condition/handover/retire. CatalogService assets không cho ghi IsActive và cấm sửa RoomId/LocationId/ConditionCode của tài sản tồn tại. Không thấy đường kích hoạt pending qua API hiện tại.
- WorkService kiểm tra tài sản, TemplateService, lựa chọn tài sản thay thế và room issue objects đều yêu cầu active. Pending không trở thành thiết bị vận hành qua các đường này.
- Dashboard InsightService/InsightService.Owner chỉ đếm tài sản active. Assets truyền vào AssetConditionBar sau lọc active. Danh mục/báo cáo vẫn liệt kê pending kèm condition/IsActive; không tìm thấy phép cộng pending vào tổng sở hữu kho.
- StockBatchAccounting khả dụng chỉ nhận CONSUMABLE/REUSABLE và posting chặn ASSET; số dư legacy ấm đun không trở thành tồn tiêu hao khả dụng.
- Kiểm kê thật và chuyển số dư sau này cần giao dịch đối soát riêng; script chưa triển khai bước đó.

## Kiểm chứng

Review hoàn thành trong phạm vi được giao. Build/test/API/DB execution: **NOT RUN** theo phạm vi; không tuyên bố migration PASS. `node --check` chương trình tạo báo cáo thành công, không phải kiểm chứng SQL/C#/TS. Không có câu hỏi nghiệp vụ D11 còn treo.
