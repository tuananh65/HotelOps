# Khởi tạo lại trạng thái phòng và dọn DEMO

Ngày 23/11/2026, người dùng duyệt reset 96 phòng và xóa các bản ghi đánh dấu DEMO cùng dữ liệu phụ thuộc. Phương án được thử trên bản sao có backup trước khi áp dụng `HotelOpsCompactDB`.

`Reset-DemoReady.ps1` sao lưu có kiểm chứng, sau đó chạy `reset-demo-ready.sql` trong một transaction. Script giữ tài khoản, grants, tài sản và audit; ghi một audit quản trị và sự kiện `ADMIN_BASELINE_RESET` cho mỗi phòng. Các trigger bảo vệ lịch sử liên quan được tạm ngưng trong transaction và bật lại trước commit. Không tạo kết quả inspection giả. Đây là công cụ quản trị theo yêu cầu cụ thể, không phải thao tác nghiệp vụ thường ngày.

View `v_room_readiness` và định nghĩa trong `04_Level2.sql` nhận sự kiện khởi tạo chỉ khi khớp phòng/lượt sử dụng hiện tại, không có cleaning hiện tại và trạng thái vận hành phù hợp. Room Block vẫn ưu tiên. Checkout tăng lượt sử dụng nên trạng thái trở lại `PENDING_CLEANING`; quy trình cleaning/inspection tiếp tục như cũ.

Kết quả áp dụng: xóa 28 công việc DEMO, 10 chứng từ DEMO và các dữ liệu phụ thuộc; 96 phòng Ready. Kiểm tra sau ghi: 0 trigger bị tắt, 0 tồn âm, 57 tài khoản, 131 grants và 56 role assignments. Checksum grants/assignments trùng bản sao trước reset. Backup chính được xác minh tại `artifacts/workday/backup-HotelOpsCompactDB.json`.

`Verify-DemoReady.ps1` chỉ đọc kết quả. `Test-DemoReadyLifecycle.ps1` chỉ cho phép database clone UX; thử block, occupancy và checkout rồi rollback. Không chạy test mutation lên primary.
