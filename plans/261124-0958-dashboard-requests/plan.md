# Dashboard, trạng thái ca và Yêu cầu của Trưởng bộ phận

Phạm vi: frontend compact; giữ API, quyền, scope chi nhánh và dữ liệu hiện có.

- [x] Kiểm kê dashboard, vị trí thống kê ca và thẻ số liệu.
- [x] Số liệu dashboard đứng trước Ca làm của tôi; màu số liệu có ý nghĩa và chữ tương phản.
- [x] Đang trong ca màu xanh, đã kết thúc màu đỏ trong lịch/trạng thái ca.
- [x] Trưởng bộ phận: một menu Yêu cầu, các tab Công việc/Yêu cầu nội bộ/Yêu cầu vật tư; giữ liên kết cũ và kiểm tra quyền.
- [x] Build frontend, kiểm thử đa vai trò/điện thoại, phân ca và menu/đường dẫn/quyền.
- [x] Rà soát, cập nhật UX/báo cáo, cập nhật Production cục bộ đã được ủy quyền và dừng preview.

Phân công: worker head_requests sở hữu app.tsx/workspace Yêu cầu và test riêng; parent sở hữu dashboard, màu, attendance và tích hợp. Explorer chỉ đọc xác định các vị trí dashboard.

Hoàn tất: báo cáo tại `plans/reports/implementation-261124-0958-dashboard-requests.md`. Review độc lập phát hiện remount attendance; đã sửa và kiểm thử số lần gọi API. Production bundle `index-OPqAIvTQ.js`, listener 30516 / launcher 35580, API 40444. Preview 5082 đã dừng.
