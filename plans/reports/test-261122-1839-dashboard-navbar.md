# Tiêu đề tổng quan trên navbar

Đã chuyển tiêu đề “Tổng quan quản trị” cùng mô tả từ nội dung trang lên navbar cố định. Bỏ nút Làm mới ở trang tổng quan quản trị; vẫn giữ Thử lại khi có lỗi tải dữ liệu. Tiêu đề chỉ xuất hiện trên dashboard quản trị và biến mất khi chuyển trang.

File sửa: `frontend/src/compact/app.tsx`, `frontend/src/compact/management.tsx`, `frontend/src/compact/task-ux.css`.

Build frontend: Passed, không cảnh báo. Kiểm tra trình duyệt thực tế: Passed ở 1440/768/375px, tiêu đề không chồng khu vực tài khoản, navbar giữ ở trên khi cuộn, không tràn ngang, không còn tiêu đề trùng trong nội dung hay nút Làm mới. Năm biểu đồ tải đủ; chuyển sang Vai trò hoạt động bình thường; không lỗi JavaScript.

Lượt kiểm thử đầu hết thời gian chờ 5 giây trước khi biểu đồ xuất hiện. Chạy lại với thời gian chờ tải 30 giây đã đạt, không bỏ kiểm tra số biểu đồ. Bằng chứng và ảnh: `artifacts/dashboard-navbar/`.

Không sửa backend hoặc dữ liệu nghiệp vụ; đã backup trước đăng nhập kiểm thử. .NET build/test không chạy lại. Trình duyệt kiểm thử đã đóng; không commit/push/deploy. Không còn lỗi được phát hiện trong phạm vi thay đổi.
