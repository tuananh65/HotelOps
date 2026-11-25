# Development và Production chạy local

Ngày 25/11/2026, Owner xác nhận cả hai môi trường chạy trên máy local. Giữ nhánh `feature/project2-role-alignment-20261123`; tên môi trường không phải yêu cầu đổi Git branch. Không triển khai EC2.

| Môi trường | Web | API | Database riêng |
|---|---|---|---|
| Development | http://127.0.0.1:5173 | http://127.0.0.1:5081 | HotelOpsCompactWorkdayTestLocalDevV2261125 |
| Production local | https://localhost:5080 | Cùng origin | HotelOpsCompactDB |

Development dùng Vite cho frontend và API có `ASPNETCORE_ENVIRONMENT=Development`. Production chạy binary Release, `ASPNETCORE_ENVIRONMENT=Production`, phục vụ frontend đã build từ `wwwroot` bằng Kestrel HTTPS. Không dùng Vite preview làm Production. Cả hai API đang dùng cùng bản Release đã kiểm thử; môi trường Development vẫn cung cấp OpenAPI và frontend hot reload. Sửa C# cần build/publish và khởi động lại.

## Dữ liệu và worker

Development giữ database thử nghiệm riêng, được restore từ `HotelOpsCompactWorkdayTestV22611250010`; dữ liệu của Development không đồng bộ sang Production. Production dùng `HotelOpsCompactDB` theo quyết định Owner ngày25/11. Trước chuyển đổi, đã backup COPY_ONLY/CHECKSUM và VERIFYONLY, diễn tập cleanup/nâng cấp trên clone mới; không restore ghi đè database chính.

Database chính được nâng cấp49 lên57 bảng và khôi phục10 Maintenance Staff bằng provenance. Chỉ xóa196 chứng từ được xác nhận test (194 fixture và MR440/ISSUE441), giữ57 tài khoản,15 danh mục vật tư,5 chứng từ còn lại và32 công việc. Bản ghi bị xóa có archive trong audit; không tạo200 tài sản ấm từ tồn test đã xóa. Username/password hash được giữ; session của vai trò phục hồi có thể bị thu hồi theo migration. Không tự nhận các chứng từ còn lại là hàng đã kiểm kê thực tế.

Worker vận hành, cảnh báo/quá hạn và tự kết ca đều bật trên database tương ứng của từng môi trường. Email thật và external AI vẫn tắt theo quyết định Owner; fallback thống kê vẫn có. Không coi chức năng gửi email/AI provider bên ngoài đã hoạt động. Hai thư mục Data Protection keys nằm trong `artifacts/local-runtime`; hiện runtime có cảnh báo chưa cấu hình XML key encryption. Không đưa key/log/private settings lên Git hoặc dùng cấu hình local này thay cấu hình bảo mật phát hành công khai.

## Khởi động, xem trạng thái và dừng

Chạy tại gốc repository, bằng tài khoản Windows đã cấu hình .NET User Secrets và có quyền SQL Server:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local/run-local.ps1 -Action Status
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local/run-local.ps1 -Action Start
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local/run-local.ps1 -Action Stop
```

Thêm `-Environment Development` hoặc `-Environment Production` để chọn riêng. Không chạy Start khi môi trường đã chạy: script từ chối cổng đang được dùng. Stop chỉ dừng PID có thời gian tạo khớp metadata của launcher, không dừng server khác. Tiến trình chạy nền, log và metadata ở `artifacts/local-runtime/`; connection string chỉ được truyền qua environment của process, không ghi vào metadata.

## Build lại sau khi sửa source

Dừng hai môi trường trước khi ghi đè thư mục publish. Không chạy lại bước restore/khởi tạo session khi chỉ cập nhật code.

```powershell
dotnet build HotelOps.slnx -c Release
dotnet test HotelOps.slnx -c Release --no-build --no-restore
dotnet publish src/HotelOps.Api/HotelOps.Api.csproj -c Release --no-build --no-restore -o .artifacts/runtime-production/api
npm --prefix frontend run build
Push-Location frontend
npm exec -- vite build --outDir ../.artifacts/runtime-production/api/wwwroot
Pop-Location
dotnet dev-certs https --check --trust
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local/run-local.ps1 -Action Start
```

Nếu chưa có certificate đáng tin cậy, chạy `dotnet dev-certs https --trust` bằng cùng tài khoản chạy ứng dụng. Dùng `https://localhost:5080`, không thay localhost bằng IP khi kiểm TLS. Không tắt kiểm tra certificate để che lỗi.

## Kiểm chứng đã chạy

- Release build:0 warning,0 error. Một lần publish trước đó retry file font bị khóa đã hoàn thành; lần publish cuối không còn warning đó.
- Automated tests của bản Release hiện tại:158 PASS,0 FAIL,3 SKIP. Không tính các test tích hợp bị bỏ qua là đạt; lượt chuyển database không sửa C# và không chạy lại suite.
- Frontend:build PASS; hai cảnh báo PURE annotation từ SignalR. Vite báo output nằm ngoài frontend và không tự xóa thư mục; đây là đích publish đã chỉ định.
- Hai `/health` trả200, schema3.0.0/57 bảng sau khi SchemaGate kiểm database thực.
- Edge kiểm cả hai URL với `ignoreHTTPSErrors=false`:login/Username/toggle, popup recovery mobile, không tràn ngang; API chưa đăng nhập401, API không tồn tại404, realtime chưa đăng nhập401.
- Production kiểm thêm asset JS200 đúng MIME, HEAD login, missing file404, POST không phải API404 và OpenAPI404. Không trả SPA HTML thay lỗi API.
- Không đăng nhập bằng mật khẩu người dùng thật hoặc gửi yêu cầu reset trong smoke này. Password-preservation được kiểm bằng SQL, không xuất hash.

Bằng chứng: [browser smoke](../../artifacts/local-runtime/browser-smoke.json), [official database verification](../../artifacts/official-v2-upgrade/HotelOpsCompactDB-verification.json), [ảnh Production](../../artifacts/local-runtime/production-login.png). Chạy lại smoke bằng `node scripts/local/verify-local-runtime.mjs`; kiểm tra database chính bằng `powershell -File scripts/local/verify-official-database.ps1`. Script `verify-runtime-databases.ps1` cũ chỉ kiểm hai clone ban đầu; không dùng `-InitializeSessions` trên môi trường đang phục vụ người dùng.

Files mới: `scripts/local/run-local.ps1`, `scripts/local/verify-runtime-databases.ps1`, `scripts/local/verify-local-runtime.mjs`. File sản phẩm sửa: `src/HotelOps.Api/Compact/CompactHost.cs` thêm static files/SPA fallback khi có frontend; giữ nguyên chế độ API-only khi không có index.html.
