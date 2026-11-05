# Task76 — Eligibility review; implementation blocked

Ngày: 03/11/2026. DEV1, branch `feature/BE1-database-integration`, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`.

## 1. Kết quả và quyết định phạm vi

**Chưa đạt mục tiêu thêm 4–5 UC hoàn tất. Không chọn UC làm completion target và không bắt đầu implementation.** Đối chiếu 75 hàng ledger, yêu cầu SRS và source/schema hiện có không xác định được UC chưa hoàn tất có thể đạt full acceptance trong phạm vi thay đổi đã duyệt. Không mở thêm GET-only/accept-only flow để tăng số PARTIAL.

Task76 chỉ cập nhật tài liệu, ledger kế thừa và kiểm tra baseline. Không phục hồi thêm file, sửa runtime/shared component/DEV2, hoặc kết nối SQL. Giữ 16 file đã phục hồi ở Task75 và 210 manifest paths deferred. Các test SQL/HTTPS mới cho UC nghiệp vụ **NOT RUN**, không phải PASS hoặc FAIL.

Nguồn đối chiếu:

- [AGENTS.md](../../../AGENTS.md), [SRS reading guide](../../../docs/requirements/SRS_README.md), [Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md).
- [Đặc tả UC](../../../docs/requirements/srs_sections/02_use_case_specifications.md), [Business rules/state catalog](../../../docs/requirements/srs_sections/05_requirement_appendix.md), [Permission Matrix draft §4.4](../../../docs/requirements/srs_sections/01_overall_requirements.md).
- [Catalog SQL thực tế](../../database/release/03_HotelOps_RBAC_Catalog_Seed.sql), [schema SQL](../../database/release/01_HotelOps_Full_Schema.sql), [DbContext](../../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), entities và services liên quan.
- [Ledger review lịch sử](../Task75Review/UC_LEDGER_RECONCILED.json), [ledger Task75](../CleaningMonitor/UC_LEDGER_TASK75.json), [Task75 implementation/evidence](../CleaningMonitor/README.md), [handoff Task75](../CleaningMonitor/DEV2_HANDOFF.md).

Không dùng Permission Matrix draft để mở quyền mới. Catalog hiện có là evidence về grants đã triển khai; việc catalog có permission không tự giải quyết model, workflow hoặc delivery contract.

## 2. Các ứng viên được yêu cầu và phương án thay thế

Các số dòng dưới đây thuộc file đặc tả UC. Full-flow acceptance bao gồm alternative flow bắt buộc; không chỉ happy path.

| UC và tên thật trong SRS | Baseline | Quyết định / bằng chứng chặn full completion |
|---|---|---|
| UC-022 Deactivate Room | NOT_IMPLEMENTED | §2.11, dòng1868: open cleaning/rework/inspection/incident/effective-block/assets, Inactive, Reactivate → Pending Cleaning, status history/audit. IncidentService.ValidateCreateAsync dòng325–327 chỉ kiểm tra room/branch, không kiểm tra IsActive. DEV1 không thể bảo đảm phòng inactive không nhận incident mới bằng endpoint deactivate riêng. Schema CHECK rooms dòng3459 không có INACTIVE; chưa có approved stored/projected-state contract và writer protocol. |
| UC-030 Reassign Cleaning Task | NOT_IMPLEMENTED | §4.3, dòng2623, steps5/7/9: đóng interval của assignee cũ, tính room status, notification cả hai assignee. Thiếu approved WorkInterval integration và notification adapter. Không làm lại assignment core Task73. |
| UC-031 Start Cleaning Task | NOT_IMPLEMENTED | §4.4, dòng2706, steps4–7 và Resume: Working Workday, một open interval/user, mở interval, đóng blocker và tính room status. DbContext/entities hiện không có Workday/WorkInterval. ActualStartAt không thay thế interval. |
| UC-032 Update Cleaning Progress | COMPLETE_VERIFIED_V1 | Đã hoàn tất Task74; loại vì trùng UC, không cộng lại. Historical receipt không phải acceptance chạy mới trên working tree hiện tại. |
| UC-033 Report Cleaning Blocker | NOT_IMPLEMENTED | §4.6, dòng2883: blocker reference/photo, close interval, On Hold, room status, notification; alternatives link Incident/Material Request. BlockerNote đơn lẻ không đủ flow. |
| UC-034 Complete Cleaning Task | NOT_IMPLEMENTED | §4.7, dòng2976: mandatory items/evidence, Pending Inspection, đóng interval, room status, notify Supervisor. Không đổi COMPLETED để thay Pending Inspection. |
| UC-035 Receive Rework Task | NOT_IMPLEMENTED | §4.8, dòng3064: Accept **và Start**, Working Workday, open interval; không thể tính complete bằng Accept-only. |
| UC-036 Complete Cleaning Rework | NOT_IMPLEMENTED | §4.9, dòng3147: close interval, Pending Reinspection, room status và notification. |
| UC-047 Reinspect Room | NOT_IMPLEMENTED | §5.9, dòng4079: current-cycle result/defects, rework/cleaning/room transitions và notification; không chỉ checklist read/write. |
| UC-103 Set Task Priority and Deadline | NOT_IMPLEMENTED | §11.2, dòng9014: expected duration/deadline history, Cancel kiểm tra recorded work time, Stop Series và notify assignee. OperationalTask không có series/interval contract. |
| UC-107 Accept Assigned Task | NOT_IMPLEMENTED | §11.6, dòng9355: Assigned→Accepted **và Decline→Pending Assignment/unassign/notify DH**. CHECK operational_tasks dòng3427 chỉ TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED; không tự map TODO thành nhiều trạng thái SRS. |
| UC-138 Manage Operational Workday and Shift Records | NOT_IMPLEMENTED | §12.2, dòng9928: shift/template/assignment/conflicts/cancel và notify affected employees (step6). Có bảng Shift không chứng minh delivery/retry đã tồn tại. |
| UC-114 Create Internal Request | NOT_IMPLEMENTED | §13.1, dòng10208: Pending Acceptance và notify receiving DH. CHECK internal_requests dòng3379 không có PENDING_ACCEPTANCE; chưa có approved mapping/delivery adapter. |
| UC-115 Route and Assign Internal Request | NOT_IMPLEMENTED | §13.2, dòng10293: route/assign authoritative handler, notify requester/handler/new DH; dùng chung missing IR state/delivery contract. |
| UC-116 Accept/Reject Internal Request | NOT_IMPLEMENTED | §13.3, dòng10383: Pending Acceptance→Accepted/Rejected, response time/deadline/reason và notify requester. Thiếu approved Accepted state/response model và delivery contract. |
| UC-117 Update Internal Request Progress | NOT_IMPLEMENTED | §13.4, dòng10467: progress/evidence/deadline history và notification khi100%; không bỏ nhánh100% để tính complete. |
| UC-123 View Internal Request History | NOT_IMPLEMENTED | §13.10, dòng10965: timeline/authorized linked work **và Withdraw** Pending Acceptance→Cancelled/reason/audit/notify DH. GET-only không hoàn tất UC. |

Đây là eligibility rejection, **không phải failed acceptance test**. Không tạo endpoint mới rồi báo business acceptance PASS bằng security runner.

## 3. Những UC còn lại

[UC_LEDGER_TASK76.json](UC_LEDGER_TASK76.json) giữ nguyên cả75 classifications/weights/evidence gốc, thêm quyết định eligibility và SRS anchor từng UC. Tất cả49 UC chưa hoàn tất đã được đối chiếu với residual scope; không đổi NOT_IMPLEMENTED thành PARTIAL/BLOCKED chỉ vì lượt review này.

- UC004: SRS §1.4 yêu cầu selected role(s)/scope; source Create chỉ single-role allowlist Task30, delegation envelope Task44 chỉ5 role. Full multi-role/global-role policy chưa được duyệt; không mở rộng allowlist từ draft.
- UC006/007/010: deactivation, removed scope/transfer/revoke/role lifecycle cần current-responsibility và retained writer guards liên module. Không bật Deactivate hoặc bỏ gates.
- UC008/009: dynamic role/grant editor mâu thuẫn fixed catalog contract; không đổi Permission Matrix.
- UC012/016/026: dependency checks và writer coordination cho branch/location/department deactivation chưa đủ; metadata update không hoàn tất alternative deactivation.
- UC027/028: occupancy/cleaning-cycle và Reception/room lifecycle model/contract chưa chốt.
- UC029/041/046: giữ PARTIAL; core có ở snapshot `cf8fb02`, delivery/retry chưa đủ. Snapshot này có partial `RoomTypeCreationService` phụ thuộc các file/schema/DI chưa phục hồi; không phải ba standalone services có thể copy và đăng ký riêng.
- UC044/045: giữ **NOT_IMPLEMENTED** đúng ledger. Câu “keep PARTIAL unless…” trong request không chứng minh có implementation để nâng trạng thái lên PARTIAL. Approval/rejection cần room/inspection/cleaning/defect/rework + notification, không làm giả delivery.
- UC037: giữ PARTIAL, GET Task75 nguyên trạng; Cancel cần reason/eligible states/atomic lifecycle/audit/assignee notification.
- UC102/104/105/106/108/110/111/112/113: recurrence, interval/time correction, verification/reopen cycle hoặc approved status mapping còn thiếu. Schema/entity scaffolding không là workflow.
- UC139/140: Workday/Pause All/acknowledged handover và các writer/permission contracts chưa có.
- UC118/119/120/121/122: mandatory linked work, acknowledged handover, close/reopen/escalation acknowledgment liên module chưa đủ contract/model.

GBR010 yêu cầu operation vẫn được giữ khi notification delivery thất bại và retry về sau, đồng thời at-most-one notification/event/recipient. `Notification`/`NotificationRecipient` và unique(notification_id,user_id,channel) không tự cung cấp producer event idempotency hoặc durable retry intent. Tìm source không thấy notification adapter/retry hosted worker hay Workday/WorkInterval implementation. Không tự triển khai FE14 thuộc DEV2.

## 4. Implementation / recovery

- Runtime files created/modified/restored in Task76: **0**.
- Shared/DEV2/schema/DI/RBAC changes: **0**.
- 16 Task75 recovery paths preserved; 210 deferred paths không phục hồi thêm.
- UC037 GET source, approved DI line, fallback/OpenAPI/health/WeatherForecast cleanup và mọi patch khác được kiểm SHA-256 theo preflight.
- HEAD/index/four worktrees được kiểm lại; không Git mutation hoặc SQL connection trong Task76.

## 5. Validation thực thi trong Task76

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS;0 errors,2 NU1900 warning entries | Không truy cập được NuGet vulnerability feed; không xác nhận package vulnerability scan thành công |
| Authorization `--production-fallback`, `--no-build` | PASS93 checks | Existing executable; loopback HTTP, real cookie/middleware, session/data doubles; không phải SQL business acceptance |
| PermissionService `--no-build` | PASS43 checks | Isolated decision/DI doubles, không `--live` |
| CurrentUserAccessor `--no-build` | PASS14 checks | Isolated identity/DI checks |
| ResourceAuthorization `--no-build` | PASS49 checks | Synthetic cookie/session/data with production authorization components; không `--live` |
| `dotnet test HotelOps.slnx --no-restore` | Exit0, không test output/cases | Framework tests NOT AVAILABLE trong API-only solution; không ghi PASS |
| New-UC real HTTPS/SQL/persistence/race/rollback/audit acceptance | NOT RUN | Không chọn/implement UC mới; không tạo trial DB chỉ để chạy lại120 checks Task75 |
| UC037120 checks Task75 | Historical evidence only | Không rerun và không nhận thành test mới Task76 |
| `git diff --check`, preservation, elapsed | Xem TASK76_VALIDATION.json | Kiểm tra cuối sau khi ghi report/ledger |

Logs hiện tại: `obj/task76-build.local.log`, `obj/task76-*-regression.local.log`, `obj/task76-framework-tests.local.log` (ignored local evidence). Các no-build runners là regression trên executables đã có, không tuyên bố đã rebuild mọi harness trong Task76. Không có C# change ở Task76.

Cached branch/worktree evidence đã kiểm tra, gồm `integration/dev1-fe-handoff=cf8fb02` và `origin/FE+BE/integration/dev1-fe-handoff=0006f47`. Không fetch nên không khẳng định DEV2 chưa có contract mới bên ngoài các refs/source đã kiểm tra. Snapshot cf8fb02 cũng không có Workday/WorkInterval hay notification adapter theo các tên/code search liên quan. Worktree rehearsal vẫn có103 staged entries có sẵn; hai worktrees phụ còn lại clean.

## 6. Tiến độ và bước tiếp theo

**26/75 complete;60/256 points=23.4375%. Delta0UC/0point. Còn49UC/196points.** Taxonomy giữ26 complete (7 COMPLETE_VERIFIED +19 COMPLETE_VERIFIED_V1),10 PARTIAL,18 NOT_IMPLEMENTED,21 BLOCKED_EXTERNAL. Không đổi mẫu số hoặc cộng partial credit.

Trạng thái Task76: **BLOCKED_BEFORE_IMPLEMENTATION**, mục tiêu4–5 UC **NOT ACHIEVED**. Đây không phải báo cáo Task76 implementation completed. Thời gian preflight→final nằm trong [TASK76_VALIDATION.json](TASK76_VALIDATION.json).

Thời gian được đo từ lúc bắt đầu ghi preflight snapshot (`2026-11-03T08:07:43.3170949Z`); lần đọc attachment/Git/AGENTS đầu tiên diễn ra trước mốc này và không có clock receipt chính xác. Không cộng một thời lượng ước lượng vào elapsed đã đo.

Next batch đề xuất có điều kiện: **UC029 Assign Cleaning Task, UC041 Assign Room Inspection, UC046 Request Cleaning Rework, UC037 Monitor Cleaning Tasks** — tổng13 điểm chưa earned. Chỉ chọn làm completion target sau khi đạt các entry criteria sau:

1. DEV2 xác nhận adapter/storage/delivery/retry/idempotency/failure contract theo [handoff Task76](DEV2_HANDOFF.md); có disposable-SQL evidence cho retry/restart/duplicate/rollback.
2. Review exact dependency closure của Task73/74 baseline: partial RoomTypeCreationService, associated controllers/helpers, mappings/schema gates và DI. Tách file missing nonshared khỏi shared diff; trình patch shared cụ thể để duyệt, không bulk210 paths.
3. Với UC037 Cancel: chốt stored/projected lifecycle/effective-block và writer lock protocol; approved patch riêng cho shared/DEV2 changes nếu cần. Không cần kéo Workday/WorkInterval vào nhánh cancel-before-start để giả hoàn tất các execution UCs.
4. Restore/integrate phạm vi đã duyệt, rồi chạy real HTTPS/SQL acceptance đầy đủ từng UC + affected regression. Nếu một entry criterion chưa đạt, batch này vẫn là **conditional**, không báo đã có4 UC độc lập sẵn làm.

Không xin blanket approval cho Program.cs/DbContext/schema/DEV2. Handoff là tài liệu review local; chưa gửi cho người khác. Những contract thiếu không thể được thay thế bằng một dòng DI hoặc permission approval chung.
