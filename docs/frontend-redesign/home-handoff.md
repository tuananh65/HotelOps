# Trang chủ Cacao

## Trạng thái hiện tại: bỏ thống kê, nút cá nhân cố định

Theo yêu cầu mới, đã xóa toàn bộ thống kê thêm trên Home và ba tệp HomeStatistics.tsx, home-statistics.css, homeUserStatistics.ts. Không thay đổi màn Dashboard độc lập. Sửa HomePage.tsx, home.css, useHomeDisplay.ts và cacao-shell.test.mjs: bỏ thanh switch, thay bằng nút cá nhân cố định góc dưới bên phải, giữ lựa chọn theo tài khoản qua reload và mở rộng nội dung khi ẩn cột phải.

Build Passed; 6/6 browser tests Passed với API fixture. Kiểm tra desktop/mobile, nút position fixed, lưu lựa chọn, không gọi API thống kê/người dùng từ Home, không tràn ngang. Cảnh báo bundle lớn và React Router còn như trước. Không sửa API/DB; không chạy backend build/test. Các mục dưới đây là lịch sử thay đổi đã được thay thế bởi yêu cầu này.

## Thống kê quản trị và tùy chỉnh hiển thị

Khi navigation có /users nhưng không có /dashboard, Home dùng API danh sách người dùng hiện có để tổng hợp trạng thái tài khoản, chi nhánh chính và phòng ban. Đọc tối đa 100 bản ghi trang đầu; ghi rõ loaded/total và cảnh báo giới hạn trang đầu nếu danh sách lớn hơn. Không thay đổi quyền. Lỗi API không thay bằng dữ liệu mẫu. Thêm homeUserStatistics.ts và useHomeDisplay.ts; cập nhật HomeStatistics.tsx, HomePage.tsx, home.css và cacao-shell.test.mjs.

Hai switch “Thống kê” và “Khu vực cá nhân” lưu lựa chọn trong localStorage theo user ID, có dự phòng khi storage bị chặn. Ẩn cột cá nhân mở rộng nội dung chính; liên kết báo cáo/yêu cầu vẫn được giữ bên dưới. Khi không có nguồn được cấp quyền, hiển thị giải thích thay vì âm thầm biến mất.

Frontend build Passed, browser kiểm chứng quản trị và switch ở 1440/390px: biểu đồ theo dữ liệu API fixture, ẩn/hiện, giữ lựa chọn qua reload, không tràn ngang. Chưa truy cập phiên đăng nhập thật trên trình duyệt người dùng; không thay mật khẩu hoặc quyền để kiểm thử. Cảnh báo build cũ vẫn còn.

## Biểu đồ thống kê trên Home

Thêm HomeStatistics.tsx và home-statistics.css; tích hợp vào HomePage.tsx, mở rộng cacao-shell.test.mjs. Ba biểu đồ thanh thể hiện trạng thái phòng ghi nhận, trạng thái công việc buồng phòng và sự cố đang mở theo trạng thái/mức độ. Tái sử dụng readDashboard và response decoder hiện có; không thêm API hoặc sửa database. Chỉ mount khi navigation cấp /dashboard; dữ liệu gắn authority generation, hủy request khi unmount, 401 vô hiệu hóa phiên. Giữ số 0 thật, không thay lỗi hoặc nguồn thiếu bằng số 0. Có snapshot, phạm vi, cảnh báo nguồn thiếu và làm mới. Không giả định trạng thái phòng ghi nhận đồng nghĩa phòng sẵn sàng.

Build Passed; 7/7 browser tests Passed (API fixture), bao gồm kiểm tra số liệu, desktop/mobile, 503 và làm mới phục hồi, không gửi request khi thiếu quyền. Đã xem ảnh render 1440px và 390px; không tràn ngang. Chưa kiểm chứng dữ liệu Dashboard bằng phiên đăng nhập thật. Tài khoản không có /dashboard không thấy biểu đồ. Cảnh báo bundle lớn và React Router vẫn còn.

Home thay hàng liên kết đơn bằng danh mục theo nhóm nghiệp vụ, tìm kiếm tiếng Việt không dấu, ngày hiện tại và khu vực cá nhân. Giữ nguyên đường dẫn, HomeNavigation xác định quyền và các liên kết báo cáo/yêu cầu nội bộ có kiểm soát quyền sẵn có. Không thêm số liệu giả hoặc sửa API/database.

Tệp: thêm HomePage.tsx và home.css trong frontend/src/shared-host; sửa SharedAuthHost.tsx và bổ sung kiểm tra trong frontend/tests/cacao-shell.test.mjs.

Kiểm chứng: frontend build Passed; 5/5 browser tests Passed với API fixture, gồm Home ở 1440px và 390px, lọc không dấu, không có kết quả, chỉ hiện liên kết được cấp và mở trang người dùng. Đã xem ảnh desktop/mobile; không tràn ngang. Kiểm tra bố cục Impeccable không có finding. Không có reviewer agent độc lập; đánh giá trực tiếp trong phiên.

Còn cảnh báo build React Router use client và bundle trên 500kB như trước. Không chạy backend build/test vì không sửa C#. Chưa kiểm chứng phiên đăng nhập thật của người dùng; browser test dùng fixture, không thêm mock vào ứng dụng. Build cập nhật frontend/dist để host cục bộ dùng bản mới.
