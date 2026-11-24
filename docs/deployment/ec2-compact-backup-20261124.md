# Backup Compact 3.0.0 cho hotelops.ap.vn

Đã có backup phù hợp, tạo ngày 24/11/2026 lúc 22:53 giờ Việt Nam từ `HotelOpsCompactDB`. Người dùng xác nhận cho phép dùng dữ liệu và tài khoản Compact local hiện có trên EC2. Không dùng database legacy, không reset hoặc sửa dữ liệu nguồn.

## File bàn giao

Thư mục: `artifacts/ec2-backup-261124/package/`.

- `HotelOpsCompactDB_schema3_20261124_ec2.bak`: **29.058.560 byte**, khoảng **27,71 MiB**.
- `SHA256SUMS`: kiểm tra file sau khi chuyển lên máy chủ.
- `manifest.json`: phiên bản, commit, metadata backup và kết quả kiểm chứng.
- `restore-linux.sql`: restore sang database mới, từ chối nếu `HotelOpsCompactDB` đã tồn tại; không có `WITH REPLACE`.
- `api-health.json`, `database-audit.json`: bằng chứng từ chính bản restore của file bàn giao.

SHA-256 của file `.bak`:

```text
9831086ce5de9c31f55728209ff41ab01c2a350543539bf2d95851f85458c93a
```

## Phiên bản và dữ liệu

| Hạng mục | Kết quả |
|---|---|
| SQL Server nguồn | SQL Server 2022 Express, 16.0.1000.6, 64-bit |
| Compatibility level | 160 |
| HotelOpsCompactSchema | 3.0.0 |
| Bảng / view | 49 / 6 |
| Foreign key / CHECK / trigger | 205 / 97 / 38 |
| Constraint không trusted hoặc bị tắt | 0 |
| Trigger bị tắt | 0 |
| File dữ liệu | 72 MiB; đang dùng khoảng 27,44 MiB |
| File log | 72 MiB |
| Tài khoản | 57; 55 đang hoạt động |
| Quản trị | 1 tài khoản hoạt động có SYS_ADMIN |
| Chi nhánh / phòng hoạt động | 2 / 96 |
| Chứng từ | 199, trong đó 194 có mã TEST |
| Attachments | 0; bản này không có tệp minh chứng cần chuyển riêng |

Backup giữ nguyên tài khoản và password hash. Đăng nhập bằng thông tin hiện có; không có mật khẩu quản trị chung mới. Không chạy `--bootstrap-admin`, seed TEST hay reset sau restore. Tài khoản SQL dùng để kết nối database khác với tài khoản đăng nhập HotelOps; SQL login cấp server không nằm trong database backup.

Đây là bộ dữ liệu local có chứng từ kiểm thử đã được người dùng cho phép sử dụng, không phải chứng từ xác nhận giao nhận thực tế.

## Tương thích nhánh

- Nhánh: `feature/project2-role-alignment-20261123`.
- Commit được đối chiếu: `ea212388904d389c906d92136470baada8c19501`.
- `SchemaGate`, toàn bộ Compact EF mappings và entities không khác HEAD tại thời điểm bàn giao.
- Workspace có nhiều thay đổi ứng dụng chưa commit. Backup không mang theo code; checkout nhánh trên EC2 chỉ có các thay đổi đã commit/push. Không tự commit hoặc push trong công việc này.
- API local Release đã mở thành công trên bản restore, xác thực SchemaGate và biên dịch truy vấn rỗng cho tất cả mappings. Health trả `{"status":"ok","schema":"3.0.0","tables":49}`.

## Linux / Express

Nguồn vốn là Express, không có persistent SKU features, TDE, user CLR assemblies, memory-optimized tables, database-scoped credentials hoặc Windows database principals. Dung lượng nhỏ hơn giới hạn 10 GB của Express 2022. Đã đối chiếu [tính năng SQL Server 2022 trên Linux](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2022?view=sql-server-ver16).

Microsoft hỗ trợ chuyển database từ Windows sang Linux bằng backup/restore; phải chuyển đường dẫn file vật lý bằng `WITH MOVE`. Xem [hướng dẫn chính thức](https://learn.microsoft.com/en-us/sql/linux/migrate/restore-database?view=sql-server-ver17).

**Chưa restore trực tiếp trên EC2/Linux** trong phiên này. Bằng chứng thực nghiệm là SQL Server 2022 Express trên Windows, cùng phiên bản nguồn, và API trên bản restore. Script Linux được chuẩn bị theo logical filenames thực tế; cần chạy kiểm chứng trên máy EC2 đích.

## Restore trên EC2

Các lệnh dưới đây giả định SQL Server 2022 chạy trực tiếp trên Linux với thư mục mặc định. Nếu chạy Docker, đưa file và script vào filesystem/container chứa SQL Server trước khi chạy. Không dùng thư mục public của website để đặt backup.

1. Chuyển toàn bộ thư mục `package` qua SCP/SFTP vào thư mục riêng trên EC2. Không đưa `.bak` vào Git. Tại thư mục chứa file:

```bash
sha256sum -c SHA256SUMS
sudo install -d -o mssql -g mssql -m 750 /var/opt/mssql/backup
sudo install -o mssql -g mssql -m 640 \
  HotelOpsCompactDB_schema3_20261124_ec2.bak \
  /var/opt/mssql/backup/HotelOpsCompactDB_schema3_20261124_ec2.bak
```

2. Dùng tài khoản SQL có quyền backup/restore. Ví dụ `sa`; thay tên nếu tài khoản quản trị SQL đã đổi. Nhập mật khẩu qua prompt, không đặt mật khẩu trong command line hoặc Git:

```bash
read -rsp 'SQL administrator password: ' SQLCMDPASSWORD
echo
export SQLCMDPASSWORD
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b \
  -i restore-linux.sql
unset SQLCMDPASSWORD
```

`-C` dùng cho kết nối SQL localhost với certificate tự ký. Nếu dùng certificate được tin cậy, bỏ `-C`. Script kiểm tra engine 16.x, dừng nếu database đã có, chạy VERIFYONLY WITH CHECKSUM, restore WITH MOVE/CHECKSUM, kiểm tra schema gate và DBCC CHECKDB. Không bỏ điều kiện từ chối hoặc thêm REPLACE để vượt qua lỗi database đã tồn tại.

Logical filenames đã xác minh là `HotelOpsCompactDB` và `HotelOpsCompactDB_log`. Đích mặc định là `/var/opt/mssql/data/HotelOpsCompactDB.mdf` và `/var/opt/mssql/data/HotelOpsCompactDB_log.ldf`. Nếu đổi tên database, dùng tiền tố `HotelOpsCompact` và chỉnh cả guard, RESTORE, USE, đường dẫn cùng connection string. Chỉ chọn tên/đường dẫn mới chưa tồn tại.

3. Cấu hình SQL login riêng cho ứng dụng bằng mật khẩu mới được lưu trong secret của máy triển khai. Nếu đã có login, tạo/mapping database user tương ứng. Quyền cần cho schema hiện tại gồm đọc/ghi bảng, EXECUTE và VIEW DEFINITION để SchemaGate thấy đầy đủ cấu trúc. Không dùng tài khoản SQL Windows local làm connection string trên Linux.

```sql
-- Chạy trong HotelOpsCompactDB sau khi DBA đã tạo server login hotelops_app.
CREATE USER [hotelops_app] FOR LOGIN [hotelops_app];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [hotelops_app];
GRANT VIEW DEFINITION TO [hotelops_app];
```

4. Đặt `ConnectionStrings__HotelOpsDb` của API trỏ đúng database vừa restore. Database First: không chạy `dotnet ef database update`. Full backup đã chứa schema, các bản cập nhật SQL và dữ liệu; không cần chạy lại migration/seed. Không dùng `Install-Level2.ps1`, `Seed-Testing.ps1`, `Reset-DemoReady.ps1` hay `--bootstrap-admin` cho database này.

5. Khởi động API. Kiểm tra `/health` tại cổng API hoặc URL reverse proxy thực sự được cấu hình trên EC2; kết quả phải có `schema=3.0.0`, `tables=49`. Sau đó đăng nhập bằng tài khoản HotelOps hiện có và kiểm tra đúng chi nhánh/quyền. Nếu SchemaGate không đạt, giữ database để chẩn đoán; không reset.

Database chứa phiên đăng nhập local còn hiệu lực tại thời điểm backup. Khi đưa bản clone lên môi trường công khai, nên thu hồi các phiên cũ trên **database EC2** bằng quy trình quản trị đã duyệt trước khi mở truy cập. Công việc này không tự thu hồi hoặc đổi mật khẩu trên local.

## Bằng chứng và phạm vi kiểm chứng

- `BACKUP ... WITH COPY_ONLY,CHECKSUM`: Passed; header `HasBackupChecksums=true`, `IsCopyOnly=true`, `IsDamaged=false`, backup FULL, không nén/không mã hóa.
- `RESTORE VERIFYONLY ... WITH CHECKSUM`: Passed.
- Restore thực tế từ file bàn giao vào `HotelOpsCompactWorkdayTestEC22611242258`: Passed.
- `DBCC CHECKDB` và toàn bộ schema gate: Passed.
- API `/health` và EF mapping: Passed; API tạm đã dừng sau kiểm chứng.
- Không sửa source C# hoặc frontend; build/unit test không chạy lại trong lượt chỉ tạo backup này. API dùng bản Release đã build/test ở lượt trước.
- Không truy cập/deploy EC2, không reset, không sửa schema hoặc dữ liệu nguồn, không commit/push.
- Hai database kiểm chứng mới được giữ lại: `HotelOpsCompactWorkdayTestEC22611242249` và `HotelOpsCompactWorkdayTestEC22611242258`. Không tự DROP dữ liệu.

Thư mục backup hệ thống chặn đọc file trực tiếp. Lượt xuất qua SQL cũng không vượt được quyền Windows; giải pháp cuối cùng là backup trực tiếp vào thư mục bàn giao riêng, chỉ cấp quyền cho dịch vụ SQL tại thư mục đó. File bàn giao cuối đã được VERIFYONLY và restore lại độc lập; không thay đổi ACL thư mục hệ thống.
