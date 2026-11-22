# Bỏ trạng thái khỏi bảng Vai trò

Đã bỏ tiêu đề và ô LED Trạng thái riêng tại bảng Vai trò, bỏ CSS căn cột trạng thái cũ. Bảng còn Tên, Mô tả, Thao tác; không thay đổi dữ liệu trạng thái hoặc các bảng danh mục khác.

File sửa: `frontend/src/compact/catalog.tsx`, `frontend/src/compact/task-ux.css`, `frontend/tests/compact-roles-ui.mjs`.

Build frontend: Passed, không có cảnh báo build.

Kiểm thử giao diện thực tế: Passed. Xác nhận đúng ba cột, không còn LED, hai nút thao tác đồng đều tại 1440/768/375px, tìm kiếm và hộp thoại chỉnh sửa/phân quyền hoạt động, quyền Admin vẫn được bảo vệ, không lỗi JavaScript. Bằng chứng: `artifacts/role-cleanup/ui-results.json` và ảnh cùng thư mục.

Đã backup trước đăng nhập kiểm thử. Không sửa backend/database nghiệp vụ; .NET build/test không chạy lại. Trình duyệt kiểm thử đã đóng. `git diff --check` Passed, chỉ có cảnh báo LF/CRLF trên file sửa từ trước. Không commit/push/deploy.
