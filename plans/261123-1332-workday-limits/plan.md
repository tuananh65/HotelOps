# Phân ca theo bộ phận, tuần và xác nhận vượt giờ

Phạm vi Project 2, một agent; không sửa schema/grants, không ghi lịch thử lên primary. Giữ Production HTTPS 5080.

- [x] Kiểm tra source, Workday schema và quan hệ membership/role thực tế. Dùng quan hệ nhân sự hiện hành để xác định đối tượng thuộc bộ phận, không hardcode role ID.
- [x] Khóa đối tượng trước khi chọn bộ phận; lọc role, xóa lựa chọn cũ khi đổi bộ phận; backend kiểm tra cùng quy tắc.
- [x] Thêm phân ca theo tuần (thứ Hai–Chủ nhật), giữ tùy chọn ngày/tháng, hỗ trợ tuần qua tháng/năm và lịch lễ.
- [x] Tính tổng phút của lịch chưa hủy trên mọi ca của nhân viên; cảnh báo vượt 480 phút/ngày hoặc 2400 phút/tuần. Xác nhận gắn với dữ liệu đã xem; backend kiểm tra lại trong transaction, giữ audit.
- [x] Căn giữa logo login; tăng lớp tối nền, không đổi ảnh.
- [x] Build, unit/API fixture, kiểm thử trình duyệt; tự review và ghi báo cáo. Tiếp tục không dùng sub-agent theo yêu cầu người dùng.
- [x] Cập nhật Production cục bộ đã được yêu cầu trong phiên; chỉ thay build, không sửa dữ liệu nghiệp vụ.

Giờ giới hạn là giờ được phân theo ca, không phải thời gian timer công việc. Chỉ vượt ngưỡng mới cần xác nhận; đúng 8/40 giờ không cảnh báo. Lịch hủy không tính, lịch đã kết thúc vẫn tính. Xác nhận không bỏ qua trùng ca, năng lực, sức chứa, scope hoặc phiên bản.

Kết quả: build backend 0 warning/error; unit 53 PASS/3 SKIP; SQL/API fixture 25 PASS; Edge fixture và login desktop/mobile PASS. Production tại HTTPS5080, API PID19928 và frontend PID13196; preview5082/API fixture đã dừng. Báo cáo: `plans/reports/implementation-261123-workday-limits.md`.
