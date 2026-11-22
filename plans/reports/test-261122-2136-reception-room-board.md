# Phòng dành cho Lễ tân

Đã ẩn Tổng quan khi vai trò trong chi nhánh hiện tại chỉ là Lễ tân; trang mặc định và đường dẫn dashboard cũ mở Phòng. Tài khoản có vai trò quản lý khác vẫn giữ dashboard.

Chi tiết phòng trượt từ mép phải, đầy chiều cao màn hình; mobile dùng toàn chiều rộng. Giữ thao tác nhận/trả/chuyển phòng, báo sự cố và lịch sử. Hạng phòng, sức chứa lấy từ danh mục room-types đúng chi nhánh (đã có quyền đọc ROOM_VIEW). Thẻ có nền xanh lá khi sẵn sàng, xanh dương khi có khách, vàng khi cần phục vụ, đỏ khi chặn; bỏ dòng Không có khách.

Lỗi phòng 102 do nhãn dùng readiness của khâu phục vụ dù isOccupied=true. Đã tái hiện kiểm thử thất bại trước sửa, sau sửa nhãn chính Có khách; cảnh báo chặn/cần phục vụ vẫn hiện nếu có. Không thay đổi readiness, quy trình, quyền hoặc dữ liệu database.

File sửa: `frontend/src/compact/app.tsx`, `rooms.tsx`, `ui.tsx`, `task-ux.css`. Dialog thêm class tùy chọn cho drawer và nhãn truy cập; giữ tham chiếu phần tử dialog trong cleanup để đóng và trả focus đúng sau unmount. File kiểm thử mới: `frontend/tests/compact-reception-rooms.mjs`. Kế hoạch: `plans/261122-2136-reception-room-board/plan.md`.

Build frontend **Passed** (`npm run build`, 1620 modules, không cảnh báo). Prettier **Passed**. Kiểm thử mới **Passed** bằng API giả lập: điều hướng Lễ tân, nhãn occupied, thông tin loại phòng/sức chứa, màu phủ ô, drawer bên phải, Escape và focus, nhận/trả phòng, danh sách đích chuyển chỉ gồm phòng sẵn sàng không có khách, cảnh báo chặn phòng đang có khách, responsive 1440/768/375, giảm chuyển động và dashboard của vai trò khác. Đã sửa lỗi focus phát hiện ở lượt kiểm thử đầu, kiểm thử lại đạt. `compact-navbar-titles.mjs` **Passed** 48 tổ hợp route/viewport, dialog và sticky navbar/table. Không lỗi JavaScript.

Đã xem ảnh board desktop, drawer desktop và mobile. Bằng chứng: `artifacts/reception-rooms/verification.json`, `board-1440.png`, `drawer-1440.png`, `drawer-768.png`, `drawer-375.png`.

API/database thật: **Not Run**; mọi POST trong kiểm thử đều bị chặn và trả lời bằng mock. C# build/test: **Not Run**, không sửa backend. Không commit/push, không khởi chạy thêm server; các trình duyệt kiểm thử đã đóng.
