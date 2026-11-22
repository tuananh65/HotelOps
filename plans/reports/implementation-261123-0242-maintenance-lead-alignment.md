# Bàn giao Maintenance Lead — Project 2 / CompactDB

Đã triển khai frontend và backend cho điều phối, nghiệm thu, Incident, PM, tra cứu thiết bị/nhân viên và các màn hình dùng chung theo quyền hiện hành. Giữ UX compact đã chấp nhận. Không sửa schema/entities/grants, Staffing V3/V4 hoặc Project 1; không commit/push/deploy.

## Kết quả theo yêu cầu

| Hạng mục | Trạng thái | Bằng chứng / giới hạn |
|---|---|---|
| MAINTENANCE_LEAD_ROLE | VERIFIED | SQL chỉ đọc trên CompactDB: role 11, đúng 14 grants; ATTENDANCE_RECORD OWN, 13 grant chuyên môn BRANCH; không hardcode ID trong nghiệp vụ |
| SOURCE_AUDIT | VERIFIED | Bảng FEATURE/CURRENT_UI/API/PERMISSION/WORKFLOW/GAP/ACTION trong plan; kiểm tra Work, Template, Worker, Asset, Insight, Workday, Notification, DTO, DbContext, entities và SQL guards; ưu tiên yêu cầu hiện tại hơn SRS draft |
| SIDEBAR_ALIGNMENT | IMPLEMENTED / VERIFIED | 8 nhóm, icon Lucide, từng mục/hành động theo grants; lịch cá nhân dùng liên kết phụ; giữ navbar, chi nhánh dưới logo và control compact |
| MAINTENANCE_DASHBOARD | IMPLEMENTED / VERIFIED | 12 KPI thật theo branch; queue phân công/review/Incident/PM, progress summary; tổng mở bao gồm COMPLETED chờ nghiệm thu, không cộng gộp Incident; null domain thiếu quyền |
| MAINTENANCE_TASKS | IMPLEMENTED / VERIFIED | Work workspace dùng API thật; tìm/sort/lọc/ngày/phân trang; metadata thiết bị, ưu tiên, người phụ trách; drawer có tiến độ, kết quả, assignment/review history và MR nguồn |
| MAINTENANCE_ASSIGNMENT | IMPLEMENTED / VERIFIED | Modal giao/đổi người; ứng viên active đúng scope/quyền thực thi; một assignment chính, giữ REASSIGNED; từ chối inactive, sai branch, Lead thiếu execution grant và RowVersion cũ |
| MAINTENANCE_REVIEW | IMPLEMENTED / VERIFIED | COMPLETED → verify → CLOSED, hoặc reopen → PENDING → staff xử lý tiếp → review; bắt buộc lý do khi mở lại; không review trước hoàn thành/trùng/phiên bản cũ; người từng được giao không tự review |
| INCIDENT_MANAGEMENT | IMPLEMENTED / VERIFIED | Incident độc lập lifecycle; thêm tạo Maintenance liên kết qua transaction/version/operation key; từ chối retry và công việc trùng cả lúc chờ review; không đóng Incident khi Maintenance chưa được duyệt |
| INCIDENT_TRIAGE | VERIFIED | Modal ưu tiên/hạn tương lai/lý do, endpoint kiểm tra INCIDENT_TRIAGE, branch, lifecycle và version |
| INCIDENT_ESCALATION | VERIFIED | Modal escalation; giữ INCIDENT_ESCALATION_HANDLE, event/audit/notification, không tự thay SLA policy hoặc branch |
| PREVENTIVE_MAINTENANCE | IMPLEMENTED / VERIFIED / PARTIAL | Tạo/sửa/dừng kế hoạch, kiểm tra asset/room/department/checklist, mã/loại/branch bất biến, version, lịch sử occurrence; worker hiện có tạo 1 Maintenance qua 2 ticks và đi hết execution/review. Primary local đang tắt worker nên không tự sinh việc theo thời gian ở cấu hình đó |
| ASSET_VIEW | IMPLEMENTED / VERIFIED | Drawer chỉ đọc vị trí, Incident/Maintenance và PM liên quan; không có nút quản lý tài sản cho Lead; API move/condition/retire bị từ chối |
| TECHNICIAN_CANDIDATES | IMPLEMENTED / VERIFIED | Endpoint MAINTENANCE_ASSIGN dùng danh sách đủ execution capability; mã/tên, workload, timer, ca hiện tại từ dữ liệu thật; không dùng USER_VIEW. Danh sách điều phối chủ động chỉ gồm tài khoản đủ điều kiện active |
| PERSONAL_WORKDAY | VERIFIED | Dùng màn hình/API có sẵn; xem/start/end OWN; chặn tạo phân ca, start tương lai và start ca của người khác |
| MATERIAL_REQUESTS | VERIFIED | Xem MR được phép và link từ công việc nguồn; không MR_CREATE/APPROVE/FULFILL/stock posting, không cấp thêm quyền |
| NOTIFICATIONS | VERIFIED | Thông báo review/escalation/PM thật; read ID của người khác trả 404; inbox/deep link dùng kiểm tra recipient và domain permission hiện có |
| BACKEND_AUTHORIZATION | VERIFIED | Permission tại service mỗi endpoint mới; branch/eligible/assignment/lifecycle/version ở backend; Lead không có MAINTENANCE_EXECUTE, USER/ROLE, inventory, occupancy, room-unblock hay SHIFT_MANAGE |
| BRANCH_SCOPE | VERIFIED | Hai actor A/B trên fixture phản chiếu scope DVBV/DVCT; dashboard/work/PM/assets/options/candidates/Workday chặn branch kia; task detail/writes, asset PM và warehouse nghiệp vụ ngoài quyền bị từ chối |
| ASSIGNMENT_ENFORCEMENT | VERIFIED | Chỉ assignee hợp lệ nhận/start/complete; Lead không thực thi thay staff; inactive, foreign và non-executor không được giao; reassign kết thúc assignment cũ và giữ audit |
| CONCURRENCY | VERIFIED | Work/PM stale version trả 409, duplicate derive bị chặn, serializable transaction và SQL unique occurrence, scheduler application lock; UI disable submit khi pending, chỉ báo thành công sau phản hồi API |
| FRONTEND_BUILD | VERIFIED | Typecheck và npm run build passed; targeted Prettier passed; không có warning build frontend |
| BACKEND_BUILD | VERIFIED | dotnet build HotelOps.slnx: 0 warnings, 0 errors |
| TEST_RESULTS | VERIFIED / NOT_TESTED | 33 checks Maintenance Lead; worker integration 1 pass; API regression 128 pass; browser thật 27 route/viewport, 4 mutations 200, 0 page errors/failed API. .NET solution 28 pass/3 conditional skip; frontend workflow 5 pass. Không ghi workflow thử trên primary; không chạy load test hay toàn bộ browser engine |
| FILES_CHANGED | IMPLEMENTED | Danh sách bên dưới, phân biệt file sửa và tạo mới trong lượt này với dirty baseline của repository |
| REMAINING_BLOCKERS | PARTIAL / BLOCKED | Chỉ tự động sinh PM trên local primary bị chặn bởi cấu hình worker=false hiện có. Không tự bật vì sẽ ghi nghiệp vụ primary. Không có contract blocker cho các luồng đã kiểm chứng trên fixture |
| FINAL_VERDICT | IMPLEMENTED / VERIFIED, có giới hạn cấu hình | Workflow Maintenance và PM đã chạy end-to-end trên fixture thật. Vận hành PM tự động trên primary cần cấu hình worker có chủ đích; chưa xác minh bằng ghi dữ liệu primary |

## Bằng chứng kiểm thử

- `scripts/database/compact-v2/Test-Backend.ps1 -MaintenanceLead -Port 5187`: **33/33**, database `HotelOpsCompactApiTest3cd42085fa72`, backup xác minh trước fixture writes. JSON: `artifacts/compact-v2/backend/maintenance-lead-verification.json`.
- `MaintenanceWorkerTests.LeadPlanCreatesOneMaintenanceOccurrenceAcrossRepeatedTicks`: **1/1**, chỉ nhận connection có InitialCatalog đúng mẫu HotelOpsCompactApiTest; template tạo qua API Lead, hai ticks không tạo trùng. TRX: `artifacts/compact-v2/backend/maintenance-worker/maintenance-worker.trx`.
- `Test-Backend.ps1 -Extended -Port 5188`: **128/128**, database `HotelOpsCompactApiTest2354e037944c`; schema giữ 40 tables/160 FKs. JSON: `artifacts/compact-v2/backend/verification.json`.
- `frontend/tests/compact-maintenance-lead-live.mjs`: **27 views** tại 1440/900/375; sidebar/guards/filter ẩn/overflow; phân công và review qua browser; PM create/edit giữ nguyên 08:00; Escape modal không đóng drawer. Request proxy chuyển toàn bộ API sang fixture, không gọi primary. JSON và ảnh trong `artifacts/maintenance-lead/`.
- `dotnet test HotelOps.slnx --no-build`: **28 passed, 3 skipped**. Skipped: Maintenance worker (đã chạy riêng 1/1 ở trên), recurrence/low-stock worker cần fixture riêng, account/email integration cần fixture/secrets riêng. Không báo ba bài này là pass trong solution run.
- `npm run test:compact`: **5/5**. `npm run test:compact:ux`: navbar 48 route/viewport, structure/settings, reception board, Workday và dashboard/account pagination đều pass (mocked APIs).
- Browser regression riêng HK Staff **27 views**, Department Head **21**, Operations Manager **24**, Storekeeper **27** đều pass (mocked APIs). Không quy các mock tests thành kiểm chứng database.
- Inline review đã sửa lỗi EF dịch DTO, duplicate repair khi pending review, review filter reset, modal Escape, PM UTC/local drift, length validation đúng schema và KPI open gồm pending review. Không dùng agent phụ theo yêu cầu Single-Agent.

## API bổ sung

| Method / Route | Permission / kiểm soát |
|---|---|
| GET `/api/v2/work/technicians?branchId=` | MAINTENANCE_ASSIGN; ứng viên trong branch và dữ liệu ca cần cho điều phối |
| POST `/api/v2/work/{id}/maintenance` | INCIDENT_VIEW của nguồn + MAINTENANCE_ASSIGN; version/key/lý do, source active, không active child, atomic create/event |
| GET `/api/v2/insights/maintenance?branchId=` | DASHBOARD_VIEW + MAINTENANCE_VIEW; Incident/PM metrics còn kiểm tra quyền chuyên môn |
| GET `/api/v2/templates/schedules/{id}` | PM_MANAGE hoặc TASK_MANAGE theo loại template, branch scope |
| PUT `/api/v2/templates/schedules/{id}` | Quyền quản lý đúng loại/branch, RowVersion; giữ identity và lịch sử occurrence |

Work list/detail và Asset detail chỉ được bổ sung DTO metadata trong scope được phép. Không expose generated EF entity qua response mới. Không đổi HTTP contract hoặc grant của Workday/notifications.

## Files thay đổi trong lượt này

Sửa backend:

- `src/HotelOps.Api/Compact/Contracts/Common.cs`
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.cs`
- `src/HotelOps.Api/Compact/Services/TemplateService.cs`
- `src/HotelOps.Api/Compact/Services/AssetService.cs`

Tạo backend:

- `src/HotelOps.Api/Compact/Services/WorkService.Maintenance.cs`
- `src/HotelOps.Api/Compact/Services/InsightService.Maintenance.cs`

Sửa frontend:

- `frontend/src/compact/app.tsx`
- `frontend/src/compact/work.tsx`
- `frontend/src/compact/workflow.ts`
- `frontend/src/compact/assets.tsx`
- `frontend/src/compact/ui.tsx` (chặn bubble cancel của modal lồng nhau)
- `frontend/src/compact/api.ts`

Tạo frontend:

- `frontend/src/compact/maintenance-overview.tsx`
- `frontend/src/compact/maintenance-plans.tsx`

Kiểm thử/tài liệu:

- Sửa `scripts/database/compact-v2/Test-Backend.ps1`, `frontend/tests/compact-workflow.test.mjs`, `docs/ui/compact-ux.md`, plan hiện tại.
- Tạo `scripts/database/compact-v2/maintenance-lead-fixture.sql`, `scripts/database/compact-v2/test-maintenance-lead.ps1`, `tests/HotelOps.Compact.Tests/MaintenanceWorkerTests.cs`, `frontend/tests/compact-maintenance-lead-live.mjs`, báo cáo này và journal cùng ngày.
- Không có file xóa. Các file dirty khác đã tồn tại từ các lượt trước, được giữ nguyên.

## Giới hạn và trạng thái môi trường

Không có trạng thái REWORK maintenance mới: từ chối kết quả dùng `reopen` hiện hữu về PENDING; khi assignment cũ còn ACTIVE, nhân viên đó xử lý tiếp hoặc Lead đổi người; khi việc đã CLOSED, cần assignment mới. Không sửa kết quả cũ; WorkEvent VERIFY/REOPEN giữ lịch sử.

Không có thay đổi tự động trạng thái room/asset/Incident sau Maintenance review. MR liên quan là nhu cầu/cấp vật tư qua chứng từ hiện có, không suy diễn toàn bộ lượng đã cấp thành lượng thực tế kỹ thuật viên đã tiêu hao nếu contract không ghi nhận tiêu hao đó.

PM scheduler dùng OperationsWorker hiện hữu. Cấu hình local primary giữ `Compact__WorkerEnabled=false`; vì vậy kế hoạch đã tạo không tự phát sinh công việc trên cấu hình này cho đến khi vận hành cấu hình worker. Không bật worker hoặc tạo chứng từ thử trên primary trong lượt này.

API Project 2 đã khởi động lại trên 5768 (PID 31524); Vite 5186 (PID 6424, parent 9324) giữ lại. API fixture được dừng trong finally; database fixture/backup và artifacts giữ lại để kiểm tra. Không thay đổi các server Demo/Project 1.
