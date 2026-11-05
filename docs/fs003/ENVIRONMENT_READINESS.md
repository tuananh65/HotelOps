# FS-003 — Environment readiness

**BLOCKED_EXTERNAL**. Developer xác nhận chưa xác nhận SQL trial disposable/test account/HTTPS, cho phép audit read-only; cấm tạo/xóa/reset/migrate/ghi DB chưa được xác nhận. Không coi historical PASS là quyền chạy lại.

| Thành phần | Evidence mới | Trạng thái / còn thiếu |
|---|---|---|
| Branch/baseline | Git status/diff 14 FS-002 files, HEAD baseline đúng; commit/push receipt README | Ready for DEV1 source work |
| API launch | `src/HotelOps.Api/Properties/launchSettings.json` HTTPS profile dùng localhost:7019, HTTP:5255 | Proxy example dùng HTTPS:7443; cần thống nhất port bằng local launch/env, không đổi shared config |
| API appsettings | Shared HotelOpsDb trống; Development chỉ logging | Không chứa local SQL host/credential mới |
| User Secrets | API UserSecretsId trong csproj; file tồn tại; key ConnectionStrings:HotelOpsDb có value | Chỉ kiểm names/status và parse nội bộ; catalog không khớp bảy historical housekeeping candidates; chưa được xác nhận disposable |
| Dedicated trial config | Không có key ConnectionStrings:HotelOpsTrial trong file secrets đã kiểm | Cần developer xác nhận target/config; không tự sửa secrets |
| Environment | Chỉ liệt kê tên biến liên quan; không thấy trial/cert/proxy override trong process hiện tại | Không xuất values hoặc dump environment |
| HTTPS dev certificate | `dotnet dev-certs https --check --trust` báo No valid certificate found | Không tạo/export/trust certificate trong task |
| Frontend TLS | `.env.local` không tồn tại; `.env.example` chỉ paths mẫu | Cần cert/key PEM ngoài Git và trust chain; không dùng secure:false/ignore-certificate-errors |
| Vite | `frontend/vite.config.ts`: strictPort, proxy /api, secure:true; cert/key phải cùng có | HTTP fixture Vite không kiểm Secure cookies |
| Historical trials | Housekeeping Task73/74 receipts: bảy unique candidate DB names; Task74 287 checks là lịch sử | Không chứng minh DB còn tồn tại hoặc không có production data |
| Historical harness | HotelStructure Program / progress Run-Progress pin instance, trial name/Task17 marker và có fixture/DDL/DML | Không chạy. Machine-specific target/cert/ephemeral fixtures không tự trở thành cấu hình máy này |
| Read-only metadata attempt | `sqlcmd` hiện có; connection được parse bằng DbConnectionStringBuilder.set_ConnectionString; integrated auth; query chỉ COUNT sys.databases trên master cho candidate allowlist | Kết nối/login/TLS chưa xác minh thành công; provider payload được redact. Không kết luận candidate absent, không đổi trust/TLS để vượt lỗi |
| Trial data classification | Chưa mở trial/query data vì chưa xác nhận target | Không thể chứng minh disposable/không production từ tên hoặc receipt |
| Test identity/grants | Historical harness tự provision credentials/fixtures khi chạy; không có current test identity được developer xác nhận | Không dùng demo login/password, không reset/provision user |

## Ranh giới read-only

Đã đọc cấu hình local chỉ để xuất **tên key, configured booleans, matching counts**; không đưa connection string, host, username/password, cookie, CSRF, PEM private key vào evidence. Metadata attempt chỉ nhắm master/system catalog, không chạy application business queries hoặc SQL write. Không có SQL metadata result thành công để suy trial availability.

API chưa khởi động: Program chạy StartupSchemaValidator trước listener; cookie validation/session touch/login có SQL effects. Đặc biệt GET nghiệp vụ không bảo đảm toàn pipeline read-only (`Middleware/SessionTouchMiddleware.cs`). Với chỉ thị hiện tại, không dùng API trên connection chưa xác nhận.

## Cần developer/DEV3 cung cấp

1. Target SQL disposable được xác nhận, nguồn dữ liệu không production, principal/connection được phép kiểm thử; read-write acceptance phải được cho phép riêng. Chỉ trỏ tới local config, không gửi secret vào chat.
2. Khả năng kết nối/TLS/trust tới SQL và quyền đọc system metadata/schema trial.
3. Current test account hợp lệ, branch được phép và không được phép; quyền ROOM_VIEW/CLEANING_EXECUTE, unique ACTIVE cleaning assignment IN_PROGRESS và progress schema đã được kiểm chứng. Không tự tạo fixture.
4. Browser-facing HTTPS PEM cert/key và CA trust; thống nhất backend port/proxy. Không commit private key.
5. Real session/login integration của DEV3, hoặc quy trình login API hiện có do developer cho phép trên trial; xác nhận session-touch/login writes nằm trong trial scope.

Các lệnh audit không mutation có thể chạy lại: `git status --short`, `dotnet dev-certs https --check --trust`, `Test-Path frontend/.env.local`, đọc launchSettings/Vite/Development config, liệt kê **tên** env keys. Không dùng `dotnet user-secrets list` hoặc dump env vào receipt vì chúng lộ values. Mở SQL chỉ sau xác nhận target; không chạy archived verification runners như một phép kiểm existence.
