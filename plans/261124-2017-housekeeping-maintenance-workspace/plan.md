# Hàng đợi vệ sinh và workspace giám sát bảo trì

- [x] Đọc source và chẩn đoán trạng thái thực: sự cố 660/662 đã vào queue bảo trì, chưa có handler; ca của giám sát 437 hôm nay còn PLANNED, chưa started_at.
- [x] Tất cả gồm phòng cần dọn và công việc; Phòng cần dọn đứng thứ hai; bỏ tab Chưa phân công, không làm mất công việc chưa giao.
- [x] Thu gọn menu HK Staff theo yêu cầu; Chờ kiểm tra màu vàng.
- [x] Hiển thị nguyên nhân chưa thể tiếp nhận và lối bắt đầu ca; giữ backend kiểm tra ca/quyền/handler.
- [x] Gộp yêu cầu khắc phục, công việc đang xử lý và sự cố của Maintenance Supervisor vào một workspace, không nhân đôi incident và maintenance child.
- [x] Build/test/API/browser, cập nhật local đã được duyệt và báo cáo.

Không sub-agent, không thay grants/schema, không tự bắt đầu ca thay nhân viên. Giữ link báo sự cố từ công việc HK; ẩn menu không thu hồi API permission.

Kết quả: `plans/reports/implementation-261124-2050-housekeeping-maintenance-workspace.md`.
