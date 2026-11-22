# Biên bản kiểm chứng Compact v1 — 21/11/2026

## Database

| Hạng mục | Kết quả thực tế |
|---|---|
| Database mới | HotelOpsCompactDB, SQL Server 16.0.1000.6 (2022), instance local SQLEXPRESS |
| Database nguồn | HotelOpsDB, 149 bảng; chỉ đọc metadata |
| Bảng mới | **40**, khớp danh mục |
| FK | **159**, không disabled/untrusted, không cascade delete |
| CHECK | **78**, không disabled/untrusted |
| Trigger | **18**, enabled |
| View | **2** |
| DBCC CHECKCONSTRAINTS | Không trả về vi phạm |
| DBCC CHECKDB | Hoàn tất, không báo lỗi |
| Test tự động | **38 PASS / 0 FAIL** |
| Fixture tồn lại sau test | **0 dòng** |
| Inventory nguồn trước/sau | SHA-256 giống nhau; metadata schema không thay đổi |
| Chuyển dữ liệu/grants/connection string | Không thực hiện, theo lựa chọn database độc lập |

Các test SQL dùng dữ liệu tổng hợp trong transaction riêng và rollback. SQL Server có thể tăng IDENTITY dù transaction rollback; không reseed hoặc chỉnh ID sau test. Khóa ngoại, unique index, CHECK và trigger đều được kiểm tra bằng thao tác thực tế, không chỉ tìm chuỗi trong file SQL.

Test bao gồm: valid cleaning/inspection/rework, FK khác chi nhánh, primary branch trùng, workflow/status/result sai, sai room/cycle/parent, thay kind, findings sai loại, ACTIVE assignment/block trùng, idempotency sự kiện, audit/history bất biến, published checklist, typed UOM, receipt/balance, ghi ledger khi chưa post, sửa ledger/posted line, sai item, quantity âm/factor bằng 0, self approval, workday attribution/open interval, attachment nhiều owner, notification dedup, stale rowversion, document status, quantity mismatch, insufficient stock, exact reversal.

**Chưa kiểm chứng:** API chạy với Compact, full end-to-end, migration dữ liệu, tải nhiều connection, stress/deadlock/retry, worker notification, performance. Các test schema không thay cho acceptance nghiệp vụ.

## Mã nguồn và công cụ

- `dotnet build HotelOps.slnx --nologo --verbosity quiet`: **PASS**, 0 error, **2 warning NU1900**. NuGet không tải được vulnerability service index tại `https://api.nuget.org/v3/index.json` trong môi trường kiểm tra. Chưa xác nhận kết quả audit dependency; không tắt cảnh báo.
- Build trên backend hiện tại; không chứng minh backend tương thích Compact. Không thay đổi C#/EF/API/frontend.
- `dotnet test HotelOps.slnx`: **Not Run**. Đợt này kiểm thử SQL bằng `Test-Compact.ps1`; không chạy suite use case dành cho schema cũ.
- `Generate-Schema.mjs`: sinh đúng 40 bảng. `Build-Mapping.mjs`: **149/149 bảng nguồn**, không có bảng bỏ sót hoặc tên đích không tồn tại.
- Không commit/push/deploy.

## File bàn giao

| File | Mục đích |
|---|---|
| `scripts/database/compact/Inspect-Source.ps1` | Xuất system catalog, không xuất dữ liệu nghiệp vụ |
| `scripts/database/compact/Generate-Schema.mjs` | Sinh DDL/manifest từ định nghĩa Compact |
| `scripts/database/compact/01_Schema.sql` | 40 bảng, khóa/index/CHECK, extended property |
| `scripts/database/compact/02_Integrity.sql` | Trigger về provenance/history/workday và hai view |
| `scripts/database/compact/03_DocumentGuards.sql` | State/ledger quantity/negative-stock/reversal guards |
| `scripts/database/compact/Install-Compact.ps1` | Tạo DB mới, transaction toàn bộ schema; từ chối DB đã có |
| `scripts/database/compact/04_Verify.sql` | Kiểm tra catalog, CHECKCONSTRAINTS, CHECKDB |
| `scripts/database/compact/Test-Compact.ps1` | 38 ca rollback, xuất JSON kết quả |
| `scripts/database/compact/table-manifest.json` | Danh mục bảng/PK/số cột |
| `scripts/database/compact/Build-Mapping.mjs` | Kiểm đủ mapping trên inventory nguồn |
| `docs/database/compact/README.md` | Đánh giá, thiết kế, giới hạn và cách sử dụng |
| `docs/database/compact/source-to-target.csv` | Mapping 149 bảng nguồn |
| `docs/database/compact/mapping-summary.json` | Số liệu kiểm kê và coverage |
| `docs/database/compact/verification.md` | Biên bản này |

Các artifact local trong thư mục Git-ignored `artifacts/compact-db/`: source inventory trước/sau, inventory Compact, `verification.json` và `backend-build.log`. Chúng là chứng cứ lần chạy local, không cần đưa dữ liệu môi trường vào source control.
