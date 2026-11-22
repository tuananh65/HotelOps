# Kiểm thử chức năng Compact v2 — 22/11/2026

**Cập nhật sau sửa lỗi:** F-01 và F-02 đã được xử lý; lượt kiểm thử mới đạt **127 API / 0 lỗi**. Xem [INVENTORY-FIX-REPORT.md](INVENTORY-FIX-REPORT.md) để đọc thay đổi và bằng chứng mới nhất. Nội dung bên dưới giữ nguyên kết quả lịch sử trước khi sửa.

Đã kiểm thử backend `/api/v2` theo 15 nhóm FE-01–FE-15 trên schema mới. **Tại thời điểm kiểm thử ban đầu: còn hai lỗi nghiệp vụ kho, tái hiện trên hai database độc lập.** Lượt ban đầu bổ sung kiểm thử và báo cáo, không sửa C#, quyền production hoặc schema.

## Kết quả thực chạy

| Hạng mục | Kết quả |
|---|---|
| Build solution | **Passed**, 0 lỗi, 0 cảnh báo |
| Unit/model tests | **23 passed**; worker mặc định skip, chạy riêng bên dưới |
| HTTP tích hợp mở rộng | **112 passed, 2 failed** |
| HTTP trên dữ liệu mẫu `HotelOpsCompactDB` | **52 passed** |
| SQL regression, mỗi tình huống rollback | **58 passed**, không còn hàng fixture |
| Worker | **1 passed**: tạo occurrence, tiến lịch, chống trùng cảnh báo tồn kho |
| DBCC CHECKDB / CHECKCONSTRAINTS | Passed trên Compact chính và database HTTP kiểm thử |
| Cấu trúc sau kiểm thử | **37 bảng, 152 FK, 77 CHECK, 26 trigger, 6 view** |
| Dữ liệu mẫu | Số hàng của 36 bảng ngoài `user_sessions` không thay đổi |
| Database cũ | Không có thao tác ghi; hash inventory metadata trước/sau giống nhau |

Đăng nhập/đăng xuất tạo và thu hồi phiên, cập nhật lần đăng nhập; đọc inbox có thể ghi dấu đã giao thông báo. Các thao tác tạo/sửa/duyệt/ghi sổ/hủy chạy trong database kiểm thử riêng. Worker tắt trong các lượt HTTP.

Database: chính `HotelOpsCompactDB`; SQL rollback `HotelOpsCompactFunctionSql873809605d23`; HTTP lần đầu và worker `HotelOpsCompactApiTestce7872a793ca`; HTTP xác nhận cuối `HotelOpsCompactApiTest8e3cc5684a67`. Fixture được giữ để tái hiện. Giờ JSON là UTC; ngày báo cáo theo Asia/Saigon.

## Từng nhóm chức năng

“Đạt” chỉ áp dụng cho những tình huống đã chạy, không phải mọi use case/tổ hợp dữ liệu của nhóm.

| Nhóm | Tình huống thực chạy | Kết quả |
|---|---|---|
| FE-01 — Tài khoản/quyền | Đăng nhập 11 tài khoản mẫu; ẩn hash/stamp; đổi mật khẩu tạm, logout, khóa sau 5 lần sai; thêm/khóa/mở tài khoản; cấp/thu hồi membership và role; phạm vi chi nhánh; scope/quyền trùng; bảo vệ admin cuối | Đạt |
| FE-02 — Cơ cấu khách sạn | Danh mục chi nhánh/bộ phận/vị trí/loại phòng/phòng/kho/vật tư/role; thêm/sửa/ngừng dùng vị trí; rowversion; dependency; FK khác chi nhánh; permission chỉ đọc; cấm sửa thẳng trạng thái phòng | Đạt |
| FE-03 — Phòng | Phòng chưa sạch không nhận khách; READY cần kiểm tra; nhận/trả làm mất hiệu lực chu kỳ cũ; khóa/mở khóa; chuyển khách trong chi nhánh; lịch sử nguồn/đích; quyền Reception đúng role | Đạt |
| FE-04 — Buồng phòng | Tạo/phân công/nhận/bắt đầu/hoàn tất dọn; chờ kiểm tra; kiểm tra lỗi tạo nhu cầu làm lại; thực hiện làm lại và chờ tái kiểm tra | Đạt |
| FE-05 — Kiểm tra | Checklist snapshot; người kiểm tra độc lập; đạt/không đạt; lỗi gắn việc làm lại; xác nhận sửa lỗi; tái kiểm tra đóng việc làm lại; kết quả đã chốt không thêm lỗi | Đạt |
| FE-06 — Sự cố/bảo trì | Phân công/xử lý/nghiệm thu độc lập; triage deadline tương lai; escalation; chặn đóng sự cố khi bảo trì con chưa nghiệm thu; mở lại; lịch bảo trì tạo/ngừng dùng | Đạt |
| FE-07 — Tài sản/bằng chứng | Chuyển vị trí/phòng, tình trạng, chặn xác nhận sửa chưa nghiệm thu; bàn giao nhận/từ chối đúng người; thanh lý giữ lịch sử; upload/download giữ bytes và ẩn storage key; vô hiệu bằng chứng | Đạt |
| FE-08 — Kho | Nhập, quy đổi đơn vị, sửa dòng nháp, bất biến sau post, hủy nháp; trả hàng có duyệt, giới hạn lượng; chuyển/nhận nhiều lần/chặn thừa; kiểm kê/điều chỉnh tăng giảm/chặn kiểm kê cũ; đảo bút toán/chống đảo hai lần | **Lỗi F-01: trạng thái nhận chuyển kho** |
| FE-09 — Vật tư | Phân nhiệm người yêu cầu/duyệt/xuất; quyền đúng role; gửi/bổ sung/gửi lại; duyệt giảm số lượng/chặn vượt; xuất một phần; xuất vượt rollback header/ledger; đóng phần còn lại chặn xuất tiếp | Đạt |
| FE-10 — Bổ sung vật tư | Tạo yêu cầu; nhận một phần/toàn phần và trạng thái; chặn nhận thừa; đóng yêu cầu | **Lỗi F-02: nhập sau khi đóng** |
| FE-11 — Công việc | Nhận trước khi làm; tiến độ đúng người và 0–100; pause/resume/hoàn tất/xác minh; operation key/rowversion; bàn giao nhận/từ chối/đúng người/không quyết định hai lần; người đủ điều kiện | Đạt |
| FE-12 — Thời gian | Timer không phụ thuộc ngày công; một timer mở/người; hủy/bàn giao đóng timer; lịch sử đã chốt/chống chồng lấn tại SQL; API thời gian mẫu | Đạt |
| FE-13 — Yêu cầu nội bộ | Tạo/gửi/route/nhận/xử lý/người yêu cầu xác nhận; route trước từ chối; đã từ chối không bắt đầu; phụ thuộc và chống chu trình | Đạt |
| FE-14 — Thông báo/dashboard | Phát sinh từ nghiệp vụ; chỉ người nhận đọc/ack và lưu thời điểm; dashboard có dữ liệu; worker tạo việc và chống lặp cảnh báo tồn kho | Đạt |
| FE-15 — Báo cáo/audit | Báo cáo việc/dọn/sự cố/bảo trì/yêu cầu/tồn/chứng từ; phạm vi chi nhánh; khoảng ngày và tên báo cáo; audit có actor/entity/action, không lộ credential | Đạt |

## F-01 — Nhận đủ nhưng phiếu chuyển vẫn IN_TRANSIT

Mức độ đề xuất **P2 — sai trạng thái nghiệp vụ/tra cứu**. UC-084/GBR-104; chức năng này được giữ trong báo cáo giản lược.

Tái hiện: nhập 20 vật tư `F-TRANSFER`; tạo/duyệt yêu cầu chuyển 6; post TRANSFER 6; post hai TRANSFER_RECEIPT lần lượt 2 và 4, cùng tham chiếu dòng chuyển; đọc lại phiếu nguồn.

Kỳ vọng: nguồn còn 14, đích có 6, phiếu phản ánh RECEIVED. Thực tế: tồn đúng và chặn nhận thừa đúng, nhưng `document.fields.status` vẫn **IN_TRANSIT**. Database xác nhận cuối: `document_id=19`, dispatched=6, received=6.

`DocumentService.Post` cập nhật nguồn MATERIAL_REQUEST và REPLENISHMENT nhưng không tính trạng thái nhận cho nguồn TRANSFER. `TR_inventory_documents_posted` đồng thời cấm sửa header có ledger. Hướng sửa cần giữ bất biến chứng từ đã post và cung cấp trạng thái nhận tính từ chứng từ con/ledger; không nên cập nhật header rồi vô hiệu trigger. Chưa áp dụng sửa trong lượt này.

## F-02 — Yêu cầu bổ sung đã đóng vẫn nhận hàng

Mức độ đề xuất **P1 — sai kiểm soát đóng chứng từ, phát sinh tồn kho ngoài quy trình đã đóng**. UC-101/GBR-122 và nhận hàng UC-080.

Tái hiện: vật tư `F-CLOSED-REPLENISH` có tồn 1; tạo REPLENISHMENT yêu cầu 10 rồi `close` có lý do; tạo RECEIPT tham chiếu yêu cầu/dòng vừa đóng, số lượng 1, rồi `post`.

Kỳ vọng: từ chối ghi sổ, nguồn giữ CLOSED, tồn vẫn 1. Thực tế: **HTTP 200**, phiếu nhận POSTED, tồn tăng thành 2, nguồn đổi thành **PARTIALLY_RECEIVED** nhưng `closedAt` vẫn còn. Database xác nhận cuối: nguồn `document_id=41`, phiếu nhận `document_id=42`.

`DocumentService.Post` kiểm tra quyền và lượng nguồn nhưng thiếu guard nguồn REPLENISHMENT đã đóng trước khi ghi ledger, sau đó vẫn tính lại trạng thái nguồn. Đây là lỗi thực chạy, không chỉ suy đoán khi đọc mã. Test giữ kỳ vọng HTTP 409 và trả exit code lỗi; không đổi kỳ vọng để làm xanh kiểm thử. Chưa sửa backend trong lượt này.

## Bằng chứng và chạy lại

Trong `artifacts/compact-v2/functional/`:

- `api-verification.json`: tên từng kiểm tra, 112 đạt, 2 lỗi.
- `defect-evidence.json`: trạng thái/số lượng SQL cho hai lỗi, inventory schema.
- `primary/functional-verification.json`: 52 kiểm tra và số hàng trước/sau.
- `functional-unit.trx`, `worker.trx`, `worker-verification.json`.
- `legacy-verification.json`: hash metadata database cũ.
- `test-cases.csv`: danh sách kiểm tra tổng hợp.
- SQL regression nằm riêng tại `artifacts/compact-v2/functional-sql-verification.json`.

```powershell
dotnet build HotelOps.slnx --no-restore -p:UseSharedCompilation=false
dotnet test HotelOps.slnx --no-build --no-restore
& scripts/database/compact-v2/Test-Backend.ps1 -Extended -Port 5192
& scripts/database/compact-v2/Test-Seeded-Functions.ps1 -Port 5193
# Dùng tên database fixture do Test-Backend in ra:
& scripts/database/compact-v2/Test-Worker.ps1 -Database <HotelOpsCompactApiTest...>
```

`Test-Backend -Extended` hiện trả exit code 1 vì hai lỗi chưa sửa. Nhóm lỗi được ghi riêng để các nhóm còn lại tiếp tục chạy; timer mở được hủy trong fixture để tránh lỗi dây chuyền. Testcase yêu cầu nội bộ ở lượt đầu thiếu route đã được hiệu chỉnh theo state machine; lượt cuối có đủ kiểm tra route/từ chối và không còn lỗi FE-13.

File thêm/sửa: `Test-Backend.ps1`; `Test-Functions.ps1`, `Test-Functions-Core.ps1`, `Test-Functions-Work.ps1`, `Test-Functions-Inventory.ps1`; `Test-Seeded-Functions.ps1`; `Inspect-Functional-Failures.sql`; báo cáo này và liên kết tại `BACKEND.md`. Không sửa C#, thêm bảng, commit/push/deploy.

## Phạm vi chưa kiểm chứng

Chưa chạy frontend/browser end-to-end, tải lớn, race/concurrency thật song song, mọi định dạng/kích thước file lỗi, mọi tổ hợp chuyển/đảo/hoàn trả liên chi nhánh hoặc tất cả 144 UC. Kiểm tra stale rowversion/operation key hiện là tuần tự. Role mẫu/tổng hợp không thay thế phê duyệt permission matrix còn pending. Ca/ngày công đã loại theo giản lược không được đưa trở lại.

Chưa thể kết luận nghiệp vụ kho sẵn sàng bàn giao trước khi xử lý hai lỗi và chạy lại hồi quy tương ứng.
