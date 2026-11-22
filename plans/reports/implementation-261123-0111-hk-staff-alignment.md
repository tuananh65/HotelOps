# HK Staff Project 2 — bàn giao ngày 23/11/2026

Áp dụng có chọn lọc theo chỉ dẫn giữ UX trước đây: navbar title, chi nhánh dưới logo, control 31px, bộ lọc đóng mặc định, phòng 15 ô/trang và drawer. Thực hiện single-agent, review inline. Không đổi schema, entity, role_permissions, Staffing V3/V4 hoặc dữ liệu nghiệp vụ primary.

| Mục | Trạng thái | Kết quả |
|---|---|---|
| HK_STAFF_ROLE | VERIFIED | Đối chiếu chỉ đọc HotelOpsCompactDB: role_id=4, đúng 13 grants, ATTENDANCE_RECORD OWN; 10 staff mỗi DVBV/DVCT. Runtime không hardcode ID. |
| SOURCE_AUDIT | VERIFIED | Đã đối chiếu Work/Workday/Room/Document/Notification/Evidence, entity/mapping và SRS/change notes; bảng FEATURE/CURRENT_UI/API/PERMISSION/OWNERSHIP/GAP/ACTION trong plan. |
| SIDEBAR_ALIGNMENT | IMPLEMENTED, VERIFIED | 7 nhóm cho profile chỉ có HK_STAFF tại chi nhánh; menu/nút theo grants. Profile đa vai trò giữ điều hướng chung. Deep link cũ được chuyển đúng nhóm. |
| PERSONAL_DASHBOARD | IMPLEMENTED, VERIFIED | API personal theo actor/branch/timezone: được giao hôm nay, chưa nhận, đang làm, gửi hoàn thành hôm nay, chờ kiểm tra, rework, quá hạn và chưa đọc; ưu tiên tối đa 10 việc, ca hiện tại/hôm nay và 7 ngày tới. Không dùng DASHBOARD_VIEW hay tổng chi nhánh làm KPI cá nhân. |
| CLEANING_TASKS | IMPLEMENTED, VERIFIED | AssignedToMe giữ assignment ACTIVE/COMPLETED, loại việc đã chuyển cho người khác. Search tên/số việc/số phòng, trạng thái, quá hạn, ngày tạo, sắp xếp và phân trang trên server. Chi tiết có chi nhánh, tầng, phòng, người giao và thời điểm giao. |
| CLEANING_EXECUTION | IMPLEMENTED, VERIFIED | Nhận → bắt đầu → tiến độ → tạm dừng/tiếp tục → hoàn thành; kiểm tra assignment trước transition. Checklist bắt buộc, RowVersion cũ và timer đang mở đều được kiểm tra. Hoàn thành đến PENDING_INSPECTION. |
| CHECKLIST | IMPLEMENTED, VERIFIED | Ghi PASS/FAIL/NA và ghi chú trên snapshot checklist của công việc theo quyền EXECUTE. Không thêm checklist song song, không mở quyền quản lý mẫu hoặc sửa Inspection. |
| REWORK_EXECUTION | IMPLEMENTED, VERIFIED | Tab Làm lại trong Cleaning; thông tin lỗi và kết quả Inspection chỉ đọc qua công việc gốc. Rework chỉ người được giao thực hiện; hoàn thành đến PENDING_REINSPECTION, không Ready. |
| ROOM_VIEW | IMPLEMENTED, VERIFIED | Giữ grid/drawer, hạng phòng, sức chứa, tầng/khu vực trong drawer, Occupancy/readiness/block và lịch sử; có liên kết tìm việc/checklist cá nhân theo số phòng. Không có nút check-in/out, Ready hoặc unblock. |
| OPERATIONAL_TASKS | IMPLEMENTED, VERIFIED | Workspace cá nhân riêng, dùng lifecycle OPERATIONAL hiện có; hoàn thành chờ xác nhận. Không phân công/hủy/mở lại/tự xác nhận. |
| INTERNAL_REQUESTS | IMPLEMENTED, VERIFIED | Tạo/gửi, theo dõi và xử lý khi được giao. Backend guard và capability ngăn IR_PROCESS hủy/điều phối yêu cầu người khác. Người tạo từng thực hiện công việc không được hiện nút xác nhận độc lập trái contract. |
| MATERIAL_REQUESTS | IMPLEMENTED, VERIFIED | Material-only workspace: tạo, thêm dòng, gửi duyệt, xem chi tiết/lịch sử/cấp phát. Thêm guard người tạo cho sửa/gửi/hủy; cấm post trực tiếp MR. Không duyệt hoặc nhập/xuất kho. |
| PERSONAL_WORKDAY | VERIFIED | Tái sử dụng Workday OWN, không tự tạo lịch. Xem tuần/ca/lịch sử, start/end chính mình, stale version/ca tương lai/end trước start bị từ chối. Dashboard không giả định đang trong ca. |
| INCIDENT_REPORTING | IMPLEMENTED, VERIFIED | Tạo incident theo nhánh, chọn phòng/tài sản và mức nghiêm trọng từ dữ liệu thật; xem tiến độ/lịch sử. Có upload PNG/JPEG/PDF tối đa 10 MB và tải file có xác thực. Không triage/giao bảo trì/đóng sự cố hoặc unblock. |
| NOTIFICATIONS | IMPLEMENTED, VERIFIED | Inbox theo recipient và branch, phân trang, read/ack; mở Work/Material sau khi GET đối tượng kiểm tra quyền. KPI unread dùng cùng kiểm tra quyền đích. |
| BACKEND_AUTHORIZATION | IMPLEMENTED, VERIFIED | Tái sử dụng Access/Atomic/Audit. Sửa guard assignment trước execution, IR handler/authority, MR creator và evidence owner. Không thêm role/permission/policy. |
| OWNERSHIP_ENFORCEMENT | VERIFIED | Staff cùng nhánh không thể thực hiện/checklist/progress/upload vào việc người khác; không sửa/gửi/hủy vật tư người khác. Concurrency và audit được giữ. |
| BRANCH_SCOPE | VERIFIED | Kiểm tra hai nhánh trên fixture: rooms, work, personal, options, Workday, documents; cross-branch reads/writes bị từ chối. |
| FRONTEND_BUILD | VERIFIED | TypeScript project build và Vite production build Passed; Prettier các file thay đổi Passed. |
| BACKEND_BUILD | VERIFIED | dotnet build HotelOps.slnx Passed, 0 warnings/errors ở lượt cuối. |
| TEST_RESULTS | VERIFIED / NOT_TESTED | HK API 26 nhóm; API hồi quy mở rộng 128 Passed, 0 Failed; .NET 28 Passed, 2 Skipped; workflow JS 4/4; HK UI 27, OM UI 24, DH UI 21; cả 5 suite UX chung Passed. Hai bài .NET WorkerIntegrationTests và AccountManagementTests cần cấu hình riêng nên chưa chạy trong unit suite. |
| FILES_CHANGED | IMPLEMENTED | Danh sách bên dưới; các thay đổi có sẵn của người dùng được giữ. |
| REMAINING_BLOCKERS | NOT_TESTED | Không còn blocker cho workflow HK Staff đã xác nhận. Kiểm thử nghiệp vụ ghi chạy trên fixture; không đăng nhập/ghi dữ liệu primary để test. Browser dùng API mock, API tích hợp dùng SQL Server thật trong fixture độc lập. |
| FINAL_VERDICT | IMPLEMENTED, VERIFIED | Luồng giao việc → thực hiện → kiểm tra → làm lại → tái kiểm tra đạt đã hoạt động; Reception check-out mở chu kỳ dọn mới. Phần HK Supervisor chỉ được kiểm chứng như đối tác workflow, không tuyên bố đã hoàn thành toàn bộ đề bài Supervisor riêng. |

## Các file của lượt này

- Frontend: `frontend/src/compact/app.tsx`, `housekeeping-personal.tsx` (mới), `work.tsx`, `workflow.ts`, `rooms.tsx`, `documents.tsx`, `insights.tsx`, `api.ts`, `task-ux.css` và `frontend/package.json`.
- Backend: `src/HotelOps.Api/Compact/Services/InsightService.Personal.cs` (mới), `InsightService.cs`, `WorkService.cs`, `DocumentService.cs`, `NotificationService.cs`, `EvidenceService.cs`; `Compact/Controllers/OperationsControllers.cs`, `WorkspaceController.cs`.
- Tests: `frontend/tests/compact-housekeeping-staff.mjs` (mới), `compact-navbar-titles.mjs`, `compact-dashboard-pagination.mjs`; `scripts/database/compact-v2/housekeeping-staff-fixture.sql` và `test-housekeeping-staff.ps1` (mới), `Test-Backend.ps1`, `Test-Functions-Work.ps1`.
- Docs/plan/journal: `docs/ui/compact-ux.md`, `plans/261123-hk-staff-alignment/plan.md`, báo cáo này và `plans/journals/2026-11-23-hk-staff-alignment-and-ownership-enforcement.md`.

## Bằng chứng và giới hạn

- HK API: `artifacts/compact-v2/backend/housekeeping-staff-verification.json`, database `HotelOpsCompactApiTest60d1620fa10b`, 26 nhóm. Bao gồm inactive assignee, duplicate cleaning, stale versions, early inspection, timer conflict, rework/independent inspection, ownership MR/IR/evidence, own/future shift và notification recipient.
- Hồi quy API: `artifacts/compact-v2/backend/verification.json`, database `HotelOpsCompactApiTestaae805b66be6`, 128 kiểm tra đạt; schema vẫn 40 bảng/160 FK. Các fixture có backup đã kiểm chứng, API 5187 tự dừng; database test giữ lại để kiểm tra.
- UI: `artifacts/housekeeping-staff/ui-verification.json` và ảnh 1440/768/375; `artifacts/navbar-page-titles`, `structure-settings`, `reception-rooms`, `workday-filter`, `dashboard-pagination`, `operations-manager`, `department-head`. Đã xem trực tiếp ảnh desktop/mobile của dashboard HK.
- Lượt HK UI cuối kiểm tra cả room drawer có tầng, upload FormData, tải file có xác thực và tên file tải về. Log build frontend cuối tại `artifacts/housekeeping-staff/frontend-build.log`, exit code 0. Hook chặn đọc thư mục output build; kiểm chứng bằng log build và UI trên server thay thế, không thay cấu hình hook.
- Các lần thất bại đã xử lý: DLL bị API ngoài sandbox giữ; expected 409 cũ trong test khi nay ownership trả 403; fixture dùng UNIT thay UOM; test cố tạo PRIORITY bằng API vốn bị khóa. Các fixture sửa theo contract hiện hành, không nới grant/trigger. Hai test UI cũ đã cập nhật endpoint/title của HK, một lượt HMR được chạy lại trên source ổn định.
- Giữ branch read BRANCH theo permission hiện có; giao diện công việc cá nhân lọc assignment, mọi mutation còn kiểm tra current assignee. Không đổi BRANCH thành OWN trong database.
- Ca đang làm và KPI theo timezone của chi nhánh ở backend. Bộ lọc ngày tạo trên Work được chuyển từ ngày theo múi giờ trình duyệt sang UTC như các form datetime hiện hành; người dùng ngoài múi giờ chi nhánh cần chọn khoảng thời gian tương ứng.
- Không tạo lifecycle/enum mới. Nghiệm thu, Ready và quản lý mẫu vẫn thuộc Supervisor; xác nhận cuối của Operational Task thuộc người quản lý. Lịch sử Inspection trong Cleaning/Rework là tóm tắt 10 phiếu gần nhất, không cấp quyền truy cập toàn bộ phiếu Inspection.
- Chưa kiểm thử hệ thống thật bằng tài khoản production hoặc gửi thông báo ngoài ứng dụng. Không commit/push/merge/deploy. Review được thực hiện inline, không gọi reviewer độc lập theo yêu cầu single-agent.
- Git còn cảnh báo LF/CRLF có sẵn ở package-lock/package/main; không có build warning C# ở lượt cuối. API local đã chạy lại PID 36268, cổng 5768, worker tắt; FE 6424/9324 cổng 5186.

Không có câu hỏi nghiệp vụ chưa giải quyết trong phạm vi HK Staff.
