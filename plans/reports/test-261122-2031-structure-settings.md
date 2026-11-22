# Chuyển Loại phòng và Bộ phận sang Cấu hình hệ thống

Đã chuyển hai danh mục vào dropdown Nội dung cấu hình cùng Vai trò; bỏ các nút tương ứng trong chi tiết khách sạn. Đường dẫn cũ chuyển sang trang mới và giữ khách sạn đã chọn. Loại phòng/Bộ phận vẫn thuộc từng chi nhánh như dữ liệu hiện tại; trang cấu hình hiển thị khách sạn và nút đổi khách sạn. Vai trò vẫn là danh mục mặc định. Không thay đổi quyền hoặc database.

## File thay đổi

- `frontend/src/compact/catalog.tsx`: nhóm danh mục, chọn khách sạn tại cấu hình theo chi nhánh, chặn tải/tạo danh mục khi chưa có chi nhánh.
- `frontend/src/compact/hotel-workspace.tsx`: bỏ hai mục, chuyển hướng liên kết cũ.
- `frontend/tests/compact-structure-settings.mjs`: mới, kiểm tra UI và request bằng API giả lập.
- `frontend/tests/compact-navbar-titles.mjs`: cập nhật tiêu đề sau chuyển hướng cũ.
- `frontend/tests/compact-permissions-ui.mjs`, `frontend/tests/compact-reference-settings.mjs`: cập nhật kỳ vọng dropdown ba danh mục, danh sách quyền/cấu hình nghiệp vụ vẫn ẩn.
- Báo cáo này và journal `plans/journals/2026-11-22-hotelops-structure-settings.md`.

## Kiểm chứng

- Build FE: **Passed**, `npm run build` tại frontend, không cảnh báo build.
- Browser cấu hình: **Passed**. Dropdown ba mục, bỏ nút cũ, đổi hai khách sạn, danh mục đúng chi nhánh, bốn request thêm/sửa đúng endpoint/branchId/rowVersion, hai liên kết cũ, desktop/mobile, chặn thiếu chi nhánh. Các request ghi đã được chặn và trả kết quả giả lập, không gửi đến backend.
- Browser navbar: **Passed**, 48 trường hợp trang/kích thước màn hình, title động, dialog và sticky header.
- Không có lỗi JavaScript trong hai lần kiểm tra. Ảnh và kết quả lưu tại `artifacts/structure-settings/` và `artifacts/navbar-page-titles/`.
- `git diff --check`: không lỗi whitespace; có cảnh báo LF/CRLF từ các file package.json/package-lock.json/main.tsx đã thay đổi từ trước, không sửa trong lượt này.
- API/DB thật, build/test BE: **Not Run**, không thay đổi backend/database. Hai script quyền/reference phụ thuộc đăng nhập thật chỉ cập nhật kỳ vọng, không chạy trong lượt này.

Chưa phát hiện lỗi còn lại trong phạm vi đã kiểm tra. Kiểm thử giả lập xác nhận hành vi UI và dữ liệu request, không xác nhận lại lưu trữ SQL Server. Không commit/push/deploy. Một agent; không tạo thêm server nền.
