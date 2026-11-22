# Excel user để chỉnh sửa

Đã xuất trực tiếp HotelOpsCompactDB sau chuẩn hóa V2: 15 user, 15 user_roles, 32 memberships; danh mục có 10 role hoạt động, 2 role ngừng, 2 branch DVBV/DVCT và 8 department.

Tệp: [HotelOps_Users_Chinh_Sua_2026-11-22.xlsx](../../docs/database/compact-v2/HotelOps_Users_Chinh_Sua_2026-11-22.xlsx).

8 sheet hiển thị: Huong_dan, Users, User_Roles, Memberships, DM_Role, DM_Branch, DM_Department, Users_Goc. Ô chỉnh sửa tô vàng, cột tham chiếu xám; filter, freeze header và các cột định danh; dropdown role hoạt động, chi nhánh, bộ phận, manager và thao tác. Tô dấu role ngừng; không tự chuyển tài khoản. Có dòng trống để điền bổ sung.

Chỉ truy vấn SELECT. Không đọc hoặc xuất password/hash, security_stamp, token phiên đăng nhập. RowVersion là mã đối chiếu concurrency dạng hex, không phải token đăng nhập. Dữ liệu ngày giờ UTC được ghi rõ; số điện thoại lưu text để giữ số0 đầu. Không công thức, không cần recalculation. Không tự import file vào database.

Kiểm chứng: đọc lại XLSX bằng ExcelJS, đối chiếu toàn bộ giá trị 15 dòng Users, 15 dòng User_Roles, 32 dòng Memberships với snapshot; dropdown, số sheet, định dạng text đạt. Không có formula/error cell. Toàn bộ XML trong archive parse thành công. Chưa kiểm tra bản in bằng ứng dụng Excel desktop.

Script riêng artifacts/user-excel-tools/create-users-workbook.cjs ban đầu thiếu dấu đóng ngoặc, đã sửa. Lượt kiểm tra phát hiện SQL timestamp→varchar trực tiếp không giữ RowVersion dưới dạng hex; đã chuyển qua varbinary trước khi CONVERT, xuất lại và kiểm tra toàn bộ đạt. npm có cảnh báo deprecated ở dependencies công cụ ExcelJS trong artifacts; không thay dependencies ứng dụng.

Build/test ứng dụng: NOT RUN vì không thay source/backend/frontend. Database không thay đổi. API không cần gọi. Không commit/push hay agent phụ. Bằng chứng: artifacts/user-excel/users-snapshot.json và validation.json.
