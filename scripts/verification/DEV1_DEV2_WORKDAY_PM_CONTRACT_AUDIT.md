# DEV1/DEV2 — Workday / Preventive Maintenance contract audit

Ngày audit: 2026-10-31 (Asia/Saigon). **Source-only; Workday/WorkInterval: NOT IMPLEMENTED / NOT VERIFIED. PM migration freeze: HOLD đối với contract cuối cùng và execution readiness.**

Snapshot: branch `feature/BE1-database-integration`, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Bốn worktree giữ nguyên. Audit đọc working tree, gồm thay đổi chưa commit; không coi chúng là integration baseline đã merge. Chỉ tạo báo cáo này; không chạy migration, kết nối SQL/HotelOpsDB, sửa source hoặc thực hiện Git mutation.

## A. Workday contract status

| Câu hỏi | Kết luận và evidence |
| --- | --- |
| Workday implemented? | **NOT IMPLEMENTED** trong source được audit. Không có entity/DbSet/controller/service/DTO Workday. [Delivery plan](DEV1_21_DAY_DELIVERY_PLAN.md), dòng 46, 112, 115–116: UC111/139 blocked, UC138 chưa implement. |
| WorkInterval implemented? | **NOT IMPLEMENTED**. Không tìm thấy model, migration hoặc API interval trong `src/HotelOps.Api` và `scripts/database`. Cùng plan dòng 130 cấm dùng ShiftAttendance thay interval. |
| Tables available? | SQL schema repository có Shift/Attendance, không có bảng Workday/WorkInterval/TimeCorrectionRequest. [HotelOpsDbContext](../../src/HotelOps.Api/Data/HotelOpsDbContext.cs): `ShiftAttendances` dòng 123, `WorkShifts` dòng 157, mapping `ShiftAttendance` dòng 2413. **Live database existence: NOT VERIFIED**; không query SQL trong audit này. |
| Authoritative schema contract? | **Chưa có executable/approved schema contract Workday/WorkInterval tìm thấy trong repository.** Có business requirements; không đủ để chốt columns/FKs/API. [ApprovedDev1ResponsibilityQueries.ReadAsync](../../src/HotelOps.Api/Services/Users/ApprovedDev1ResponsibilityQueries.cs), dòng 27 giữ `WORKDAY_CONTRACT` trong unresolved; đây là gate marker, không phải contract implementation. `Unsupported` còn nêu `UNSCHEDULED_WORKDAY_MODEL`, `MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL`. |
| API/service entry point? | Không có Workday/Interval entry point. [Program](../../src/HotelOps.Api/Program.cs) đăng ký `IMaintenanceWorkService` dòng 32, không có time service. `MaintenanceWorkService.StartWorkAsync` là corrective transition, không phải Workday/Interval adapter. |
| Committed/planned/blocked? | Time workflow **planned/blocked**, không có implementation để gọi là committed. PM entities, main DbContext, full schema và `MaintenanceWorkService.Commands.cs` là tracked, không có status change tại snapshot; delivery plan và `StructureDeactivationDependencies.cs` untracked; `ApprovedDev1ResponsibilityQueries.cs` tracked-modified. Những evidence này phải được bàn giao cùng working-tree scope, không chỉ HEAD. |
| Ai enforce single open interval? | **Chưa có component thực thi được tìm thấy.** GBR-137 yêu cầu tối đa một interval mở toàn hệ thống cho mỗi user; không có schema uniqueness/locking/service tương ứng để tái sử dụng. |
| Ai quyết định Working workday? | Business authority là UC139; runtime authority **chưa implement**. Không suy ra Working từ lịch ca, trạng thái assignment, check-in hoặc `ActualStartAt` maintenance. |

Requirements: [SRS UC062](../../docs/requirements/srs_sections/02_use_case_specifications.md), dòng 5397 và flow 5438–5473 yêu cầu Working day, single interval, mở khi Start, đóng khi Complete; occurrence + interval + next due date + audit phải cùng transaction. [GBR136–140](../../docs/requirements/srs_sections/05_requirement_appendix.md), dòng 144–148 quy định server-time events, không nhập/backdate intervals, corrections có kiểm soát, effort từ closed intervals; shift duration không thay task time. UC139, dòng 10012–10093, quy định Working/Ended, unscheduled reason, không hai Working workdays, không End khi còn interval mở.

[ShiftAttendance.ShiftAssignmentId](../../src/HotelOps.Api/Entities/ShiftAttendance.cs) là `long` bắt buộc; không đại diện được unscheduled workday theo giả định. [REMAINING_BLOCKERS — D05](UserAccountDeactivation/REMAINING_BLOCKERS.md), dòng 45–49 ghi quyết định đã duyệt: Working day vẫn phải End trước deactivation dù không có interval; Ended không tự giải phóng assignments. Không đổi UC006 gate trong audit.

Source priority: [AGENTS](../../AGENTS.md), phần Requirements Documentation and Source Priority; [SRS_CHANGE_NOTES](../../docs/requirements/SRS_CHANGE_NOTES.md), dòng 10–12, 56–61, 82–86. Split SRS là business reference; không tuyên bố historical draft hoặc Permission Matrix pending là bản final đã ký.

## B. Existing schema and source entry points

| Component | Hiện trạng / symbol |
| --- | --- |
| PM schedule | [PreventiveMaintenanceSchedule](../../src/HotelOps.Api/Entities/PreventiveMaintenanceSchedule.cs): nullable `AssetId`, `RoomId`, `AreaId`; `Frequency`, `FrequencyInterval`, `NextDueAt`, `AssignedDepartmentId`, `IsActive`. Không có BranchId/default technician/check-item collection trong entity hiện tại. |
| PM run | [PreventiveMaintenanceRun](../../src/HotelOps.Api/Entities/PreventiveMaintenanceRun.cs): `ScheduleId`, nullable `MaintenanceWorkId`, `PlannedAt`, `PerformedAt`, `Result`, `NextDueAt`, `PerformedBy`. Không có occurrence status/assignee/rowversion/item-result model trong entity hiện tại. `PerformedBy` không tự đồng nghĩa current assignee. |
| SQL / mappings | [Full schema](../database/release/01_HotelOps_Full_Schema.sql), dòng 1058/1079: hai bảng PM; dòng 1975 unique `(schedule_id, planned_at)`; dòng 2930–2968 FKs. [DbContext](../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), dòng 2000–2085: `PreventiveMaintenanceRun`/`Schedule` mapping, cùng unique key và nullable link. |
| PM item tables | `preventive_maintenance_schedule_items` / `preventive_maintenance_run_items`: **NOT FOUND** trong SQL scripts, entities và mappings được tìm kiếm. Không có proposed PM migration artifact trong repository snapshot để duyệt DDL cụ thể. |
| Maintenance | [MaintenanceWork](../../src/HotelOps.Api/Entities/MaintenanceWork.cs): nullable `IncidentId`, `AssigneeId`, coarse `Status`, `ActualStartAt`, `ResolvedAt`, `ReviewedAt`, collection `PreventiveMaintenanceRuns`. FK nằm ở Run, không phải một RunId có sẵn trên Work. |
| HTTP | [MaintenanceWorksController](../../src/HotelOps.Api/Controllers/MaintenanceWorksController.cs): `POST /api/incidents/{incidentId}/maintenance-works`, `POST /api/maintenance-works/{id}/start`, assignment/hold/resume/submit/verify/return/cancel và queries. Đây không phải UC061/062 implementation. |
| Corrective service | [ExecuteCorrectiveAsync](../../src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Commands.cs): reject khi IncidentId null; lock Incident → Work, synchronize statuses, business/audit transaction. [StartWorkAsync / SubmitResultAsync](../../src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Workflow.cs), dòng 66–94 chỉ đổi work/incident/timestamps, không check/open/close interval. Không thể gọi trực tiếp cho PM incidentless run. |
| Existing consumer | [AssetService.GetMaintenanceHistoryAsync](../../src/HotelOps.Api/Services/Assets/AssetService.MaintenanceHistory.cs): corrective query loại Work có PM runs; preventive query từ `Run.Schedule.AssetId`, đọc nullable Work và `PerformedAt/PlannedAt`. Đổi target/link/time semantics phải regression consumer này. |

## C. Required DEV1 implementation / contract coordination

DEV1 phải cung cấp model + approved mapping/migration + SQL-backed API/service cho UC138/139/111 trước khi UC062 time acceptance có thể PASS. Contract cần chốt identity user/workday/work item; optional shift association cho unscheduled day; Working/Ended authority; one Working day/user; one open interval/user xuyên cleaning/rework/incident/maintenance/task; mở/đóng từ server-time transitions; corrections/overlap/old-new audit; End/Pause All và account-duty integration.

Đây là **requirements cho thiết kế tiếp theo**, không phải phê duyệt tên bảng/cột/signature mới. Cần thống nhất shared transaction participation để PM transition + interval + schedule + audit commit/rollback cùng nhau; không thay bằng HTTP callback hoặc event hậu commit rồi tuyên bố atomic. Lock order participant User → time/location/feature resources cần joint review với các writer hiện hữu; chưa có time lock protocol đã được chứng minh để copy.

## D. Safe DEV2 reuse and authorization

1. Reuse [HotelOpsDbContext](../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), existing PM keys/FKs và SQL-backed [IPermissionService](../../src/HotelOps.Api/Security/Authorization/IPermissionService.cs): `EvaluateBranchAsync` chỉ xác nhận scope với branch đã được backend xác lập; `EvaluateResourceAsync` đòi resolver xác minh resource/ownership.
2. Reuse [PermissionDataSource](../../src/HotelOps.Api/Security/Authorization/PermissionDataSource.cs) và [PermissionGrantRules.IsEligible/EffectiveScope](../../src/HotelOps.Api/Security/Authorization/PermissionGrantRules.cs): active role/assignment, assignment time/expiry, active branch/access, ALL/BRANCH/OWN semantics. Không tin branch/actor/ownership từ payload, không hard-code role bypass.
3. Corrective branch path hợp lệ về model: `MaintenanceWork.IncidentId → Incident.BranchId`. `ExecuteCorrectiveAsync` dùng branch đó cho audit; **audit branch không chứng minh authorization**. Controller/service đang đọc không tích hợp `IPermissionService` cho workflow này; `RequireUserAsync` chỉ kiểm tra user tồn tại. Không chứng nhận các route maintenance đã có resource-scope enforcement.
4. PM branch path cần implement: Work → `PreventiveMaintenanceRuns` → Schedule → Asset/Room/Area → BranchId. [Asset](../../src/HotelOps.Api/Entities/Asset.cs), [Room](../../src/HotelOps.Api/Entities/Room.cs), [Area](../../src/HotelOps.Api/Entities/Area.cs), [Incident](../../src/HotelOps.Api/Entities/Incident.cs) đều có BranchId. Schedule không có branch riêng; Work có collection runs, nên không được mặc định mapping 1:1. Reject absent/ambiguous targets, branches không đồng nhất hoặc department/default assignee sai branch; không dùng first target tùy ý. Đây là cần chốt contract, chưa có resolver chứng minh.
5. [Program](../../src/HotelOps.Api/Program.cs), dòng 73 đăng ký `CleaningTaskResourceResolver`; [resolver](../../src/HotelOps.Api/Security/Authorization/Resources/CleaningTaskResourceResolver.cs) chỉ giải cleaning resource. **Không có maintenance/PM resource resolver được tìm thấy**. OWN cần current assignee từ SQL, không suy từ `PerformedBy` hay creator. Branch-only check không đủ thay OWN.
6. [RBAC seed](../database/release/03_HotelOps_RBAC_Catalog_Seed.sql), dòng 59–62, 153–169 có `MAINTENANCE_VIEW`, `MAINTENANCE_EXECUTE`, `PM_MANAGE` và scope grants. Đây là catalog source, không chứng minh live grants. `SHIFT_MANAGE`/`ATTENDANCE_RECORD` dòng 80–81 không tự cấp quyền interval/correction; SRS `work_interval.correct` cần mapping được duyệt. Không tạo permission mới trong audit.

DEV2 tránh duplicate Workday/WorkInterval riêng cho maintenance, fake Working từ attendance, tính effort từ ActualStartAt/log minutes, hoặc tạo interval ngoài transaction. Reuse time adapter chỉ sau khi DEV1 contract tồn tại và được kiểm chứng; hiện **không có adapter để reuse**.

## E. PM migration compatibility risks

| Risk | Evidence / điều kiện trước freeze |
| --- | --- |
| UC016 misses PM-only work | [StructureDeactivationDependencies.ReadAsync](../../src/HotelOps.Api/Services/HotelStructure/StructureDeactivationDependencies.cs) chỉ có `OPEN_MAINTENANCE_WORK` qua nonnull IncidentId trong incident subtree. Incidentless PM Run/Schedule không được dò. Phải phối hợp active schedules/open occurrences, target qua asset current placement/room/area, terminal semantics và writer serialization. Không tự thêm business blocker chỉ từ table existence. |
| Existing writer races | [Task66 report](HotelStructure/TASK66_UC016_IMPLEMENTATION.md), dòng 67–100 ghi actual SQL defect evidence lịch sử. Source hiện [IncidentService.ValidateCreateAsync](../../src/HotelOps.Api/Services/Incidents/IncidentService.cs), dòng 228/232/319–330 vẫn validate location trước transaction, chỉ existence+branch, không active ancestry. [AssetService.MoveAsync](../../src/HotelOps.Api/Services/Assets/AssetService.Location.cs), dòng 21–64 lock Asset rồi active destination read thông thường. Không tìm thấy retained common structure protocol trong các path này; audit không rerun race. PM writer/incident-from-occurrence không được lặp lại lỗ hổng. |
| UC016 mutation remains gated | Task66 classification **PARTIAL**, actual deactivation mutation blocked; dependency queries không phải accepted write protocol. Reuse đề xuất User → Branch/location ancestry → Asset/feature chỉ sau joint lock-order review và hai chiều race/rollback acceptance, không tuyên bố đã fix. |
| User responsibility predicates | [DeactivationResponsibilityQueries.ReadApprovedDev2BlockersAsync](../../src/HotelOps.Api/Services/Users/DeactivationResponsibilityQueries.cs), dòng 17: assignee blocks PENDING/IN_PROGRESS/ON_HOLD. [Dev2ResponsibilityStateQueries.ReadUnresolvedAsync](../../src/HotelOps.Api/Services/Users/Dev2ResponsibilityStateQueries.cs), dòng 18–39 phân loại vocabulary PENDING/IN_PROGRESS/ON_HOLD/COMPLETED/CLOSED/CANCELLED và completion markers. Thêm ASSIGNED/PENDING_ASSIGNMENT trực tiếp vào MaintenanceWork hoặc lưu assignee chỉ trên Run có thể tạo unresolved hoặc bỏ sót duty. Cần explicit adapter/predicate proposal; không đổi UC006 release gate. |
| Cardinality / FK | Run→Work nullable, không có unique Work link trong mapping đã đọc; giữ existing unique Schedule+PlannedAt để chống generate trùng JOB04. Không đảo FK hoặc áp 1:1/destructive backfill nếu chưa kiểm tra legacy. Item FK/index/status constraints và incident-occurrence link chưa có artifact để xác nhận. |
| Legacy/additive behavior | Cột NOT NULL mới cần migration strategy; trigger có thể ảnh hưởng EF SQL OUTPUT. Không coi additive tự động tương thích. Phải review defaults/backfill, unknown legacy states, EF mapping và fresh/upgrade/idempotent/rollback trên trial. Không thực thi trong audit. |
| Shared partial mapping | [HotelOpsDbContext.RoomTypes](../../src/HotelOps.Api/Data/HotelOpsDbContext.RoomTypes.cs), dòng 10–12 đã implement `OnModelCreatingPartial` và gọi `ConfigureChecklists`; [Checklists mapping](../../src/HotelOps.Api/Data/HotelOpsDbContext.Checklists.cs) là helper. Không thêm implementation partial thứ hai hoặc overwrite shared mapping khi thêm PM. |

Không có proposed PM DDL cụ thể được tìm thấy, nên kết luận này là dependency audit, **không phải approval tương thích của một migration cụ thể**. UC061 schedule CRUD có thể phát triển độc lập với time execution, nhưng target/branch/assignment/schema approval vẫn bắt buộc. UC062 bắt buộc time dependency; không bỏ acceptance để freeze.

## F. Shared ownership boundaries

[AGENTS](../../AGENTS.md), Backend Module Ownership / Collaboration: DEV1 sở hữu FE12 Work-Time, FE02 structure, FE03 room, FE04/05/11; DEV2 sở hữu FE06 PM/incident, FE07 asset, FE14 notifications và FE15 reporting/audit. DbContext, schema, auth infrastructure/config là shared, cần impact explanation và approval trước thay đổi. DEV2 quản PM lifecycle/generation/items/incident linkage; DEV1 quản time authority và DEV1 dependency adapters; hai bên chốt transaction/locking/branch contract. Audit không cấp quyền sửa module bên kia hoặc triển khai shared database.

## G. Coordination blockers

1. Chưa có approved executable time schema/service/permission mapping và acceptance: **BLOCKED cho UC062 time readiness**.
2. Chưa có PM target ownership/cardinality/current assignee authorization resolver; cần contract ambiguity/cross-branch rejection rõ ràng.
3. Chưa có proposed migration artifact để kiểm tra DDL, compatibility, fresh/upgrade/rollback; cần DEV2 cung cấp diff/migration chính xác.
4. PM-only dependency coverage, lifecycle vocabulary và common location writer locking chưa được jointly chốt; UC016 vẫn PARTIAL.
5. UC062 generation/assignment notification cũng cần FE14 delivery/retry acceptance theo UC062 và notification flow SRS dòng 11094; enqueue không tự chứng minh delivery.

## H. Recommended freeze decision and validation

**HOLD final UC061/062 shared contract/migration freeze.** Có thể chuẩn bị additive PM-only draft để review; chưa được coi frozen/approved/deployable qua báo cáo này. Chỉ freeze sau khi hai bên duyệt DDL cụ thể, legacy compatibility, branch/assignee resolution, state/predicate adapters, time transaction integration và location writer protocol. Nếu tách một schema-only milestone khỏi UC062 execution, ghi rõ time contract còn BLOCKED; không tuyên bố UC062 COMPLETE_VERIFIED_V1.

Validation audit: repository search + đọc source/mappings/SQL/SRS/Git metadata; không build/test/API/SQL execution vì không sửa executable code. Database existence/runtime permissions/concurrency/migration safety **NOT VERIFIED trong audit này**; Task66 SQL failures là historical evidence được dẫn nguồn, không phải lần chạy mới. Không cộng tiến độ UC và không đổi delivery plan.

Handoff DEV2: Workday/WorkInterval chưa implement; `WORKDAY_CONTRACT` là unresolved gate. Reuse existing context/PM keys và SQL permission foundations, chờ approved DEV1 time adapter; không tạo maintenance-only time model. Trước freeze cần chốt PM branch/OWN resolver, UC016 PM-only dependencies, lifecycle/predicate compatibility và retained location locks. Xin cung cấp migration diff để review cụ thể; hiện đề nghị HOLD final freeze.

Post-write check: git diff --check PASS (Git phát cảnh báo LF/CRLF trên các file đã thay đổi sẵn). Tracked diff hash giữ nguyên a91afbc176c1c92dbed97e152d5a733e07d0eaef; staged diff hash giữ nguyên e69de29bb2d1d6434b8b29ae775ad8c2e48c5391; HEAD giữ nguyên. Báo cáo là file mới untracked; không stage. Build/Test: NOT RUN (source-only audit).
