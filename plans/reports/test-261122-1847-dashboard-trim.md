# Thu gọn dashboard quản trị

Đã bỏ ba ô Vai trò hoạt động, Bộ phận, Quyền hệ thống và ba biểu đồ Số bộ phận theo khách sạn, Số quyền theo vai trò, Trạng thái khách sạn.

Dashboard còn ba ô Chi nhánh hoạt động, Tài khoản hoạt động, Phòng trên một hàng desktop; hai biểu đồ Số phòng theo khách sạn và Trạng thái tài khoản nằm cạnh nhau. Giữ navbar cố định và bố cục thích ứng màn hình nhỏ.

Không tải riêng danh mục vai trò/quyền/bộ phận hoặc quyền của từng vai trò khi mở dashboard. API tổng quan hiện hữu giữ nguyên contract. Dữ liệu và phân quyền không thay đổi.

File sửa: `frontend/src/compact/management.tsx`, `frontend/src/compact/management-details.tsx`, `frontend/src/compact/task-ux.css`, `frontend/tests/compact-task-ux.mjs`.

Build frontend: **Passed**, không cảnh báo. Kiểm thử giao diện/API: **12/12 Passed**; xác nhận đúng ba ô, hai biểu đồ, số phòng và tài khoản khớp dữ liệu thật, không có các request riêng đã bỏ. Kiểm tra responsive 375/768/1440, các luồng tài khoản, khách sạn và các actor liên quan đạt; không lỗi JavaScript hoặc API đọc thất bại.

Lượt đầu có ba ca hết thời gian chờ 5 giây (chọn khách sạn, fallback WebGL, mở phiên Owner). Chạy lại với thời gian chờ 30 giây, giữ nguyên các assertion, cả ba đạt. Kết quả đầu giữ tại `artifacts/admin-ux/dashboard-trim-first-run.json`; kết quả cuối `artifacts/admin-ux/results.json`. Đã xem ảnh dashboard desktop; ảnh các kích thước trong cùng thư mục.

`git diff --check` Passed; cảnh báo LF/CRLF ở các file sửa từ trước. Không sửa C#; .NET build/test không chạy lại. Đã backup trước đăng nhập kiểm thử; không ghi dữ liệu nghiệp vụ. Trình duyệt kiểm thử và tiến trình build đã kết thúc, giữ frontend/backend phục vụ người dùng. Không commit/push/deploy.
