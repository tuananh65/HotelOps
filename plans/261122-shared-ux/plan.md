# Chuẩn hóa UX toàn bộ giao diện compact

Phạm vi: frontend các trang đang dùng trong HotelOps; giữ nghiệp vụ, quyền và dữ liệu hiện tại. Không sửa schema/backend. Các công việc trước đã hoàn thành; lượt này xử lý nguyên nhân lặp lại trong bố cục chung.

Brief: Product; nhân viên/quản lý khách sạn dùng desktop và điện thoại. Giữ phong cách hiện tại, màu xanh #21665e, nền #f4f7f6, chữ #203b38, Segoe UI. Bố cục gọn, cùng lề navbar, thao tác cạnh bộ lọc; màu trạng thái phòng là điểm nhận diện. Mật độ 6/10, chuyển động 2/10.

- [x] Ghi nhận hiện trạng và kiểm tra các tuyến trang ở 1440/768/375.
- [x] Chuẩn hóa khoảng cách đầu trang, toolbar/tìm kiếm/nút chính; bỏ hàng làm mới dư thừa.
- [x] Sửa trạng thái rỗng khi tìm kiếm, khả năng thử lại và hành vi chung dialog/phân trang nếu có lỗi thực tế.
- [x] Kiểm tra build, hồi quy UI nhiều vai trò, responsive, bàn phím, trạng thái lỗi/rỗng và ngăn trượt phòng.
- [x] Ghi tài liệu chuẩn UX và báo cáo bằng chứng, giới hạn.

Điều kiện bàn giao: tiêu đề chỉ ở navbar; không hàng trống do portal; các trang dùng chung khoảng cách đầu trang; desktop thao tác liên quan cùng nhóm; mobile không tràn ngoài viewport; nút hành động không mất; không khôi phục dashboard lễ tân/các mục admin đã bỏ; phòng vẫn 15/trang, 5 cột desktop. Kiểm thử API giả lập, không login/ghi DB thật. Cần phân biệt kiểm chứng UI với backend thực.

Process: tái sử dụng Vite PID 6424/9324 port 5186 và API dotnet PID 31332; không tạo server mới. Đọc chi tiết process qua CIM bị môi trường từ chối, Get-Process xác nhận vẫn chạy. Không có process thừa do lượt này tạo.
