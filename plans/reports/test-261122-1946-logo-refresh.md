# Thay logo Đức Vượng Group

Đã dùng nguyên ảnh `C:/Users/Hax/Downloads/logo.jpg` tại mọi vị trí nhận diện đang được tham chiếu trong frontend và prototype V10: đăng nhập/đổi mật khẩu, sidebar, biểu tượng tab, logo đầu/cuối sidebar của giao diện shared-host cũ. Giữ đúng tỷ lệ ảnh; đường dẫn JPG mới tránh dùng lại ảnh PNG/JPEG đã cache.

## File thay đổi

- `frontend/public/duc-vuong-logo.jpg` — ảnh mới, byte giống file người dùng.
- `frontend/index.html` — favicon.
- `frontend/src/compact/app.tsx` — sidebar.
- `frontend/src/compact/auth.tsx` — đăng nhập/đổi mật khẩu.
- `frontend/src/compact/compact.css` — kích thước logo đăng nhập.
- `frontend/src/shared-host/CacaoShell.tsx` — logo đầu/cuối sidebar cũ.
- `frontend/src/styles/design-system.css` — giữ tỷ lệ ảnh shared-host.
- `HotelOps_UX_Prototype_V10/assets/duc-vuong-logo.jpg` — ảnh mới cho prototype.
- `HotelOps_UX_Prototype_V10/index.html` — favicon prototype.
- `HotelOps_UX_Prototype_V10/js/ui.js`, `js/v8-compat.js`, `js/v9-compat.js` — logo prototype và các lớp tương thích.

## Kiểm chứng

- Đối chiếu byte ảnh được FE phục vụ với file nguồn: **Passed**, HTTP 200, MIME image/jpeg.
- Browser desktop/mobile: logo đăng nhập tải thành công, favicon đúng; không tràn ngang ở 375px. Đã xem screenshot.
- Sidebar ứng dụng: **Passed**, dùng profile giả và chặn toàn bộ request API để tránh ghi database.
- Prototype V10 đang chạy tại cổng 5188: logo và favicon mới **Passed**.
- Tìm tham chiếu logo PNG/JPEG cũ và chữ H trong các vị trí brand đã sửa: không còn.
- Build FE: **Passed** (`npm run build`: TypeScript + Vite), không có lỗi/cảnh báo.
- Không đổi C#, database hoặc quyền; không cần build/test backend. Không chạy lại bộ regression toàn hệ thống cho thay ảnh.
- Shared-host cũ không phải entry point hiện tại; đã sửa mã và kiểm tra build, chưa mở riêng giao diện cũ.

Screenshot kiểm chứng nằm trong `artifacts/logo-refresh/`. Không commit/push/deploy; giữ nguyên FE 5186 và API 5768 đang chạy.
