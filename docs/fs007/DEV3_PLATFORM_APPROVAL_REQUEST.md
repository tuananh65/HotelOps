# Yêu cầu DEV3 — platform contracts tối thiểu cho Cleaning Lifecycle

**Người gửi:** DEV1 Room Operations. **Baseline:** `a5b3f66e9732ff00035f0f5fc43971322e0122e6`, feature/dev1-room-ops, .NET10 single API/EF Database First. **Trạng thái:** REQUEST/PROPOSED; không thay FE01/02/12 và không tự gửi request. Mục tiêu giữ platform đã chạy, chỉ xin exact bindings/shared room/time integration thật sự cần cho UC028/029 và execution tiếp theo.

## 1. Không tạo blocker giả hoặc yêu cầu duyệt lại nền hiện có

Existing protected Cookie + SQL Session + CSRF/PermissionService/active-user/branch/access checks dùng nguyên trạng. CLEANING_ASSIGN/CLEANING_VIEW/CLEANING_EXECUTE hiện có; không thêm role/grant/numeric IDs. Existing Assign core đã kiểm active employment/HK department/branch access/role validity/current shift; GET planning hoạt động và không thể coi candidates/raw shifts là eligible-now. Initial Assign không yêu cầu Working Workday; Create không yêu cầu Vacant (có stayover). Không yêu cầu hoàn thành UC138, B1.2 Unscheduled/PauseAll hay Notification Workday để Create/Assign chạy.

Branch Active là prerequisite thật và có thể kiểm dưới retained Branch lock trong transaction; không phải đợi full UC012 Deactivate/GBR033 completion. Deactivate hiện fail closed trong source và recovery route inactive; không mở nó để nghiệm thu cleaning. Future create/open writers phải phối hợp Branch Active protocol, nhưng không bắt DEV3 triển khai toàn FE06–10 để duyệt own cleaning slice.

## 2. Các quyết định cụ thể xin DEV3/joint review

| ID / scope | V1 đề nghị | Shared impact / acceptance |
| --- | --- | --- |
| P01 UC028 resource authorization | Reuse CLEANING_ASSIGN on actual active Room.BranchId, active actor/employment/access/hierarchy/type. Không chỉ check query-string branch, không require role-name string như policy | Confirm exact room resource binding/service gate, broad/branch grants không vượt room/branch; new route/DTO reviewed; no grant seed change |
| P02 Room cycle/readiness | Giữ rooms.operational_status và legacy room_status_history; hai proposed sidecars current-cycle pointer + readiness events, derived projection. Occupancy không infer; current-cycle explicit, unknown legacy fails closed | Exact DDL/EF/coordinator/DI diff shared cần chủ dự án/DEV1/DEV3 review, DEV2 block protocol review; Database First additive only, no generated edits. Blocked+occupied/old-pass/duplicate/missing-cycle/concurrent-create tests |
| P03 Assigned recipient read | Proposed GET /api/branches/{branchId}/cleaning-tasks/{id}?assignmentId=<persistedAssignmentId> header DTO, CLEANING_VIEW BRANCH/GLOBAL hoặc OWN từ exact actual assignment relationship; not branch monitor expansion | OWN current/historical assignment read identity phải được duyệt tách executing ownership. Chỉ minimal task facts/assignment mình; không timeline/PII người khác. Current grant/access/restricted/session gates, cross-branch deny; notification không cấp quyền |
| P04 Explicit recipient authority | Worker đọc source theo actual recipient userId/assignment fact, existing effective permission paths/account/employment/branch access; không cần web session, không fake principal hoặc ambient producer privileges | DEV3 supplies/approves exact explicit-user evaluator semantics and source adapter; transient auth/data unavailable khác definitive denied. No role shortcuts, no broad resolver behavior change |
| P05 UC031 A1 integration (downstream only) | Reuse existing scoped same-instance IWorkTimeTransactionParticipant/SqlWorkTimeTransactionParticipant; all users/time before location/source locks; no second interval; source state/order + own Working day verified | Exact reviewed flush/lock/rollback protocol and tests; no ShiftAttendance substitution, no method change needed unless proven. Không chặn Create/Assign vì P05 chưa duyệt |
| P06 B1.1 public activation (downstream only) | Current WorkdaysController has [NonController]; service/DI source exists. Review exact own scheduled4routes if actual Start workflow needs public Working day establishment | Không 'reapprove' A1 business principles; selective route collision/security/DI activation review, normal session/CSRF. Không enable other recovery controllers; không unscheduled/PauseAll assumption |

P03 đề nghị ownership read policy tối thiểu: persisted AssignmentId belongs to caller and task/Room/branch agree; nonrevoked effective CLEANING_VIEW OWN and current account/branch eligibility. Read relationship có thể còn hợp lệ sau ended assignment để đọc header của source mình từng được phân công, nhưng không cho execute/current assignee info. Chính sách lifetime/history này là **PROPOSED** cần DEV3 duyệt; không dùng current progress resolver chỉ IN_PROGRESS. Với caller BRANCH/GLOBAL view vẫn phải có task→branch lineage. Ended assignment không tự là grant: current permission/access vẫn mandatory. Missing/foreign identities trả masked403/404 theo approved API policy; không leak existence.

P04 không thay existing IPermissionService ambient behavior. Xin exact additive evaluator/adaptor hoặc accepted existing API nếu có, cùng revoke/expiry/branch-history semantics; role hierarchy/Admin bypass không được thêm. DEV2 worker không được sử dụng producer cookie/session để đánh giá người nhận.

## 3. UC138/B1.2 và giới hạn ownership

Task80 `UC138_APPROVAL_SCOPE.md` tại28f0a5e4f582bc56ba101b48915d33baadeb7b95: three shift rowversions + five C# targets chỉ PROPOSED, NOT APPROVED/APPLIED, full roster not implemented. Actual retained trial metadata chưa có ba row_version fields; existing shifts vẫn là facts có thể query để eligibility. Không xin blanket shift schema/SHIFT_MANAGE activation cho UC029.

COORD-FINAL-01 đã chốt B1.2 reason≥10, primary eligible branch DH, cross-user read và PauseAll atomic; exact adapters/publisher/DI NOT APPLIED. Các chữ REVIEW PROPOSAL trong interface không phủ nhận A1 accepted implementation, nhưng cũng không cấp quyền áp dụng B1.2. UC031 dùng scheduled A1 nếu approved/available; unscheduled support có thể defer, phải fail closed nếu không có valid Working day. No invented branch-DH routing/Department.ManagerUserId fallback.

Reception/DH revised matrix vẫn pending; không thay quyền FE01/Room/Workday hoặc suy DH quản lý một department. Occupancy/check-in/out ownership chưa đủ source; không cần mở Reception writer để làm UC028, và không tuyên bố Ready/check-in acceptance từ cleaning alone.

## 4. Bằng chứng độc lập với cuộc trò chuyện

- Baseline paths: Controllers/WorkdaysController.cs [NonController]; Services/WorkTime/{WorkdayService,SqlWorkTimeTransactionParticipant,IWorkTimeTransactionParticipant}.cs; Program scoped alias.
- Existing Assign + Batch: Services/Housekeeping/{HousekeepingAssignmentsService,HousekeepingPlanningService}.cs; Services/HotelStructure/RoomTypeBatchService.cs; Security/Authorization/PermissionService.cs và CleaningTaskResourceResolver.
- RoomDirectoryContracts/RoomDirectoryService: recorded legacy only; generated Room lacks Occupancy/Readiness/current cycle. Actual dbo.room_status_history CHECKs legacy-only, rooms default DIRTY while EF default AVAILABLE; no automatic shared fix requested.
- Handover28f0a5e... under scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104_02/: DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md, DEV1_ROOM_WORKDAY_SHARED_CONTRACT.md, DEV1_GLOBAL_LOCK_AND_AUTHORITY_MATRIX.md. Source/SQL evidence in FS007 accompanies this request.

Xin phản hồi từng P01–P06: APPROVE exact scope hoặc REVISE với lý do + accepted existing alternative; ref/commit/file/hunk/hash, required tests và named owner. Đặc biệt P05/P06 là future execution, không bắt initial Create/Assign chờ full Workday/UC138/B1.2. Shared DDL/EF/DI/security changes chỉ áp dụng sau explicit chủ dự án approval; không có approval triển khai/SQL writes trong request này.
