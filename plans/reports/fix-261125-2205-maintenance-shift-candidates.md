# Sửa danh sách nhân viên bảo trì khi phân ca

Ngày25/11/2026. Đã triển khai bản sửa trên Development5173/API5081 và Production HTTPS5080. Production giữ HotelOpsCompactDB, Development giữ database riêng.

## Nguyên nhân và thay đổi

Năm nhân viên bảo trì Bùi Viện có role/EMPLOYMENT hiệu lực từ21:49 sau khôi phục đội bảo trì. Ca44 từ16:00 đến24:00 đang diễn ra, nhưng EligibleQuery yêu cầu hiệu lực từ16:00, nên không có ứng viên. Kiểm kê chỉ đọc trên database chính xác nhận bộ lọc cũ0 người, điều kiện mới5 người, giữ cùng điều kiện chi nhánh/bộ phận/vai trò và trùng lịch.

WorkdayService dùng mốc bắt đầu kiểm tra điều kiện là max(giờ bắt đầu ca, thời điểm hiện tại). Ca tương lai vẫn cần đủ quyền/bộ phận suốt ca; ca đang diễn ra cần đủ điều kiện tới hết ca. Các lệnh phân ca đơn/hàng loạt và bắt đầu ca dùng chung bộ lọc. Không sửa thời điểm cấp vai trò hoặc tạo giờ chấm công giả; không thay đổi dữ liệu nghiệp vụ để làm hiện danh sách.

## Files

- src/HotelOps.Api/Compact/Services/WorkdayService.cs
- tests/HotelOps.Compact.Tests/WorkdayCandidateTests.cs

## Kiểm chứng

- Backup CHECKSUM/VERIFYONLY database chính và clone HotelOpsCompactWorkdayTestCandidates2611252205 trước kiểm thử.
- Build Release: Passed,0 errors,0 warnings.
- Test toàn solution với HOTELOPS_WORKDAY_CANDIDATE_TEST trỏ clone:159 passed,0 failed,3 skipped. Ba bài account/maintenance/recurrence tích hợp vẫn chưa bật cấu hình riêng.
- Bài mới gọi Candidates và BatchCandidates thật qua EF/SQL, xác minh nhân viên phục hồi được chọn trong ca còn hiệu lực, ngày mai vẫn có ứng viên, ca hết giờ bị từ chối, kết quả không lẫn nhân viên khác chi nhánh hoặc trùng lịch, không thêm assignment/event/audit.
- Publish thành công; hash DLL publish khớp bản build đã test. Hai runtime health ok/57 bảng, đúng database và cổng.
- Không chạy thao tác phân ca thật hoặc bắt đầu ca cho người dùng trên Production; không xác minh hộp chọn qua phiên trình duyệt đăng nhập thật. Đóng/mở hộp phân ca hoặc tải lại trang để lấy danh sách mới.

Lần truy vấn chẩn đoán đầu dùng alias SQL reserved word và bị từ chối; đã sửa alias rồi chạy lại thành công, chỉ đọc. Git cảnh báo chuẩn hóa LF/CRLF, không có lỗi whitespace. Không thay schema, sửa mật khẩu, commit/push hoặc triển khai EC2.
