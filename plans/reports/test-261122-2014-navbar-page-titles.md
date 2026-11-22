# Tiêu đề các trang trên navbar

## Kết quả

Đưa tiêu đề và mô tả của các trang đã đăng nhập lên navbar cố định: Dashboard quản trị/vận hành/cá nhân, Khách sạn và chi tiết khách sạn, Cấu hình hệ thống, Nhân sự, Phòng, Công việc, Lịch làm việc, Chứng từ, Tài sản, Tồn kho, Danh mục vật tư, Checklist, Báo cáo và Thông báo. Các tiêu đề phụ trong nội dung và hộp thoại giữ đúng phạm vi. Nút thao tác và bộ lọc vẫn ở nội dung; vùng tiêu đề rỗng không chiếm khoảng trống.

Mỗi trang sở hữu nội dung tiêu đề qua React portal tới navbar chung, nên tên khách sạn và tiêu đề theo vai trò cập nhật khi trang thay đổi. Trên điện thoại, tiêu đề xuống hàng dưới cụm nút navbar để tránh chồng lấn.

## File tạo/sửa trong lượt này

Trong `frontend/src/compact/`:

- Tạo `page-title.tsx`.
- Sửa `app.tsx`, `task-ux.css`.
- Sửa `admin.tsx`, `assets.tsx`, `catalog.tsx`, `documents.tsx`, `hotel-workspace.tsx`, `insights.tsx`, `management.tsx`, `personal-overview.tsx`, `rooms.tsx`, `stock.tsx`, `templates.tsx`, `work.tsx`, `workday.tsx`.

Kiểm thử/tài liệu:

- Tạo `frontend/tests/compact-navbar-titles.mjs`.
- Tạo `plans/261122-2014-navbar-page-titles/plan.md`, báo cáo này và `plans/journals/2026-11-22-hotelops-navbar-page-titles.md`.
- Bằng chứng tại `artifacts/navbar-page-titles/verification.json` và 6 ảnh chụp hotels/roles-scrolled ở 1440/768/375 px.

## Kiểm chứng

- Build FE: **Passed** — `npm run build` tại `frontend`, TypeScript và Vite thành công; không có cảnh báo build.
- Format: **Passed** — Prettier kiểm tra các file đã sửa.
- Browser: **Passed** — 48 trường hợp route/kích thước màn hình, mỗi trang có đúng một h1 trên navbar và không còn h1 trong nội dung; không tràn chiều ngang hoặc chồng tiêu đề với tài khoản.
- **Passed** — đổi hash trong cùng shell, tiêu đề theo khách sạn và ba loại Dashboard, mở hộp thoại thêm khách sạn không thay tiêu đề trang, navbar và tiêu đề bảng giữ vị trí khi cuộn. Không phát sinh lỗi JavaScript trong lần chạy cuối.
- Đã xem ảnh chụp tablet/mobile để kiểm tra bố cục. Mô hình khách sạn có thể hiện hình chờ trong ảnh mobile khi tài nguyên 3D chưa tải xong; không thay đổi module 3D.
- API/database thật: **Not Run** — mọi request API được chặn và trả dữ liệu giả lập; không đăng nhập thật, không gửi request thay đổi dữ liệu.
- BE build/test: **Not Run** — không sửa backend.

## Ghi nhận và giới hạn

Lệnh build đầu tiên chạy nhầm ở root (không có package.json); chạy lại đúng `frontend` đã qua. Các lần browser test ban đầu lỗi do fixture trả sai dạng collection cho users/insights-time và chưa reload profile khi đổi vai trò giả lập; đã sửa harness và chạy lại toàn bộ thành công. `git diff --check` không có lỗi whitespace, chỉ cảnh báo LF/CRLF trên ba file đã sửa từ trước (package.json, package-lock.json, main.tsx); lượt này không sửa các file đó. Nhiều file compact hiện chưa được Git theo dõi từ trước nên diff Git không mô tả riêng toàn bộ thay đổi lượt này.

Không phát hiện lỗi còn lại trong phạm vi kiểm tra navbar. Kiểm thử này xác nhận UI với dữ liệu giả lập, không xác nhận lại nghiệp vụ backend/database. Không commit, push hoặc deploy. AgentKit cook/journal dùng trong một agent; AgentWiki publish skipped.
