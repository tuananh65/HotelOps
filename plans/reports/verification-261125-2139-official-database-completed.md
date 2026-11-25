# Hoàn tất dọn test và chuyển database chính thức

Ngày25/11/2026, Owner xác nhận xóa MR440/ISSUE441 cùng nhóm test đã chứng minh. Đã áp dụng lên HotelOpsCompactDB, không restore ghi đè database và không chuyển dữ liệu từ database test vào database chính.

## Kết quả thực tế

- Xóa196 chứng từ:194 fixture AUTHORIZED_ROOM_TEST_DATA và2 chứng từ được Owner xác nhận bổ sung. Xóa các dòng, stock movements, document events và thông báo phụ thuộc thuộc nhóm này.
- Lưu toàn bộ bản ghi xóa trong before_json của audit AUTHORIZED_TEST_CLEANUP, operation_key be99df91-a554-482a-b90c-ce71b5de6066. Audit gốc giữ nguyên. Workflow666 và4 sự kiện giữ nội dung/lịch sử, chỉ gỡ tham chiếu chứng từ440.
- Giữ57 tài khoản và mật khẩu,15 danh mục vật tư,5 chứng từ ngoài nhóm,46 stock movements và32 công việc. Không âm kho; không tự thêm nguồn nhập hoặc xác nhận kiểm kê thực tế.
- Nâng49 lên57 bảng,234 FK,116 CHECK,38 trigger; mọi constraint trusted và trigger bật. Khôi phục10 Maintenance Staff theo provenance hiện có.
- Không tạo200 tài sản ấm từ nguồn test đã xóa. Item222 vẫn là danh mục ASSET, đăng ký0 tài sản từ nguồn tổng hợp này; giữ các tài sản khác.
- Production https://localhost:5080 thực sự dùng HotelOpsCompactDB. Development http://127.0.0.1:5173/API5081 vẫn dùng clone Development riêng, không bị dọn hoặc đồng bộ.

## Kiểm chứng

- Backup trước thao tác và backup sau cùng đều COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY WITH CHECKSUM đạt.
- Clone diễn tập HotelOpsCompactWorkdayTestOfficial2611252140: rollback rehearsal đạt; commit rehearsal đạt; API thật khởi động với worker tắt, toàn bộ scalar mappings/views qua SchemaGate và health đạt.
- Official áp dụng cùng hash script và fingerprint dữ liệu đầu vào, cleanup+nâng cấp trong một transaction. Bảo toàn dữ liệu ngoài phạm vi, DBCC CHECKCONSTRAINTS và đối soát tồn đạt.
- Script verify-official-database.ps1 kiểm trực tiếp:196 removed,57 tables,57 users,15 items,5 retained documents,46 retained movements,32 retained work,10 Maintenance Staff, credential preserved, nonnegative stock.
- Edge kiểm hai môi trường: login/hiện mật khẩu/quên mật khẩu mobile, không tràn ngang, HTTPS chứng chỉ thật; API401/404, SignalR401 và static hosting đúng. Không đăng nhập bằng mật khẩu thật, không reset mật khẩu hoặc gửi email thật trong smoke.
- Build/test C# không chạy lại vì lượt này chỉ đổi SQL/PowerShell/tài liệu; binary Release hiện tại trước đó build0 lỗi/0 cảnh báo và158 test passed/3 skipped. Không coi3 test bỏ qua là đạt. Email/AI ngoài vẫn tắt như cấu hình đã chốt.
- Lần rehearsal đầu lỗi temp table kế thừa IDENTITY; đã sửa khai báo khóa bảng tạm rõ ràng và chạy lại đạt. Database chính không bị ghi ở lần lỗi.

## Backup sau hoàn tất

`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261125_215147796.bak`

Metadata: artifacts/workday/backup-HotelOpsCompactDB.json. Backup trước cutover vẫn lưu riêng tại đường dẫn trong artifacts/official-v2-upgrade/production.json. Không xóa các backup hay clone diễn tập.

## Files của đợt chuyển đổi

- scripts/database/compact-v2/Invoke-OfficialV2Upgrade.ps1
- scripts/database/compact-v2/official-cleanup-room-test.sql
- scripts/database/compact-v2/official-kettle-after-cleanup.sql
- scripts/local/inspect-official-cleanup.ps1
- scripts/local/verify-official-database.ps1
- scripts/local/verify-official-rehearsal.ps1
- scripts/local/run-local.ps1
- docs/deployment/local-development-production.md
- plans/261125-2122-official-database/plan.md và các báo cáo kiểm kê/diễn tập liên quan.

Không commit/push hoặc triển khai EC2. Các công việc/phân ca không có bằng chứng test được giữ theo phạm vi Owner yêu cầu; archive kiểm toán vẫn có tên và nội dung fixture để truy vết, không còn là chứng từ vận hành.
