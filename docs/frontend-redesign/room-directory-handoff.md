# Bố cục danh mục phòng

Sửa HotelWorkspace.tsx, thêm room-directory-layout.css và mở rộng cacao-shell.test.mjs. CSS chỉ áp dụng workspace readOnly của màn rooms. Bộ lọc thành lưới 4/2/1 cột theo viewport, nhãn nằm trên trường, checkbox tách rõ, nút tải ở cuối và trạng thái ban đầu có hướng dẫn. Tách các liên kết điều hướng và thêm mô tả đầu trang. Giữ nguyên trường, giá trị, quyền và hành vi API. Không sửa backend/schema.

Build frontend Passed; 7/7 browser tests Passed với API fixture. Đã xem ảnh 1440px và 390px; không tràn ngang. Kiểm tra nhập branch/record và chuyển browse/detail giữ nút tải hoạt động. Chưa kiểm chứng dữ liệu phòng hoặc quyền bằng phiên đăng nhập thật. Không chạy backend build/test vì không sửa C#. Vẫn có cảnh báo bundle lớn và React Router use client như trước.
