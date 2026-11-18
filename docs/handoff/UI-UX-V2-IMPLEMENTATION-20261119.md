# HotelOps — Báo cáo triển khai UI/UX V2

Mở giao diện: https://127.0.0.1:5080 — tải lại bằng Ctrl+F5 nếu còn bundle cũ.

Ngày thực hiện: 19/11/2026. Nhánh: `integration/dev1-dev2-dev3-unblock-20261114`.

## 1. Kết quả và phạm vi

Đã triển khai giao diện trắng/xanh, shell điều hướng mới, dashboard vận hành mới và bộ component dùng chung vào frontend thực tế. Chuẩn màu, chữ, nút, input và bảng áp dụng qua stylesheet chung cho các module hiện có; các bảng/form cũ tiếp tục giữ cơ chế đọc/ghi và kiểm tra quyền của chúng.

Giữ tiếng Việt vì dự án đã có i18n tiếng Việt. Không thêm dữ liệu mẫu vào ứng dụng. Dữ liệu giả trong các tệp test chỉ phục vụ kiểm thử tự động, không nằm trong luồng chạy ứng dụng.

Không sửa backend, schema, dữ liệu vai trò, API contract hoặc quy trình nghiệp vụ trong lượt này. Hai tệp C# đang thay đổi trong working tree là thay đổi Production đã có từ lượt trước.

**Chưa thể tuyên bố hoàn thành toàn bộ chỉ thị hoặc tất cả UC:** không có ba ảnh Novotel trong attachment, source chưa có module quản lý khách/đặt phòng để triển khai Guest Management và Front Desk theo ngày lưu trú, và vẫn còn lỗi ở một số bộ kiểm thử cũ được liệt kê bên dưới.

## 2. Báo cáo theo giai đoạn

| Giai đoạn | Đã thực hiện | Giới hạn/công việc còn lại |
|---|---|---|
| Audit | Kiểm tra React 19, TypeScript, Vite, routing, CSS, Lucide, nguồn quyền, API dashboard/thông báo/người dùng, test và Git | Attachment chỉ có văn bản; chưa có ba ảnh Novotel để đối chiếu trực quan |
| Design tokens | Thêm bộ token V2 thống nhất; màu nền, bề mặt, border, chữ, spacing, radius, font, focus, trạng thái và alias cho module cũ | Dùng font Inter nếu máy có; fallback theo chỉ thị, không thêm tải font/CDN |
| Lucide | Dùng package hiện có, named imports; mapping icon nghiệp vụ tập trung; spinner LoaderCircle, hỗ trợ giảm chuyển động; thay ký hiệu trạng thái danh sách vệ sinh bằng Lucide | Funnel không có trong phiên bản cài đặt; dùng Filter đã xác minh. Không thêm package hoặc icon font |
| Application shell | Sidebar trắng 240px/72px, nhóm mở/đóng, active route, tooltip, header 72px, tìm chức năng theo quyền, breadcrumb, menu tài khoản, đăng xuất qua hành động hiện có | Không tạo tìm kiếm nghiệp vụ toàn cục hoặc API mới; ô tìm trên header chỉ tìm chức năng được phép |
| Thông báo | Popover top-layer mở từ chuông, đọc 5 thông báo mới nhất từ API thật; trạng thái đã đọc/chưa đọc, thời gian Việt Nam, empty/error/retry; mở nguồn qua resolver hiện có | Kiểm thử thao tác trên hộp thư dùng phản hồi API mô phỏng; chưa thử trên tài khoản thật trong Production |
| Shared components | AppButton, IconButton, AppInput, AppSelect, WorkspacePageHeader, ContentCard, DataTable, SortHeader, Pagination, StatusBadge, LoadingSkeleton, EmptyState, ErrorState, ActionMenu, AppModal, AppDrawer, FilterDrawer, ConfirmDialog, AppToast, AppPopover | Không tạo counter khách hoặc bộ chọn nghiệp vụ cho module chưa tồn tại. Ngày/giờ tiếp tục dùng input native và formatter hiện có |
| Tables/forms | Dùng DataTable/Pagination/ActionMenu/StatusBadge trong quản lý người dùng; bảng dashboard dùng DataTable; danh sách động và nhiều màn công việc dùng StatusBadge chung; chuẩn hóa bảng/form native qua CSS | Không tự thêm sorting/export/bulk action vào endpoint chưa hỗ trợ. SortHeader chỉ là component để truyền callback sorting được backend hỗ trợ |
| Người dùng | Bảo toàn chọn chi nhánh/bộ phận/vai trò theo tên, xác thực trùng trường, mật khẩu bàn giao một lần; nút tạo chỉ hiện khi nguồn quyền hiện có có USER_MANAGE và ROLE_MANAGE phạm vi GLOBAL | Điều kiện actor và quan hệ nghiệp vụ cuối cùng tiếp tục được POST API kiểm tra. Không suy luận quyền từ việc route tồn tại |
| Bộ lọc | Drawer có draft/apply, giữ lựa chọn đã áp dụng khi mở lại, hủy không áp dụng, số filter thực tế, xóa từng chip/xóa tất cả; pagination giữ bộ lọc | Không thay đổi field hoặc cách tính khoảng thời gian UTC của API |
| Dashboard | KPI từ API, biểu đồ phân bố mức sẵn sàng, nhóm hồ sơ nguồn, drawer xem nguồn, bảng số liệu kho, nguồn thiếu không biến thành số 0, hiển thị giờ Việt Nam | Không giả lập lượt nhận/trả phòng, doanh thu, giá phòng, tỷ lệ đặt phòng hoặc lịch sử công suất nếu API không có |
| Module khác | Áp dụng theme chung cho buồng phòng, sự cố/bảo trì, vật tư/kho, yêu cầu nội bộ, công việc, ca và nhật ký thời gian; giữ nguyên luồng API | Chưa chạy kiểm thử thủ công từng UC bằng từng tài khoản thật. Các module không được viết lại toàn bộ logic chỉ để đổi giao diện |
| Validation | Đã chạy các bộ kiểm thử được liệt kê ở mục 5, xem ảnh desktop/tablet/mobile và kiểm tra Production local | Lint toàn repo và bộ test ca tiếng Anh cũ chưa đạt; không ghi nhận chúng là Passed |

## 3. Accessibility và các điều chỉnh có lý do

- Chữ trắng trên primary-500 #1677FF chỉ đạt khoảng 4,10:1. Nút chính dùng primary-600 #0B63E5, đạt 5,34:1; primary-500 vẫn dùng cho icon, đồ họa và điểm nhấn.
- Màu chữ semantic nhỏ dùng biến thể đậm hơn để đạt AA: success #127442, warning #965900, error #C52B2B, info #0954C5.
- Các cặp màu đã đo: chữ chính 11,80:1; chữ phụ 4,76:1; success 5,30:1; warning 5,19:1; error 4,91:1; info 6,08:1. Đây là các cặp màu đã kiểm tra, không phải tuyên bố toàn bộ ứng dụng đã qua audit WCAG.
- Modal/drawer dùng dialog native, chặn focus ra ngoài bằng Tab/Shift+Tab, Escape an toàn, khôi phục focus và khóa cuộn nền. Có trạng thái busy và cảnh báo khi đóng dữ liệu chưa lưu.
- Action menu render trên portal, giới hạn theo viewport, hỗ trợ phím mũi tên/Home/End/Escape. Không bị table overflow cắt.
- Spinner/skeleton dừng animation khi prefers-reduced-motion.
- Bảng cuộn trong khung, không làm tràn toàn trang tại các viewport đã kiểm tra.

## 4. Lưu ý khi tiếp tục phát triển

- Import token và stylesheet toàn cục qua `src/main.tsx`; `design-system.css` được nạp sau stylesheet cũ để giữ một bộ giá trị hiển thị chung.
- Component dùng chung ở `src/components/design-system.tsx`; popover ở `src/components/AppPopover.tsx`.
- Khi thêm row action, chỉ truyền các action đã có và đã được phép hiển thị. API tiếp tục là nơi quyết định quyền thực thi.
- Không tạo local sorting trên một trang dữ liệu rồi gọi đó là sorting toàn bộ danh sách. Endpoint phải hỗ trợ thì mới nối SortHeader vào query.
- Không thay null bằng 0 và không cộng số lượng khác đơn vị/kho chỉ để tạo KPI.
- Giữ các cơ chế chặn gửi lặp và trạng thái kết quả chưa xác định. Shell đã được sửa để không dựng lại trang khi chuyển trạng thái phiên; ca hồi quy đăng xuất/khôi phục đã được chạy.
- Các ảnh trong artifacts/design-system-v2/screenshots là ảnh từ kiểm thử bằng dữ liệu tổng hợp, trừ production-login.png là trang đăng nhập Production thật.

## 5. Kiểm chứng

| Lệnh/nhóm kiểm tra | Kết quả | Bằng chứng |
|---|---|---|
| npm run build | Passed, exit 0; JS index-DNY4c4KJ.js và CSS index-pnnv0xHR.css | artifacts/design-system-v2/build-final.txt |
| TypeScript --noEmit | Passed, exit 0; build cũng chạy tsc -b | artifacts/design-system-v2/typecheck-final.txt |
| Prettier cho 41 tệp frontend trong manifest | Passed | artifacts/design-system-v2/changed-file-lint.txt |
| git diff --check | Passed; Git còn nhắc chuẩn hóa LF/CRLF khi cập nhật working copy | Đã chạy trực tiếp |
| users API, create-user API, dashboard workload links, selected notification read | 249/249 Passed | artifacts/design-system-v2/api-tests.txt |
| workday API, scheduled workday API, room status | 136/136 Passed | artifacts/design-system-v2/workday-room-api.txt |
| Shell/navigation/session recovery regression | 20/20 Passed | artifacts/design-system-v2/shell-regression.txt |
| Quản lý người dùng/tạo tài khoản và chặn tài khoản chỉ xem | 9/9 Passed | artifacts/design-system-v2/user-browser-final.txt |
| Dashboard V2, keyboard, filter, thông báo, responsive, reduced motion | 10/10 Passed | artifacts/design-system-v2/design-system-tests.txt |
| Room workspace và nhãn tiếng Việt | 18 ca Passed trong lần chạy nhóm; ca test ca cũ trong cùng lệnh lỗi harness | artifacts/design-system-v2/module-regression.txt |
| Luồng ca tiếng Việt hiện tại | 5/5 Passed | artifacts/design-system-v2/scheduled-vietnamese-tests.txt |
| Phân trang 10 dòng giữ bộ lọc | 1/1 Passed; số dòng chọn tại footer, có accessible name rõ ràng | artifacts/design-system-v2/pagination-final.txt |
| Smoke Production thực tế | 13/13 Passed; login, asset mới, SQL health, CSRF/Secure cookie, chặn truy cập không đăng nhập, Development vẫn hoạt động | artifacts/design-system-v2/production-smoke.txt |
| .NET build/test | Not Run trong lượt UI này vì không sửa C# | Hai thay đổi C# có sẵn từ lượt Production trước |

### Các bước kiểm tra chưa đạt

1. `npm run lint`: lần chạy toàn repo báo nhiều tệp sai định dạng và thiếu `playwright.config.ts` mà script tham chiếu; exit 2. Đã format và kiểm tra riêng các tệp frontend trong manifest. Không sửa hàng trăm tệp ngoài phạm vi để làm xanh lint. Log: `artifacts/design-system-v2/lint.txt`.
2. `scheduled-workday-browser.test.mjs`: ban đầu không cấu hình output cho CSS import. Đã thêm outfile để chạy được harness. Lần chạy tiếp theo có 8 Passed / 48 Failed; nhiều ca vẫn tìm nhãn tiếng Anh như `Working day 20`, `No current working day.` và `Review scheduled workday` đã không còn trong UI tiếng Việt. Chưa xử lý toàn bộ bộ test này; không khẳng định mọi lỗi đều chỉ do selector. Log: `artifacts/design-system-v2/scheduled-tests.txt`.
3. Vite còn cảnh báo `use client` của react-router bị bỏ qua và bundle lớn hơn 500 kB. Không che hoặc đổi ngưỡng cảnh báo.
4. Chưa chạy toàn bộ `npm test`, chưa hoàn thành audit WCAG tự động toàn ứng dụng và chưa thử tất cả vai trò bằng phiên đăng nhập thật.

### Kích thước bundle

Bản cuối: JavaScript 1.787,19 kB (gzip 450,80 kB), CSS 145,44 kB (gzip 26,39 kB). So với bản UI trước lượt V2, JavaScript tăng khoảng 2,2%; không thêm dependency. Chưa loại bỏ cảnh báo chunk lớn sẵn có.

## 6. Những việc cần nguồn bổ sung

- Đối chiếu ba ảnh Novotel: attachment hiện chỉ có `Pasted text.txt`, không có ảnh.
- Guest Management và Front Desk cho khách/đặt phòng theo khoảng ngày: chưa có route/API tương ứng trong source đang làm việc. Giữ riêng trạng thái phòng vật lý và khả năng đặt phòng; không tạo trang giả thay thế. Các màn hiện có `/rooms/status` và `/room-directory` nhận theme chung nhưng không được đổi tên thành chức năng đặt phòng.
- Các KPI check-in/check-out, room rate, occupancy lịch sử, customer feedback: chỉ triển khai khi có dữ liệu/backend đã hỗ trợ.
- Chuyển dần bảng/form đặc thù sang primitive chung khi có kiểm thử phù hợp; hiện chúng đã nhận chuẩn hiển thị qua CSS chung.

## 7. Git và bảo toàn thay đổi

- Nhánh làm việc giữ nguyên: `integration/dev1-dev2-dev3-unblock-20261114`.
- HEAD giữ nguyên: `f9855f1b24f04fa0b107e0841eee6cad47673d98`.
- Không commit, push, merge, reset hoặc đổi nhánh.
- Giữ các thay đổi local đã có ở quản lý người dùng, tài liệu bàn giao và hai tệp hạ tầng Production.
- Bản chụp các tệp cần bảo toàn nằm ở `artifacts/design-system-v2/before`; Git status và manifest ở cùng thư mục.
- `artifacts` là thư mục bằng chứng local; không có password/token/connection string trong báo cáo này.

## 8. Tệp tạo/sửa

Các tệp frontend được format/kiểm tra nằm trong manifest bên dưới. Manifest bao gồm một số tệp đã có thay đổi từ lượt sửa quản lý người dùng trước đó; không có nghĩa tất cả đều được tạo mới trong lượt V2.

- `frontend/src/components/LoadingIndicator.tsx`
- `frontend/src/components/loading-indicator.css`
- `frontend/src/dev2/DataView.tsx`
- `frontend/src/dev2/IncidentListView.tsx`
- `frontend/src/hotel/room-directory-layout.css`
- `frontend/src/insights/DashboardFilters.tsx`
- `frontend/src/insights/DashboardPage.tsx`
- `frontend/src/internalRequests/HistoryPage.tsx`
- `frontend/src/internalRequests/staff-workspace.css`
- `frontend/src/main.tsx`
- `frontend/src/operationalTasks/TaskReadPage.tsx`
- `frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx`
- `frontend/src/shared-host/CacaoShell.tsx`
- `frontend/src/shared-host/home.css`
- `frontend/src/styles.css`
- `frontend/src/styles/cacao.css`
- `frontend/src/users/CreateUserFlowBoundary.tsx`
- `frontend/src/users/CreateUserPage.tsx`
- `frontend/src/users/InitialCredentialHandoff.tsx`
- `frontend/src/users/UserDirectoryPage.tsx`
- `frontend/src/workTime/MyTimeLogPage.tsx`
- `frontend/src/workTime/TimeCorrectionsPage.tsx`
- `frontend/src/workTime/work-time.css`
- `frontend/src/workday/ScheduledWorkdayPanel.tsx`
- `frontend/src/workday/WorkdayReadPanel.tsx`
- `frontend/src/workday/shift-planning.css`
- `frontend/tests/cacao-shell.test.mjs`
- `frontend/tests/scheduled-workday-browser.test.mjs`
- `frontend/tests/users-api.test.mjs`
- `frontend/src/components/AppPopover.tsx`
- `frontend/src/components/design-system.tsx`
- `frontend/src/insights/dashboard.css`
- `frontend/src/shared-host/HeaderNotifications.tsx`
- `frontend/src/shared-host/navigationIcons.ts`
- `frontend/src/styles/design-system-tokens.css`
- `frontend/src/styles/design-system.css`
- `frontend/src/users/useUserCreationAccess.ts`
- `frontend/src/users/useUserOptions.ts`
- `frontend/src/users/user-workspace.css`
- `frontend/tests/design-system-v2-browser.test.mjs`
- `frontend/tests/users-workspace-browser.test.mjs`
