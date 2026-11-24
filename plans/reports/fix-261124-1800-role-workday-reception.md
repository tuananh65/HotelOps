# Phân ca sau chuyển vai trò và báo sự cố lễ tân

## Phạm vi / tiến độ

- Audit Production: hoàn tất (chỉ đọc).
- Sửa source: IMPLEMENTED, đã kiểm chứng API trên TEST.
- Build backend Debug/Release: PASSED, 0 warning, 0 error.
- .NET tests Debug/Release: 101 passed, 3 skipped (các test có sẵn được cấu hình SKIP).
- Frontend build và 9 compact unit tests: PASSED.
- Production API và Develop API: đã cập nhật gói Release, health OK.

## Bằng chứng nguyên nhân

`scripts/verification/workday-role-audit.ps1` ghi kết quả tại `artifacts/workday-role-validation/audit.json`.

- Bốn giám sát bảo trì của DVBV/DVCT được cấp vai trò mới ngày 24/11/2026 lúc 16:02:20 giờ Việt Nam. Ca 08:00–11:30 và 13:00–17:00 bắt đầu trước thời điểm cấp quyền. Bộ lọc ứng viên yêu cầu vai trò/membership phủ cả ca nên không cho phân hồi tố.
- Mỗi chi nhánh có hai giám sát phù hợp cho ngày 25/11. Không có phân công kỹ thuật cũ từ ngày 24/11. Không tìm thấy cặp phân công khác CANCELLED trùng giờ cho cùng nhân viên ở hai chi nhánh.
- Tám phân công kỹ thuật PLANNED ngày 23/11 được giữ nguyên lịch sử. Các mẫu ca cũ đã inactive nhưng giao diện lịch vẫn đưa tất cả vào dropdown và bảng.
- Nút báo sự cố chỉ kiểm tra IR_CREATE; API CreateRoomIssue không kiểm tra ca lễ tân.

Không sửa hồi tố thời điểm cấp quyền, xóa phân công hay cập nhật database Production. Các mẫu ca HK 8 giờ và ca HK chia sáng/chiều đều đang active; đây là định nghĩa ca khác nhau, không phải bằng chứng nhân viên bị trùng lịch.

## Thay đổi

- Lịch quản lý mặc định ẩn mẫu ca inactive. Checkbox mở lại lịch sử, có nhãn chỉ xem; lịch cá nhân vẫn giữ lịch sử.
- Không cho phân công vào ca hết giờ. Chặn cả API candidates, batch candidates, single/batch assignment; chọn tuần/tháng loại ca hôm nay đã hết giờ. Giữ kiểm tra role, membership, capacity, overlap và branch.
- Lễ tân dùng chung điều kiện ca cho thao tác phòng và báo sự cố: WORKING, đã bắt đầu, chưa kết thúc, đúng ngày/khung giờ/chi nhánh, mẫu ca và role RECEPTION active.
- Ẩn nút báo sự cố khi không đủ điều kiện; API tải đối tượng báo cáo và API tạo báo cáo đều trả 403 RECEPTION_NOT_ON_SHIFT ngoài ca. Không chỉ ẩn UI.

## Kiểm chứng thực tế

- Runtime TEST: `HotelOpsCompactWorkdayTestWarehouse261124`, được backup trước khi thay fixture. Hai chi nhánh thử được ứng viên và phân công giám sát bảo trì, từ chối lặp và hai mẫu ca chồng giờ (SHIFT_OVERLAP), từ chối chéo branch, hủy phân công thử qua API.
- Ca hết giờ: ứng viên đơn/batch và API phân công trực tiếp đều bị từ chối. Template thử được ngừng sử dụng sau test, giữ audit.
- Lễ tân WORKING còn giờ: canOperate=true, tải đối tượng báo cáo được; báo cáo phòng giả đi tới validation ROOM_NOT_FOUND. PLANNED/ENDED/WORKING ở ngày đã qua: canOperate=false, GET biểu mẫu và POST báo cáo đều 403. Fixture được phục hồi trong finally; kiểm tra lại canOperate=true sau phục hồi.
- Browser Playwright dùng API fixtures tại Develop và bundle Production 5080, viewport 1440 và 390: lịch cũ ẩn/mở được, ca quá giờ không có nút, ngày tiếp theo có ứng viên; lễ tân ngoài ca ẩn nút, trong ca hiện nút, deep-link bị chặn. Không có pageerror. Đã xem ảnh desktop/điện thoại; không phát hiện tràn ngang ở phần thay đổi.
- Bằng chứng: `artifacts/workday-role-validation/audit.json`, `api-results.json`, `browser-results.json` và ảnh PNG cùng thư mục.
- `git diff --check`: PASSED sau sửa dòng trống cuối tài liệu.

## Files thay đổi trong lượt này

- `src/HotelOps.Api/Compact/Services/RoomService.Attendance.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.RoomIssues.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.RoomIssueConfig.cs`
- `src/HotelOps.Api/Compact/Services/WorkdayService.cs`
- `src/HotelOps.Api/Compact/Services/WorkdayService.Scheduling.cs`
- `frontend/src/compact/rooms.tsx`, `api.ts`, `workday.tsx`, `workday-assignment.tsx`
- `frontend/tests/workday-role-reception-browser.mjs`
- `scripts/verification/workday-role-audit.ps1`, `reception-shift-fixture.ps1`, `workday-role-runtime.mjs`
- `docs/ui/compact-ux.md` và báo cáo này.

Các sửa đổi từ lượt cấu hình/kho/xóa ca trước đó được giữ nguyên. Không commit/push trong lượt này.

## Cập nhật cục bộ và phục hồi

- Production API: PID 8040, https://localhost:5769, database HotelOpsCompactDB.
- Develop API: PID 23404, http://127.0.0.1:5774, database HotelOpsCompactWorkdayTestP0261124.
- Frontend Production: https://127.0.0.1:5080 (PID 7680). Develop: http://127.0.0.1:5174 (PID 48136).
- Health hai API: schema 3.0.0, 49 tables, status ok. Frontend HTTP 200, API qua proxy không token trả 401.
- Backup Production đã VERIFYONLY: `HotelOpsCompactDB_before_workday_20261124_182121778.bak`; manifest `artifacts/workday/backup-HotelOpsCompactDB.json`.
- Release: `artifacts/production-261124-workday-role`. Frontend cũ: `frontend-before` trong thư mục này. API cũ: `artifacts/production-261124-warehouse-final/api`. Có thể khởi động lại gói API cũ và phục hồi frontend-before nếu cần, không cần rollback DB vì không có data/schema migration.
- API TEST tạm cổng 5775 đã dừng sau kiểm thử.

## Giới hạn / cảnh báo

- Không đăng nhập giả làm nhân viên thật hoặc tạo phân công thử trên Production. Kiểm chứng nghiệp vụ bằng API thật trên TEST; Production được audit SQL chỉ đọc, health/proxy và browser fixture trên bundle đang phục vụ.
- Ba test .NET skipped không được tính PASS. Lần publish đầu có MSB3026 do file font OFL.txt bị khóa tạm; retry thành công. Publish cuối không còn warning. Vite thông báo outDir ngoài frontend và không tự xóa thư mục; artifact được lưu để rollback.
- Các lỗi ban đầu của test harness (nhận diện mã fixture, dùng query id thay vì mở card phòng) đã sửa và chạy lại đạt; không thay dữ liệu Production để làm test pass.
