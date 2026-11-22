# Chi nhánh dưới logo và lịch từng đối tượng

Đã chuyển BranchContext vào sidebar ngay dưới logo, dùng chung mọi vai trò. Bỏ các bản sao trong nội dung trang và cấu hình. Giữ tên chi nhánh, biểu tượng, nút đổi khách sạn theo phạm vi được cấp; sidebar thu gọn vẫn mở được hộp chọn bằng biểu tượng, điện thoại sử dụng qua menu.

Lịch quản lý bỏ lựa chọn Tất cả đối tượng và tiêu đề Lịch toàn chi nhánh. Mặc định chọn đối tượng đầu tiên trong danh sách được sắp xếp hiện tại, chỉ hiển thị ca thuộc đối tượng đó. Lựa chọn giữ khi chuyển tuần/lưu phân ca; không có nhóm thì dropdown khóa với Chưa có đối tượng. Lịch cá nhân và quyền phân ca không đổi.

Tệp sửa: `frontend/src/compact/app.tsx`, `catalog.tsx`, `branch-context.tsx`, `task-ux.css`, `workday.tsx`; `frontend/tests/compact-navbar-titles.mjs`, `compact-structure-settings.mjs`, `compact-workday-filter.mjs`; `docs/ui/compact-ux.md`.

Build frontend: **Passed**, 1621 modules, không cảnh báo. Kiểm thử trình duyệt giả lập API: **Passed** cho navbar (16 tuyến × 3 kích thước, chi nhánh nằm giữa logo/menu và không còn trong main), cấu hình (đổi chi nhánh, sidebar thu gọn/mobile), lịch (chọn từng đối tượng, giữ lựa chọn, phân ca đúng payload, lịch cá nhân/rỗng). Đã xem ảnh lịch desktop và sidebar mobile trong `artifacts/workday-filter/filter-1440.png` và `artifacts/structure-settings/settings-375.png`.

Không sửa backend/database; C# build/test và kiểm chứng API/database thật: **Not Run**. Không gửi email, không login thật, không commit/push/deploy. Không có lỗi hoặc cảnh báo mới trong các kiểm tra đã chạy.
