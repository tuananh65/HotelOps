# Thu gọn khoảng trống trang tài khoản

File sửa: `frontend/src/compact/task-ux.css`.

Đặt padding trên của nội dung trang tài khoản về 0, giữ 8px bên trong vùng tìm kiếm ngay dưới navbar. Giảm khoảng cách giữa tìm kiếm, bộ lọc chi tiết, tổng tài khoản và bảng; bỏ đường phân cách dư. Giữ ghim bộ lọc và tiêu đề bảng.

Kiểm thử trình duyệt **Passed** với API giả lập: phân trang, tìm kiếm, lọc, ghim khi cuộn trang/bảng, mở/đóng bộ lọc ở desktop/tablet/mobile/ngang; không lỗi JavaScript hoặc request ghi dữ liệu. Đã xem ảnh `artifacts/dashboard-pagination/sticky-filters-1440-false.png`. Prettier **Passed**. Kết quả máy đọc được tại `artifacts/dashboard-pagination/verification.json`.

Build frontend **Passed**: `npm run build`, 1620 modules, không cảnh báo. Không sửa backend/database; API thật và C# build/test **Not Run** trong lượt này. Không commit/push.
