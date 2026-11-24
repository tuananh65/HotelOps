# Phòng đã đặt và tự kết thúc ca

Thực hiện bằng một agent. Phạm vi Project 2; giữ quyền Reception hiện có, không mở quyền cho role khác.

- [x] Rà soát RoomService, Workday, constraints/views/triggers, frontend và ảnh người dùng.
- [x] Bỏ lý do nhận/trả phòng; Việt hóa lịch sử, trạng thái và tên hạng phòng chuẩn.
- [x] Thêm RESERVED: đặt, hủy đặt, xác nhận khách đến; chuyển đặt giữ vệ sinh nguồn, chuyển khách đã ở làm bẩn nguồn.
- [x] Tự kết thúc ca theo giờ chi nhánh, audit hệ thống, chống xử lý lặp; chặn start trước/sau giờ ca; cập nhật UI.
- [x] Chuẩn bị patch SQL constraint/readiness/trigger, kiểm chứng trên fixture; sao lưu trước mọi thay đổi database.
- [x] Build FE/BE, unit tests, API/SQL fixtures, browser.
- [x] Người dùng duyệt sao lưu/áp dụng Production cục bộ ngày 23/11/2026; patch primary và Release API đã chạy. Health 200, frontend 200, anonymous 401. Ba ca quá giờ tự kết thúc lúc 08:00 Việt Nam, audit actor hệ thống.
- [x] Báo cáo: plans/reports/implementation-261123-1324-reservations-shift-expiry.md.
