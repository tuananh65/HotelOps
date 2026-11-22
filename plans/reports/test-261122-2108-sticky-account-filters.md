# Ghim bộ lọc tài khoản

Đã ghim ô tìm kiếm, nút Bộ lọc, Tạo tài khoản và vùng lọc chi nhánh/bộ phận/vai trò dưới navbar. Độ lệch cập nhật theo chiều cao navbar thực tế khi đổi kích thước màn hình. Tiêu đề bảng nằm dưới bộ lọc khi cuộn trang hoặc cuộn bảng. Màn hình thấp cho phép cuộn trong vùng lọc để tiếp cận đủ các trường.

File sửa: `frontend/src/compact/account-filters.tsx`, `frontend/src/compact/use-sticky-headers.ts`, `frontend/src/compact/task-ux.css`, `frontend/tests/compact-dashboard-pagination.mjs`.

- Build frontend: **Passed**, không cảnh báo.
- Kiểm thử bộ lọc/phân trang/dashboard: **Passed** với API giả lập. Kiểm tra ghim khi mở/đóng bộ lọc, cuộn trang và bảng tại 1440×820, 768×820, 375×812, 812×375; ô tìm kiếm/nút tạo vẫn trong viewport, tiêu đề bảng không bị che, trường vai trò tiếp cận được.
- Hồi quy navbar: **Passed**, 48 tổ hợp trang/kích thước; kiểm tra tiêu đề động, dialog và sticky header.
- Đã xem ảnh desktop và mobile khi cuộn tại `artifacts/dashboard-pagination/sticky-filters-*.png`.
- Lần chạy đầu thất bại vì bộ chọn `getByLabel` không tìm được trường vai trò; đổi sang trường select trong vùng lọc và chạy lại đạt. Prettier phát hiện định dạng bài kiểm tra; đã format lại.
- Không sửa backend/database; API thật và C# build/test **Not Run** trong lượt này. Không có lỗi chức năng còn tồn tại trong phạm vi kiểm tra; không commit/push.
