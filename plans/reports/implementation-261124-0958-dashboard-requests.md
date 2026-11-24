# Dashboard, màu ca và Yêu cầu của Trưởng bộ phận

Ngày: 24/11/2026. Phạm vi: frontend compact. Đã cập nhật Production cục bộ https://127.0.0.1:5080/.

## Hành vi

- Thẻ số liệu trên các dashboard nằm trước Ca làm của tôi, rồi đến chi tiết. Attendance mount ổn định, tải độc lập với số liệu và chỉ gọi API khi có ATTENDANCE_RECORD.
- Thẻ có màu theo ý nghĩa: xanh lá sẵn sàng/hoàn thành, vàng chờ xử lý, đỏ quá hạn/sự cố, xanh dương số liệu chung, tím thông báo/yêu cầu; giữ tên và con số.
- Badge Đang trong ca nền xanh; Đã kết thúc nền đỏ; chữ trắng. Banner ca đang hoạt động có nền xanh nhạt và viền xanh.
- Trưởng bộ phận chỉ có một menu Yêu cầu; ba tab Công việc, Yêu cầu nội bộ, Yêu cầu vật tư lọc theo quyền hiện có. Giữ URL cũ, query và phạm vi chi nhánh. Không đổi điều hướng vai trò khác.

## Các file sửa trong lượt này

Trong `frontend/src/compact/`:

- `app.tsx`: bỏ attendance toàn cục phía trên dashboard, nhóm menu Head và giữ attendance ở Reception.
- `attendance-summary.tsx`, `ui.tsx`, `workday.tsx`: guard quyền, màu thống kê và badge/banner ca.
- `metric-tone.ts`, `dashboard-colors.css`, `shift-status.css`: helper và CSS màu dùng chung mới.
- `housekeeping-personal.tsx`, `housekeeping-overview.tsx`, `maintenance-personal.tsx`, `maintenance-overview.tsx`, `inventory-dashboard.tsx`, `personal-overview.tsx`, `management.tsx`, `owner-overview.tsx`, `insights.tsx`, `operations-metrics.tsx`, `branch-metrics.tsx`: thứ tự số liệu/attendance và màu thẻ. OperationsMetricDetails tách phần chi tiết để attendance không bị remount.

Tests: cập nhật `frontend/tests/compact-attendance-summary.mjs`; thêm `compact-head-requests.mjs`, `verify-dashboard-requests-production.mjs` cùng thư mục.

Tài liệu: `docs/ui/compact-ux.md`, plan `plans/261124-0958-dashboard-requests/plan.md`, báo cáo này và nhật ký local. Helper triển khai tại `artifacts/production-261124-dashboard-requests/switch-frontend.ps1`.

Nhiều file đã có chỉnh sửa/untracked từ các yêu cầu trước; danh sách trên là file được tác động trong lượt này, không phải toàn bộ thay đổi Git. Không commit/push hoặc đổi branch.

## Kiểm chứng

- Build Passed: `npx --no-install tsc -b`, `npx --no-install vite build --outDir ../artifacts/production-261124-dashboard-requests/frontend`.
- Test Passed: `compact-attendance-summary.mjs` — 8 vai trò, metric trước attendance, màu nền thực tế, viewport 320/375/1440, không tràn ngang, attendance không gọi hai lần khi dashboard trả chậm, bắt đầu/kết thúc ca, lỗi/thử lại và guard quyền.
- Test Passed: `compact-head-requests.mjs` — menu gộp, 3 tab, các quyền đầy đủ/MR-only/TASK-only, URL cũ, scope, điện thoại.
- Test Passed: `compact-dashboard-pagination.mjs` — Admin/OM/Owner/HK dashboard và biểu đồ ở 1440/768/375; hồi quy tài khoản/phân trang.
- Test Passed: `compact-workday-scheduling.mjs` — bộ phận/đối tượng, lịch tuần/tháng/nhiều người, xác nhận vượt giờ, retry version cũ, lịch cá nhân và responsive.
- Kiểm tra ảnh desktop/mobile thực tế: các thẻ màu rõ, số liệu trên bảng ca; menu Head chỉ còn Yêu cầu.
- Production Passed: HTTPS tin cậy, HTTP200, bundle `index-OPqAIvTQ.js`, login hiện đúng, `/api/v2/auth/me` không đăng nhập trả 401, không lỗi JavaScript. Kết quả tại `artifacts/production-261124-dashboard-requests/verification.json`.
- API health thực tế: `status=ok`, schema `2.1.0`, 40 bảng. Production frontend launcher 35580/listener 30516; API 40444 giữ nguyên. Preview 5082 đã dừng.

## Lỗi đã xử lý và giới hạn

- Review phát hiện fallback attendance gây remount/gọi lại API; đã tách phần số liệu và chi tiết, giữ một attendance ổn định.
- Hai test ban đầu lỗi fixture: ngày hôm qua làm mất nút Bắt đầu ca; endpoint danh sách trả object thay vì array làm trang Work lỗi. Đã cố định thời gian fixture và trả đúng kiểu array, chạy lại đạt; không thay logic sản phẩm để vượt test.
- Vite có cảnh báo outDir nằm ngoài frontend và không tự làm rỗng; đây là thư mục release mới chủ động tách khỏi bản đang phục vụ. Không có lỗi build hoặc cảnh báo TypeScript.
- Browser nghiệp vụ dùng API giả lập, không tạo công việc/ca thật trên Production. Production xác minh đăng nhập, bundle, proxy xác thực và health; không tuyên bố đã thử giao dịch nghiệp vụ trên database thật.
- Không đổi C#/schema/grant/dữ liệu: dotnet build/test Not Run (không thuộc thay đổi). Không kiểm thử toàn bộ backend.
