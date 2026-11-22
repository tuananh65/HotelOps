# Cấu hình nghiệp vụ Admin

Đã bỏ cột Mã và ô nhập mã trong màn hình cấu hình nghiệp vụ. Thêm mới tự sinh mã kỹ thuật `CFG-` + UUID; khi sửa không gửi thay đổi mã, giữ nguyên mã cũ và nhóm. Mã sinh dài 36 ký tự, nằm trong giới hạn database 60 ký tự.

Ẩn Đơn vị tính (UOM), Nhóm vật tư (ITEM_CATEGORY) khỏi danh sách, tìm kiếm và lựa chọn nhóm khi thêm mới của Admin. Giữ Độ ưu tiên, Mức nghiêm trọng, Nhóm công việc/sự cố và Loại tài sản. Loại tài sản là nhóm riêng, không được coi là Nhóm vật tư trong thay đổi này.

## Đánh giá mã

Không xóa cột mã khỏi database: `ReferenceValue.Code` hiện bắt buộc, được khai báo trong API và EF mapping, có unique index `(Kind, Code)` tại `HotelOpsCompactDbContext.cs`; các script nạp/kiểm thử dữ liệu vẫn ghi mã. Xóa cột cần sửa đồng bộ schema, mapping và API. Khóa liên kết nghiệp vụ của cấu hình dùng ID; không khẳng định các khóa ngoại dùng mã. Riêng mã vai trò/quyền đang được kiểm tra trực tiếp trong `Access.cs`, `AuthService.cs`, `UserService.cs` nên không thể loại bỏ chung mọi mã.

Đây là thay đổi giao diện quản trị, không sửa chính sách phân quyền API và không xóa các cấu hình vật tư mà kho đang sử dụng.

## Kiểm chứng

- Frontend build: Passed, không có cảnh báo build.
- `compact-reference-settings.mjs`: Passed. API đọc dữ liệu thật; UI hiển thị 3 dòng cấu hình, ẩn 2 dòng vật tư; không có mã trong bảng/form.
- Form thêm mới chỉ có 4 nhóm cho phép; payload có mã sinh tự động. Form sửa giữ nhóm cố định, không gửi thay đổi mã/nhóm và giữ rowVersion.
- Hai request ghi thử được chặn tại Playwright, không gửi tới database; dữ liệu cấu hình đọc trước/sau giống nhau. Backup thành công trước đăng nhập kiểm thử.
- Desktop 1440px và mobile 375px: không tràn ngang toàn trang; không lỗi JavaScript. Ảnh và kết quả trong `artifacts/admin-reference-settings/`.
- `git diff --check`: Passed; cảnh báo Git LF/CRLF chỉ ở các file sửa từ trước.
- Không sửa C# hoặc schema; .NET build/test không chạy lại. Chưa thực hiện lưu cấu hình mới vào database thật trong lượt này.

File sửa: `frontend/src/compact/catalog.tsx`. File thêm: `frontend/tests/compact-reference-settings.mjs`, kế hoạch và báo cáo này. Trình duyệt kiểm thử đã đóng; không commit/push/deploy.
