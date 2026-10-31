# UC-006 — Deferred workflow safety (Task57)

Ngày 2026-10-30; HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280` + preserved local source.
Delivery deferred đã được developer duyệt; **không phải OUT_OF_SCOPE hoặc safety exclusion**.
Đọc cùng [deployment proposal](UC006_V1_DEPLOYMENT_SCOPE.md) và
[UC/SRS contract evidence](UC006_V1_CONTRACT_MATRIX.md), S1–S7/D1–D2.

## 1. Safety matrix cho task riêng

`Không tìm thấy` chỉ nói source checkout: đã rà controller, DI, service/direct writer, hosted
registration, SQL release/patch, import/export và jobs. Chưa có deployed DB/job/principal
inventory hoặc legacy scan; cột external/data vì vậy giữ UNKNOWN, không mặc định zero.
Classification của mọi row dưới đây là **SPECIFIED_BUT_NOT_IMPLEMENTED** đối với missing
workflow writer; khả năng writer ngoài checkout là **EXTERNAL_OR_DEPLOYMENT_UNKNOWN**.

| ID / workflow / owner | Production writer hiện tại trong source | SQL support / legacy current-duty risk | Trigger/job/import/external | Reliable predicate hiện có và giới hạn | Task riêng / deployment evidence để quyết định an toàn |
|---|---|---|---|---|---|
| F01 Internal Request / DEV1 | Không thấy assign/route/UC119 ack/UC121 reopen/UC122 escalation service/controller/job | internal_requests + internal_request_assignments có thể lưu ACTIVE current handler; events có actor/free text, thiếu typed receiver/cycle. DRAFT/SUBMITTED/COMPLETED không map đủ UC114–123. Existing deployed current rows UNKNOWN | UNKNOWN; SQL DML có thể tạo assignment dù API thiếu | Positive ACTIVE unended + IN_PROGRESS handler; valid ended history được phân biệt. Không complete next actor/terminal/receiver rule; INTERNAL_REQUEST_CONTRACT còn | D01 resolve BF06 vs UC120; authoritative current-cycle/closure và ack-transfer writer. Deployment owner cung cấp request import/job/proc/write ACL inventory + copy-data report, không dùng creator FK như pending approver |
| F02 Operational Task / DEV1 | Không thấy assign/submit/verify/return/reopen/series writer | operational_tasks/assignments/progress/dependencies cho current assignee; SQL COMPLETED không phân biệt Pending Verification và accepted Closed. Legacy ACTIVE hoặc contradictory ended/current có thể tồn tại | UNKNOWN; future JOB05 có thể restore/tạo occurrence | Positive ACTIVE unended TODO/IN_PROGRESS/BLOCKED và history subset; COMPLETED vẫn ambiguity. OPERATIONAL_TASK_CONTRACT còn | Task verification/current-cycle mapping, UC105 return/106 reopen Users-first; proof Closed mới release. Existing data/job/import inventory và state/cycle report trước bất kỳ exclusion |
| F03 Shift / DEV1 | Không thấy roster/attendance/handover mutation service/job | work_shifts + shift_assignments + shift_attendance cho roster/actual timestamps; ASSIGNED/PLANNED/NOT_STARTED không phải complete SRS lifecycle. Existing open/current roster UNKNOWN | UNKNOWN; HR/roster import hoặc SQL writer có thể tạo rows | SHIFT_OPEN_ATTENDANCE positive từ hợp lệ actual check-in/no check-out; lịch hết giờ không release. SHIFT_CONTRACT còn | D03 chốt future roster; exact lifecycle/overnight/attendance binding, coordinated writer; deployed roster/import principal report, current timestamps/status validation |
| F04 Workday / DEV1 | Không có UC139 Start/PauseAll/End writer/service/job | Không Workday/WorkInterval entity/table trong canonical/mapping; attendance không tự đại diện Working/Ended. Có deployed alternative model/current data hay không UNKNOWN | UNKNOWN; absent repository model không loại external HR/day system | **D05 APPROVED**: Working block kể cả no interval; Ended không clear source assignments. WORKDAY_CONTRACT vẫn unresolved vì chưa có authoritative model, không phải policy chưa duyệt | Task UC139 representation/interval/employee End + Users-first; không auto-end/forced Admin End. Owner proof actual external model/paths; employee không End được cần operational exception approval riêng |
| F05 Unscheduled Workday / DEV1 | Không có No Shift Assignment UC139 alternative writer | shift_attendance.shift_assignment_id nonnullable không biểu diễn independent unscheduled day. Không tạo fake shift/nullable FK trong Task57. Legacy alternative representation UNKNOWN | UNKNOWN; external timekeeping/import có thể tồn tại | UNSCHEDULED_WORKDAY_MODEL Unsupported. D05 áp dụng cả Working day không theo ca; missing row không NoBlocker | Approved model trong task UC139 hoặc D06 scope amendment + deployed no-writer/legacy proof; không loại requirement có thật trong SRS GBR142 |
| F06 Request mandatory acknowledgment / DEV1 | Không thấy UC119 submit/ack/reject receiver writer | Event actor/free text thiếu designated receiver/pending/ack/current version; asset_handovers là subtype khác, không thay được request model | UNKNOWN; trigger/import/external receiver workflow chưa kiểm kê | MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL giữ Unsupported; approval S7 mandatory-next-action block đã có. Pending/rejected giữ sender current handler; ack chỉ release khi transfer atomic | D04 exact subtype/receiver/cycle + representation/atomic transfer, no reassignment by notification; owner inventories + existing pending handover assessment |
| F07 Shift mandatory acknowledgment / DEV1 | Không thấy UC140 sender/receiver/snapshot-ack writer | Không structured shift receiver ack mapping. SRS UC140 awareness không chuyển owner từng source item; không suy mọi receiver FK là current duty | UNKNOWN; shift/handover external app hoặc job chưa chứng minh absent | Cùng existing MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL, không tạo Unsupported ID mới. Historical ack/reject không block vĩnh viễn; source item còn ownership vẫn block | D04 bind mandatory receiver action + shift subtype; explicit source-item reassignment độc lập. Deployment/snapshot receiver lifecycle proof trước exclusion |
| F08 JOB04 preventive generation / DEV2 | Không registered generator/scheduler hoặc schedule mutation path tương ứng; Maintenance CreateWorkAsync chỉ corrective và assignee NULL | preventive_maintenance_schedules/runs tồn tại; schema có assigned_department_id nhưng không default User field như full SRS design. Maintenance rows có thể chứa preventive current assignee do external writer; không coi CreatedBy/PerformedBy là default future owner | UNKNOWN: SQL Agent/external scheduler. JOB04 được chỉ định ở SRS §16.4 dù không có hosted code | Concrete maintenance assignee predicate cover current generated work khi status consistent; chưa chứng minh producer eligibility/default semantics; thuộc pending release coverage | DEV2 task schedule/occurrence/default mapping + idempotency + retained User eligibility; deployed Agent steps/services/permissions + existing runs/works source proof. Không sửa DEV2 trong Task57 |
| F09 JOB05 recurring Operational Task / DEV1 | Không có series/default-assignee model hoặc generator trong API | Current operational tasks/assignments tồn tại; recurring series/default model chưa có. Không thể từ absence series suy existing task rows vô chủ | UNKNOWN: external/SQL scheduled generator hoặc import có thể tạo task trực tiếp | Task current positive subset chỉ phần output, không producer safety; OPERATIONAL_TASK_CONTRACT và DEV1 release coverage giữ | SRS §16.5 daily generation: task riêng cho series/default eligibility, cycle, duplicate prevention, per-series rollback; không invent inactive fallback. Deployed schedules/import/legacy provenance proof |
| F10 Line Manager transfer / DEV1 | Không manager/department transfer hoặc employment-lifecycle mutation service; UserProfile whitelist, Create không set manager. Scalar FK không tự là DH role | users.manager_user_id và departments.manager_user_id/is_active có thể lưu current management. Nondeleted employment ACTIVE subordinate là positive; inactive login không giải phóng employment duty. Existing deployed designations UNKNOWN | UNKNOWN: HR import/admin SQL/other services có thể update manager/employment | LINE_MANAGER_DESIGNATION và DEPARTMENT_MANAGER positive; LINE_MANAGER_CONTRACT chưa đủ current/history/release vocabulary. Không bỏ approved management blocker | D02 exact release/history semantics + Users-first old/new manager/subordinate transfer/audit; deployed HR/write principals, active/deleted/employment rows và ownership authority report |

Cleaning/rework/inspection/defect và department-management writer gaps liên quan cũng thuộc
source category C01 của deployment inventory. Có predicate subsets đã SQL-test trước đây;
không vì vậy toàn housekeeping/inspection workflow đã implemented hoặc không thể có legacy work.

## 2. Handoff và acceptance không lẫn với current-duty evaluation

1. **Existing-backend work có thể làm ngay:** giữ và mở rộng real SQL protocol suites cho
   actual Maintenance/Warehouse/Asset/MR/account writers; đóng per-operation evidence gaps,
   actor authorization và global graph review. Không cần chờ implement toàn bộ missing modules
   để làm những việc này. Trước acceptance source promotion, actual Deactivate vẫn chỉ denial.
2. **Deferred task done criteria:** approved exact model/lifecycle/current owner; writer
   participants discovered rồi Users ascending → feature → authoritative reread → eligibility →
   atomic write/audit; current/history tests, return/reopen/default/ack restoration, both-order
   SQL waits, cancel/timeout/deadlock rollback. Không tự implement schema hoặc module trong Task57.
3. **Deployment evidence package:** artifact/version and active routes/services/schedulers;
   deployed schema/module/trigger/job definitions; effective DML/EXECUTE and cross-DB/import
   access; copy-data current/history/unknown-state counts và provenance; named owner review.
   Không yêu cầu secret hoặc truy cập HotelOpsDB trong task này. Evidence missing → UNKNOWN.
4. **Nếu muốn UC006 limited release trước deferred tasks:** D06/SA57 phải được human review
   với exact restrictions và legacy handling. Chưa có proof/approval thì giữ cả five runtime
   Unresolved, two Unsupported, three pending release gates; không lấy rowcount=0 ở trial
   hoặc controller absence để loại khỏi evaluator. D05 đã approved nhưng thiếu model vẫn block.

## 3. D05 operational controls (approved policy, chưa implementation)

Employee End Workday qua workflow hợp lệ trước Admin Deactivate; Working không có interval
vẫn block. Ended chỉ kết thúc daily record; kiểm tra source tasks/requests/maintenance/custody
độc lập. Pause All không reassign. Deactivate không auto-end và Task57 không cấp Admin forced-end.
Employee mất quyền truy cập/không thể End: giữ BLOCKED, escalation để duyệt actor/SoD/audit và
recovery/exception riêng; không chỉnh status hoặc giả End để đạt success.

**Không workflow nào trong bảng được tự chuyển sang OUT_OF_SCOPE_NOT_IMPLEMENTED.**
Delivery priority có hiệu lực, release/scope exclusion chưa có hiệu lực; runtime source giữ nguyên.
