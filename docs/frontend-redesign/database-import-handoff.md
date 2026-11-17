# Database cục bộ từ dbdemo.sql

- Nguồn: `F:\DO AN FILE\dbdemo.sql`; giữ nguyên file gốc.
- SQL Server: `localhost`, service `MSSQLSERVER`; database `HotelOpsDB`.
- Import bằng sqlcmd thành công. Chỉ thay khối tạo database để dùng đường dẫn dữ liệu mặc định trên máy và dừng nếu database đã tồn tại; giữ schema và dữ liệu nguồn.
- Kiểm tra thực tế: 72 bảng, 26 người dùng, 2 chi nhánh, 15 phòng; có `users.row_version`, không có foreign key bị vô hiệu hóa. DBCC CHECKCONSTRAINTS không báo vi phạm.
- Đã cập nhật `ConnectionStrings:HotelOpsDb` trong .NET User Secrets; không sửa appsettings hoặc đưa thông tin xác thực vào repository.
- Backend khởi động ở Development và vượt qua kiểm tra schema lúc khởi động. Frontend build Cacao được phục vụ cùng origin tại `https://localhost:7443/login` qua `CanonicalFrontend:Root` trong môi trường tiến trình.
- Vite ở `http://127.0.0.1:5175` được chạy với `NODE_USE_SYSTEM_CA=1` để proxy tin cậy chứng chỉ Windows, giữ `secure: true`.
- Không sửa C#, schema, migration hoặc dependency trong lượt import. Build/test: Not Run cho lượt database này; kết quả frontend trước đó xem cacao-handoff.md.
- Còn cảnh báo EF index của RoomTypeBranchV1; WorkAlertWorker báo InvalidOperationException; IncidentNotificationWorker báo FormatException khi dựng truy vấn. Không coi backend toàn bộ module đã kiểm thử thành công.
- Chưa có thông tin đăng nhập để kiểm chứng các workflow có quyền với dữ liệu thật. Không đổi mật khẩu hoặc tài khoản nguồn.
- Browser thật (Edge, không mock, không bỏ kiểm tra chứng chỉ): login HTTP 200, CSS HTTP 200, có ô mật khẩu và không có pageerror. API session trực tiếp và qua Vite đều HTTP 401 đúng khi chưa đăng nhập; CSRF HTTP 200. Vite cần chạy trong phiên người dùng có kho chứng chỉ Windows đã tin cậy (phiên sandbox không đọc được cùng kho này).

## Chạy lại tại máy này

Từ thư mục gốc trong PowerShell có quyền truy cập SQL Server bằng Windows Authentication:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:CanonicalFrontend__Root='F:\HotelOps_Latest\frontend\dist'
dotnet run --project src/HotelOps.Api --no-launch-profile --no-build --urls https://localhost:7443
```

Nếu dùng Vite, mở terminal khác tại `frontend`:

```powershell
$env:NODE_USE_SYSTEM_CA='1'
npm.cmd run dev -- --port 5175
```
