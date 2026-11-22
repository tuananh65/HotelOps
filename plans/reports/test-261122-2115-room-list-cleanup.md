# Rút gọn trang danh sách phòng

Đã bỏ nút Chỉnh sửa khách sạn, Thêm danh mục phòng, liên kết Tất cả khách sạn và hai tiêu đề tầng/Danh mục phòng khỏi trang danh sách phòng. Bỏ cột Bộ phận. Ô Tìm phòng căn trái, nút Tầng & phòng nằm cùng hàng trên desktop; màn hình nhỏ tự xuống hàng để giữ vùng bấm.

Thay đổi chỉ áp dụng cho trang phòng trong cấu trúc khách sạn. Giữ chỉnh sửa từng phòng và điều hướng về danh sách tầng; việc ẩn cột không thay đổi dữ liệu bộ phận của phòng.

## File sửa

- `frontend/src/compact/hotel-workspace.tsx`: bỏ các thành phần dư trong trang phòng, truyền nút quay lại vào thanh tìm kiếm.
- `frontend/src/compact/catalog.tsx`: ẩn tiêu đề, nút thêm và cột Bộ phận ở danh sách phòng nhúng; bỏ request lấy bộ phận chỉ phục vụ cột này.
- `frontend/src/compact/task-ux.css`: căn trái ô tìm phòng và bố trí nút quay lại.

## Kiểm chứng

- Build frontend: **Passed**, `npm run build`, 1620 modules, không cảnh báo. Đã chạy lại sau gián đoạn vì không thu hồi được log build ban đầu.
- Prettier: **Passed** cho cả ba file.
- Kiểm thử trình duyệt trang phòng: **Passed**, API giả lập; xác nhận các nút/tiêu đề/cột đã bỏ, căn hàng desktop, lọc đúng tầng, tìm kiếm, mở dialog sửa phòng, quay về tầng và mở phòng lại. Desktop 1440px/mobile 375px không tràn ngang toàn trang, không lỗi JavaScript, không request ghi dữ liệu.
- Hồi quy cấu hình loại phòng/bộ phận: **Passed**, hai chi nhánh, thêm/chỉnh sửa giả lập, điều hướng cũ, desktop/mobile và trường hợp không có chi nhánh.
- Bằng chứng: `artifacts/room-list-cleanup/verification.json`, `rooms-1440.png`, `rooms-375.png`; `artifacts/structure-settings/verification.json`.
- Backend/database không thay đổi; API thật và C# build/test: **Not Run**. Không có lỗi còn tồn tại trong phạm vi kiểm tra. Không commit/push.
