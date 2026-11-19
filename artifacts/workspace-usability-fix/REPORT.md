# Sửa trải nghiệm các trang công việc và vật tư

Ngày kiểm tra: 19/11/2026.

## Thay đổi

1. **Trạng thái phòng:** đổi tầng sẽ bỏ khu vực/hành lang đã chọn; danh sách khu vực theo tầng hiện tại, không hard-code riêng tầng 1.
2. **Tạo công việc:** mở popup tại danh sách công việc buồng phòng. Hiển thị tên chi nhánh được phép làm việc; nhập/chọn số phòng và chuyển sang ID nội bộ khi gửi API. Cả biểu mẫu tạo công việc vận hành cũng dùng số phòng và tên chi nhánh.
3. **Phân công kiểm tra:** nút “Phân công kiểm tra” tại hàng chờ mở popup, giữ kiểm tra quyền, checklist và điều kiện ghi của API.
4. **Lịch sử kiểm tra:** bỏ các ô nhập ID phòng/người kiểm tra/người dọn phòng; giữ tìm số phòng/mã kiểm tra, ngày, kết quả và kích thước trang.
5. **Lịch sử luân chuyển:** tự chọn phòng đầu tiên được phép xem và tải lịch sử khi vào trang. Đây là lịch sử của phòng được chọn, chưa phải báo cáo gộp tất cả phòng. Liên kết có phòng cụ thể vẫn được giữ; sửa tham số phân trang.
6. **Làm lại:** bỏ giới hạn mặc định “hôm nay” khiến dữ liệu cũ bị ẩn. Ngày lọc là tùy chọn; kết quả vẫn giới hạn theo quyền, chi nhánh và phân trang. DTO ngày bắt đầu/kết thúc cho phép null để phản ánh đúng bộ lọc.
7. **Thông báo:** bố cục thẻ, trạng thái chưa đọc, thời gian Việt Nam, bộ lọc và thao tác hàng loạt rõ hơn; hiển thị tốt ở chiều rộng 390px và 1280px.
8. **Yêu cầu vật tư:** thu gọn thanh thao tác, bỏ các điều hướng/thao tác trùng ở danh sách và tạo mới, cải thiện khoảng cách và bộ lọc.
9. **Đóng popup vật tư:** quay về danh sách/chi tiết phù hợp khi hủy, kể cả đã nhập dữ liệu; giữ xác nhận bỏ thay đổi và không tự gửi POST.
10. **Công việc được giao:** bộ lọc và nhãn danh sách dùng cùng trạng thái công việc đã ghi nhận (TODO, IN_PROGRESS, COMPLETED, CANCELLED), tránh dùng trạng thái workflow để lọc các bản ghi cũ. Chi tiết vẫn hiển thị thông tin workflow khi có.

Không thay đổi schema, vai trò hoặc dữ liệu quyền trong lượt sửa này. Không commit, push hay deploy.

## File mã nguồn

- frontend/src/screens/rooms/RoomStatusBoardScreen.tsx
- frontend/src/screens/housekeeping/RoomWorkspace.tsx
- frontend/src/screens/housekeeping/CreateCleaningTaskScreen.tsx
- frontend/src/screens/housekeeping/CleaningTaskListScreen.tsx
- frontend/src/screens/housekeeping/RoomTurnoverHistoryScreen.tsx
- frontend/src/features/inspectionExecution/PendingInspectionsPage.tsx
- frontend/src/features/inspectionExecution/InitialInspectionAssignmentPage.tsx
- frontend/src/features/inspectionHistory/HistoryWorkspace.tsx
- frontend/src/operationalTasks/api.ts
- frontend/src/operationalTasks/TaskReadPage.tsx
- frontend/src/operationalTasks/TaskCreatePanel.tsx
- frontend/src/dev2/Workspace.tsx
- frontend/src/dev2/operations-read.css
- frontend/src/notifications/NotificationCenter.tsx
- frontend/src/notifications/notifications.css (mới)
- frontend/src/api/dev1/cleaningMonitor.ts
- src/HotelOps.Api/Services/Housekeeping/CleaningMonitorService.cs
- src/HotelOps.Api/DTOs/Housekeeping/CleaningMonitorDtos.cs

## File kiểm thử

- frontend/tests/room-workspace-browser.test.mjs
- frontend/tests/mai-access-ui.test.mjs
- frontend/tests/requests-maintenance-ui.test.mjs
- frontend/tests/time-workspace-browser.test.mjs
- frontend/tests/operational-task-read.test.mjs
- frontend/tests/cleaning-monitor.test.mjs
- frontend/tests/operational-task-planning.test.mjs
- frontend/tests/cleaning-create.test.mjs
- frontend/tests/notification-layout-browser.test.mjs (mới)
- tests/HotelOps.Dev1.RegressionTests/WorkspaceReadSqlTests.cs (mới)

Một số fixture và selector cũ đã được cập nhật cho receipt API hiện tại, nhãn tiếng Việt và bộ lọc thu gọn; không nới lỏng validator để làm test qua.

## Kiểm chứng

- Backend build: **Passed**, 0 lỗi, 0 cảnh báo (`dotnet build HotelOps.slnx --no-restore`).
- `dotnet test HotelOps.slnx --no-restore`: thành công; solution hiện chỉ chứa API nên chạy thêm project regression riêng.
- Regression backend liên quan quyền monitor, route, luân chuyển, thông báo đổi người kiểm tra: **53 Passed**.
- SQL thực tế: **1 Passed**, đọc dữ liệu/quyền hiện có của mai.tran; xác nhận làm lại tự tải dữ liệu cũ, lọc ngày đúng, chi nhánh ngoài phạm vi bị từ chối và số audit không đổi. Ranh giới session được thay thế trong test; đây không phải đăng nhập API bằng tài khoản thật.
- Frontend build: **Passed**. Vẫn có cảnh báo React Router directive và bundle JS khoảng 1,93 MB lớn hơn ngưỡng 500 kB.
- Lượt kiểm thử frontend cuối: **96 Passed, 0 Failed, 0 Skipped**, xem `final-tests.log`.
- Browser tests chạy React thật trên Edge với HTTP mô phỏng. Kiểm tra popup, số phòng, lọc tầng, quyền, danh sách, đóng vật tư không POST và bố cục thông báo desktop/mobile.
- Các bài kiểm thử nhật ký thời gian, đề nghị điều chỉnh và quyền ca làm việc đã chạy qua với HTTP mô phỏng. Chưa chấm công/gửi đề nghị thật bằng tài khoản người dùng.
- API Debug local đã khởi động lại; endpoint monitor trả 401 khi chưa đăng nhập, Vite trả 200.

Log frontend: `frontend-build.log`, `final-tests.log`; các log lần kiểm tra trước được giữ nguyên để truy vết. Các lỗi fixture cũ trong `regression.log`/`verification.log` đã được sửa và chạy lại.

Ảnh kiểm tra: `screenshots/notifications-1280.png`, `screenshots/notifications-390.png`; popup công việc ở `../room-workspace-fix/screenshots/cleaning-create-popup.png`.

## Kiểm tra lại trên web

1. Đổi tầng sau khi chọn hành lang; kiểm tra hành lang cũ bị bỏ và lựa chọn mới đúng tầng.
2. Mở Tạo công việc, kiểm tra tên chi nhánh, nhập số phòng thuộc chi nhánh; thử hủy và mở lại.
3. Tại hàng chờ kiểm tra, mở nút Phân công kiểm tra bằng tài khoản có quyền.
4. Vào lịch sử luân chuyển và Làm lại không nhập bộ lọc; kiểm tra dữ liệu tự tải.
5. Lọc từng trạng thái trong Công việc được giao và đối chiếu nhãn kết quả.
6. Tại vật tư, mở Tạo mới rồi đóng; thử lại sau khi nhập dữ liệu và xác nhận bỏ thay đổi. Danh sách phải xuất hiện lại.
7. Xem thông báo trên màn hình rộng/hẹp; thử chọn thông báo và đánh dấu đã đọc.

Giới hạn: chưa thực hiện toàn bộ luồng ghi bằng phiên đăng nhập thật trên dữ liệu chung. Kết quả browser mô phỏng và kiểm tra SQL đọc riêng được phân biệt như trên.
