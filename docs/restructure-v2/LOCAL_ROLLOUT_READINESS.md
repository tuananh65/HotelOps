# Chuẩn bị đưa V2 vào môi trường local

Cập nhật 25/11/2026. Tài liệu này chuẩn bị đường nâng cấp để review, **không xác nhận đã nâng cấp database vận hành**. Không thay secret, dữ liệu, tài khoản hoặc tiến trình khi viết tài liệu.

## Kết luận hiện tại

| Kiểm tra | HotelOpsCompactDB | TEST V2 |
|---|---|---|
| Bảng nghiệp vụ | 49 | 57 |
| Schema property | 3.0.0 | 3.0.0 |
| Maintenance Staff hoạt động | Role inactive, 0 người | Role active, 10 người phục hồi |
| Maintenance Supervisor hoạt động | 4 người | 4 người |
| Dùng được với binary V2 hiện tại | Không: schema gate sẽ chặn | Có nếu toàn bộ schema gate và healthcheck đạt |

Nguồn: [audit vai trò hiện tại](../../artifacts/restructure-v2/maintenance-role-current-audit.json), [schema delta](DATABASE_SCHEMA_DELTA.md), [SchemaGate](../../src/HotelOps.Api/Compact/Infrastructure/SchemaGate.cs). Không dùng chuỗi `3.0.0` để suy ra backup 49 bảng tương thích với V2. Gate hiện yêu cầu 57 bảng, 234 FK, 116 CHECK, 38 trigger đang bật, FK/CHECK trusted và truy vấn được mọi scalar mapping, gồm sáu view.

## Xem bản V2 mà không đổi User Secrets

Dùng TEST đã xác minh để review. Không dùng `Start-P0Api.ps1`: script đó trỏ tới database P0 cũ. Không chạy server review cùng lúc với các fixture đang giữ cổng 5779. Chọn cổng riêng còn trống, ví dụ 5781/API và 5175/Vite; kiểm tra cổng trước khi khởi chạy, không dừng server khác để lấy cổng.

Trong terminal PowerShell riêng ở repository root:

```powershell
if (Get-NetTCPConnection -LocalPort 5781 -State Listen -ErrorAction SilentlyContinue) { throw 'Port 5781 đang được sử dụng' }
$reviewConfigPath = Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json'
$reviewSettings = Get-Content -LiteralPath $reviewConfigPath -Raw | ConvertFrom-Json
$reviewBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($reviewSettings.'ConnectionStrings:HotelOpsDb')
$reviewBuilder['Initial Catalog'] = 'HotelOpsCompactWorkdayTestV22611250010'
$env:ConnectionStrings__HotelOpsDb = $reviewBuilder.ConnectionString
$env:Compact__WorkerEnabled = 'false'
$env:Compact__CleaningOverdueEnabled = 'false'
$env:Compact__AutoEndShiftsEnabled = 'false'
$env:Email__Enabled = 'false'
$env:AI__Enabled = 'false'
dotnet run --project src/HotelOps.Api --no-launch-profile -- --urls http://127.0.0.1:5781
```

Không in `$reviewSettings`, `$reviewBuilder` hoặc biến connection string. Các giá trị trên chỉ nằm trong process terminal; đóng terminal sau khi review. Tắt worker ở phiên review để không tự làm thay đổi dữ liệu khi chỉ kiểm giao diện; đây **không** phải cấu hình nghiệm thu tự kết thúc ca.

Terminal khác ở repository root:

```powershell
$env:API_PROXY_TARGET = 'http://127.0.0.1:5781'
npm --prefix frontend run dev -- --host 127.0.0.1 --port 5175 --strictPort
```

Kiểm `http://127.0.0.1:5781/health` trước khi mở `http://127.0.0.1:5175/login`. Đăng nhập bằng Username hiện có trên TEST; không tạo mật khẩu chung hoặc đổi mật khẩu tài khoản cũ. Khi xong, Ctrl+C ở hai terminal. Phiên review có thể ghi dữ liệu TEST nếu người dùng thao tác nghiệp vụ; không xem đây là chế độ read-only của sản phẩm.

## Đường nâng cấp local cần hoàn thiện và kiểm chứng

Gói review hiện có [manifest 11 bước và SHA256](rollout-manifest.json) cùng [preflight offline](../../scripts/verification/v2-rollout-preflight.mjs). Manifest ghi thứ tự phụ thuộc, guard database TEST, điều kiện baseline và các bất biến cần đối chiếu. Chạy từ repository:

```powershell
node scripts/verification/v2-rollout-preflight.mjs
```

[Kết quả đã chạy](../../artifacts/restructure-v2/rollout-offline-preflight.json): kiểm 11 hash, dependency order, guard TEST và hash/schema gate đạt. Script chỉ đọc file; không đọc secrets, kết nối SQL, backup, restore hoặc áp dụng migration. Script từ chối mọi tham số, kể cả `--apply`/tên database. Đây **không phải migration runner** và không chứng nhận dữ liệu hiện tại đáp ứng điều kiện SQL. Nếu hash thay đổi, phải review nội dung rồi cập nhật manifest; không tự sinh lại manifest trong preflight để bỏ qua thay đổi.

Các guard có chủ đích cần giữ khi chuẩn bị rehearsal: foundation batch/reusable từ chối reseed; recovery yêu cầu provenance P0 chính xác và không có việc HK/allocation đang mở; mapping vật tư kiểm mã/ID baseline; ấm đun yêu cầu đúng 200=103+97 và từ chối đăng ký dở dang; settlement kiểm custody và định nghĩa trigger gốc. Không được sửa tên database hàng loạt để vượt các guard này. Manifest hiện là gói delta cho baseline đã biết, không phải bộ khởi tạo database rỗng.

1. Chốt bản source/binary, đối chiếu schema/property với manifest thực tế. Chốt hợp đồng phát hành V2 57 bảng; yêu cầu backup 49 bảng cũ không đồng thời đáp ứng binary mới. Không hạ gate về 49 để cho khởi động.
2. Chụp audit chỉ đọc từ database nguồn: schema, constraint trust, tổng số dư/ledger, allocation còn mở, công việc đang hoạt động, provenance role và phiên người dùng. Đối chiếu tại thời điểm cutover, không tái sử dụng audit buổi sáng như trạng thái hiện tại.
3. Tạo backup mới bằng [Backup-Workday.ps1](../../scripts/database/compact-v2/Backup-Workday.ps1):

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/compact-v2/Backup-Workday.ps1 -Database HotelOpsCompactDB
   ```

   Script thực hiện `COPY_ONLY, CHECKSUM` và `RESTORE VERIFYONLY ... WITH CHECKSUM`; metadata ở `artifacts/workday/backup-HotelOpsCompactDB.json`. Không dùng `WITH REPLACE`, không restore đè DB đang có. Rehearsal phải restore vào tên TEST mới chưa tồn tại, từ backup nguồn mới, không sao chép ngược TEST hiện có có chứng từ synthetic vào vận hành.
4. Chuẩn bị migration runner cho đích rehearsal/cutover được chỉ định rõ: xác minh database nguồn/đích, version, backup, hash script; lưu checkpoint và audit từng bước. **Chưa có runner production V2 đã kiểm chứng.** Các SQL dưới đều khóa đúng tên TEST hiện tại; không xóa guard hay thay chuỗi tên hàng loạt rồi chạy trên local.
5. Thứ tự phụ thuộc đã áp dụng trên TEST:

   | Bước | Script | Điều kiện chính |
   |---|---|---|
   | Vai trò | `v2-role-provenance.sql` → `v2-recover-maintenance.sql` → `v2-checklist-permissions.sql` | Audit `P0_ROLE_MIGRATION/261124-v1` xác định đúng người; không suy role từ tên |
   | Assessment | `v2-room-assessment.sql` → `v2-assessment-guards.sql` | Giữ reservation, số dư và lịch sử cũ |
   | Tài khoản | `v2-invitations.sql` | Không đổi password hash; email thật vẫn tắt |
   | Kho | `v2-inventory-batches.sql` → `v2-reusable-inventory.sql` | Không bịa expiry, tồn kho hoặc tình trạng vật lý |
   | Đối soát | `v2-reusable-legacy-settlement.sql` → `v2-kettle-pending-assets.sql` | Chỉ chuyển phần có provenance; phần chưa rõ chờ kiểm kê |
   | Định mức B | `v2-reusable-shift-configuration.sql` | Profile REUSABLE phù hợp, giữ revision guard |

   Script nằm tại [scripts/database/compact-v2](../../scripts/database/compact-v2). Đây là thứ tự thiết kế, không phải tập lệnh có thể chạy lặp tùy ý; có DDL tạo bảng/constraint phải kiểm checkpoint trước khi chạy lại.
6. Recovery bảo trì phải dừng khi người bị chuyển trước đây còn công việc HK hoạt động hoặc allocation chưa đóng. Phục hồi membership/lịch tương lai đúng provenance, giữ lịch đã bắt đầu/lịch sử, thu hồi session liên quan để quyền mới có hiệu lực. Script không thay credential. Chỉ bật role không đủ để phục hồi 10 người, membership và quyền đúng.
7. Sau rehearsal: DBCC CHECKCONSTRAINTS, kiểm FK/CHECK trusted, schema gate, đối soát tổng sở hữu/ledger trước–sau; chạy role/branch denial, ca, assignment nội bộ, checklist, nghiệm thu kỹ thuật/HKS, tồn kho và transfer. Ghi rõ PASS/FAIL từng ca, không suy từ build.
8. Cutover local chỉ sau khi rehearsal từ backup mới đạt và gói thay đổi có thể review. Tạm ngưng ghi nghiệp vụ trong cửa sổ chuyển đổi, backup cuối, áp dụng đúng migration đã review, chạy smoke rồi mới mở ghi. Nếu thất bại, giữ log và database lỗi để điều tra; phục hồi sang database mới để đối chiếu trước khi đổi ứng dụng, không reset/ghi đè dữ liệu phát sinh.

## Những phần không được tự hoàn tất bằng dữ liệu giả

- 200 hồ sơ ấm đun chờ kiểm kê thực tế theo D11; không tự xác nhận chiếc đang dùng/mất hoặc tạo thêm tồn. Chứng từ cũ và tổng sở hữu được bảo toàn.
- Lô legacy thiếu nguồn/hạn dùng, số dư reusable chưa đối soát: tiếp tục trạng thái chờ kiểm kê/LEGACY_REVIEW theo bằng chứng; không biến thành READY để làm test xanh.
- Chênh lệch điều chuyển cuối cùng giữ chờ đối soát theo D12, không tự ghi mất hoặc hoàn kho.
- AI ngoài và SMTP thật: `BLOCKED_EXTERNAL_CONFIGURATION`. AI cần `AI__Provider`, `AI__Model`, `AI__ApiKey`, `AI__Enabled`. Email cần `Email__SmtpHost`, `Email__SmtpPort`, `Email__Username`, `Email__Password`, `Email__FromAddress`, `Email__Enabled`, `Email__PublicBaseUrl`, `Email__UseTls`, `Email__AllowLiveDelivery`. Secret chỉ cấu hình trong User Secrets/environment/secret store; không commit. Chưa được phép gửi tới nhân viên thật. Xem [AI](AI_INTEGRATION_DESIGN.md) và [email](ACCOUNT_INVITATION_SECURITY.md).

## Trạng thái bàn giao tài liệu

Đã đối chiếu script, gate và artifact hiện có; preflight offline và `node --check` đạt. Chưa chạy lệnh preview/cutover trong tài liệu, chưa sửa DB local, chưa tạo backup phát hành V2 hoặc kiểm restore trên SQL Server Express Linux. Kiểm Linux/EC2 cần restore rehearsal và healthcheck của đúng binary; `VERIFYONLY` trên Windows không thay thế kiểm đó. Gói vẫn `BLOCKED_REHEARSAL_AND_TARGET_SPECIFIC_MIGRATION_REVIEW`; không dùng kết quả offline để tuyên bố sẵn sàng Production.
