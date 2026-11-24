# Giám sát buồng phòng: Công việc, lịch cá nhân và thống kê ca

## Kết quả triển khai

- Sidebar giám sát có **Công việc** gồm Phân công dọn phòng / Kiểm tra phòng / Xử lý làm lại. Tái sử dụng màn hình và lifecycle hiện hữu, giữ tab phiếu kiểm tra/checklist/lỗi kiểm tra. Liên kết cũ `#cleaning`, `#inspection`, `#rework`, `#templates` và liên kết chi tiết theo kind vẫn hoạt động. Các tab lọc theo quyền.
- Thêm **Lịch làm việc** trực tiếp trên sidebar giám sát, dùng Workday OWN để xem lịch, bắt đầu/kết thúc ca cá nhân. Đã đọc primary xác minh HOUSEKEEPING_SUPERVISOR có ATTENDANCE_RECORD/OWN và không có SHIFT_MANAGE; không thay grants.
- Dashboard của mọi người có ATTENDANCE_RECORD dùng bảng **Ca làm của tôi**, hiển thị tuần hiện tại (thứ Hai–Chủ nhật), tháng hiện tại, ca đã làm, giờ đã làm, ca đang làm và liên kết lịch. Lễ tân có thêm Tổng quan cá nhân để truy cập; không yêu cầu DASHBOARD_VIEW.
- Endpoint mới `GET /api/v2/workday/personal-summary?branchId=...` kiểm tra ATTENDANCE_RECORD và branch, luôn lọc actor hiện tại. Không nhận userId làm chủ thể, không cấp xem chấm công người khác cho SHIFT_MANAGE.
- Ca đã làm = ENDED có đủ started_at/ended_at hợp lệ; giờ đã làm = tổng chênh lệch vào–ra thực tế, làm tròn hiển thị 2 số lẻ. Không lấy giờ dự kiến, không cộng PLANNED/CANCELLED/WORKING vào số hoàn tất; WORKING hiển thị riêng. Dùng work_date theo timezone chi nhánh để chọn kỳ. Ca hết giờ được xử lý bởi cơ chế auto-end hiện hữu trước tổng hợp. Không thay trạng thái hoặc quy tắc ca.

## Kiểm chứng

- Backend build: PASS, 0 warning/error.
- `dotnet test HotelOps.slnx --no-build`: **56 PASS, 3 SKIP**. Ba integration test tùy chọn có sẵn chưa chạy: worker recurrence/low-stock, maintenance worker, account management.
- Frontend: `tsc -b` và Vite build PASS. Publish Release backend PASS.
- API fixture: **13 kiểm tra PASS**, database `HotelOpsCompactApiTest6bee4cdca55b`, có backup trước seed. Bao gồm own/branch/permission, giả mạo userId, timezone, giám sát start/end thực tế, thống kê cập nhật sau end và ca đang làm riêng. Không ghi test lên primary.
- Browser fixture: bảng thống kê trên 8 vai trò (HK Supervisor/Staff, Maintenance Lead/Staff, Storekeeper, Reception, Department Head, OM); start/end giám sát, lỗi/thử lại, không gọi endpoint khi thiếu attendance, responsive 1440/768/375 đều PASS. Fixture cấp grants minh họa theo chức năng; không thay database grants thật.
- Hồi quy giám sát: 24 nhóm kiểm tra nghiệp vụ hiện hữu trên ba viewport PASS; giữ kiểm tra/phân công đúng parent, checklist versioning, room drawer/filter/pagination và liên kết cũ. Click trực tiếp ba tab Công việc cũng PASS.
- Bằng chứng: `artifacts/attendance-{build,unit,frontend,publish,api,browser,supervisor-browser}.log`; `artifacts/compact-v2/backend/attendance-summary-verification.json`; `artifacts/attendance-browser/verification.json`; `artifacts/housekeeping-supervisor/`.

## File của lượt thay đổi này

- Frontend: `frontend/src/compact/app.tsx`, `housekeeping-workspace.tsx`, mới `attendance-summary.tsx`.
- Backend: `src/HotelOps.Api/Compact/Controllers/WorkdayController.cs`; mới `Compact/Services/WorkdayService.Summary.cs`, `Compact/Contracts/AttendanceSummaryDto.cs`.
- Tests: mới `tests/HotelOps.Compact.Tests/AttendanceSummaryTests.cs`, `scripts/database/compact-v2/test-attendance-summary.ps1`, `frontend/tests/compact-attendance-summary.mjs`; cập nhật `Test-Backend.ps1`, `compact-housekeeping-supervisor.mjs`.
- Docs/plan: `docs/ui/compact-ux.md`, `plans/261123-1353-supervisor-workday-summary/plan.md`, báo cáo này. Release/launcher/log ở `artifacts/production-261123-attendance/`.

## Review và nhật ký

Giữ endpoint thống kê trong Workday để không mở quyền DASHBOARD_VIEW hoặc sửa API thống kê nghiệp vụ của các bộ phận. Tách DTO khỏi entity, đọc AsNoTracking và CancellationToken, dùng lại expiry service đã được duyệt. Đã sửa lỗi cú pháp C# do biến `from` trong query và dấu phẩy còn lại khi chuyển tab state sang giá trị URL; build lại đạt. Fixture browser được cập nhật theo permission dashboard của từng vai trò, không hạ guard sản phẩm.

Tổng quan Lễ tân chỉ có thống kê ca cá nhân, không tái sử dụng các thẻ công việc cá nhân có thể gọi CLEANING API ngoài workflow Reception. Kiểm thử xác nhận không phát sinh request này. Bản Production cục bộ cập nhật tại https://127.0.0.1:5080/; API Release PID9920 chạy Production trên5769. Preview5082 và các tiến trình con được tắt sau khi chuyển frontend. Kết quả smoke HTTPS/anonymous401 nằm trong `artifacts/production-261123-attendance/verification.json`.

Không thay schema/grants, commit/push hoặc Project 1. Các thay đổi dirty từ trước được giữ. Kiểm thử ghi chỉ chạy trên database thử nghiệm; không đăng nhập tài khoản thật để tạo ca thử trên Production. Bảng hiện tại thống kê kỳ đang diễn ra và chi nhánh đang chọn, không phải báo cáo lương hoặc tổng liên chi nhánh.
