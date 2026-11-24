# Nhãn phòng và popup thông báo — 24/11/2026

Đã cập nhật Production cục bộ https://127.0.0.1:5080/.

## Kết quả

- BLOCKED hiển thị “Bảo trì” trên tab, bộ lọc, bảng, badge và chi tiết phòng. Không đổi mã trạng thái hoặc nghiệp vụ.
- Hủy đặt phòng và nút xác nhận hủy màu đỏ; Chuyển phòng và nút xác nhận chuyển màu vàng, có hover/focus/disabled và chữ tương phản.
- Các alert/notice trong compact dùng popup góc dưới với Thu gọn/Mở ra. Nội dung mới tự mở. Nhiều popup xếp theo từng dialog; màn hình thấp cuộn dọc trong popup. Giữ retry, form và xác nhận vượt giờ; không che nút đóng drawer.

## Tệp thay đổi trong lượt này

Mới: `frontend/src/compact/alert-popup.tsx`, `alert-popup.css`, `room-actions.css`; `frontend/tests/compact-alert-popups.mjs`, `verify-room-alerts-production.mjs`.

Sửa giao diện: `rooms.tsx`, `ui.tsx` và thay container thông báo trong `account-management.tsx`, `admin.tsx`, `app.tsx`, `assets.tsx`, `auth.tsx`, `catalog.tsx`, `documents.tsx`, `hotel-workspace.tsx`, `housekeeping-checklists.tsx`, `insights.tsx`, `internal-settings.tsx`, `inventory-documents.tsx`, `maintenance-plans.tsx`, `paper-inspection.tsx`, `personal-profile.tsx`, `reset-password-form.tsx`, `role-permissions.tsx`, `room-issues.tsx`, `templates.tsx`, `work.tsx`, `workday-assignment.tsx`, `workday-settings.tsx`, `workday.tsx`. Các tệp này thuộc `frontend/src/compact/`.

Sửa test: `compact-reception-rooms.mjs`, `compact-operations-manager.mjs` đổi nhãn; `compact-workday-scheduling.mjs` phân biệt popup lỗi với popup cảnh báo vượt giờ. Cập nhật `docs/ui/compact-ux.md` và plan `plans/261124-0921-room-alerts/plan.md`. Giữ các thay đổi có sẵn của phiên trước; không commit/push.

## Kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| TypeScript `tsc -b`, Vite production build | Passed |
| Popup: nhiều lỗi, retry, bàn phím, thu gọn/mở ra, form không gửi ngoài ý muốn, login | Passed |
| Responsive 1440/375/320px, viewport thấp 140px, nút đóng drawer | Passed |
| Luồng phòng lễ tân | Passed |
| Phân ca/vượt giờ/xác nhận lại sau xung đột | Passed |
| Kiểm tra giấy và lịch sử theo ngày | Passed |
| Cấu hình nội bộ, toggle, lỗi concurrency và mặc định yêu cầu | Passed |
| Bộ quản lý vận hành cũ | Failed tại kỳ vọng 9 menu so với 10 thực tế; mục Sự cố phòng đã có từ trước, không do popup. Các bước sau chưa chạy. |
| Production thực | HTTP 200, bundle mới 200, TLS hợp lệ, login hiển thị, API chưa đăng nhập 401, không lỗi JavaScript |
| API health chỉ đọc | 200, schema 2.1.0, 40 bảng |
| .NET build/test, DB workflow | Not Run: không sửa C#/API/database trong lượt này |

Các kiểm thử nghiệp vụ giao diện dùng API giả lập, không ghi dữ liệu primary. Không cần thay schema, grants hoặc sao lưu database cho bản frontend này. Không tuyên bố đã chạy toàn bộ kiểm thử hệ thống.

Rà soát độc lập tìm được hai lỗi vị trí/chiều cao popup; đã sửa và kiểm thử lại. Test phân ca lần đầu lỗi selector do giờ có hai role=alert, đã sửa selector cụ thể và chạy Passed. Một lệnh chỉnh test dùng nhầm đường dẫn `frontend/frontend`, không sửa được tệp; đã sửa đúng đường dẫn. Vite cảnh báo outDir ngoài frontend không tự xóa (thư mục release riêng, có chủ ý); Git cảnh báo LF/CRLF.

## Runtime

- Frontend cuối: `artifacts/production-261124-room-alerts/frontend-v2`, bundle `index-ChZfi19R.js`, cổng 5080. PID hiện hành nằm trong `artifacts/production-5080/frontend-5080.json`.
- API giữ PID 40444/cổng 5769 từ release internal-settings.
- Đã dừng frontend cũ và preview tạm 5082; không thay tiến trình Demo không liên quan.
- Bằng chứng: `artifacts/room-alert-popups/verification.json`, `artifacts/production-261124-room-alerts/verification.json` và các báo cáo browser trong `artifacts/reception-rooms`, `workday-scheduling`, `paper-inspection-ui`, `internal-settings-ui`.

Rà soát cuối bổ sung ba thông báo thành công (tài khoản, chứng từ kho, hồ sơ cá nhân) sang popup. Build/typecheck và bộ popup chạy lại Passed, có kiểm tra lưu hồ sơ, thu gọn/mở ra trong dialog. Preview lần đầu mở trước khi thư mục build-v2 tồn tại nên kết thúc và test báo connection refused; đã khởi động lại sau build, xác minh HTTP 200 và chạy lại Passed. Bản frontend cuối đã được kiểm chứng Production. Giữ thông báo tải/rỗng và lỗi riêng từng trường tại vị trí của chúng.
