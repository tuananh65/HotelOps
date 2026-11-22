# Department Head — triển khai chọn lọc, giữ UX đã chốt

## DEPARTMENT_HEAD_ROLE / SOURCE_AUDIT

Nguồn: bản P0 đính kèm ngày 22/11/2026, được áp dụng theo chỉ dẫn **chọn lọc** của người dùng. Quy tắc này đã ghi vào `AGENTS.md` và `docs/ui/compact-ux.md` cho các yêu cầu tiếp theo.

Đã đọc quyền thật bằng SELECT tại `HotelOpsCompactDB`: DEPT_MANAGER có 14 quyền DASHBOARD_VIEW, SHIFT_MANAGE, TASK_MANAGE, TASK_VIEW, IR_CREATE, IR_VIEW, IR_PROCESS, IR_OVERDUE_HANDLE, MR_VIEW, MR_APPROVE_L1, MR_APPROVE_FALLBACK, REPORT_VIEW, ROOM_VIEW, INVENTORY_VIEW; tất cả BRANCH. Cả hai chi nhánh có ca HK, ENG, FO, STORE. Không sửa quyền, membership, staffing, schema, tài khoản hoặc dữ liệu vận hành tại database chính.

Giữ UX: chi nhánh dưới logo; tiêu đề/mô tả ở navbar; khoảng cách đầu trang 12px; nút tạo cùng toolbar; không nút Làm mới; lịch chọn một đối tượng, không Tất cả; phòng 15 ô/trang và drawer; phân trang và bộ lọc tài khoản hiện có. Không áp dụng các đề xuất header/breadcrumb/chi nhánh hoặc nhiều bộ lọc lịch trùng lặp trong bản P0.

## SIDEBAR_ALIGNMENT

DEPT_MANAGER đơn vai trò tại chi nhánh có 7 nhóm, mỗi nhóm vẫn phải qua permission guard: Tổng quan, Lịch làm việc, Công việc, Yêu cầu nội bộ, Yêu cầu vật tư, Báo cáo, Nhân sự. Tài khoản đa vai trò giữ điều hướng hiện có. Phòng/tồn kho truy cập qua liên kết tham khảo từ Tổng quan. Route thông báo tới yêu cầu nội bộ vẫn mở đúng nhóm.

## DASHBOARD / REPORTING

- API dashboard bổ sung số nhân viên phân ca trong tuần, nhân viên đang trong ca, số ca chưa đủ người trong tuần, việc chưa kết thúc/quá hạn, IR chờ xử lý và MR chờ duyệt. Mỗi miền dữ liệu có permission guard riêng, không có quyền trả `null`.
- Các thẻ mới chỉ hiển thị trong giao diện DH; giữ biểu đồ và bố cục dashboard của các vai trò khác.
- Báo cáo bổ sung yêu cầu vật tư và lịch phân ca, kiểm tra REPORT_VIEW cùng MR_VIEW/SHIFT_MANAGE. Giữ xuất CSV, thêm phân trang 25 dòng và nhãn cột tiếng Việt. Giới hạn truy vấn hiện hữu 10.000 dòng được thông báo khi đạt ngưỡng.
- Số MR chờ duyệt là tổng trong chi nhánh, không tuyên bố mọi yêu cầu đều thuộc thẩm quyền duyệt của người xem. Ca thiếu người là số ca/ngày trong cả tuần hiện tại, không chỉ ca tương lai.

## WEEKLY_WORKDAY / STAFF_CANDIDATE_VIEW

Workday backend hiện có đã quản lý toàn chi nhánh, không dùng bộ phận employment của DH để giới hạn ca được quản lý. Giữ nguyên luồng phân công/hủy và start/end cá nhân; không tạo lịch mẫu hoặc phân ca thật.

Nhân sự là trang chỉ đọc: chọn ca/ngày, tìm ứng viên, 10 người/trang, vai trò/bộ phận/chi nhánh và dialog lịch tuần của ứng viên. Tái sử dụng `/workday` và `/workday/candidates`; không gọi `/users`. Đây là **ứng viên theo ca/ngày**, không phải danh sách đầy đủ mọi nhân sự (người đã bận ca được loại khỏi danh sách). Sức chứa, quyền thực hiện và điều kiện hợp lệ được backend kiểm tra lại lúc phân ca.

## TASK_MANAGEMENT / INTERNAL_REQUEST

Tái sử dụng Work với phạm vi OPERATIONAL hoặc INTERNAL_REQUEST; không nhân bản workflow. Tìm kiếm mã/tên và bộ lọc trạng thái, bộ phận, quá hạn chạy trước phân trang trên backend, giữ kiểm tra scope/ownership. Bộ lọc mặc định đóng. Các hành động chuyên môn vẫn theo permission và workflow hiện hữu.

## MATERIAL_REQUEST / BACKEND_AUTHORIZATION / BRANCH_SCOPE

Tái sử dụng Documents trong chế độ MR. Tìm kiếm mã/lý do, lọc chờ duyệt chạy tại API. Detail trả `canApprove` được tính bằng chính `ApprovalAuthority`: người tạo, role, quyền L1/fallback và DEPARTMENT_AUTHORITY, kể cả bộ phận kho của fallback. FE không suy ra quyền duyệt chỉ từ MR_APPROVE_L1. API ghi vẫn kiểm tra lại toàn bộ điều kiện và row version; không sửa luật duyệt, không cấp quyền kho.

## Kiểm chứng

- Backend build: `dotnet build HotelOps.slnx --no-restore` **Passed**, 0 warning / 0 error. Lần đầu lệnh build thường bị MSB3026/MSB3027/MSB3021 vì API giữ DLL; đã dừng đúng PID 31332 rồi build lại thành công. Đây là lỗi khóa file, không phải lỗi C#.
- `dotnet test HotelOps.slnx --no-build`: **28 Passed, 2 Skipped, 0 Failed**. Hai test WorkerIntegrationTests và AccountManagementTests thiếu cấu hình fixture riêng nên tự skip; không coi là đã kiểm chứng.
- `Test-Backend.ps1 -DepartmentHead -Port 5187`: **23 nhóm Passed**, SQL Server/API thật trên database riêng `HotelOpsCompactApiTesta8738d222824`. Bao gồm hai chi nhánh, quản lý ca khác bộ phận employment, candidate đúng chi nhánh, account inactive, row version cũ, phân ca hợp lệ, overlap, capacity, không start ca nhân viên bằng DH, L1/fallback/self-approval/unmapped authority, filter API, IR create/submit, chặn User Management/chuyên môn/kho, dashboard thật và báo cáo đúng scope.
- UI mới: `npm run test:compact:department-head` **Passed**; 21 tổ hợp trang × kích thước (1440/768/375), 7 menu theo quyền, phân trang ứng viên, dialog lịch, filter server, capability duyệt, không gọi User Management API, không tràn ngang. API của browser test được giả lập.
- Bộ hồi quy `npm run test:compact:ux`: **Passed cả 5 suite**, navbar 48 tổ hợp, cấu trúc/cấu hình, phòng lễ tân, lịch theo đối tượng, dashboard/phân trang tài khoản. Lần chạy cuối exit 0.
- Frontend `npm run build`: **Passed**, TypeScript và Vite 1623 modules, không cảnh báo; xác nhận lại sau chỉnh nhãn báo cáo/nút liên kết/phân trang.
- API cục bộ khởi động lại PID 40104, cổng 5768, worker tắt; `/health` trả ok, schema 2.1.0, 40 tables. API fixture tự dừng. Database fixture giữ riêng để chẩn đoán; không có process kiểm thử cần chạy nền.

Bằng chứng: `artifacts/department-head/verification.json`, ảnh cùng thư mục; `artifacts/compact-v2/backend/department-head-verification.json`; các verification.json tại navbar-page-titles, structure-settings, reception-rooms, workday-filter, dashboard-pagination.

## FILES_CHANGED (chỉ lượt này)

- `AGENTS.md`, `docs/ui/compact-ux.md`: quy tắc tiếp nhận tài liệu chọn lọc và bảo toàn UX.
- `frontend/package.json`: lệnh kiểm thử DH.
- `frontend/src/compact/app.tsx`: điều hướng DH và route tái sử dụng.
- `frontend/src/compact/work.tsx`: phạm vi loại việc, tìm kiếm/bộ lọc API.
- `frontend/src/compact/documents.tsx`: chế độ MR, filter và capability duyệt.
- `frontend/src/compact/insights.tsx`: metric DH, báo cáo mới, nhãn và phân trang.
- `frontend/src/compact/scheduling-staff.tsx` (mới): ứng viên phân ca chỉ đọc.
- `frontend/src/compact/branch-metrics.tsx` (mới), `frontend/src/compact/task-ux.css`: thẻ thống kê.
- `src/HotelOps.Api/Compact/Services/InsightService.cs`: thống kê/báo cáo theo quyền và chi nhánh.
- `src/HotelOps.Api/Compact/Services/WorkService.cs`, `DocumentService.cs`: filter và capability theo luật hiện hữu.
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`: truyền tham số filter.
- `scripts/database/compact-v2/Test-Backend.ps1`: tùy chọn suite DH.
- `scripts/database/compact-v2/test-department-head.ps1`, `department-head-fixture.sql` (mới): kiểm thử độc lập.
- `frontend/tests/compact-department-head.mjs` (mới): hồi quy UI DH.
- `plans/261122-department-head-selective/plan.md` và báo cáo này.

Các thay đổi Git tồn tại trước lượt này (Program.cs, csproj, solution, main.tsx, package-lock, v.v.) được giữ nguyên; không commit/push/deploy.

## REMAINING_BLOCKERS / FINAL_VERDICT

Không có blocker cho phạm vi chọn lọc đã triển khai. Không tuyên bố hoàn tất mọi đề xuất P0: không bổ sung toàn bộ bộ lọc lịch, danh sách nhân viên quản trị hoặc báo cáo hiệu suất/tỷ lệ lấp đầy chuyên sâu. Phần nghiệp vụ chưa thay đổi vẫn dùng luật hiện hành. Không kiểm thử ghi trên database chính; UI browser dùng mock và API workflow kiểm thử riêng trên SQL fixture, không phải browser E2E ghi dữ liệu thật. Báo cáo tối đa 10.000 dòng; danh sách Nhân sự chỉ là ứng viên phù hợp ca/ngày.
