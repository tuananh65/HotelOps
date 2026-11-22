# Sửa lỗi kho Compact v2 — 22/11/2026

Đã xử lý F-01 và F-02 trong báo cáo kiểm thử chức năng. Bộ HTTP mở rộng chạy lại trên SQL Server thật đạt **127 kiểm tra, 0 lỗi**. Không thêm bảng, sửa schema, entity sinh tự động hoặc ghi vào database cũ `HotelOpsDB`.

## Thay đổi

**F-01 — Trạng thái nhận chuyển kho:** API danh sách chứng từ, chi tiết chứng từ và báo cáo chứng từ tính `fields.status` từ lượng nhận đã ghi sổ của từng dòng chuyển. Trạng thái lần lượt là `IN_TRANSIT`, `PARTIALLY_RECEIVED`, `RECEIVED`. Phiếu nhận nháp không được tính; bút toán nhận đã đảo không được tính. Nhận đủ một vật tư không làm hoàn tất các dòng vật tư còn thiếu.

Header đã ghi sổ vẫn bất biến. `fields.recordedStatus` trả trạng thái lưu trên header; `fields.status` trả tiến độ nhận hiện tại. Truy vấn SQL trực tiếp vào header/view vẫn thấy trạng thái lịch sử, không phải trạng thái suy ra của API. Không vô hiệu hóa trigger để cập nhật header.

**F-02 — Nhập sau khi đóng yêu cầu bổ sung:** khi post phiếu nhận, backend kiểm tra nguồn trong transaction serializable. Nguồn có `Status=CLOSED` hoặc `ClosedAt` khác null bị từ chối bằng HTTP **409 / `REPLENISHMENT_CLOSED`**. Điều kiện này áp dụng cả với phiếu nháp tạo trước khi đóng và dữ liệu nguồn có trạng thái không nhất quán nhưng đã có thời điểm đóng. Thao tác bị từ chối không thay đổi tồn kho, rowversion, trạng thái phiếu hoặc tạo sự kiện POST.

## Kiểm chứng thực tế

| Hạng mục | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx --no-restore -p:UseSharedCompilation=false` | Passed, 0 lỗi, 0 cảnh báo |
| `dotnet test HotelOps.slnx --no-build --no-restore` | 23 passed; 1 worker test mặc định skip, đã chạy riêng |
| API mở rộng, `Test-Backend.ps1 -Extended -Port 5192` | 127 passed, 0 failed |
| Dữ liệu mẫu chính, `Test-Seeded-Functions.ps1 -Port 5193` | 52 passed, 0 failed |
| SQL rollback, `Test-Compact.ps1` | 58 passed, 0 failed; 0 hàng fixture tồn lại |
| Worker, `Test-Worker.ps1` | 1 passed, 0 failed |
| Kiểm tra cấu trúc/toàn vẹn trên database chính | Passed; 37 bảng, 152 FK, 77 CHECK, 26 trigger, 6 view |

Các ca hồi quy bổ sung xác nhận nhận từng phần/toàn phần, đảo từng phiếu nhận, nhận lại sau đảo, chuyển nhiều vật tư, bỏ qua phiếu nháp, và trạng thái thống nhất trên danh sách/chi tiết/báo cáo. Các ca yêu cầu đã đóng kiểm tra cả phiếu mới, phiếu nháp có trước khi đóng và nguồn có trạng thái không nhất quán. Kịch bản cố tình tạo trạng thái không nhất quán chỉ chạy trong database fixture.

Database API: `HotelOpsCompactApiTest4cc91b0b3ad7`. Database SQL rollback: `HotelOpsCompactFunctionSql873809605d23`. Database mẫu chính: `HotelOpsCompactDB`; số hàng của 36 bảng ngoài `user_sessions` không đổi sau lượt kiểm tra. Đăng nhập/đăng xuất và metadata phát thông báo có thể thay đổi trong lượt kiểm tra dữ liệu mẫu.

Bằng chứng lưu tại `artifacts/compact-v2/inventory-fixes/`: `api-verification.json`, `primary-verification.json`, `sql-verification.json`, `unit.trx`, `worker-verification.json`, `worker.trx`. Kết quả lỗi cũ được giữ trong thư mục `artifacts/compact-v2/functional/` để đối chiếu.

## File thay đổi trong lượt sửa

- Thêm `src/HotelOps.Api/Compact/Services/TransferStatusProjection.cs`: tính tiến độ nhận chuyển kho và xuất DTO.
- Sửa `src/HotelOps.Api/Compact/Services/DocumentService.cs`: áp dụng trạng thái cho danh sách/chi tiết; chặn nhập yêu cầu đã đóng.
- Sửa `src/HotelOps.Api/Compact/Services/InsightService.cs`: đồng bộ trạng thái trong báo cáo chứng từ.
- Sửa `scripts/database/compact-v2/Test-Functions-Inventory.ps1`: bổ sung kiểm thử hồi quy.
- Cập nhật `BACKEND.md`, `FUNCTIONAL-TEST-REPORT.md` và thêm báo cáo này trong `docs/database/compact-v2/`.

Các kết quả chỉ xác nhận những tình huống đã thực chạy. Chưa kiểm thử giao diện frontend, tải lớn hoặc đầy đủ mọi tổ hợp thao tác đồng thời. Không có lỗi còn mở trong bộ kiểm thử đã chạy; không commit, push hoặc deploy.
