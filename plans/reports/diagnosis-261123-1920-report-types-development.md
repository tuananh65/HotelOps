# Kiểm tra loại báo cáo và Development

## Kết quả

- Production `https://127.0.0.1:5080/` trả HTTP200. API5769 trả health200, schema2.1.0, 40 bảng.
- Phòng416 là phòng104 thuộc chi nhánh413, Bùi Viện, trong HotelOpsCompactDB.
- Không có bản ghi WORK_CATEGORY với code ROOM_ISSUE_* trên Production. Audit RoomIssueType cũng có 0 bản ghi. Vì vậy API RoomIssueTypes không có loại nào để trả cho Giám sát; không phải lỗi lọc frontend.
- Hai hàng Hỏng đồ vật/Thiếu đồ vật trong Cấu hình nội bộ là các lựa chọn chưa lưu, mặc định tắt. Trưởng bộ phận chọn đúng chi nhánh, vào Cấu hình nội bộ → Báo cáo phòng và bật công tắc sẽ tạo/lưu loại và cho phép Giám sát chọn.
- Development của workspace TestFE/HotelOps chưa chạy: cổng5186 không lắng nghe; API hiện tại của workspace chỉ có bản Production5769.
- HTTPS localhost5173 đang chạy nhưng thuộc dự án cũ `D:/SEP490/Projects/Demo/HotelOps_Web/frontend/src/main.tsx`, xác minh bằng module do Vite trả về. Entry dùng CacaoShell/AuthRouteHost, khác entry compact/app trong workspace hiện tại. API cũ7019/7443 cũng chạy DLL từ Demo/HotelOps_Web.

## Kiểm chứng và thay đổi

Chỉ đọc SQL, tiến trình/cổng, HTTP và code. Không bật loại thay Trưởng bộ phận, không đổi database, không sửa code hay dừng/khởi chạy ứng dụng. Build/Test: Not Run vì đây là lượt chẩn đoán không thay mã nguồn.

Truy vấn audit đầu dùng nhầm entity_id và thất bại trước khi trả dữ liệu; đã kiểm tra entity AuditLog (EntityKey) và chạy lại truy vấn chỉ đếm bản ghi thành công. Không có dữ liệu bị ghi.
