# Chọn đối tượng trên lịch làm việc

Đã thêm dropdown Đối tượng trong lịch quản lý chi nhánh. Lựa chọn được lấy từ cặp vai trò/bộ phận của ca hiện có, gộp các ca sáng/chiều/đêm cùng nhóm, giữ các nhóm cùng vai trò nhưng khác bộ phận riêng biệt. Tất cả đối tượng hiển thị toàn lịch; chọn nhóm chỉ hiển thị ca và nhân sự của nhóm đó. Lựa chọn được giữ khi chuyển tuần và tải lại sau phân ca; danh sách nhân viên đủ điều kiện vẫn dùng API cũ. Lịch cá nhân giữ nguyên phạm vi và không hiện bộ lọc quản lý.

File sửa: `frontend/src/compact/workday.tsx`, `frontend/src/compact/task-ux.css`. File mới: `frontend/tests/compact-workday-filter.mjs`.

Build frontend **Passed**, 1620 modules, không cảnh báo. Prettier **Passed**. Kiểm thử mới **Passed** bằng API giả lập: gộp/phan biệt nhóm, lọc ba ca, đổi tuần, đúng shiftId/ngày khi phân ca, tất cả đối tượng, lịch cá nhân và lịch trống; responsive 1440/768/375. Không lỗi JavaScript; POST phân ca chỉ gửi tới mock. Kiểm thử `compact-navbar-titles.mjs` **Passed** 48 route/viewport.

Bằng chứng: `artifacts/workday-filter/verification.json`, ảnh cùng thư mục. Ảnh mobile được chụp sau khi chuyển động đóng sidebar kết thúc để không ghi lại khung hình giữa chuyển động. Không sửa backend hoặc database; C# build/test và API/database thật **Not Run**. Không commit/push, không khởi chạy thêm server; trình duyệt kiểm thử tự đóng.
