# Bàn giao đặt phòng và tự kết thúc ca — Project 2

Đã triển khai và cập nhật Production cục bộ sau khi người dùng duyệt sao lưu/patch database trong phiên ngày 23/11/2026. Frontend: http://127.0.0.1:5186; API Release: https://localhost:5769, PID 40364, environment Production.

## Hành vi đã thực hiện

- Sẵn sàng → Đặt phòng → Đã đặt → Khách đến nhận phòng → Có khách. Hủy đặt đưa phòng chưa sử dụng về Sẵn sàng.
- Chuyển đặt phòng: nguồn Sẵn sàng, đích Đã đặt; giữ lượt và bằng chứng vệ sinh nguồn. Chuyển khách đã ở: nguồn Cần dọn, tăng turnover, xóa cleaning pointer; đích Có khách. Trả phòng đã ở cũng yêu cầu vệ sinh lại.
- Kiểm tra đồng thời phiên bản nguồn/đích, cùng chi nhánh, Reception và permission, phòng đích thực sự sẵn sàng, block và thao tác lặp. Nguồn/đích/lịch sử cập nhật trong cùng transaction.
- Bỏ ô lý do đặt/nhận/trả/hủy đặt. Chuyển phòng và block giữ lý do. Việt hóa các sự kiện phòng, kết quả kiểm tra, ca làm việc và tên hạng phòng chuẩn trên trang phòng.
- WORKING tự kết thúc theo giờ chi nhánh; worker chạy mỗi 30 giây và kiểm tra lại khi tải Workday. Không tự bắt đầu hoặc kết thúc PLANNED. Chặn bắt đầu ngoài giờ ca. UI cập nhật 30 giây khi không mở form/chi tiết.
- AUTO_END ghi audit actor NULL, hiển thị Hệ thống trong lịch sử ca theo quyền cá nhân, không đòi AUDIT_VIEW. Thời điểm kết thúc lấy giờ kết thúc theo lịch; bản ghi lịch sử bất thường có started_at sau giờ hết ca được kết thúc tại started_at để không tạo khoảng thời gian âm.
- Báo cáo phòng nhận diện RESERVED; chỉ số phòng sẵn sàng không cộng phòng đã đặt.

## Kiểm chứng

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| Backend build | PASS, 0 warning, 0 error | artifacts/reservations-build.log |
| Frontend typecheck + build | PASS | artifacts/reservations-frontend-build.log |
| dotnet test HotelOps.slnx | 46 passed, 3 skipped, 0 failed | artifacts/reservation-unit-tests.log |
| API + SQL fixture | 32 nhóm kiểm tra PASS | artifacts/compact-v2/backend/reservation-shifts-verification.json |
| Room browser | PASS, 1440/768/375px; API mock | artifacts/reception-rooms/verification.json |
| Workday browser | PASS; polling, hết ca, lịch sử và giới hạn giờ; API mock | artifacts/shift-expiry/browser.json |
| Release publish | PASS; một warning MSB3026 khi copy OFL.txt, retry thành công; SHA256 nguồn/đích khớp | artifacts/reservations-publish.log |
| Production | health 200, frontend 200, proxy anonymous 401 | artifacts/production-261123-reservations/api.log |
| Primary SQL | 96 phòng; constraint RESERVED trusted/enabled; trigger enabled; 3 AUTO_END; 0 ca quá giờ WORKING | artifacts/production-261123-reservations/database-verification.json |

Fixture cuối: `HotelOpsCompactApiTestac2f2a31c63a`, API fixture đã dừng. Có kiểm tra worker thật khi không gọi Workday, quyền OWN, stale RowVersion, retry, chuyển đặt/chuyển khách đã ở, rollback khi đích không hợp lệ, block, giờ bắt đầu và giờ kết thúc theo múi giờ. Ba test .NET bỏ qua là các test integration có điều kiện môi trường riêng (recurring worker, maintenance worker, account/email); không tính là PASS.

Backup primary mới được COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước patch: `HotelOpsCompactDB_before_workday_20261123_132504736.bak`, vị trí trong `artifacts/workday/backup-HotelOpsCompactDB.json`. Patch chỉ thay constraint, view, trigger liên quan trạng thái phòng; không sửa grants hoặc reset phòng. Worker đã kết thúc đúng 3 ca hết lúc 08:00 Việt Nam ngày 23/11/2026, ghi audit hệ thống. Không tạo booking thử trên primary.

## File thuộc lượt thay đổi này

- Backend: `src/HotelOps.Api/Compact/Services/RoomService.cs`, `WorkdayService.cs`, **mới** `ShiftExpiryService.cs`, `InsightService.cs`, `InsightService.Owner.cs`, `InsightService.OwnerReports.cs`; đăng ký worker tại `src/HotelOps.Api/Compact/CompactHost.cs`.
- Frontend: `frontend/src/compact/rooms.tsx`, `workday.tsx`, `ui.tsx`, `api.ts`, `task-ux.css`.
- Database/tooling: **mới** `scripts/database/compact-v2/enable-room-reservations.sql`, `Enable-RoomReservations.ps1`, `test-reservation-shifts.ps1`; sửa `Test-Backend.ps1`.
- Tests: **mới** `tests/HotelOps.Compact.Tests/ShiftExpiryTests.cs`, `frontend/tests/compact-shift-expiry.mjs`; cập nhật `frontend/tests/compact-reception-rooms.mjs`.
- Docs: `docs/ui/compact-ux.md`, `docs/requirements/SRS_CHANGE_NOTES.md`, `plans/261123-reservations-shift-expiry/plan.md` và báo cáo này.

Giữ các thay đổi đã có trong workspace; không commit/push, không tác động Project 1. Đây là quản lý trạng thái đặt phòng hiện tại, không thêm thông tin khách, ngày đặt tương lai hoặc module booking độc lập. Kiểm thử nghiệp vụ ghi chạy trên fixture; Production chỉ kiểm tra health/proxy, trạng thái schema và kết quả worker được duyệt.
