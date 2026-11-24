# Popup thông báo và xác nhận — 24/11/2026

## Đã thực hiện
- Thông báo dùng chung ở phía trên, đóng bằng ×; nội dung mới xuất hiện lại, nhiều thông báo không đè nhau.
- Hộp xác nhận ở giữa; Xác nhận xanh, Hủy đỏ; Hủy/× không gửi yêu cầu, đang xử lý khóa nút.
- Áp dụng vào cảnh báo vượt giờ, thao tác phòng, công việc, yêu cầu, tài sản, chứng từ, điều chuyển và xóa/kết thúc ca.
- Form báo sự cố bỏ cả Mức ảnh hưởng và Ghi chú; API cho phép không có ghi chú, bỏ lựa chọn bắt buộc ghi chú trong cấu hình OM.
- Không đổi schema, quyền, luồng phê duyệt hay dữ liệu nghiệp vụ Production/Develop.

## Tệp của lượt này
Trong `frontend/src/compact/`:
- `alert-popup.tsx`, `alert-popup.css`, `ui.tsx`: popup và nút dùng chung.
- `workday-assignment.tsx`, `workday-settings.tsx`, `workday.tsx`: xác nhận phân ca/vượt giờ/xóa ca/kết thúc ca.
- `rooms.tsx`, `assets.tsx`, `work.tsx`, `requests.tsx`, `documents.tsx`, `transfers.tsx`, `inventory-document-form.tsx`, `inventory-documents.tsx`, `cleaning-incidents.tsx`: hộp xác nhận thao tác.
- `room-issues.tsx`, `internal-settings.tsx`: form báo sự cố và cấu hình ghi chú.
Backend: `src/HotelOps.Api/Compact/Services/WorkService.RoomIssues.cs` bỏ bắt buộc ghi chú lúc tạo.
Kiểm thử: `frontend/tests/compact-alert-popups.mjs`, `compact-workday-scheduling.mjs`, `incident-configuration-browser.mjs`; `scripts/verification/alert-modal-runtime.mjs`, `alert-modal-local.mjs`.
Tài liệu: `docs/ui/compact-ux.md`, `plans/261124-2208-alert-modal/plan.md`, báo cáo này.
Các tệp trên có thể chứa thay đổi từ lượt trước; giữ nguyên các thay đổi có sẵn.

## Kiểm chứng
- Backend build: Passed, 0 warning, 0 error.
- `dotnet test HotelOps.slnx --no-build`: Passed 101, Skipped 3 đã có từ trước (WorkerIntegrationTests, MaintenanceWorkerTests, AccountManagementTests).
- Frontend build: Passed; 9 kiểm thử compact CI Passed.
- Edge: popup ở 1440/375/320px và viewport cao 140px, đóng/mở thông báo mới, thử lại, lỗi trong modal, đăng nhập không submit ngoài ý muốn, xác nhận ở giữa và đúng màu, Hủy không mutation: Passed.
- Edge: phân ca nhiều người/ngày, vượt giờ, Hủy, xác nhận lại khi đổi người/token, retry stale: Passed.
- Edge: form báo sự cố không có hai trường ở 1440/390px, gửi và trở về công việc, giám sát xác nhận: Passed.
- API/database TEST `HotelOpsCompactWorkdayTestWarehouse261124`: sao lưu được xác minh trước ghi dữ liệu; báo hỏng 823 và báo thiếu 824 không có ghi chú; công việc 822 hoàn tất sau xử lý, phòng Sẵn sàng. Không ghi nghiệp vụ vào Production/Develop.
- Ban đầu kịch bản cũ thất bại do selector Thu gọn/ô giờ và fixture thiếu currentMinute; đã sửa và chạy lại Passed. Phiên API TEST hết hạn đã đăng nhập lại bằng fixture riêng, không lộ thông tin đăng nhập.

## Giới hạn
- UI dùng API giả lập; kiểm chứng nghiệp vụ thật dùng database TEST riêng, không thao tác nghiệp vụ trên Production.
- Không tuyên bố ba test bị skip đã được chạy. Không commit/push hoặc triển khai remote.
- Bằng chứng trong `artifacts/alert-modal/`, `artifacts/incident-configuration/`, `artifacts/workday-scheduling/`.

- Rà soát diff không có lỗi whitespace; Git có cảnh báo chuẩn hóa LF sang CRLF theo cấu hình hiện hành. Không có warning từ build backend.

## Bản local
- Release build Passed. Production API PID 23304, Develop API PID 25424; DLL SHA256 61F01674EAF6DC0F83C85B561E55FA9752C0A3A8AD5F7729528C93FB603D1143.
- Frontend local 5080 cập nhật từ dist; giữ bản trước tại artifacts/production-261124-alert-modal/frontend-before. API TEST 35808 đã dừng.
- Kiểm tra local Passed: HTML dùng index-DVvFo5NN.js và index-qzCS7Xrr.css, các asset HTTP 200, API chưa đăng nhập HTTP 401 tại 5080/5769/5774; không có lỗi JavaScript. Bằng chứng: artifacts/alert-modal/local-release.json.
