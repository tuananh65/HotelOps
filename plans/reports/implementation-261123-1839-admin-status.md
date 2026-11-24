# Admin: trạng thái và mô tả danh mục

- Cột trạng thái dùng công tắc xanh/đỏ, có nhãn trợ năng và thao tác bàn phím. Không dùng dấu tích. Giữ kiểm tra quyền, dữ liệu đang dùng và row version; lỗi không làm giao diện hiển thị trạng thái giả.
- Bộ phận/loại phòng có cột mô tả và trạng thái. Form Admin không còn trường trạng thái; sửa mô tả/tên không thay trạng thái. Tài khoản có cột trạng thái riêng, mở xác nhận và lý do theo quy tắc hiện có; không tự khóa tài khoản đang đăng nhập.
- SYS_ADMIN không còn khối chọn chi nhánh; các vai trò khác vẫn có. Không xóa menu quản lý cấu trúc khách sạn.
- Đã được duyệt thêm departments.description nullable nvarchar(2000), đồng bộ entity/mapping/API. Dữ liệu cũ để trống. Không đổi grant.

Tệp chính: frontend/src/compact/status-switch.tsx, status-switch.css, catalog.tsx, catalog-specs.ts, admin.tsx, account-management.tsx, branch-context.tsx; src/HotelOps.Api/Compact/Entities/Department.cs, Data/HotelOpsCompactDbContext.cs, Services/CatalogService.cs; scripts/database/compact-v2/add-department-description.sql, Enable-DepartmentDescription.ps1, 01_Schema.sql, Generate-Schema.mjs, Test-Backend.ps1, test-admin-catalog.ps1; frontend/tests/compact-admin-status.mjs, compact-structure-settings.mjs.

Build .NET Passed, 0 warning/error; frontend Passed. Unit 66 Passed/3 Skipped. API fixture b0d4abb4bbf9 Passed. Hai browser suites Admin/cấu trúc Passed; có kiểm tra vai trò khác, stale version, dependency rejection, tài khoản và viewport mobile. Không tạo dữ liệu giả trên Production.

Release publish Passed với cảnh báo MSB3026 retry OFL.txt; đã so khớp SHA256 nguồn/đích. Vite thông báo outDir nằm ngoài frontend và không tự xóa; dùng thư mục release riêng. Backup mới CHECKSUM/VERIFYONLY trước thêm cột, API Production31636. Không commit/push.
