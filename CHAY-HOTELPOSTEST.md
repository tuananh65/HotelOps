# Chạy hotelposTest với database cũ

Cách chạy trong **một cửa sổ PowerShell**:

```powershell
cd D:\test\hotelposTest
npm.cmd start
```

Lệnh này mở cả Backend và Frontend, hiển thị log BE/FE trong cùng terminal. Nhấn Ctrl+C để dừng cả hai process do launcher mở. Backend dùng bản đã build; sau khi sửa C# cần build lại bằng lệnh bên dưới. SQL Server CHI cần hoạt động và User Secrets cần có sẵn. Nếu port đang được dùng, launcher báo lỗi, không tự dừng tiến trình cũ.

Cách chạy thủ công từng phần nếu cần chẩn đoán:

Chạy hai terminal bằng cùng Windows user đã cấu hình SQL User Secrets. SQL Server instance CHI cần đang chạy. Không chạy lại SQL schema/patch/seed.

Terminal 1:

```powershell
cd D:\test\hotelposTest
& D:\test\integration-artifacts\dotnet\dotnet.exe run --project src/HotelOps.Api --no-build --launch-profile https --urls https://localhost:7029
```

Terminal 2:

```powershell
cd D:\test\hotelposTest
npm.cmd run dev -- --config vite.hotelposTest.config.ts --port 5175
```

Mở **https://localhost:5175/login**. Có thể đăng nhập bằng username hoặc email của tài khoản SQL hiện có. Không lưu mật khẩu trong source.

Kiểm tra Backend–SQL: **https://localhost:7029/api/health/database**.

Luồng: FE 5175 → proxy /api → BE 7029 → HotelOpsDB cũ. Các cổng này dành cho bản hợp nhất để tránh gọi nhầm các server gốc. `npm.cmd run dev` đơn thuần vẫn dùng cấu hình/cổng gốc, nên hãy dùng đúng lệnh trên.

Backend cần build sau khi sửa source:

```powershell
& D:\test\integration-artifacts\dotnet\dotnet.exe build HotelOps.slnx
npm.cmd run build
```

Nếu báo port đang sử dụng, kiểm tra trang đang chạy trước khi mở thêm server. Chứng chỉ tự ký local có thể cần được chấp nhận trên browser. Production chưa được cấu hình.
