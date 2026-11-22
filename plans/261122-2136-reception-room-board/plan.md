# Luồng phòng cho Lễ tân

- [x] Ẩn dashboard đối với vai trò Lễ tân, mặc định mở Phòng; giữ các vai trò khác.
- [x] Chuyển chi tiết phòng sang ngăn trượt từ phải; giữ thao tác, lịch sử và điều khiển bàn phím.
- [x] Thẻ và chi tiết hiển thị hạng phòng, sức chứa từ danh mục cùng chi nhánh, bỏ dòng Không có khách, phủ màu nền theo tình trạng.
- [x] Sửa nhãn chính để phòng có khách không hiển thị Sẵn sàng; giữ nguyên điều kiện nghiệp vụ và dữ liệu readiness.
- [x] Kiểm chứng build, trình duyệt giả lập, responsive, nhận/trả phòng và danh sách phòng chuyển hợp lệ; báo cáo.

Nguyên nhân lỗi 102: `rooms.tsx` đã phân loại occupied cho bộ lọc/màu nhưng Badge dùng readiness trực tiếp. API cung cấp isOccupied và readiness riêng theo yêu cầu được duyệt. Sửa biểu diễn ở frontend, không đổi trạng thái database hoặc cấp quyền. Danh mục room-types đã cho ROOM_VIEW đọc tên và Capacity theo branch; dùng truy vấn này thay vì sửa API/schema.
