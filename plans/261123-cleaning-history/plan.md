# Lịch sử vệ sinh theo ngày

Yêu cầu: thay trang Nhân viên & Ca trực của Giám sát (#staff) bằng Lịch sử vệ sinh theo ngày.

- [x] Kiểm tra API HousekeepingHistory hiện có: CLEANING_VIEW theo chi nhánh, ngày CompletedAt của CLEANING/REWORK theo timezone chi nhánh, phân trang25.
- [x] Đổi menu và route staff sang component lịch sử; giữ liên kết lịch sử trong nhóm Công việc. Đổi tiêu đề, hiển thị ngày/tổng, link tên công việc, responsive320px.
- [x] TypeScript/Vite build, browser thực tế cho route staff, ngày trước/sau, chi tiết và giao diện hẹp.
- [x] Cập nhật frontend Production5080 trong phạm vi phiên đã được yêu cầu, xác minh bundle/HTTPS. Không thay backend, database hay grants.

Hoàn tất: Production trả200, bundle index-Cd5bBEYs.js tải200, API ẩn danh401, TLS tin cậy, không lỗi browser. Frontend launcher49360/listener18184; API40444. Preview5082 đã dừng.
