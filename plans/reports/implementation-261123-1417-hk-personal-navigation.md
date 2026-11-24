# Lịch cá nhân theo ngày và giao diện nhân viên buồng phòng

## Kết quả

IMPLEMENTED / VERIFIED bằng build và browser fixture:

- Lịch cá nhân có Ngày trước, Ngày sau, chọn ngày và Hôm nay; chỉ hiển thị các ca không bị hủy trong ngày đã chọn. Chuyển ngày qua tuần/tháng/năm tải đúng tuần từ API hiện hữu rồi lọc ngày. Tiêu đề đổi thành Công việc, ngày xem hiển thị rõ.
- Ca đang hoạt động vẫn có nút Kết thúc ca khi xem ngày khác. Start/end dùng API và RowVersion hiện tại; không phát sinh cơ chế chấm công mới. Lịch quản lý giữ điều hướng tuần và bảng phân ca.
- HK Staff bỏ mục Phòng & Checklist, còn sáu nhóm điều hướng. Route phòng cũ trở về trang mặc định, không tải danh sách phòng. Chi tiết phòng/checklist trong công việc được giao được giữ nguyên. Vai trò khác và grants không thay đổi.
- Công việc khác giữ riêng nghiệp vụ OPERATIONAL; mô tả rõ là việc vận hành được giao, ngoài dọn phòng. Danh sách dùng assignedToMe=true, không có nút chuyển sang xem toàn nhánh.

## Đánh giá Công việc khác

Chức năng có mục đích riêng, không nên xóa hoặc gộp lifecycle với dọn phòng. Quy trình hiện có: phân công → nhân viên nhận → bắt đầu → tạm dừng/tiếp tục khi cần → hoàn thành → chờ xác nhận độc lập → đóng. Công việc vệ sinh và làm lại dùng CLEANING/REWORK cùng checklist/inspection riêng.

Kiểm tra source WorkService.List cho thấy assignedToMe lọc assignment ACTIVE hoặc COMPLETED của actor; WorkService.Act kiểm tra assignee hiện hành cho accept/start/progress/pause/resume/complete, version và operation key. WorkRules dùng TASK_VIEW/TASK_EXECUTE cho nhân viên, TASK_MANAGE cho thao tác quản lý/xác nhận. Không cần thay permission hoặc backend cho yêu cầu này. Đây là kết quả rà soát source, không phải tuyên bố đã kiểm thử tích hợp lại mọi trạng thái API.

## Kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| Frontend TypeScript + Vite production build | PASS (`npm run build -- --outDir ../artifacts/production-261123-personal-days/frontend`) |
| Điều hướng lịch cá nhân | PASS: ngày trước/sau/Hôm nay, ngày trống/chỉ có ca hủy, qua tuần/năm, ca đang chạy, start/end version, lỗi/thử lại, 1440/768/375px |
| HK Staff hồi quy | PASS: 27 lượt kiểm tra responsive, không danh sách phòng, OPERATIONAL được giao, nhận/bắt đầu/tiến độ/checklist/hoàn thành, conflict và modal |
| Lịch quản lý | PASS: bộ phận–đối tượng, tuần/tháng/nhiều người, xác nhận vượt giờ và lịch thay đổi, lịch sử, OWN |
| Thống kê ca | PASS: tám vai trò, start/end cá nhân, lỗi/thử lại và không gọi thống kê khi thiếu ATTENDANCE_RECORD |
| Production HTTPS5080 | PASS: HTTP200, chứng chỉ hợp lệ, login hiển thị, API anonymous401, không lỗi runtime trong smoke |
| Backend build / unit / integration | NOT_RUN trong lượt này: không sửa C# hoặc contract API |
| Database ghi/kiểm thử mutation thật | NOT_RUN: toàn bộ browser mutations là fixture, không ghi primary |

Cảnh báo build: outDir ngoài frontend nên Vite không tự xóa thư mục đầu ra; đây là thư mục release riêng mới. Git báo chuẩn hóa LF/CRLF; diff-check không có lỗi whitespace.

Bằng chứng: artifacts/personal-workday-days/verification.json và daily-*.png; artifacts/housekeeping-staff/ui-verification.json; artifacts/workday-scheduling/verification.json; artifacts/attendance-browser/verification.json; artifacts/production-261123-personal-days/verification.json.

## Production và nhật ký kiểm thử

Frontend tại https://127.0.0.1:5080/ dùng bundle index-BsI0pCjU.js, PID44764 (launcher18400). API Production9920/5769 giữ nguyên. Preview5082 đã dừng, không tạo sub-agent, commit hoặc push.

Lần kiểm tra đầu ngay sau restart gặp CONNECTION_REFUSED do preview chưa sẵn sàng; kiểm tra readiness trả200 rồi smoke PASS. Bộ attendance bổ sung phát hiện fixture cũ trả object cho endpoint Rooms vốn trả array; sửa fixture trả [] và thêm bắt pageerror/chờ empty state. Chạy lại PASS; không sửa sản phẩm để che lỗi test.

## Files trong lượt này

- frontend/src/compact/workday.tsx
- frontend/src/compact/app.tsx
- frontend/src/compact/work.tsx
- frontend/tests/compact-personal-workday-days.mjs (mới)
- frontend/tests/compact-housekeeping-staff.mjs
- frontend/tests/compact-attendance-summary.mjs (sửa fixture có từ lượt trước)
- docs/ui/compact-ux.md
- plans/261123-1417-hk-personal-navigation/plan.md
- plans/reports/implementation-261123-1417-hk-personal-navigation.md

Helper/evidence local nằm trong artifacts/production-261123-personal-days. Working tree có nhiều thay đổi từ các lượt trước; không hoàn nguyên hoặc commit các thay đổi đó.

Không có blocker còn lại cho phạm vi UI này. Chưa đăng nhập tài khoản thật để kiểm thử lại dữ liệu Workday primary; kiểm thử hành vi dùng fixture và production smoke chỉ đọc/anonymous.
