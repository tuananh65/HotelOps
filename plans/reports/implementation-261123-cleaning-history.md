# Lịch sử vệ sinh theo ngày

Đã thay trang Nhân viên & Ca trực (#staff) của Giám sát thành Lịch sử vệ sinh. Lịch làm việc vẫn là trang riêng.

Hiển thị dọn phòng và làm lại theo ngày gửi hoàn thành tại múi giờ chi nhánh; có Ngày trước, Ngày sau, Hôm nay, chọn ngày và tìm công việc/phòng. Bảng gồm công việc, phòng, người hoàn thành, thời điểm và trạng thái. Bấm tên công việc mở chi tiết. Mobile320px không tràn ngang.

Tệp thay đổi: frontend/src/compact/app.tsx, housekeeping-workspace.tsx, housekeeping-history.tsx; thêm housekeeping-history.css. Cập nhật kiểm thử compact-paper-inspection.mjs và phần kỳ vọng route staff của compact-housekeeping-supervisor.mjs. Tài liệu: docs/ui/compact-ux.md và kế hoạch cleaning-history.

Kiểm chứng:
- TypeScript/Vite build: Passed. Vite thông báo thư mục release ngoài frontend không tự xóa là dự kiến.
- Browser compact-paper-inspection: Passed route/menu staff mới, chuyển ngày trước/sau, dữ liệu lịch sử, hồi quy kiểm tra giấy, màn hình1440/768/375/320 và không lỗi runtime. Đã xem ảnh320px thực tế.
- Lần browser đầu bị connection-refused do preview chưa lắng nghe; đã kiểm tra HTTP200 rồi chạy lại đạt.
- SQL chỉ đọc: Bùi Viện có2 công việc CLEANING/REWORK đã gửi hoàn thành, Cao Thắng có0; timezone Asia/Ho_Chi_Minh. Không ghi dữ liệu hoặc thay API/database.
- Không sửa C#: dotnet build/test Not Run trong lượt này. API lịch sử hiện có được giữ nguyên; browser dùng fixture, không thực hiện nghiệp vụ thử trên Production.
- Frontend Production release artifacts/production-261123-cleaning-history, bundle index-Cd5bBEYs.js, launcher49360. API Production40444 giữ nguyên. Preview5082 đã dừng.
- Kiểm tra sau triển khai Passed: HTTPS5080 và bundle200, API ẩn danh401, TLS tin cậy, không lỗi runtime. Frontend listener18184.

Không commit/push; không còn lỗi triển khai đã biết trong phạm vi thay giao diện này. Test toàn bộ supervisor suite chưa chạy lại; chỉ cập nhật kỳ vọng của route staff để phù hợp chức năng mới.
