# HotelOps — luồng V10, vai trò và tiêu đề bảng

Hoàn tất ngày 22/11/2026. Làm việc bằng một agent; dùng quy trình AgentKit fix/debug/scout, kiểm thử và tự review. Không tạo agent phụ, commit, push hay deploy.

## Kết quả

- Tiêu đề bảng giữ vị trí khi cuộn trang, cuộn trong bảng và cuộn ngang; hỗ trợ cả bảng lịch ca và bảng trong hộp thoại. Cột tiêu đề khớp cột dữ liệu.
- Admin có tổng quan quản trị, chi nhánh, luồng chi nhánh → tầng/khu vực → phòng, loại phòng, cấu hình nghiệp vụ, danh mục, nhân sự, phân quyền và nhật ký.
- Giám sát có hàng đợi V10 bốn cột Phòng / Người dọn / Trạng thái / Thao tác; bộ lọc có số lượng; phân công, kiểm tra và dọn lại mở đúng công việc liên quan. Kiểm tra không đạt → ghi lỗi → dọn lại → xác minh lỗi → kiểm tra đạt đã chạy xuyên suốt với API thật.
- Sự cố có lối tạo công việc bảo trì liên kết; phòng có chặn/gỡ chặn theo quyền và lối tạo việc dọn/báo sự cố. Kỹ thuật mặc định mở nhóm bảo trì; các nhân viên có tổng quan cá nhân và lối vào ca/công việc/vật tư.
- Thủ kho có tồn kho, kho/định mức và sửa vật tư dùng chung theo lựa chọn đã duyệt. Quyền này không mở quyền sửa vai trò, chi nhánh hoặc cấu hình tham chiếu chung.
- Checklist/lịch định kỳ mở theo đúng quyền CHECKLIST_MANAGE, PM_MANAGE, TASK_MANAGE. Báo cáo có chọn nội dung, ngày và xuất CSV. Owner ưu tiên tổng quan/báo cáo, chỉ có quyền đọc.
- Sửa deadlock ở trang thông báo: truy vấn được phép xem, sau đó cập nhật DeliveredAt bằng một lệnh idempotent, không giữ transaction Serializable đọc rồi nâng khóa ghi.
- Hai vai trò quản lý đã duyệt được điều phối yêu cầu giữa các bộ phận trong chi nhánh; nhân viên thường vẫn chịu ràng buộc bộ phận/người được giao.

## Dữ liệu và quyền đã áp dụng

Người dùng duyệt [bảng đối chiếu V10](../../docs/requirements/V10-ROLE-MAPPING-PROPOSAL.md), chọn cho thủ kho sửa vật tư chung. Đã backup, áp dụng trên database thử, kiểm chứng rồi áp dụng vào HotelOpsCompactDB.

| Tài khoản mới | Vai trò |
|---|---|
| demo_om | OPERATION_MANAGER |
| demo_hks | HOUSEKEEPING_SUPERVISOR |
| demo_maintenance_lead | MAINTENANCE_LEAD |
| demo_owner | OWNER |

Mật khẩu dùng cấu hình tài khoản mẫu hiện tại; không lưu mật khẩu vào source/tài liệu. Danh sách trong file credentials local đã bổ sung bốn tài khoản. Giữ tài khoản/role mẫu cũ; Admin thêm quyền nhật ký, Department Head thêm quyền công việc/yêu cầu theo bảng duyệt. Ca giám sát HK và trưởng kỹ thuật chuyển sang vai trò riêng, giữ nguyên giờ và định mức đã chốt.

Database chính sau kiểm chứng: **40 bảng, 160 FK, 12 vai trò, 15 tài khoản, 2 chi nhánh, 16 phòng, 30 định nghĩa ca, 0 phân ca**. Dữ liệu tạo bởi các bài CRUD và luồng thử nằm trong database riêng. Không thực hiện lệnh tác động database legacy.

Backup chính đã VERIFYONLY/CHECKSUM: `HotelOpsCompactDB_before_workday_20261122_142128586.bak`. Metadata: [backup local](../../artifacts/workday/backup-HotelOpsCompactDB.json). Schema vẫn 2.1.0, không migration hay bảng mới.

## Kiểm chứng

| Kiểm tra | Kết quả | Bằng chứng |
|---|---|---|
| dotnet build HotelOps.slnx | Passed, 0 lỗi / 0 warning | Build cuối sau sửa scope quản lý |
| dotnet test HotelOps.slnx --no-build | 26 passed; worker optional skipped trong lượt chung | [TRX](../../artifacts/actor-lists/v10-final.trx) |
| Worker chạy riêng trên DB đã backup | 1 passed, 0 skipped | [Worker](../../artifacts/compact-v2/backend/worker-verification.json) |
| Backend mở rộng | 127 passed, 0 failed | [API regression](../../artifacts/compact-v2/backend/verification.json) |
| Frontend build / kiểm tra định dạng | Passed | npm run build; Prettier check |
| Frontend logic | 4 passed | npm run test:compact |
| Actor/menu | 15 tài khoản, 110 lượt mở trang, 0 lỗi trang | [Actor matrix](../../artifacts/actor-lists/after.json) |
| Luồng V10 | 10 nhóm kịch bản passed | [V10 flows](../../artifacts/actor-lists/v10-flows.json) |
| Admin danh mục/phân quyền | Toàn bộ tab mở được; role thử lưu quyền đúng scope; SYS_ADMIN được bảo vệ | [Admin tests](../../artifacts/actor-lists/admin-tests.json) |
| Visual desktop/tablet/mobile | 54 màn, 0 lỗi / 0 warning | [Visual](../../artifacts/actor-lists/visual/results.json) |
| Sticky header | 36 phép đo trên 9 nhóm bảng, lệch đỉnh tối đa 1px, lệch ngang 0px; hộp thoại cũng passed | Visual + Admin tests |
| Database chính | Health 200/schema2.1/40 bảng; 7 tài khoản kiểm tra endpoint đúng vai trò trả200 | [Primary API](../../artifacts/actor-lists/primary-verification.json), [counts](../../artifacts/actor-lists/primary-counts.json) |
| FE chính | Admin/HKS đăng nhập và mở màn tương ứng qua port5178 | [Primary browser](../../artifacts/actor-lists/primary-browser.json) |

V10 flow tests gồm CRUD chi nhánh; tạo tầng/phòng; timezone/stale version/403/cross-branch; sửa vật tư chung; 12 GET inbox đồng thời; hàng đợi/phiếu kiểm tra; vòng fail/rework/pass; ca HKS và chống thao tác hộ; điều phối liên bộ phận; cuộn desktop/mobile. Dữ liệu stress layout là các hàng DOM sao chép tạm trong browser để tạo độ dài cuộn, không giả mạo dữ liệu API hoặc ghi thêm bản ghi database.

## Tệp tạo/sửa trong lượt này

- FE mới trong `frontend/src/compact/`: `management.tsx`, `catalog.tsx`, `catalog-specs.ts`, `stock.tsx`, `templates.tsx`, `cleaning-queue.tsx`, `personal-overview.tsx`, `role-permissions.tsx`, `use-sticky-headers.ts`.
- FE cập nhật: `app.tsx`, `auth.tsx`, `compact.css`, `api.ts`, `rooms.tsx`, `work.tsx`, `documents.tsx`, `insights.tsx`.
- BE services: `CatalogService.cs`, `InsightService.cs`, `NotificationService.cs`, `TemplateService.cs`, `WorkService.cs`, `UserService.cs`; controllers: `OperationsControllers.cs`, `WorkspaceController.cs`, `UsersController.cs` dưới `src/HotelOps.Api/Compact/`.
- Database data-only: `scripts/database/compact-v2/07_V10Roles.sql`, `Apply-V10Roles.ps1`.
- Tests: `frontend/tests/compact-actors.mjs`, `compact-v10-flows.mjs`, `compact-v10-admin.mjs`, `compact-visual.mjs`, `compact-primary-smoke.mjs`.
- Tài liệu: bảng đối chiếu quyền, hướng dẫn Workday, plan `261122-1340-actor-lists`, báo cáo này và journal local.

## Giới hạn và các lỗi đã xử lý khi kiểm thử

- Lượt đầu phát hiện nút Lưu bật khi lookup chưa tải xong; đã khóa theo trạng thái từng lookup và chạy lại thành công. Lỗi script dùng `token` thay cho `accessToken`, sai đường dẫn `workday/week` và timeout do ngưỡng login15/phút đã sửa trong test harness. Không nới rate limit của ứng dụng để làm test qua.
- Smoke test frontend chính ban đầu lỗi mã hóa selector tiếng Việt khi pipe từ PowerShell sang Node; đã chuyển sang tệp UTF-8 và chạy lại. Cleanup tiến trình Node ban đầu báo lỗi Stop-Process; đã dừng lại bằng PID đã xác minh và kiểm tra cổng thực tế.
- Đã dùng schema/mappings snake_case thực tế sau hai truy vấn đọc ban đầu sai tên bảng/cột. Không có thay đổi dữ liệu từ các truy vấn thất bại đó.
- `git diff --check` không có lỗi whitespace; Git nhắc LF→CRLF ở `frontend/package.json` và `frontend/src/main.tsx` (các thay đổi tồn tại từ lượt trước).
- Bộ `npm test` legacy không được coi là đã qua; kết quả trên dành cho Compact và các luồng V10 đã kiểm chứng. Không tuyên bố giao diện giống từng pixel prototype hoặc các mục OPEN trong V10 đã được thiết kế lại toàn bộ.
- Preview local dùng helper tắt worker nền; worker sinh lịch/thông báo đã được chạy và kiểm chứng riêng. Lập lịch Workday và Start/End hoạt động trực tiếp qua API. Không tự tạo phân ca sản xuất.
- AgentWiki publish skipped. Không có câu hỏi phê duyệt còn chờ.

Preview chính: http://127.0.0.1:5178, API http://127.0.0.1:5768. Server thử5179/5770 được dừng sau kiểm thử.
