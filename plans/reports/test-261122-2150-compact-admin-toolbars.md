# Thu gọn thanh thao tác khách sạn và cấu hình

Sửa `frontend/src/compact/hotel-workspace.tsx`, `frontend/src/compact/catalog.tsx`, `frontend/src/compact/task-ux.css`.

Trang tầng/phòng gom nút quay lại bên trái và nhóm chỉnh sửa khách sạn/xem phòng/thêm tầng bên phải. Thống kê tầng/phòng nằm ở dòng nhỏ ngay trên danh sách. Trang cấu hình đưa nút thêm cùng hàng dropdown và tìm kiếm; dùng chung hành vi tạo hiện tại cho vai trò/bộ phận/loại phòng. Cả hai bố cục cách navbar 20px; các nhóm tự xuống dòng trên màn hình nhỏ. Không đổi quyền hoặc nghiệp vụ.

Build frontend **Passed**, 1620 modules, không cảnh báo. Prettier **Passed**. `compact-structure-settings.mjs` **Passed** với API giả lập. Kiểm tra riêng 1440/768/375 **Passed**: khoảng cách navbar, căn hàng desktop, xuống dòng responsive, không tràn ngang, tìm kiếm, mở form thêm vai trò/thêm tầng/chỉnh sửa khách sạn. Không lỗi JavaScript hoặc request ghi thật. Hai lần chạy kiểm tra riêng đầu tiên dùng giả định sai về selector trạng thái rỗng và yêu cầu một hàng trên tablet có sidebar; đã sửa kiểm tra theo DOM/bố cục thực và chạy lại đạt. Không thay đổi mã sản phẩm để né kiểm thử.

Đã xem ảnh desktop hai trang và mobile cấu hình. Bằng chứng: `artifacts/compact-admin-toolbars/verification.json` và ảnh cùng thư mục. API/database thật và C# build/test **Not Run** vì chỉ đổi frontend. Không commit/push; không có server mới, trình duyệt kiểm thử đã đóng.
