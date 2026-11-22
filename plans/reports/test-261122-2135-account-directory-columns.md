# Chi nhánh, chức vụ và trạng thái trong danh sách tài khoản

Đã sửa `frontend/src/compact/admin.tsx` và `frontend/src/compact/task-ux.css`.

Bảng gồm Nhân viên, Tài khoản, Chi nhánh, Chức vụ, Thao tác. LED trạng thái đặt cạnh tên nhân viên, giữ nhãn truy cập và tooltip; bỏ cột Trạng thái riêng. Chi nhánh lấy từ danh sách quyền truy cập/công tác hiện có, hỗ trợ nhiều chi nhánh và phản hồi cũ chỉ có employments. Chức vụ hiển thị tên vai trò hiện có; gộp tên trùng. Thiếu phân công có nhãn rõ ràng. Bảng cuộn ngang trên màn hình hẹp.

- Build frontend: **Passed**, `npm run build`, 1620 modules, không cảnh báo.
- Kiểm thử trình duyệt: **Passed**, `node tests/compact-dashboard-pagination.mjs`, API giả lập; phân trang, tìm kiếm, bộ lọc, ghim và bố cục responsive.
- Kiểm tra riêng trên trình duyệt: **Passed** cho năm cột, chi nhánh từ branch access không có employment, nhiều chi nhánh, gộp chức vụ trùng, thiếu phân công và LED tài khoản khóa. Không lỗi JavaScript, không request ghi dữ liệu.
- Prettier: **Passed** cho hai file sửa.
- Đã xem ảnh `artifacts/dashboard-pagination/account-directory-1440.png`. Kết quả kiểm thử tổng hợp: `artifacts/dashboard-pagination/verification.json`.
- API/database thật và C# build/test: **Not Run**, thay đổi chỉ ở frontend. Không sửa schema, quyền hay dữ liệu tài khoản. Không commit/push; các trình duyệt kiểm thử đã đóng.
