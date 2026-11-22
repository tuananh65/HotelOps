# Phân trang và thu gọn bảng phòng

Sửa `frontend/src/compact/rooms.tsx`, `frontend/src/compact/task-ux.css`, cập nhật `frontend/tests/compact-reception-rooms.mjs`.

Mỗi trang tối đa 15 phòng, desktop hiển thị 5 cột × 3 hàng. Thẻ giảm padding, kích thước số phòng và khoảng cách. Tablet/mobile giảm số cột để vẫn đọc được; giữ 15 phòng/trang. Bỏ nút Làm mới, nội dung cách navbar 12px; giữ nút thử lại khi truy vấn lỗi và tự tải lại sau thao tác. Tìm kiếm/đổi bộ lọc/xóa lọc về trang 1, số trang luôn nằm trong phạm vi kết quả. Chi tiết và danh sách phòng chuyển vẫn truy cập dữ liệu toàn bộ chi nhánh.

Build frontend **Passed**, 1620 modules, không cảnh báo. Prettier **Passed**. Kiểm thử trình duyệt với API giả lập **Passed**: 48 phòng qua 4 trang không trùng/thiếu; 5 cột/3 hàng desktop; trang cuối 3 phòng; tìm kiếm từ trang cuối; kết quả rỗng; lọc 25 phòng thành hai trang 15/10; mở đúng phòng ở trang sau; responsive 1440/768/375. Các kiểm tra ngăn trượt, trạng thái phòng, nhận/trả phòng và điều kiện chuyển phòng vẫn đạt. Không lỗi JavaScript. Đã xem ảnh desktop và mobile.

Bằng chứng: `artifacts/reception-rooms/verification.json`, `pagination-1440.png`, `pagination-768.png`, `pagination-375.png`. Không sửa backend/database; C# build/test và API/database thật **Not Run**. Mọi request ghi của kiểm thử dùng mock. Không commit/push; các trình duyệt kiểm thử đã đóng.
