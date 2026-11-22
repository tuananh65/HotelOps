# Bố cục tìm phòng và quay lại

Đã sửa `frontend/src/compact/catalog.tsx` và `frontend/src/compact/task-ux.css`: nút Tầng & phòng nằm trước ô tìm kiếm theo cả thứ tự DOM và giao diện; desktop/tablet căn nút trái, tìm kiếm phải, cùng đáy và thẳng mép bảng. Khoảng cách nội dung với navbar còn 20px, toolbar cách bảng 16px. Mobile xếp nút trước và ô tìm kiếm toàn chiều rộng phía dưới. Thêm gợi ý Nhập số phòng…

Build frontend **Passed** (`npm run build`, 1620 modules, không cảnh báo). Prettier **Passed**. Kiểm thử trình duyệt giả lập tại 1440/768/375 **Passed**: thứ tự, căn hàng/căn mép, khoảng cách bảng, không tràn ngang toàn trang, tìm phòng 103, quay lại danh sách tầng. Bảng trên mobile giữ cuộn ngang nội bộ. Đã xem ảnh desktop và mobile tại `artifacts/room-toolbar-layout/`; kết quả tại `verification.json`.

Kiểm thử sẵn có `compact-structure-settings.mjs` **Passed**, gồm cấu hình hai khách sạn, form thêm/sửa giả lập và điều hướng. Không sửa backend/database; API thật và C# build/test **Not Run**. Không commit/push, không tạo server mới; trình duyệt kiểm thử đã đóng.
