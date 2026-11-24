# Kiểm chứng API UX refinements — 23/11/2026

Đã hoàn thành bộ kiểm thử được giao: **30/30 nhóm Passed, 0 Failed** trên HTTP thật và SQL Server fixture mới. Kết quả áp dụng cho assembly có sẵn lúc chạy; chưa chứng minh ba thay đổi source mới hơn assembly nêu dưới đây. Không phát hiện lỗi sản phẩm được tái hiện trong phạm vi đã chạy.

## Phạm vi và lệnh tái hiện

Nguồn yêu cầu: `plans/261123-ux-role-refinements/api-validation.md` và ba báo cáo accounts, workday-refinements, operations integration được chỉ định. Chỉ sửa script kiểm thử và harness; không sửa mã ứng dụng, schema nguồn, quyền hoặc dữ liệu primary.

```powershell
& scripts/database/compact-v2/Test-Backend.ps1 -UxRefinements -Port 5192
```

Chạy từ `D:/SEP490/Projects/TestFE/HotelOps`, Windows PowerShell, SQL Express với Windows authentication. Cần chạy ngoài sandbox trên máy này do SQL SSPI. Mỗi lần harness tự tạo một database có tên ngẫu nhiên, từ chối cổng đang bận, tạo thư mục bằng chứng riêng và dừng đúng PID API của mình trong `finally`.

## Kết quả

| Nhóm | Bằng chứng HTTP/SQL thực |
| --- | --- |
| Startup/authentication | Schema 2.1.0 với 40 bảng; anonymous bị từ chối 401. |
| Tài khoản | Mã NV tự sinh; hai request tạo đồng thời có mã khác nhau; mật khẩu đúng 8 ký tự được chấp nhận, thiếu độ dài/độ phức tạp bị từ chối; họ tên trùng được phép; username sau trim/email trùng trả 409. |
| Membership/quyền tạo | User, BRANCH_ACCESS primary, EMPLOYMENT và audit cùng được ghi; không tự cấp role. Branch thiếu/không tồn tại hoặc department khác branch trả 400 và không để lại dữ liệu. Người chỉ có quyền branch không tạo được tài khoản global. |
| Hồ sơ cá nhân | Đổi mật khẩu 8 ký tự thu hồi session cũ; tài khoản không có quyền admin tự sửa hồ sơ; trường userId/status/role/branch bổ sung không thay đổi danh tính hay quyền. Stale version trả 409; email trùng giữ nguyên hồ sơ và audit. |
| Danh mục | Sinh mã branch/department/room type/location/room/asset; trùng tên department trong cùng branch bị chặn, khác branch được phép; sửa khác branch bị chặn. Liên hệ chi nhánh round trip, cập nhật version và stale guard; metadata liên hệ không lộ trong reference catalog. |
| Transaction danh mục | Tạo branch rồi gặp representativeName sai kiểu trả 400 và rollback branch đã được SaveChanges cùng audit. |
| Permission guard | Vô hiệu hóa ROOM_VIEW đang được tài khoản active sử dụng trả 409 PERMISSION_USED_BY_ACTIVE_USER; giá trị và row version không đổi. |
| Assets | Nhãn phòng/vị trí đúng với hai tài sản riêng theo constraint; tài sản chưa đặt vị trí/ngừng sử dụng vẫn có trong danh sách; không lộ tài sản khác branch; list/detail khác branch trả 403. |
| Dashboard | Day/week/month theo lịch UTC+7, tuần bắt đầu thứ Hai; dữ liệu ở trước cận dưới, đúng cận dưới, trước cận trên và đúng cận trên được so với SQL. Count khớp, kỳ sai trả 400, branch ngoài quyền trả 403. |
| Workday cấu hình | Đọc department/role hợp lệ, tạo ca, từ chối department khác branch và người ngoài phạm vi. |
| Workday hàng loạt | Hai người × toàn bộ 31 ngày tháng 12/2026 = 62 assignment, 62 event và 62 audit. Candidate intersection và sức chứa phản ánh trạng thái đã ghi. |
| Workday từ chối/rollback | Replay, vượt capacity, nhân sự không hợp lệ, ngày sau hết chỗ, branch ngoài quyền, user/date trùng, chọn khác tháng đều bị chặn; không có bản ghi dở dang. |
| Workday lỗi lúc ghi | Chèn trước một child operation key trong fixture để gây unique conflict ở bước ghi event, sau khi assignment đã được insert. API trả 409 DUPLICATE_RECORD; assignment/event/audit mới đều rollback. |
| Workday lịch sử/version | Có lịch sử thì không đổi giờ; không giảm capacity dưới số đã phân; không ngừng ca còn PLANNED; version cũ khi phân ca bị chặn. |

**Build: Not Run** theo lệnh không full build; không sửa C#. **Test: Passed** cho suite API/SQL này. `dotnet test HotelOps.slnx`: Not Run trong nhiệm vụ kiểm chứng cô lập. PowerShell parser và `git diff --check` trên harness: Passed. Git có cảnh báo LF sẽ chuyển CRLF, không phải lỗi build/test.

## Bằng chứng và lịch sử thất bại

- Lượt sandbox ban đầu: lỗi `Cannot generate SSPI context` trước khi tạo database; không chạy được test.
- Lượt HTTP/SQL thứ nhất: **26 Passed, 4 Failed**, database `HotelOpsCompactApiTesta9eb664ee1f3`. Giữ nguyên [receipt lần đầu](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTesta9eb664ee1f3/ux-verification.json).
  - Hai request tạo đồng thời gặp lỗi gửi HTTP, chưa đủ inner-exception evidence để kết luận nguyên nhân gốc. Lượt sau dùng HttpClient trực tiếp loopback, tắt proxy và thêm ghi base exception; cả hai request qua.
  - Fixture asset truyền cả roomId và locationId, vi phạm `CK_assets_location`. Sửa fixture thành hai tài sản riêng, giữ nguyên constraint và API.
  - Ca capacity dùng user từ ca concurrency bị lỗi, nên nhận 400 do thiếu ID. Đổi sang worker hợp lệ sẵn có để kiểm tra capacity độc lập.
  - Kiểm tra fingerprint phát hiện `CatalogService.cs` thay đổi trong lúc chạy. Đây là thay đổi ngoài nhiệm vụ, không sửa/hoàn tác.
- Lượt xác minh sau sửa test: **30 Passed, 0 Failed**, exit 0, database `HotelOpsCompactApiTestff9e3b39d4e7`.
- [Receipt cuối](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTestff9e3b39d4e7/ux-verification.json), [inputs trước](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTestff9e3b39d4e7/inputs-before.json), [inputs sau](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTestff9e3b39d4e7/inputs-after.json), [log API](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTestff9e3b39d4e7/api.log).
- SHA256 receipt cuối: `802DE1BB5513D7D2C405D93C5534AE75AC1173C03A07073F16407EB7E0753458`.

## Tính mới của bản được kiểm thử

Nhánh `feature/project2-role-alignment-20261123`, worktree có nhiều thay đổi từ trước và các phiên khác. Assembly `src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll` có thời gian sửa 10:13:16 ngày 23/11/2026, SHA256 `0401DBD4877404BC365A96D7FE5CF174351D02302A9C97F880A81E83783B4AC6`. Fingerprint source và binary ổn định suốt lượt cuối.

Ba tệp source mới hơn binary: `Compact/Controllers/WorkspaceController.cs`, `Compact/Services/CatalogService.cs`, `Compact/Services/WorkdayCalendar.cs`. Đặc biệt logic mới sinh số phòng theo tầng trong Catalog chưa nằm trong binary đã chạy. Không khẳng định suite này xác minh bản source tích hợp cuối. Parent cần build bản cuối rồi chạy lại lệnh trên. Không có kiểm thử tải lớn, chứng minh mọi race condition hoặc kiểm tra UI trong suite này.

## Backup, dọn tiến trình và tệp thay đổi

Harness hiện hữu backup trước nâng schema; switch mới thêm backup xác minh sau nâng schema, trước bootstrap và các fixture mutations. [Receipt backup fixture cuối](../../artifacts/workday/backup-HotelOpsCompactApiTestff9e3b39d4e7.json) xác nhận COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY. Không backup/restore/ghi database primary.

API lượt đầu PID 49076 đã dừng. API lượt cuối PID 2576 đã dừng; cổng 5192 trống, có [cleanup receipt](../../artifacts/compact-v2/backend/ux-HotelOpsCompactApiTestff9e3b39d4e7/cleanup.json). Hai fixture và backup được giữ lại để chẩn đoán theo harness; không xóa database. Không commit/push.

Tệp do nhiệm vụ này tạo/sửa:

- `scripts/database/compact-v2/test-ux-refinements.ps1` — mới, các kiểm tra và receipt/fingerprint.
- `scripts/database/compact-v2/Test-Backend.ps1` — thêm switch `-UxRefinements`, thư mục riêng, backup và nhánh chạy suite; giữ nguyên thay đổi HousekeepingSupervisor có sẵn.
- `plans/reports/validation-261123-1016-ux-api.md` — báo cáo này.
- Bằng chứng fixture dưới `artifacts/compact-v2/backend/ux-*` và receipt backup dưới `artifacts/workday`.
