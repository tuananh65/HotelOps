# Lịch cá nhân giám sát và thống kê ca thực tế

- [x] Kiểm tra route HK Supervisor, Workday OWN, dashboard và schema thời gian.
- [x] Gộp sidebar dọn phòng/kiểm tra/làm lại thành Công việc, giữ các tab và liên kết cũ; thêm Lịch làm việc dễ truy cập.
- [x] API thống kê riêng actor theo chi nhánh, tuần/tháng hiện tại theo timezone: ca đã kết thúc và số giờ vào–ra thực tế; ca đang làm hiển thị riêng, không tính giờ dự kiến là giờ đã làm.
- [x] Widget dùng chung trên mọi dashboard có ATTENDANCE_RECORD; đảm bảo Reception cũng có dashboard cá nhân, không cần DASHBOARD_VIEW để xem thống kê của mình.
- [x] Build/unit/API fixture/browser, review; cập nhật Production5080 đã được yêu cầu trong phiên, bàn giao bằng chứng.

Không sub-agent, schema/grants hoặc primary test writes. Giữ nguyên workflow start/end/auto-end và backend ownership. Tuần thứ Hai–Chủ nhật; tháng lịch chi nhánh. Ngưỡng 8/40 giờ phân ca không thay đổi.

Kết quả: build FE/BE PASS; unit56 PASS/3 SKIP; API fixture13 PASS; dashboard8 vai trò và hồi quy giám sát24 nhóm PASS. Production API9920/frontend43396 trên HTTPS5080; preview5082/test5187 dừng. Báo cáo: `plans/reports/implementation-261123-1353-supervisor-workday-summary.md`.
