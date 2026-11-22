# Quản lý tài khoản và reset mật khẩu qua email

Đã triển khai chỉnh họ tên/email/điện thoại, xem vai trò/phạm vi hiện tại, gán và thu hồi qua các API hiện hữu. Reset yêu cầu mật khẩu xác nhận của quản trị viên, lý do và rowVersion; gửi mật khẩu ngẫu nhiên 24 ký tự đến email đã lưu, hiệu lực 24 giờ, thu hồi session và buộc đổi mật khẩu. Hộp quản lý giữ mở sau thao tác, thông báo kết quả và cập nhật dữ liệu.

**Chưa gửi email ra hộp thư thật:** kiểm tra code, appsettings, tên khóa User Secrets và biến môi trường cho thấy chưa có SMTP. Đã thêm mail transport và [hướng dẫn cấu hình](../../docs/requirements/ACCOUNT-PASSWORD-EMAIL.md). Đang chờ người dùng chọn nhà cung cấp/địa chỉ gửi và cấu hình credential local. Không tự tạo tài khoản mail hoặc bịa cấu hình. Khi thiếu SMTP, UI khóa gửi; API trả 503 và giữ nguyên dữ liệu.

## File tạo/sửa trong lượt này

- `src/HotelOps.Api/Compact/Contracts/AccountManagement.cs` — request DTO và validation.
- `src/HotelOps.Api/Compact/Services/AccountManagementService.cs` — đọc chi tiết, cập nhật hồ sơ, reset/audit/session transaction.
- `src/HotelOps.Api/Compact/Services/PasswordResetEmail.cs` — SMTP STARTTLS, timeout, sinh mật khẩu an toàn.
- `src/HotelOps.Api/Compact/Services/AuthService.cs` — từ chối đổi mật khẩu khi mật khẩu tạm đã hết hạn, kể cả session được tạo trước lúc hết hạn.
- `src/HotelOps.Api/Compact/Controllers/UsersController.cs` — GET/PUT users/{id}, POST users/{id}/reset-password.
- `src/HotelOps.Api/Compact/CompactHost.cs` — đăng ký service và rate limit riêng cho reset.
- `frontend/src/compact/account-management.tsx` — hộp quản lý và các form/xác nhận/kết quả.
- `frontend/src/compact/admin.tsx` — dùng hộp quản lý mới; tải role/department theo tác vụ.
- `frontend/src/compact/api.ts` — thông báo lỗi tiếng Việt.
- `frontend/src/compact/task-ux.css` — bố cục quản lý tài khoản desktop/mobile.
- `tests/HotelOps.Compact.Tests/AccountManagementTests.cs` — mật khẩu ngẫu nhiên, thiếu cấu hình, kiểm thử SQL tích hợp với mail transport giả.
- `frontend/tests/compact-account-management.mjs` — API thật và UI với các thao tác ghi được chặn.
- `docs/requirements/ACCOUNT-PASSWORD-EMAIL.md`, `plans/261122-1914-account-management/plan.md`, báo cáo này và nhật ký local.

## Kiểm chứng

- FE `npm run build`: **Passed**.
- BE `dotnet build HotelOps.slnx`: **Passed**, 0 cảnh báo, 0 lỗi ở bản cuối.
- `dotnet test HotelOps.slnx --no-build`, có HOTELOPS_ACCOUNT_TEST trỏ database sao chép: **29 Passed, 1 Skipped**. Worker fixture không liên quan bị bỏ qua.
- UI/API quản lý: **8/8 Passed**. Kiểm tra dữ liệu chi tiết thật, input sai trả 400, không đăng nhập trả 401, SMTP chưa cấu hình trả 503 với rowVersion không đổi; form sửa/thu hồi/reset; xử lý lỗi và thành công; 375/1440 px; không có browser exception.
- Hồi quy bộ lọc: **8/8 Passed**; hồi quy giao diện/actor: **12/12 Passed**.
- Test SQL sao chép kiểm tra: trùng email, sai mật khẩu quản trị, stale version, tự reset, thiếu SMTP/email, tài khoản inactive, mail thất bại rollback hash/stamp/session/audit, gửi thành công đổi hash, thu hồi session, cooldown, login yêu cầu đổi mật khẩu, password hết hạn và đổi password thành công, không có quyền bị từ chối, API/audit không chứa password rõ.
- Primary DB sau test: **40 bảng, 56 users, 56 user-role assignments còn hiệu lực, 0 PASSWORD_RESET_EMAIL audit**. Không reset tài khoản nhân viên thật.
- Runtime: FE 5186, API 5768 PID 33052; health ok/schema 2.1.0. API cũ 5304 dừng đúng scope để build. Không thêm background service; browser test đóng trong finally.

## Bằng chứng, lỗi đã xử lý và giới hạn

- Backup VERIFYONLY: `HotelOpsCompactDB_before_workday_20261122_191842087.bak`; test copy `HotelOpsCompactWorkdayTestAccount20261122`. Không cần thay đổi schema/table/role/permission. Giữ test copy cho kiểm chứng tiếp theo.
- `artifacts/account-management/results.json`, `manage-375.png`, `manage-1440.png`; `artifacts/account-filters/results.json`; `artifacts/admin-ux/results.json`.
- UI các request ghi hợp lệ được mock; thông báo email thành công trong ảnh/test không chứng minh gửi inbox thật. Endpoint reset thật chỉ kiểm chứng nhánh thiếu SMTP, không thay đổi tài khoản. Tính đúng đắn của transaction/reset được thử trên SQL sao chép bằng transport giả nhận password trong bộ nhớ.
- Lượt UI đầu có lỗi harness do goto cùng hash giữ nguyên dialog và chặn click; sửa test reload trang, không sửa sản phẩm để né kiểm tra. `first-run.json` giữ dấu vết. Hai cảnh báo analyzer test được xử lý bằng assertion riêng chỉ in thông báo đã redacted thay vì có thể in password khi thất bại.
- Python launcher local không chạy được khi chuẩn bị chỉnh file; dùng Node đã có để hoàn thành thao tác. Web tool ban đầu không khả dụng; tra tài liệu chính thức Microsoft bằng search connector thay thế. Không chỉnh skill/tool code.
- SQL và SMTP không commit nguyên tử: nếu SMTP đã nhận thư nhưng SQL commit thất bại, thư có thể chứa mật khẩu không sử dụng được; API báo lỗi, quản trị viên cần thử lại. Không tự retry gửi mail. SMTP chấp nhận không đồng nghĩa inbox đã nhận.
- Không commit/push/deploy. Không ghi credential vào source hoặc log.

## Còn chờ

Nhà cung cấp SMTP, địa chỉ gửi và credential local để bật gửi email thật và kiểm tra inbox với người nhận được chỉ định. Phần source/build/test đã hoàn tất; cấu hình gửi mail thật và xác minh inbox chưa hoàn tất.
