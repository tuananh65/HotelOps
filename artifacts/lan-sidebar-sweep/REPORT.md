# Kiểm tra sidebar, liên kết và popup của lan.dang

Ngày thực hiện: 19/11/2026. Phạm vi: tài khoản Lan trên web localhost, API Development và HotelOpsDB hiện có.

## Thay đổi

- Popup chức năng dùng chung được căn giữa, giới hạn theo viewport; nội dung dài cuộn bên trong, header/footer và nút đóng luôn nằm trong màn hình. Điều hướng mobile vẫn dùng panel điều hướng.
- Các liên kết thực hiện công việc, cập nhật tiến độ, thao tác phòng và bàn giao được trình bày bằng nút thiết kế chung, có khoảng cách và tự xuống hàng trên màn hình hẹp. Sửa nhãn nút `Lưutạo mới` thành tên thao tác rõ ràng.
- Hồ sơ vệ sinh cũ thiếu checklist/workflow có trang tra cứu dữ liệu thật thay vì chỉ hiển thị lỗi đỏ. Áp dụng cả đường dẫn thực hiện và tiến độ. Endpoint đọc mới chỉ cho người đang được phân công và có quyền đọc đúng chi nhánh; không mở quyền thực hiện hồ sơ chưa hợp lệ.
- Trang chủ không còn gọi API báo cáo khi server navigation không cấp chức năng này.
- Cả trang ngày làm việc và trang bắt đầu/kết thúc ca chỉ hiện liên kết bàn giao khi có quyền tương ứng.
- Danh sách vật tư kiểm tra tính nhất quán của dữ liệu liên kết giống trang chi tiết. Phiếu cũ #1/#3 vẫn hiện thông tin tóm tắt và nhãn cần đối soát, không còn tạo liên kết dẫn đến 404.
- API chi tiết vật tư trả danh sách loại liên kết đã cấu hình. UI kiểm tra thêm quyền đọc công việc, không tự gọi API liên kết thiếu cấu trúc dữ liệu và không hiển thị phần bảo trì khi Lan không được xem.
- Gỡ hai liên kết vượt quyền phát hiện ở nội dung mở rộng: xem tồn kho khả dụng và hàng đợi hoàn trả. Dùng danh sách thao tác/navigation đã được máy chủ xác nhận.

Không sửa schema, vai trò hoặc quyền SQL; không thay đổi hồ sơ nghiệp vụ cũ. Gói DEMO từ lượt trước được giữ nguyên.

## Kiểm chứng thực tế

- Đọc navigation của Lan từ API: 18 đường dẫn; quét trang chủ, sidebar và các liên kết phát sinh, tổng cộng 120 URL trong đợt tìm lỗi. Con số này có cả các URL vượt quyền đã được phát hiện và gỡ khỏi UI.
- Kiểm tra lại 9 trang sau các sửa đổi cuối: không có banner lỗi, lỗi JavaScript hoặc liên kết vượt quyền được theo dõi (`final-verification.json`). Kết quả này không có nghĩa mọi API của hồ sơ cũ đều trả 200.
- Mở chi tiết yêu cầu nội bộ bằng nút mã phiếu; bung các phần lịch sử, thông tin liên quan; kiểm tra các trang công việc, làm lại, lịch ca, nhật ký và đề nghị điều chỉnh thời gian.
- 29 bước kiểm tra tương tác; 9 popup và 9 lần đóng popup tại 1440×900, 390×900, 667×375 đều nằm giữa, không tràn, không gây trang trắng. Đợt chụp/xác minh popup cuối lưu riêng trong `popup-results.json`.
- Kiểm thử hình học riêng ở 1440×900, 390×844 và 667×375 có nội dung dài và bảng rộng; kiểm tra cuộn trong popup, vị trí footer, Escape, khoảng cách và màu nút.
- Endpoint `/api/branches/1/cleaning-tasks/1/record` và `/2/record` trả 200, đúng phòng 201/202. Truy cập task 1 qua chi nhánh 2 trả 403.
- Danh sách vật tư trả `canOpenDetail: false` cho #1/#3; các phiếu hợp lệ vẫn mở được. Phiếu #15 trả `availableLinkKinds: ["incident"]`, phản ánh đúng cấu hình hiện có.
- Những API execution cũ trả 409/503 do dữ liệu thiếu/không nhất quán vẫn giữ kiểm tra an toàn. UI chuyển sang endpoint tra cứu có kiểm tra quyền, không coi đó là công việc hợp lệ để ghi dữ liệu.

Các lần mở web dùng một phiên kiểm chứng riêng của Lan, chặn POST/PUT/PATCH/DELETE. Không gửi yêu cầu nghiệp vụ, không chấm công, không đổi mật khẩu hay đăng xuất phiên người dùng.

Đã thu hồi phiên kiểm chứng riêng và xóa file cookie tạm sau khi kiểm tra; xác nhận trong `session-cleanup.log`.

## Build và test

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx` | Passed; 0 warnings, 0 errors |
| `dotnet test HotelOps.slnx` | Thành công; solution hiện không chạy project regression riêng |
| Regression backend liên quan | 26 Passed, 1 Skipped do cần môi trường SQL ghi riêng |
| Hồi quy frontend chính | 113/113 Passed |
| Kiểm tra Lan/vật tư sau sửa liên kết cuối | 26/26 Passed |
| Bộ ca làm việc tiếng Việt | 5/5 Passed |
| Hồi quy ca làm việc sau đồng bộ fixture/selector | 56/56 Passed |
| `npm run build` cuối | Passed |

Bộ test ca cũ ban đầu thất bại do còn dùng nhãn/selector tiếng Anh và thông báo trước khi UI được Việt hóa. Selector được đồng bộ, tình huống bắt đầu ngoài kế hoạch dùng dữ liệu không có ca được phân và mở popup trước khi nhập. Giữ các kiểm tra gửi trùng, CSRF, đổi phiên/chi nhánh và xử lý kết quả chưa rõ; kết quả chạy lại nằm ở `scheduled-verified-tests.log`.

Lần build backend đầu bị khóa DLL bởi API đang chạy. Đã dừng đúng API debug, build lại đạt và khởi động lại localhost. Không dừng API Release khác.

## File tạo/sửa trong lượt này

- Thành phần/UI chung: `frontend/src/components/ActionLink.tsx` (mới), `design-system.tsx`, `frontend/src/styles/design-system.css`, `frontend/src/shared-host/CacaoShell.tsx`.
- Buồng phòng: `frontend/src/api/dev1/cleaningRecord.ts` (mới), `AssignedCleaningHeader.tsx`, `CleaningExecutionPanel.tsx`, `CleaningProgressPanel.tsx`, `CleaningMonitorDetail.tsx` trong `frontend/src/screens/housekeeping/`; `frontend/src/screens/rooms/RoomWorkflowActions.tsx`.
- Quyền/liên kết hiển thị: `frontend/src/insights/ReportsPage.tsx`, `frontend/src/workday/WorkdayReadPanel.tsx`, `ScheduledWorkdayPage.tsx`; `frontend/src/dev2/AuthorizedOperations.tsx`, `MaterialListView.tsx`, `MaterialHistoryView.tsx`, `MaterialTaskLinks.tsx`, `Workspace.tsx`.
- Backend: `src/HotelOps.Api/Services/Housekeeping/AssignedCleaningRecordService.cs` (mới), `Controllers/CleaningExecutionController.cs`, `DTOs/MaterialRequests/MaterialRequestContracts.cs`, `Services/MaterialRequests/MaterialRequestService.Queries.cs`, `Services/Dev2Authorization/MaterialReadContract.cs` (truyền DbContext cho kiểm tra liên kết ở danh sách; không đổi chính sách phân quyền).
- Tests: `frontend/tests/popup-layout.test.mjs` (mới), `lan-access-ui.test.mjs`, `workday-browser.test.mjs`, `scheduled-workday-browser.test.mjs`, `requests-maintenance-ui.test.mjs`; `tests/HotelOps.Dev1.RegressionTests/LanAccessReadSqlTests.cs`.
- Bằng chứng/scripts kiểm chứng: thư mục báo cáo hiện tại. Workspace vốn có nhiều thay đổi từ các lượt trước; danh sách này không phải toàn bộ `git status`.

## Giới hạn và cảnh báo

- Các hồ sơ cũ thiếu workflow/checklist và phiếu vật tư cần đối soát vẫn là hồ sơ tra cứu; chưa sửa hoặc suy diễn dữ liệu lịch sử để cho phép thực hiện.
- Liên kết vật tư với công việc vận hành/vệ sinh/bảo trì chưa đủ cấu hình database. UI thông báo đúng trạng thái; lượt này không cài thêm schema hay cấp quyền mới.
- Quét thực tế xác minh đọc, mở liên kết, popup và đóng form. Các thao tác ghi được kiểm thử bằng fixture; không khẳng định đã thực hiện tất cả giao dịch nghiệp vụ trên database thật.
- Frontend vẫn có cảnh báo bundle lớn hơn 500 kB và directive `use client` của thư viện. Runtime backend có cảnh báo index mapping `RoomTypeBranchV1` và log JOB01 giữ lần quét sau `InvalidOperationException` từ cấu hình hiện có, ngoài thay đổi này.
- Không commit, push hoặc publish.

## Bằng chứng

- `crawl-results.json`: các URL đã mở, tiêu đề, lỗi API, banner và lỗi JavaScript.
- `final-verification.json`: kiểm tra lại các trang được sửa sau đợt quét, bao gồm liên kết tồn kho/hoàn trả và bàn giao.
- `interactions.json`, `popup-results.json`, `screenshots/`: các thao tác và kích thước popup thực tế.
- `api-check.json`, `backend-build.log`, `backend-tests.log`, `frontend-build-final.log`, `frontend-tests-final.log`, `material-final-tests.log`, `scheduled-vietnamese-tests.log`, `scheduled-verified-tests.log`, `session-cleanup.log`.
