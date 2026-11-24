# Housekeeping Supervisor — implementation and verification

Phạm vi: Project 2 / HotelOpsCompactDB. Giữ UX compact, phòng cần tạo việc và quyền hiện có. Thực hiện một agent, không commit/push/deploy. Validation cuối hoàn tất ngày 23/11/2026; các giới hạn được ghi riêng.

## Bàn giao theo yêu cầu

| Hạng mục | Trạng thái | Kết quả / giới hạn |
|---|---|---|
| HOUSEKEEPING_SUPERVISOR_ROLE | VERIFIED | Primary read-only audit: code HOUSEKEEPING_SUPERVISOR, role_id 10; không hardcode ID vào nghiệp vụ. |
| SOURCE_AUDIT | VERIFIED | Đọc yêu cầu, change notes, EF/mappings, trigger, workflow, API, UI và role/grants thực tế. Bảng FEATURE / CURRENT_UI / API / PERMISSION / WORKFLOW / GAP / ACTION ở plan liên kết bên dưới. |
| SIDEBAR_ALIGNMENT | IMPLEMENTED, VERIFIED | Bảy nhóm có permission guard; người có nhiều role giữ shell hiện hữu. |
| HK_DASHBOARD | IMPLEMENTED, VERIFIED | API theo nhánh, KPI theo VIEW grant, phòng/vệ sinh/có khách riêng, workload, ưu tiên, chờ kiểm tra, quá hạn. Không giả KPI thiếu quyền. |
| ROOM_STATUS | IMPLEMENTED, VERIFIED | Grid 15 phòng/trang, bảng, floor/cleaning/occupancy/block filters, drawer và current-turnover workflow. SQL xác minh cleaning COMPLETED/inspection PASSED nhưng phòng vẫn BLOCKED; UI kiểm tra kết hợp vệ sinh và block. |
| CLEANING_ASSIGNMENT | IMPLEMENTED, VERIFIED | Phòng cần dọn chưa có việc mở → tạo → modal phân công; ứng viên active đúng nhánh/execution grant. Chống duplicate, stale room/work version và reassign sau submission. |
| CLEANING_TASKS | VERIFIED | Tái sử dụng Staff accept/start/progress/checklist/complete và timer/audit hiện hành. HKS không thực hiện thay staff. |
| INSPECTION | IMPLEMENTED, VERIFIED | Queue công việc gốc đã nộp; exact-parent deep link, checklist, kết luận đạt/không đạt, verifier độc lập. Không inspection sớm hoặc tạo trùng. Block vẫn ngăn READY. |
| REWORK | IMPLEMENTED, VERIFIED | Giữ failed inspection; rework execution phải được nộp trước reinspection. Lượt cũ bị FAILED mới hơn thay thế không còn đủ điều kiện. Cho lượt sửa tiếp theo nhưng chặn thực hiện song song, không tạo lifecycle mới. |
| CHECKLIST_MANAGEMENT | PARTIAL | Xem detail và tạo/version mới cùng mã/loại; giữ snapshot cũ. Published template/item bị trigger khóa update/delete, nên kích hoạt/ngừng trực tiếp BLOCKED bởi contract. |
| HK_STAFF_CANDIDATES | IMPLEMENTED, VERIFIED | API dựa trên CLEANING_ASSIGN/candidate contract, không dùng User Management. Giữ giới hạn 200 ứng viên hiện hữu. |
| SHIFT_VISIBILITY | IMPLEMENTED, VERIFIED | Ngày/múi giờ theo branch; ca và StartedAt/EndedAt thực tế; open/completed workload từ phân công. Lịch cá nhân dùng Workday OWN; không có weekly scheduling. |
| INCIDENTS | VERIFIED | Reuse API tạo/xem sự cố đúng nhánh; không triage, điều phối kỹ thuật hay đóng incident ngoài quyền. |
| DEFECTS | IMPLEMENTED, VERIFIED | List/search/status/sort; resolve cần DEFECT_RESOLVE + INSPECTION_PERFORM, cùng branch/root/turnover và rework đã nộp, verifier độc lập; giữ finding cũ. |
| NOTIFICATIONS | VERIFIED | Reuse inbox người nhận, đánh dấu đọc; đọc đối tượng đích trước điều hướng, lỗi không bị biến thành thành công. |
| BACKEND_AUTHORIZATION | VERIFIED | Guard backend, ownership, lifecycle, RowVersion, transaction/audit có sẵn. Không sửa grants. |
| BRANCH_SCOPE | VERIFIED | Hai actor HKS ở hai branch fixture: mọi API mới và Workday/template từ chối branch còn lại; room workflow/template detail từ chối foreign IDs. Primary chỉ đọc audit, không tạo dữ liệu kiểm thử. |
| FRONTEND_BUILD | VERIFIED | npm run build: typecheck và production build PASS, 1644 modules, không có warning build. |
| BACKEND_BUILD | VERIFIED | dotnet build HotelOps.slnx: PASS, 0 warnings, 0 errors. |
| TEST_RESULTS | VERIFIED | .NET 28 PASS / 3 conditional SKIP; workflow unit 5 PASS. Fixture SQL 51/51 nhóm PASS. Browser HKS 24 nhóm ở 1440/768/375 PASS, gồm filter kết hợp vệ sinh/block, exact-parent inspection/phân công, phiên bản checklist và đóng bộ lọc sau refresh. Intake, HK Staff/OM và shared UX đều PASS. |
| FILES_CHANGED | IMPLEMENTED | Danh sách bên dưới; không ghi đè local artifacts hoặc thay đổi unrelated modules. |
| REMAINING_BLOCKERS | BLOCKED / NOT_TESTED | Published-template activation/deactivation cần thay đổi contract được duyệt. Ba conditional tests worker/account không chạy do chưa cấu hình fixture riêng. Không chạy browser ghi nghiệp vụ trên primary. |
| FINAL_VERDICT | PARTIAL | IMPLEMENTED + VERIFIED cho ứng dụng và workflow trong contract hiện hữu. Activation/deactivation mẫu đã công bố vẫn BLOCKED bởi trigger; ba conditional tests SKIP không tính PASS. |

## Baseline được bảo toàn

11 grants: ATTENDANCE_RECORD — OWN; CHECKLIST_MANAGE, CLEANING_ASSIGN, CLEANING_VIEW, DASHBOARD_VIEW, DEFECT_RESOLVE, INCIDENT_CREATE, INCIDENT_VIEW, INSPECTION_PERFORM, INSPECTION_VIEW, ROOM_VIEW — BRANCH. Không thêm ROOM_STATUS_UPDATE/ROOM_RELEASE, USER_VIEW, SHIFT_MANAGE hoặc quyền kho/bảo trì. DB primary không nhận dữ liệu test; không chạy Staffing V3/V4, không đổi schema/generated entities/migrations.

## Luồng nghiệp vụ

Reception checkout tạo turnover cần vệ sinh → HKS chọn phòng đủ điều kiện/tạo việc/phân công → HK Staff nhận/bắt đầu/checklist/hoàn thành → PENDING_INSPECTION → HKS được phân phiếu inspection nhận/bắt đầu/ghi checklist/kết luận → PASSED và READY nếu không block, hoặc FAILED và REWORK_REQUIRED → staff thực hiện rework → PENDING_REINSPECTION → HKS xác minh lỗi và tạo phiếu kiểm tra mới. Rework nộp không tự thành READY; failed review và lịch sử phân công không bị xóa.

## API đọc được bổ sung

| GET /api/v2 | Permission / dữ liệu |
|---|---|
| insights/housekeeping?branchId | DASHBOARD_VIEW, các metrics/list theo VIEW grant tương ứng |
| work/housekeepers?branchId&date | CLEANING_ASSIGN; ứng viên, Workday thật, workload, timer |
| work/inspection-queue?branchId&page&search&sort | CLEANING_VIEW + INSPECTION_VIEW; current turnover đủ điều kiện |
| work/defects?branchId&page&search&status&sort | INSPECTION_VIEW; finding/history, không resolve trực tiếp khi thiếu quyền |
| templates/checklists/{id} | CHECKLIST_MANAGE, scope template |
| rooms/{id}/workflow | ROOM_VIEW; từng domain array theo VIEW grant |

POST templates/checklists được tái sử dụng cho phiên bản mới; không có endpoint giả edit/disable published. Các mutation vẫn đi qua WorkService hiện hữu và Atomic transaction. Resolve finding có thêm kiểm tra grant và link rework cùng cycle. Hàng đợi hỗ trợ sort phía server; không sắp xếp giả chỉ trong một trang.

## Files tạo/sửa trong lượt alignment

- Frontend mới: `frontend/src/compact/housekeeping-workspace.tsx`, `housekeeping-overview.tsx`, `housekeeping-review-queue.tsx`, `housekeeping-staff.tsx`, `housekeeping-checklists.tsx`.
- Frontend dùng lại: `frontend/src/compact/app.tsx`, `work.tsx`, `rooms.tsx`, `cleaning-queue.tsx`, `api.ts`. Các file này cũng chứa thay đổi pending-room intake từ task trước; giữ nguyên thay đổi đó.
- Backend mới: `src/HotelOps.Api/Compact/Services/InsightService.Housekeeping.cs`, `WorkService.Housekeeping.cs`, `RoomService.Workflow.cs`.
- Backend sửa: `src/HotelOps.Api/Compact/Services/WorkService.cs`, `RoomService.cs`, `TemplateService.cs`; thin controller routes trong `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`.
- Tests: `frontend/tests/compact-housekeeping-supervisor.mjs`; `scripts/database/compact-v2/test-housekeeping-supervisor.ps1`, `test-housekeeping-staff.ps1`, `housekeeping-staff-fixture.sql`, `Test-Backend.ps1`.
- Docs/plan/journal: `docs/ui/compact-ux.md`, `plans/261123-0801-housekeeping-supervisor-alignment/plan.md`, báo cáo này và journal local.
- `WorkService.Cleaning.cs`, cleaning-rooms UI/CSS/browser test và `Contracts/Common.cs` là thay đổi intake đã có từ task trước, được giữ và kiểm tra lại.

## Validation và giới hạn

Lượt cuối: `Test-Backend.ps1 -HousekeepingSupervisor` exit 0, database `HotelOpsCompactApiTest7aeefa946273`, 51/51 nhóm, verifiedAt `2026-11-23T02:31:27.1600472Z`. API fixture dừng trong finally; primary local được khôi phục port 5768, PID 3144, `/health` trả status ok/schema 2.1.0/40 tables. Vite 5186 trả HTTP 200; không còn listener fixture 5187/5188. Browser cuối HKS và Reception exit 0. HKS không có pageerror; ảnh dashboard/staff/defect đã chờ dữ liệu trước khi chụp.

- SQL fixture chỉ ghi vào database mới tên HotelOpsCompactApiTest*, backup riêng và API bị dừng trong finally. QA/OTHER là hai nhánh fixture dùng để kiểm tra chính sách; không tuyên bố mutation đã chạy trên DVBV/DVCT primary.
- Browser dùng API mock, riêng với API/SQL tests. Không coi browser mock là bằng chứng authorization. Đã kiểm tra HKS 1440/768/375, HK Staff 27 views, OM 24 views, navbar 48 route/viewport checks, settings, Reception, Workday audience và dashboard pagination.
- .NET 3 SKIP: hai worker integration và AccountManagement integration có điều kiện môi trường riêng; không tính là PASS.
- Các lỗi test đã sửa đúng contract: Workday bắt buộc date; sidebar dùng button; select label chứa text options; cell có thêm mã finding; duplicate pass trả 403 vì assignment đã kết thúc. Không hạ guard để làm test qua.
- Regression có thật được tái hiện: inspection sau FAILED có thể dùng lại submission rework cũ. Guard, queue và dashboard nay cùng loại bỏ submission đã bị review thất bại mới hơn thay thế.
- Git diff --check PASS; Git cảnh báo LF → CRLF theo cấu hình checkout Windows. Không có build warning bị bỏ qua.
- Single-agent review theo chỉ đạo, không mở phiên simplifier/subagent. AgentWiki publish skipped; không có Git publication.

Plan/audit: [plan](../261123-0801-housekeeping-supervisor-alignment/plan.md). Evidence API: `artifacts/compact-v2/backend/housekeeping-supervisor-verification.json`; browser: `artifacts/housekeeping-supervisor/verification.json`, `artifacts/cleaning-requests/verification.json`. Artifacts không được stage/publish.
