# Ẩn danh sách quyền

Đã bỏ mục Quyền hệ thống khỏi dropdown Cấu hình hệ thống. Đường dẫn cũ `#settings?resource=permissions` hiển thị mục cấu hình hợp lệ thay vì danh sách quyền. Giữ chức năng Phân quyền trong Vai trò và API cung cấp quyền cho hộp thoại này.

File sửa: `frontend/src/compact/catalog.tsx`, `frontend/tests/compact-permissions-ui.mjs`.

Frontend build: Passed, không có cảnh báo build. `git diff --check`: Passed; chỉ có cảnh báo Git LF/CRLF trên file đã sửa từ trước.

Kiểm thử trình duyệt thực tế: Passed. Dropdown chỉ còn Cấu hình nghiệp vụ và Vai trò; URL cũ không hiện danh sách quyền; hộp thoại Phân quyền vẫn tải đủ 67 quyền tiếng Việt. Kiểm tra thêm màn hình 375/768px, không lỗi JavaScript. Bằng chứng: `artifacts/admin-ux/permission-results.json` và ảnh cùng thư mục. Không sửa dữ liệu quyền, vai trò hoặc schema. Đã backup trước đăng nhập kiểm thử (đăng nhập tạo phiên).

Không sửa C#; .NET build/test không chạy lại. Trình duyệt kiểm thử đã đóng. Không commit/push/deploy.
