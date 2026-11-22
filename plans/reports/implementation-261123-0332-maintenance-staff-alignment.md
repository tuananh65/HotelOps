# Bàn giao Maintenance Staff — Project 2 / CompactDB

Đã triển khai các chức năng được quyền hiện hành cho phép, giữ UX compact đã chấp nhận. Luồng assignment → accept → start/progress/evidence → complete → review → rework/review chạy bằng API và SQL thật trên fixture độc lập. Ba phần chưa thể cung cấp đúng yêu cầu đầy đủ là Decline, tạo Incident và tạo Internal Request; không nâng quyền hoặc phát minh lifecycle để vượt giới hạn.

## Trạng thái theo yêu cầu

| Hạng mục | Trạng thái | Kết quả / giới hạn |
|---|---|---|
| MAINTENANCE_STAFF_ROLE | VERIFIED | SQL chỉ đọc trên HotelOpsCompactDB: role_id 5, 13 grants thực tế; 5 tài khoản active mỗi DVBV/DVCT. ID chỉ ghi bằng chứng, không hardcode trong nghiệp vụ |
| SOURCE_AUDIT | VERIFIED | Bảng FEATURE/CURRENT_UI/API/PERMISSION/OWNERSHIP/WORKFLOW/GAP/ACTION trong plan; đã đọc Change Notes, SRS draft UC054/055, WorkRules, entities/mappings, services/API và UI liên quan; ưu tiên yêu cầu hiện tại, không dùng lifecycle Incident cũ trong SRS để tạo Decline giả |
| SIDEBAR_ALIGNMENT | IMPLEMENTED / VERIFIED | 7 nhóm, Lucide, từng mục theo grant; role profile chỉ chọn cách trình bày, không thay backend authorization; tab phụ MR/IR/Operational và notifications/history theo quyền |
| PERSONAL_DASHBOARD | IMPLEMENTED / VERIFIED | 10 KPI cá nhân có dữ liệu thật và số ca hôm nay; việc ưu tiên, ca thực tế, rework/history/inbox. Tách submitted/completed, chờ review và accepted; không dùng số toàn branch làm KPI cá nhân |
| MAINTENANCE_TASKS | IMPLEMENTED / VERIFIED | Assignment cá nhân, search/sort/pagination, trạng thái/ngày/priority/queue, overdue/review/rework/scheduled; drawer có room/floor/asset, người giao, thời gian, kết quả/review, evidence và MR nguồn |
| TASK_ACCEPT | VERIFIED | PENDING + assignment ACTIVE → accept → start; chưa nhận không start; retry không tạo event thứ hai, phiên bản cũ bị từ chối |
| TASK_DECLINE | BLOCKED | Không có transition/API Decline trong WorkRules hiện hành. Request decline trả 409, giữ assignment; không có nút giả, enum hay lịch sử assignment bị xóa. Cần thống nhất contract nếu muốn bổ sung chức năng này |
| TASK_EXECUTION | IMPLEMENTED / VERIFIED | Accept/start/pause/resume/progress dùng modal/API thật; timer không chồng nhau; tiến độ 0–100; ghi chú, attachment PNG/JPEG/PDF theo API; backend kiểm tra assignee trước ghi |
| TASK_COMPLETION | IMPLEMENTED / VERIFIED | IN_PROGRESS → COMPLETED, đóng timer và gửi chờ Lead review; nhãn hiển thị Chờ nghiệm thu, không coi đã accepted; không tự đóng Incident hoặc đổi room/asset/inventory |
| MAINTENANCE_REVIEW_RESULT | IMPLEMENTED / VERIFIED | Drawer hiển thị completion note, review reason/actor/time và lịch sử; staff không verify/reopen; người nghiệm thu độc lập theo workflow hiện hữu |
| REWORK_EXECUTION | IMPLEMENTED / VERIFIED | Lead reopen → PENDING theo contract, giữ reason và lịch sử. Assignment ACTIVE cho phép staff tiếp tục thực hiện; reopen từ CLOSED cần được giao lại. Không có entity hoặc status Rework song song |
| INCIDENT_REPORTING | PARTIAL / BLOCKED | INCIDENT_VIEW và INCIDENT_MANAGE thực tế cho xem/thực thi Incident được giao. Không có INCIDENT_CREATE nên tạo mới bị 403 và không có nút tạo. Triage/escalation/coordination không được mở rộng |
| ASSET_VIEW | VERIFIED | Dùng drawer/chức năng asset read-only hiện có; chi tiết, vị trí, Incident/Maintenance/PM liên quan theo quyền; move/condition/retire bị từ chối |
| MATERIAL_REQUESTS | IMPLEMENTED / VERIFIED | MR_CREATE/MR_VIEW: tạo theo EMPLOYMENT, gắn công việc bảo trì hợp lệ, thêm vật tư, gửi duyệt, xem trạng thái và issued/missing theo view fulfillment thật; owner-only edits, retry key ổn định; không approve/fulfill/post kho |
| INTERNAL_REQUESTS | PARTIAL / VERIFIED | IR_VIEW/IR_PROCESS: xem và xử lý khi được giao, route/assign cần thẩm quyền bộ phận. Không có IR_CREATE nên tạo mới bị chặn; TASK_VIEW/TASK_EXECUTE được đưa vào tab công việc khác, giữ lifecycle riêng |
| PERSONAL_WORKDAY | VERIFIED | Dùng Workday chung: OWN xem/start/end; chặn phân ca, thao tác ca người khác, start tương lai và end trước start. Timezone lấy từ chi nhánh, không suy đoán đang trong ca từ ACTIVE |
| NOTIFICATIONS | IMPLEMENTED / VERIFIED | Bổ sung thông báo verify/reopen tới assignee thực tế hoặc assignee gần nhất; recipient-only read, liên kết domain theo quyền; peer không đọc được thông báo của nhân viên |
| PERSONAL_HISTORY | IMPLEMENTED / VERIFIED | historyMine lấy assignment của actor, kể cả đã được chuyển cho người khác; giữ event/review/time, không cấp AUDIT_VIEW. Lịch sử ghi Bắt đầu công việc, không nhầm Bắt đầu ca |
| BACKEND_AUTHORIZATION | IMPLEMENTED / VERIFIED | Incident coordination và candidates yêu cầu INCIDENT_TRIAGE ngoài quyền MANAGE hiện có; Maintenance handover/candidates cần MAINTENANCE_ASSIGN; IR điều phối không dùng assignee làm authority. Không đổi grants, không role-ID hardcoding |
| ASSIGNMENT_ENFORCEMENT | IMPLEMENTED / VERIFIED | Từ chối unassigned/peer/foreign execution; không tự đổi assignee. Maintenance evidence không cho creator bỏ qua active assignment; MR nguồn bảo trì kiểm tra quyền sở hữu/assignee/điều phối; đọc theo BRANCH vẫn được phép |
| BRANCH_SCOPE | VERIFIED | Hai Staff A/B trên fixture phản chiếu scope DVBV/DVCT; work, assets, rooms, options, personal stats, Workday, documents, attachments và mutations chặn branch kia |
| CONCURRENCY | VERIFIED | RowVersion cũ/retry/state không hợp lệ bị 409; một timer đang mở; MR create operation key chống tạo trùng; evidence append-only kiểm tra lại owner/lifecycle trong transaction. UI pending, lỗi trong modal, refresh sau mutation; không success giả |
| FRONTEND_BUILD | VERIFIED | Typecheck, npm run build và targeted Prettier passed; 1633 modules; không có warning build |
| BACKEND_BUILD | VERIFIED | dotnet build HotelOps.slnx: 0 warnings, 0 errors |
| TEST_RESULTS | VERIFIED / NOT_TESTED | 27 nhóm Staff, 128 API regression, 33 Lead, PM worker 1 pass; browser Staff 30 views + 10 browser mutations 200 + Lead reopen qua fixture API, không page error/API failure; .NET 28 pass/3 conditional skip; FE workflow 5 pass. Chi tiết bên dưới; không ghi thử lên primary |
| FILES_CHANGED | IMPLEMENTED | Danh sách chính xác của lượt Maintenance Staff bên dưới; không quy toàn bộ dirty baseline thành thay đổi của lượt này |
| REMAINING_BLOCKERS | BLOCKED / PARTIAL | Decline thiếu contract; INCIDENT_CREATE và IR_CREATE không có trong grants. Material consumption không có execution field riêng nên không giả số liệu; chỉ theo dõi MR và fulfillment được hỗ trợ. Không thêm schema/permission |
| FINAL_VERDICT | IMPLEMENTED / VERIFIED trong contract hiện hành; PARTIAL so với toàn bộ đề xuất | Các luồng được quyền cho phép chạy end-to-end trên fixture. Không tuyên bố đã triển khai Decline hoặc tạo Incident/IR. Không kiểm chứng bằng ghi nghiệp vụ primary |

## Quyền thực tế đã đối chiếu

ATTENDANCE_RECORD — OWN. Các quyền BRANCH: ASSET_VIEW, INCIDENT_MANAGE, INCIDENT_VIEW, IR_PROCESS, IR_VIEW, MAINTENANCE_EXECUTE, MAINTENANCE_VIEW, MR_CREATE, MR_VIEW, ROOM_VIEW, TASK_EXECUTE, TASK_VIEW.

Không có INCIDENT_CREATE, INCIDENT_TRIAGE, INCIDENT_ESCALATION_HANDLE, IR_CREATE, MAINTENANCE_ASSIGN, MAINTENANCE_REVIEW, SHIFT_MANAGE, ASSET_MANAGE, stock posting hoặc quản trị User/Role. Không chỉnh role_permissions trên primary.

## Bằng chứng kiểm thử

- `scripts/database/compact-v2/Test-Backend.ps1 -MaintenanceStaff -Port 5187`: **27/27** nhóm assertion, fixture cuối `HotelOpsCompactApiTest4eabd21e611e`. Mỗi nhóm có nhiều HTTP/SQL assertions; không gọi đây là 30 test cases đã pass vì Decline là blocker. Timestamp nằm trong `artifacts/compact-v2/backend/maintenance-staff-verification.json`.
- `frontend/tests/compact-maintenance-staff-live.mjs`: **30 route/viewport** tại 1440/900/375, 10 browser writes trả 200; một thao tác Lead reopen độc lập qua fixture API để xác minh UI rework; 0 page errors, 0 failed API. Screenshot dashboard/rework đã xem trực tiếp. Proxy toàn bộ browser API sang fixture, không gửi mutation primary. JSON: `artifacts/maintenance-staff/live-verification.json`.
- `Test-Backend.ps1 -Extended -Port 5188`: **128/128**, fixture `HotelOpsCompactApiTest89b50dabfcf8`; schema giữ 40 tables/160 FKs. JSON: `artifacts/compact-v2/backend/verification.json`.
- Regression Lead: **33/33**, fixture `HotelOpsCompactApiTest70ae263a142b`, **27 views** và assignment/review/PM mutations thật. Existing MaintenanceWorker integration **1/1** qua hai ticks không phát sinh trùng; TRX có warning ghi đè kết quả file cũ, không phải build warning hay test failure.
- `dotnet test HotelOps.slnx --no-build`: **28 pass / 3 skip**. Các bài conditional skip: MaintenanceWorker, recurrence/low-stock Worker, account/email integration. MaintenanceWorker đã chạy riêng trong Lead fixture; hai bài còn lại không được tính là pass trong lượt này.
- `npm run test:compact`: **5/5**. `npm run test:compact:ux`: **48 route/viewport** và settings/Reception/Workday/dashboard-account suites, API mock. Regression riêng HK Staff **27**, OM **24**, Department Head **21**, Storekeeper **27** responsive views đều pass (mock). Không dùng UI mock làm bằng chứng SQL.
- Không chạy load test, toàn bộ browser engine hoặc email/account integration. Không chạy lại Staffing V3/V4, không deploy/push/commit.
- `git diff --check` không báo lỗi whitespace; có cảnh báo LF → CRLF cho package-lock.json, package.json và main.tsx đã dirty từ baseline. Không sửa các file ngoài phạm vi để dẹp cảnh báo này. Kiểm tra Git này không bao phủ nội dung untracked; các file Project 2 được rà soát trực tiếp và qua build/test/format.

## Các file tạo/sửa trong lượt này

Backend sửa:

- `src/HotelOps.Api/Compact/Services/WorkService.cs`
- `src/HotelOps.Api/Compact/Services/InsightService.Personal.cs`
- `src/HotelOps.Api/Compact/Services/DocumentService.cs`
- `src/HotelOps.Api/Compact/Services/EvidenceService.cs`
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`

Frontend sửa/tạo:

- `frontend/src/compact/app.tsx` — sửa.
- `frontend/src/compact/work.tsx` — sửa.
- `frontend/src/compact/documents.tsx` — sửa.
- `frontend/src/compact/maintenance-personal.tsx` — tạo.

Tests sửa/tạo:

- `scripts/database/compact-v2/Test-Backend.ps1` — thêm lựa chọn MaintenanceStaff.
- `scripts/database/compact-v2/maintenance-staff-fixture.sql` — tạo, chỉ cho DB test theo pattern được kiểm tra.
- `scripts/database/compact-v2/test-maintenance-staff.ps1` — tạo.
- `frontend/tests/compact-maintenance-staff-live.mjs` — tạo.

Docs:

- `docs/ui/compact-ux.md` — cập nhật mục Maintenance Staff.
- `plans/261123-0332-maintenance-staff-alignment/plan.md` — plan và audit.
- Báo cáo này và `plans/journals/2026-11-23-maintenance-staff-role-alignment.md`.

Generated entities, DbContext, schema, grants, application secrets và Project 1 không thay đổi. Nhiều thư mục Project 2 đã untracked trước lượt này; không stage/reset hoặc quy các thay đổi có sẵn là công việc mới.

## Trạng thái môi trường

API Project 2 đã khôi phục tại 127.0.0.1:5768, database HotelOpsCompactDB, worker=false, health schema 2.1.0 / 40 tables. Vite hiện có ở 5186 được giữ. API fixture dừng trong finally; DB fixture và backup được giữ làm bằng chứng, không tự xóa. Không tác động các process Demo/Project 1 của người dùng.
