# DEV1 — Full scope audit & 21-day delivery plan (Task59)

**Current verified snapshot — Task74,31/10/2026:26/75 UC,60/256=23.44%; UC032/109
COMPLETE_VERIFIED_V1, gain+2UC/+4points from Task73. UC012 PARTIAL; UC028/026 BLOCKED_EXTERNAL;
UC029/041/046 PARTIAL pending DEV2 notification delivery/retry; UC016 remains PARTIAL. Batch acceptance: §22;
UC017 deployment evidence and rollout prerequisites remain in §17.**

Ngày audit: **30/10/2026**. Đơn vị đo: full backend UC trong SRS đang lưu, không phải số endpoint, bảng hay SQL assertions. **80–85% toàn DEV1 trong 126 giờ không khả thi theo baseline này.** Kế hoạch thay thế: hoàn tất tối đa 14 UC account/cơ cấu trong 108 giờ dự kiến + 18 giờ dự phòng, có điều kiện duyệt contract; không giảm mẫu số hay kích hoạt scope SA57 để đạt tỷ lệ.

## 1. Baseline, nguồn và giới hạn

- Branch `feature/BE1-database-integration`; HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`; upstream `origin/feature/BE1-database-integration`, local tracking comparison ahead6/behind0 (không fetch nên không khẳng định remote mới nhất). Index không có staged changes. Preflight: 14 tracked modified +370 untracked files riêng lẻ; cả384 ghi SHA-256 manifest `obj/task59-preflight-20261030-015036/manifest.json`. Task59 chỉ thêm một Markdown ngoài ignored obj.
- Bốn worktrees hiện hữu: root; `obj/task11-dev2-rehearsal` (3688ba9); `obj/task12-pr7-resolution` (d6ef057); `D:/SEP490/Projects/HotelOps-AI-Loop` (8e0b673). Không tạo/switch/xóa worktree, không fetch/merge/commit/push/reset/clean/rebase. Không truy cập SQL/HotelOpsDB.
- Ownership từ [AGENTS](../../AGENTS.md): FE-01/02/03/04/05/11/12/13 thuộc DEV1. [Danh mục UC](../../docs/requirements/srs_sections/01_overall_requirements.md) và [đặc tả từng UC](../../docs/requirements/srs_sections/02_use_case_specifications.md) có 144 UC: **75 DEV1 độc quyền + 2 shared UC-063/064 + 67 DEV2**. UC-111 chỉ tính một lần dù liên quan FE-11/12.
- [SRS README](../../docs/requirements/SRS_README.md), [Change Notes](../../docs/requirements/SRS_CHANGE_NOTES.md), [functional/screens/jobs](../../docs/requirements/srs_sections/03_functional_requirements.md), [business rules](../../docs/requirements/srs_sections/05_requirement_appendix.md) đã đối chiếu. SRS/Permission Matrix trong repository là **historical draft**, không tìm thấy bản final ký duyệt sau revision Reception/DH, full approved matrix V4 hay bảng giao việc DEV1 đã ký độc lập. Không suy ra SRS không có yêu cầu. Assignment authority hiện có là AGENTS; [Task16 inventory](Dev1BackendScope/USE_CASE_COVERAGE_MATRIX.md) hỗ trợ trace UC/model, **trạng thái Task16 đã cũ** và được kiểm tra lại bằng source.
- Database evidence: [DbContext](../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), [entities](../../src/HotelOps.Api/Entities), [schema description](../../docs/database/DATABASE_SCHEMA.md), [DB rules](../../docs/database/DATABASE_RULES.md), [catalog seed](../database/release/03_HotelOps_RBAC_Catalog_Seed.sql). Đây là source/schema design evidence, không khẳng định database production hiện tương thích. [DEV2 coordination](../../docs/coordination/HotelOps_Dev2_Backend_Sync_RBAC_2026-10-20.md) có Reception occupancy và DH branch-wide; không phê duyệt mọi transition/grant.
- Audit chỉ thêm tài liệu này; không sửa runtime, schema, generated entity, DI, DEV2 hoặc catalog; không đăng ký Deactivate/activate SA57/clear gates. Kết quả runtime dưới đây là evidence đã chạy trước Task59, không phải test mới.

## 2. Kiểm kê source theo UC

Source hiện có 18 controllers, trong đó sáu controller nghiệp vụ DEV2; DEV1 business API tập trung account/auth. [Program](../../src/HotelOps.Api/Program.cs) không đăng ký workflow Hotel Structure, Room, Cleaning, Inspection, Operational Task, Shift/Workday hay Internal Request. Cleaning resource resolver chỉ là authorization adapter; responsibility predicates không phải workflow implementation. Hai thư mục verification HotelStructureRead/EffectiveAccessReview chứa proposals/blocked decisions, không có implementation APIs để cộng tiến độ.

Các profile dưới đây là cột ghép cho chín mặt kiểm tra của **từng UC** trong bảng: controller/endpoint; service; DTO/validation; entities/query; authorization; transaction/concurrency; audit; tests; DEV2 dependency. `Absent` nghĩa là không tìm thấy UC controller/service/DTO/query nghiệp vụ tương ứng trong source hiện tại; entity không được tính là implementation. Weight/hours riêng từng UC ở bảng sau.

| Profile | Controller/service/DTO + SQL data | Authorization / transaction / audit / verification / dependency |
|---|---|---|
| A | [AuthController](../../src/HotelOps.Api/Controllers/AuthController.cs); AuthenticationLoginService/AuthenticationCredentialWorkflow; DTOs/Authentication; User/UserSession/AuditLog | SQL session, credential/lockout; User → Session, atomic mutations/audit. Task58 actual login/logout/change-password faults; auth/reauth regression. Không cần DEV2 writer |
| B | [Create controller](../../src/HotelOps.Api/Controllers/UserAccountCreationController.cs); UserAccountCreationService; DTOs/Users; User/UserRole/UserBranchAccess/credential | USER_MANAGE + validated server-side actor; GlobalAdmin → Users + conditional final guards; atomic credential/audit. Task58 Create + creation regression; full multi-role draft chưa được release. Không sửa DEV2 |
| C | [Profile controller](../../src/HotelOps.Api/Controllers/UserProfileController.cs), [Reset controller](../../src/HotelOps.Api/Controllers/AdminCredentialResetController.cs); UserProfileService/AdminCredentialResetWorkflow; DTOs/Users | USER_MANAGE + SQL session/scope + reset reauth; strict ETag/profile allowlist; LastAdmin + revoke sessions + atomic audit. Profile 329 / reauth 165 assertions và Task58. Không phụ thuộc DEV2 mutation |
| D | [Activate controller](../../src/HotelOps.Api/Controllers/UserAccountActivationController.cs); UserAccountActivationService + internal UserAccountDeactivationService; status/reason/ETag contracts | USER_MANAGE; retained Users/LastAdmin/sessions/audit guards; Activation SQL verified, Deactivate success/release chưa PASS. DEV2 writer integration là dependency; không mở endpoint |
| E/G | [Branch controller](../../src/HotelOps.Api/Controllers/UserBranchAssignmentController.cs) / [Role controller](../../src/HotelOps.Api/Controllers/UserRoleAssignmentController.cs); corresponding assignment services/DTOs | Existing permission/scope and strong ETag; retained Users/GlobalAdmin as applicable; atomic version/access/audit. Task58 Add/Assign verified; transfer/revoke absent, dependent current responsibility DEV1/DEV2 |
| F | UC008/009 endpoints/services/DTOs absent; User/Role/Permission mappings exist | UC008/009 conflict canonical fixed-role/grant policy. Historical Task23 UC011 decisions superseded for V1 by Task62's explicit ASSUMED_V1 policy; see J |
| H | [UsersController](../../src/HotelOps.Api/Controllers/UsersController.cs); UserDirectoryService; Task62 includeAccess drilldown | Global USER_VIEW list/minimal detail unchanged; opt-in detail additionally ROLE_MANAGE and shared UC011 projection. Retained SQL read boundary, no business write/audit; Task62 acceptance (§10) |
| J | [UC011 implementation](AccessReview/TASK62_UC011_IMPLEMENTATION.md); UserAccessReviewController/Service/DTO | Existing USER_VIEW+ROLE_MANAGE intersection, SQL global/branch scope, conditional OWN, no self exemption. Shared eligibility/read boundary, HTTPS+SQL regression (§10) |
| S | Absent. Branch/Department/Floor/Area/RoomType mapped; no UC-specific query/DTO | MASTER_DATA_MANAGE/ROOM_VIEW exist but not blanket read grant. Missing endpoint auth/validation, writes tx/concurrency/audit, SQL/HTTP tests. RoomType has no BranchId; no fabricated ownership. DEV2 needs selectors/reference contracts, owns consumers |
| S16 | [UC016 Task66](HotelStructure/TASK66_UC016_IMPLEMENTATION.md); existing structure controller/service + dedicated dependency DTO/query | SQL actor/session/scope/retained locks/ETag/GBR035 rejection; production POST exists but no mutation allowed. DEV2 Incident inactive-location and Asset stale-location defects reproduced. PARTIAL, not verified completion (§14) |
| S14 | [UC014 implementation](HotelStructure/TASK63_UC014_IMPLEMENTATION.md); HotelStructureCreationController/Service/CreateStructureDtos | MASTER_DATA_MANAGE global/branch, SQL session + retained User/parent locks, branch-unique codes, atomic CREATE audit. Production HTTPS/SQL215 PASS; schema/catalog/DEV2 unchanged (§11) |
| S13 | [UC013 implementation](HotelStructure/TASK64_UC013_IMPLEMENTATION.md); BranchStructureController/Service/DTOs | Approved ASSUMED_V1 independent ROOM_VIEW basic physical read or MASTER_DATA_MANAGE management read. SQL branch/session/grants, paginated hierarchy/details/filters, SERIALIZABLE count/page, no business writes. Production HTTPS/SQL159 new assertions +UC014215 regression PASS (§12) |
| S15 | [UC015 implementation](HotelStructure/TASK65_UC015_IMPLEMENTATION.md); HotelStructureUpdateController/Service/DTOs | MASTER_DATA_MANAGE SQL scope; GET edit +conditional PUT, strong state/audit revision ETag without schema change, retained User/branch/parent/target locks, atomic reason/audit and fault rollback. Production HTTPS/SQL215 new assertions PASS (§13) |
| S17 | [Task68 UC017](HotelStructure/TASK68_UC017_IMPLEMENTATION.md); RoomTypesController/RoomTypeCreationService/DTO/module schema gate +candidate SQL | User explicitly prioritizes SRS branch/cleaning/default-checklist over legacy global model. Successful full-field trial creation, atomic audit, branch uniqueness and SQL fault/rollback acceptance; production migration/mapping integration pending, PARTIAL (§16) |
| S18 | [Task69 UC017 deployment](HotelStructure/TASK69_UC017_DEPLOYMENT_ACCEPTANCE.md); explicit canonical -Uc017 module + controlled V1/legacy mapping | Fresh/legacy/replay/fault/recovery deployment, real SQL cross-branch rejection,165 HTTPS/SQL checks and consumer/Auth regression PASS. COMPLETE_VERIFIED_V1, no production deployment (§17); supersedes historical S17 classification |
| R | [Task67 UC023](RoomDirectory/TASK67_UC023_COMPLETION.md); RoomsController +extended existing RoomDirectoryService/contracts; Room write workflows absent | ROOM_VIEW SQL branch/explicit-global scope, no role bypass; list/detail/recorded reason/source/history HTTPS+SQL verified (§15). No status orchestration/writers. UC027/022 involve DEV2 Incident/RoomBlock; Reception occupancy model unresolved |
| K | Absent. CleaningTask/Assignment/Progress/Rework + Room mapped; CleaningTaskResourceResolver is not service | CLEANING_* catalog/foundation exists, workflow checks absent. Required User-first eligibility/time/SoD/room tx/audit/tests absent. GBR138 references intervals for UC030/031/033–036: current entities do not prove full time flow. DEV2 maintenance/room-block/material/notification interfaces only through agreed contracts |
| I | Absent. InspectionChecklist/Version/Items + RoomInspection/Result/Defect mapped | INSPECTION_*/CHECKLIST_MANAGE candidates, exact revised actors/SoD required. Version model **exists**; inspection flow/DTO/tx/audit/tests absent. Room release/rework connects DEV1 Cleaning and DEV2 incidents/blocks |
| T | Absent. OperationalTask/Assignment/Progress/Dependency mapped | TASK_* catalog exists but draft execute matrix conflicts actors. No task writer, tx/audit/concurrency/test suite. SQL fields do not model recurrence/JOB05 or full verification/time cycle. Actor roles using task do not change DEV1 ownership; notifications/time/DEV2 consumers need contracts |
| W | Absent. ShiftTemplate/WorkShift/ShiftAssignment/ShiftAttendance exist; dedicated Workday/WorkInterval/Correction/Handover acknowledgment absent | SHIFT_MANAGE/ATTENDANCE_RECORD foundations not a work_interval.correct implementation. No DTO/auth/tx/audit/tests for these UCs. Requires approved models/permission mapping and DEV2 time-event integration; no payroll module invented |
| Q | Absent. InternalRequest/Assignment/Event/Feedback mapped | IR_* catalog exists; no production request service/DTO/auth/tx/audit/tests. Events text not proof of acknowledged transfer/mandatory links. DEV2 Incident/MR/notification adapter contract needed for corresponding UCs; request history UC123 includes withdrawal, not list-only |

Classification: COMPLETE_VERIFIED = full described backend flow with relevant executable evidence; PARTIAL = actual production subset; IMPLEMENTED_UNVERIFIED = full service/API exists without sufficient tests; NOT_IMPLEMENTED = missing implementation; BLOCKED_EXTERNAL = missing implementation **and** identified external approval/model/interface prevents full delivery. Blocked is included in missing, not an extra UC. Foundations are not fractional verified points.

| UC | Tên trong SRS | Profile | Status | Điểm | Giờ còn lại | Residual / dependency cụ thể |
|---|---|---|---|---:|---:|---|
| UC-001 | Login | A | COMPLETE_VERIFIED | 3 | 0 | Login/session/lockout và temporary credential có service + SQL evidence |
| UC-002 | Logout | A | COMPLETE_VERIFIED | 1 | 0 | Logout idempotent và audit/revoke transaction đã kiểm thử |
| UC-003 | Change Password | A | COMPLETE_VERIFIED | 3 | 0 | Đổi mật khẩu + revoke/replace sessions + atomic audit đã kiểm thử |
| UC-004 | Create User Account | B | PARTIAL | 5 | 8 | Chỉ single-role allowlist V1; chưa full selected role(s)/scope của draft |
| UC-005 | Update User Account | C | COMPLETE_VERIFIED | 5 | 0 | Profile allowlist + ETag; reset có reauth/identity confirmation/one-time handoff |
| UC-006 | Activate/Deactivate User Account | D | PARTIAL | 5 | 24 | Activate có API; Deactivate internal còn blocked và không đăng ký |
| UC-007 | Assign User to Branch/Department | E | PARTIAL | 5 | 20 | Add membership có; thiếu transfer department/primary branch/revoke + open-work guards |
| UC-008 | Create/Update User Role | F | BLOCKED_EXTERNAL | 3 | 12 | Dynamic role CRUD xung đột fixed canonical catalog; cần quyết định editable surface |
| UC-009 | Configure Role Permissions | F | BLOCKED_EXTERNAL | 5 | 16 | Thiếu grant editor; cần approved permission/scope anti-escalation contract |
| UC-010 | Assign/Revoke User Roles | G | PARTIAL | 5 | 14 | Assign allowlist có; thiếu revoke/full role lifecycle/expiry và responsibility integration |
| UC-011 | Review User Access Permissions | J | COMPLETE_VERIFIED | 3 | 0 | Task62 production HTTPS/SQL + regression PASS theo ASSUMED_V1, không thêm grants; §10 |
| UC-141 | View User List | H | COMPLETE_VERIFIED | 2 | 0 | Task62 hoàn tất step6 bằng shared UC011 projection; giữ list/minimal-detail contract; §10 |
| UC-012 | Update Branch Information | S74 | PARTIAL | 2 | 6 | Task74 create/read/conditional metadata update HTTPS/SQL verified; no deactivation mutation, GBR033/DEV2 writer protocol unresolved; aggregate ETag without invented Branch rowversion; no earned points (§22) |
| UC-013 | View Branch Structure | S13 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task64 production GET, scoped physical tree/expansion/details/filter/pagination + management departments; explicit V1 ROOM_VIEW boundary, real HTTPS/SQL/concurrency and regressions PASS (§12) |
| UC-014 | Create Floor/Area | S14 | COMPLETE_VERIFIED | 2 | 0 | Task63 both production POSTs, scoped authority/parents/uniqueness/atomic audit, HTTPS+SQL fault/concurrency acceptance PASS (§11) |
| UC-015 | Update Floor/Area | S15 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task65 both production GET/PUT edit paths, scoped strong If-Match/reason/audit, safe Area parent, ABA/no lost update, SQL faults +regressions PASS (§13) |
| UC-016 | Deactivate Floor/Area | S16 | PARTIAL | 3 | 8 | Task66 production HTTPS rejection/GBR035 SQL implemented; actual DEV2 inactive-location/stale-placement defects confirmed. Mutation BLOCKED, no verified gain (§14) |
| UC-017 | Create Room Type | S18 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task69 full SRS fields; explicit canonical deployment + Database First legacy/V1 mapping; fresh/legacy/replay/recovery PASS;165 HTTPS/SQL checks + consumer/Auth regression PASS. No production deployment (§17) |
| UC-018 | Update Room Type | S70 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task70:64 independent HTTPS/SQL checks; strong ETag/If-Match, branch/code/checklist validation, session/grant final gates, atomic audit/rollback, historical Room/inspection preservation; §18 |
| UC-019 | Deactivate Room Type | S71 | COMPLETE_VERIFIED_V1 | 3 | 0 | Task71:50 independent checks; scoped confirmation/reason/ETag, active-room dependency DTO, soft deactivation/atomic audit, create/deactivate race and rollback; §19 |
| UC-020 | Create Room | S70 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task70:55 independent HTTPS/SQL checks; scoped active references, DIRTY+INIT history+audit transaction, duplicate race and post-INSERT expiry/cancel/audit rollback; §18 |
| UC-021 | Update Room Information | S71 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task71:50 independent checks; scoped editable GET/conditional PUT, active references/uniqueness/reason, preserve identity/status/open work/history, reject inconsistent legacy parents, atomic audit/concurrency/rollback; §19 |
| UC-022 | Deactivate Room | R | NOT_IMPLEMENTED | 5 | 12 | Room deactivate cần open cleaning/inspection/incident/block/reference checks với DEV2 |
| UC-023 | View Room Directory | R | COMPLETE_VERIFIED_V1 | 2 | 0 | Task67 production list/detail/reason/source/history; SQL ROOM_VIEW scope;227 UC023+2 startup checks,117 SQL regression and targeted structure/Auth regression PASS (§15) |
| UC-024 | Create Department | S71 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task71:53 independent checks; developer-approved branch DH designation, SQL role/membership/User eligibility initial/final, sorted User locks, multi-department DH, no RBAC expansion, legacy schema and rollback; §19 |
| UC-025 | Update Department | S71 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task71:51 independent checks; scoped strong ETag/update/audit, immutable branch/code, commit-time DH eligibility and manager revocation race, no designation-induced RBAC changes; §19 |
| UC-026 | Deactivate Department | S | BLOCKED_EXTERNAL | 5 | 12 | Task74 review: GBR042 active users/warehouses/open work/requests/assets and retained writer protocol unresolved; Maintenance Create checks department branch but not active state; no mutation/points (§22) |
| UC-142 | View Room Types List | S70 | COMPLETE_VERIFIED_V1 | 1 | 0 | Task70:49 independent HTTPS/SQL checks; explicit/automatic SQL branch scope, filters/sort/page/count/detail, global-only legacy read, final expiry/cancel rejection, no business writes; §18 |
| UC-027 | Manage Operational Room Status | R | BLOCKED_EXTERNAL | 5 | 20 | Reception occupancy/release contract và model chưa chốt; DEV2 Incident/RoomBlock precedence |
| UC-028 | Create Cleaning Task | K | BLOCKED_EXTERNAL | 3 | 10 | Task74 cleaning-item foundation supplied; GBR043/044 room lifecycle/effective-block precedence and DEV2 writer coordination not approved/verified; no create/room-status mutation or points (§22) |
| UC-029 | Assign Cleaning Task | K73 | PARTIAL | 3 | 8 | Task73: SQL-scoped planning/workload and eligible on-shift assignment, actual rowversion/audit, explicit legacy opt-in; notification delivery/retry adapter unapproved/unintegrated; no earned points (§21) |
| UC-030 | Reassign Cleaning Task | K | NOT_IMPLEMENTED | 5 | 12 | Assign/reassign + retained Users + close interval + actor eligibility |
| UC-031 | Start Cleaning Task | K | NOT_IMPLEMENTED | 3 | 8 | Start requires workday/interval and room state; resolver không phải workflow |
| UC-032 | Update Cleaning Progress | K74 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task74:87 per-UC HTTPS/SQL checks; scoped current-assignee OWN, distinct frozen cleaning items, notes/private evidence, actual rowversion, 100% not completion, atomic audit/expiry rollback, real race/DMV cancellation and legacy parent revision; §22 |
| UC-033 | Report Cleaning Blocker | K | NOT_IMPLEMENTED | 3 | 10 | Report blocker + hold/resume + work interval và room status |
| UC-034 | Complete Cleaning Task | K | NOT_IMPLEMENTED | 5 | 12 | Complete + interval close + inspection/room state/audit atomic |
| UC-035 | Receive Rework Task | K | NOT_IMPLEMENTED | 3 | 8 | Start rework + original defect/assignee/time checks |
| UC-036 | Complete Cleaning Rework | K | NOT_IMPLEMENTED | 5 | 16 | Complete rework + verification/defect/room state coordination |
| UC-037 | Monitor Cleaning Tasks | K | NOT_IMPLEMENTED | 2 | 6 | Assignment/progress history query + any draft alternative write flows |
| UC-038 | View Room Turnover History | K72 | COMPLETE_VERIFIED_V1 | 2 | 0 | Production scoped cycle/detail/private evidence; real HTTPS/SQL and revocation/cancellation evidence (§20) |
| UC-039 | Create Room Inspection Checklist | I72 | COMPLETE_VERIFIED_V1 | 2 | 0 | Conditionally approved trial extension; SQL-scoped multi-type Draft/create/publish/group/mandatory, atomic audit, fresh/upgrade/replay/fault/rollback verified (§20) |
| UC-040 | Update Room Inspection Checklist | I72 | COMPLETE_VERIFIED_V1 | 3 | 0 | Real rowversion If-Match; edit Draft/new Draft; immutable published/used content; atomic replacement/retirement; HTTPS/SQL races and historical version preservation verified (§20) |
| UC-041 | Assign Room Inspection | I73 | PARTIAL | 3 | 8 | Task73: eligible inspector/SoD, explicit pending cycle, latest applicable Published version, one open inspection, due/audit; notification delivery/retry pending DEV2; no earned points (§21) |
| UC-042 | Perform Room Inspection | I73 | COMPLETE_VERIFIED_V1 | 3 | 0 | Task73: assigned-inspector Start/Save Draft/Finish, exact frozen version, PASS/FAIL/photo evidence, mandatory/defect validation, Pending Conclusion distinct from conclusion; HTTPS/SQL/rowversion/audit/cancellation verified (§21) |
| UC-043 | Record Inspection Defects | I73 | COMPLETE_VERIFIED_V1 | 3 | 0 | Task73: assigned inspector + failed criterion, severity/location/private evidence links, Inspecting/Pending Conclusion only, concluded rejection, atomic audit/files rollback and stale-write race verified (§21) |
| UC-044 | Approve Room Inspection | I | NOT_IMPLEMENTED | 5 | 14 | Inspection/rework integration + current assignee/verification cycle |
| UC-045 | Reject Room Inspection | I | NOT_IMPLEMENTED | 3 | 10 | Reject inspection + defects/room state/audit; không thay defect resolution |
| UC-046 | Request Cleaning Rework | I73 | PARTIAL | 5 | 16 | Task73: failed-source/due/selected-defect relational links, eligible on-shift staff, one open rework/audit; notification delivery/retry pending DEV2; no earned points (§21) |
| UC-047 | Reinspect Room | I | NOT_IMPLEMENTED | 5 | 16 | Exception/reinspection path + current-cycle evidence/SoD |
| UC-048 | View Room Inspection History | I72 | COMPLETE_VERIFIED_V1 | 2 | 0 | Production SQL-scoped filters/detail/exact historical criteria/results/reworks/private evidence; HTTPS/SQL verified (§20) |
| UC-102 | Create Operational Task | T | BLOCKED_EXTERNAL | 5 | 20 | Recurring series/JOB05/completion criteria model missing; ad-hoc alone partial |
| UC-103 | Set Task Priority and Deadline | T | NOT_IMPLEMENTED | 2 | 8 | Priority/deadline/cancel includes recurrence stop; source planning fields only |
| UC-104 | Assign/Reassign Task | T | BLOCKED_EXTERNAL | 5 | 16 | Reassign closes work interval; interval model/contract required |
| UC-105 | Review and Verify Task Completion | T | BLOCKED_EXTERNAL | 5 | 14 | Pending Verification/Closed/Return semantics differ SQL status support; decision required |
| UC-106 | Reopen Assigned Task | T | BLOCKED_EXTERNAL | 5 | 14 | Reopen verification cycle/status + last eligible assignee needs approved mapping |
| UC-107 | Accept Assigned Task | T | NOT_IMPLEMENTED | 2 | 6 | Accept/decline + assigned actor + status/concurrency/audit |
| UC-108 | Start Assigned Task | T | BLOCKED_EXTERNAL | 3 | 12 | Start requires Working workday + one open interval; models missing |
| UC-109 | Update Task Progress | T74 | COMPLETE_VERIFIED_V1 | 2 | 0 | Task74:89 per-UC HTTPS/SQL checks; IN_PROGRESS/explicit PAUSED current assignee, immutable completion criteria/results, private evidence, rowversion/audit/expiry rollback, real race/DMV cancellation; no fake time or auto-completion; §22 |
| UC-110 | Pause/Resume Task | T | BLOCKED_EXTERNAL | 5 | 14 | Pause/resume opens/closes intervals; no interval model |
| UC-111 | Record Actual Working Time | W | BLOCKED_EXTERNAL | 5 | 24 | WorkInterval/TimeCorrectionRequest + permission mapping not implemented; DEV2 time integration |
| UC-112 | Complete Assigned Task | T | BLOCKED_EXTERNAL | 5 | 14 | Complete -> Pending Verification + close interval + criteria/evidence missing mapping |
| UC-113 | Review Task Workload and Time Logs | W | BLOCKED_EXTERNAL | 2 | 12 | Effort from intervals not ShiftAttendance duration; model missing |
| UC-138 | Manage Operational Workday and Shift Records | W | NOT_IMPLEMENTED | 3 | 12 | ShiftTemplate/WorkShift/Assignment exist; service/conflict rules/authorization absent |
| UC-139 | Record Workday Start and End | W | BLOCKED_EXTERNAL | 5 | 20 | Workday including Unscheduled and Pause All; dedicated model absent |
| UC-140 | Manage Operational Shift Handover | W | BLOCKED_EXTERNAL | 5 | 24 | Snapshot + receiver ack/reject model missing; cross-module open work |
| UC-114 | Create Internal Request | Q | NOT_IMPLEMENTED | 3 | 10 | Create request + receiving branch department + notifications/audit |
| UC-115 | Route and Assign Internal Request | Q | NOT_IMPLEMENTED | 5 | 14 | Route/assign + authoritative handler reread + Users lock + notification |
| UC-116 | Accept/Reject Internal Request | Q | NOT_IMPLEMENTED | 3 | 8 | Accept/reject + scoped DH policy + atomic decision/audit |
| UC-117 | Update Internal Request Progress | Q | NOT_IMPLEMENTED | 2 | 8 | Progress/deadline change + reason/notification/audit |
| UC-118 | Create Linked Operational Work | Q | BLOCKED_EXTERNAL | 5 | 20 | Generic mandatory linked work contract/storage absent; DEV2 Incident/MR adapters |
| UC-119 | Record Request Handover | Q | BLOCKED_EXTERNAL | 5 | 16 | Acknowledged handover must retain sender until receiver accepts; model absent |
| UC-120 | Complete Internal Request | Q | BLOCKED_EXTERNAL | 5 | 16 | Close requires mandatory linked work closed/no pending handover; model/contract required |
| UC-121 | Reopen Internal Request | Q | BLOCKED_EXTERNAL | 5 | 12 | Reopen window/cycle + last handler eligible; approved policy needed |
| UC-122 | Escalate Overdue Internal Request | Q | BLOCKED_EXTERNAL | 3 | 12 | Escalation alert requires acknowledgment; DEV2 notification contract/storage dependency |
| UC-123 | View Internal Request History | Q | NOT_IMPLEMENTED | 2 | 10 | History includes withdraw write and authorized linked work; not read-only list alone |
Shared, không tự nhận toàn bộ công DEV1: **UC-063 Manage Work Categories (2 điểm/6h)** và **UC-064 Manage Operational Reference Data (2 điểm/6h)**: NOT_IMPLEMENTED; WorkCategory/ReferenceValue exist, no CRUD controller/service/DTO/auth/tx/audit/tests. DEV1+DEV2 phải chỉ định owner/catalog mutability/consumers; GLOBAL Users→feature protocol khi quản lý references. Hai UC này nằm ngoài mẫu số DEV1 độc quyền; sensitivity có tính cả hai ở dưới.

Một số rules làm tăng effort dù table tồn tại: GBR005 concurrency; GBR007 atomic side effects/audit; GBR013 SoD; GBR124 recurring JOB05; GBR138–140 intervals/corrections/effort; GBR142 unscheduled day; GBR144 shift snapshot/ack; GBR150–153 linked work/ack transfer/closure/reopen. Không dùng ShiftAttendance thay Workday/interval hoặc JSON/text events làm model mặc định. JOB05 nằm trong UC102, không cộng UC mới; JOB01/notification delivery phối hợp FE14 DEV2, không tự giao notification/reporting/inventory cho DEV1.

## 3. Tiến độ và độ bất định

Các số trong §3–§8 là **baseline Task59 lịch sử**; bảng UC ở §2 đã cập nhật tới Task68.
Snapshot Task62 ở **§10**, Task63 ở **§11**, Task64 ở **§12**, Task65 ở **§13**, Task66 ở **§14**, Task67 ở **§15**, hiện tại Task68 ở **§16**; không cộng gain hai lần vào kế hoạch gốc.

| Thước đo | DEV1 độc quyền | Nếu DEV1 nhận cả hai shared UC |
|---|---:|---:|
| Tổng UC / weighted scope | 75 / **256** | 77 / **260** |
| COMPLETE_VERIFIED | **4 UC / 12 điểm** | 4 / 12 |
| PARTIAL | **5 UC / 22 điểm full-UC chưa earned** | 5 / 22 |
| IMPLEMENTED_UNVERIFIED | **0 / 0 điểm** | 0 / 0 |
| NOT_IMPLEMENTED / BLOCKED_EXTERNAL | **44 / 22 UC** | 46 / 22 UC |
| Còn lại tới full verified | **244 điểm** | 248 điểm |
| Verified completion | **12/256 = 4.69%** | 12/260 = 4.62% |
| Estimated implemented equivalent (không phải verified) | **24.25/256 = 9.47%** | 24.25/260 = 9.33% |
| Mốc 80% / điểm mới phải earned | ceil(204.8)=**205 / +193** | ceil(208)=208 / +196 |
| Mốc 85% / điểm mới phải earned | ceil(217.6)=**218 / +206** | ceil(221)=221 / +209 |

Weight: simple CRUD/read=1; moderate=2; stateful=3; cross-module/concurrency-critical=5. Rule-driven complexity chứ không lấy số method. Partial implementation estimate: UC004 80%×5=4; UC006 50%×5=2.5; UC007 35%×5=1.75; UC010 50%×5=2.5; UC141 75%×2=1.5; cộng 12 verified =24.25. Đây là engineering judgment khoảng **8–12%**, không “12.25 implemented-unverified full UC points”. Verify toàn UC mới earned full weight; không giảm mẫu số vì blocked/deferred.

Theo bảng: **790 giờ** còn lại cho UC độc quyền (bao gồm implementation, focused SQL/HTTP tests, local integration từng UC; các model mới chỉ sau approval). Cộng **24 giờ** cross-module end-to-end/release integration không trùng UC tests = **814 giờ trung tâm**, sensitivity **611–1,221 giờ** (−25%/+50%). Nếu nhận cả shared: **826 giờ** trung tâm. Chưa bao gồm thời gian chờ product/schema/DEV2, frontend, hosting, data migration thực tế. 126h chỉ bằng 15.5% phần effort trung tâm; AI parallelism không nhân budget nhân lực 126h. Không suy tuyến tính points/hour để hứa 80–85%.

## 4. Backlog 21 ngày — mỗi session tối đa 6 giờ

Effort trong tasks dưới đây đã bao gồm focused tests; cột session chia nhỏ review, không tạo project Application/Domain/Infrastructure. `Gain` chỉ ghi khi **toàn bộ UC** đạt DoD; lắp DTO/query slice nhận 0 điểm. Không cộng lại points của task tích hợp. Tất cả SQL chỉ trên disposable canonical trial có startup schema guard, không HotelOpsDB; approval production schema là task riêng.

| ID / ưu tiên | Module / UC | Session / giờ | Dependency | Acceptance cụ thể + SQL cần có | Gain mới |
|---|---|---:|---|---|---:|
| P01 | Room UC023 | Query+SQL 3; endpoint/full read flow 3 = **6** | Room model có; Task20A D3/D5/D7 và shared DI approval trước exposure | List search/filter/sort/paging + detail source reason/history; không đoán missing reason. SQL hai branches, nullable Area, inactive references, status history, branch denial/count leak, no tracking/no writes; HTTP session/grant/invalid filters | **2**, chỉ full flow |
| P02 | Access UC011 | 4+6 = **10** | Người duyệt chốt existing permission/scope/self/inactive-target/field whitelist của Task23; shared evaluator review nếu cần | Read target grant paths without spoofed principal; source grants/branches/departments/conditional permissions, feature filters. SQL expired/revoked/multiple paths, no branch-role stitching, denied report/no count leak | **3** |
| P03 | Accounts UC141 | 2+4 = **6** | P02; UC005 link đã có | Full detail roles/access drilldown; preserve list contract and Global USER_VIEW grant. SQL/HTTPS detail/list/read-scope regression, secrets absent | **2** |
| P04 | Structure UC014 | 3+3 = **6** | P05 + approve Floor/Area create contract | Create both Floor/Area paths; SQL branch mismatch, nullable FloorId, duplicate race, permission denial/audit rollback | **2** |
| P05 | Shared design/enabler, 0 new UC | 4+4 = **8** | Explicit AGENTS approval; choose GBR005 strategy/schema rollout separately | Only approved reusable structure concurrency/validation/audit primitives; no invented rowversion. SQL stale-write/rollback proof, catalog preserved. If schema cannot be approved, stop writes; do read fallback | **0** |
| P06 | Structure UC012 | **6** | P05 + MASTER_DATA_MANAGE existing contract + branch code uniqueness rules | Update branch DTO validation + stale/uniqueness/atomic audit; SQL simultaneous update, actor revoked, audit failure rollback, immutable catalog | **2** |
| P07 | Structure UC013 | **6** | P04 + approved read mapping/tree shape | Full branch/floor/area/department structure and filters, historical/inactive policy; SQL scope/tree integrity, paging, no N+1 or branch leak | **2** |
| P08 | Structure UC015 | **6** | P04/P05 + branch existence/scope | Floor/Area update strict branch consistency/duplicate guard; SQL stale/races/audit rollback and permission revocation | **2** |
| P09 | Structure UC016 | 4+4 = **8** | P08 + approved deactivation refs/concurrency | Deactivate Floor/Area safely; SQL stale version, room references deny, audit failure rollback; no rooms moved silently | **3** |
| P10 | Structure UC017 | **6** | Global RoomType semantics confirmed for create; P05 | Create global type no invented BranchId, uniqueness/capacity; SQL duplicate race + audit rollback + scoped denial | **2** |
| P11 | Structure UC019 | 4+4 = **8** | P10 + existing rooms/history deactivation policy | Deactivate without historical corruption; SQL references/stale/audit rollback and concurrent room create guard | **3** |
| P12 | Structure UC024 | **6** | P05 + branch/department policy, DH branch-wide unchanged | Create department + code uniqueness/scope/audit; SQL duplicate/cross-branch/revoked actor/rollback | **2** |
| P13 | Structure UC025 | **6** | P12 + manager reference policy | Update department no implicit reassignment; SQL stale/manager inactive/crossbranch/audit rollback, existing auth tests preserved | **2** |
| P14 | Structure UC020 | **6** | P04/P05/P10; no lifecycle transition invented | Create room only with validated compatible branch/floor/area/type; SQL composite consistency/duplicate room number/race/audit rollback | **2** |
| P15 | Structure UC021 | **6** | P14 + approved editable fields/reference guards | Room update no arbitrary occupancy/state change; SQL stale/invalid linked IDs/audit rollback and reference preservation | **2** |
| P16 | Decision review + final integration | **4+4=8** | Decisions early; final after P01–15 | First4h close exact contracts below; final4h targeted end-to-end/authorization/rollback/build review. No feature point twice; no deferred acceptance claimed | **0** |

P01–P04, P06–P15 = **92h**; P05=8h; P16=8h; **108h planned +18h defect buffer=126h**. Maximum new earned points =31; baseline12→**43/256 =16.80%**; completed4→**18/75 UC**. Đây là conditional stretch về số UC cho cơ cấu, không phải cam kết trước approval. Không đưa UC142 vào plan vì branch/global model còn chưa quyết định; không tính cleanup/test harness là UC.

Sau 21 ngày, ưu tiên tiếp: UC004 residual8h; UC010 residual14h (role/revoke surface approval); UC007 residual20h (transfer/current work); Cleaning/Inspection reads UC037/038/048 mỗi6h; checklist UC039/040 mỗi8h; rồi một housekeeping vertical slice UC028/030/031/034 gắn approved workday/time contract, không chỉ tạo các endpoint rời rạc. UC006 chỉ quay lại khi business/release dependencies thật sự đóng. Không đầu tư thêm audit lặp lại trong 126h.

### Quyết định gói gọn cần review đầu kỳ, không mở thêm safety gates

1. **UC011:** đề xuất Global-only existing USER_VIEW với unbound ALL, same reviewer permission khi self, target inactive được xem cấu hình nhưng đánh dấu effective/eligibility riêng, field whitelist không credential/session. Đây là đề xuất để người duyệt chọn, **chưa áp dụng**; Task23 nói rõ không tự chọn USER_VIEW thay ROLE_MANAGE. Nếu không chấp thuận, P02/P03 giữ blocked.
2. **UC023:** đề xuất current catalog ROOM_VIEW/Branch, không SYS_ADMIN/Owner bypass; union authorized active branches nếu UI All Branches được duyệt; trả stored status code và source/history có bằng chứng, không invent lifecycle label/reason; paging/sort/error contract theo Task20A. D3/D5/D7 phải quyết định trước route/DI. List-only không tính full UC023.
3. **Structure:** chốt MASTER_DATA_MANAGE cho admin mutations và read/tree mapping D1/D4/D7; chọn GBR005 concurrency design cho non-rowversion roots. Không tự thêm token/cột/permission; schema approval nếu cần là change-control riêng và có thể đẩy writes khỏi kỳ này.
4. **RoomType:** xác nhận catalog global + per-branch usage view hay branch-owned business model; UC142 chưa được code khi thiếu quyết định. UC017/019 chỉ làm khi global CRUD semantics được xác nhận; UC018 update nằm ngoài kỳ này.

Nếu hết D2 chưa có approvals, không ngồi chờ 21 ngày hoặc tự mở API: chuyển buffer lên trước; làm P01 internal read query/SQL technical proof, approved existing account regression automation và prepare focused fixtures; **0 new full-UC points cho các technical slices**. Trình owner chọn scope/tasks đã được duyệt hoặc điều chỉnh thời gian/nguồn lực. Không hứa một điểm số fallback giả khi mọi planned read mapping còn pending.

## 5. Lịch 21 ngày, 6 giờ/ngày

Giả định bắt đầu 31/10/2026, kết thúc 20/11/2026, calendar days bao gồm cuối tuần; đổi ngày bắt đầu chỉ dịch lịch. I=implementation, T=focused testing, G=contract/integration/review, B=defect buffer. Giờ task ở backlog là tổng I+T+G, không cộng test thêm lần nữa. Các writes bắt đầu phần P04 ở D6 phụ thuộc P05/approval; tuyệt đối không migration tự động. Ngày không phân bổ T riêng vẫn phải hoàn tất focused verification trong task hoặc final review trước earned points; không xem code-only day là SQL PASS.

| Ngày / ngày lịch | Target (giờ task) | I | T | G | B | Mốc earned khi đạt đủ DoD |
|---|---|---:|---:|---:|---:|---|
| D01 / 31-10 | P01 Room023 (6), query3 trước; endpoint3 chỉ sau approval | 4 | 2 | 0 | 0 | +2 nếu full UC023 |
| D02 / 01-11 | P02 access (6); quyết định chưa có thì không expose | 4 | 1 | 1 | 0 | Chưa earned UC011 |
| D03 / 02-11 | P02 (4) + P03 (2) | 4 | 2 | 0 | 0 | +3 UC011 |
| D04 / 03-11 | P03 (4) + P16 decisions (2) | 4 | 1 | 1 | 0 | +2 UC141 |
| D05 / 04-11 | P05 approved enabler (6) | 2 | 2 | 2 | 0 | 0 |
| D06 / 05-11 | P05 (2) + P16 decisions (2) + P04 Floor/Area create (2) | 4 | 1 | 1 | 0 | 0 |
| D07 / 06-11 | P04 (4) + buffer (2) | 2 | 0 | 2 | 2 | +2 UC014; **21/256=8.20%; 8 completed UCs** |
| D08 / 07-11 | P06 branch update (6) | 4 | 1 | 1 | 0 | +2 UC012 |
| D09 / 08-11 | P07 branch structure (6) | 4 | 1 | 1 | 0 | +2 UC013 |
| D10 / 09-11 | P08 Floor/Area update (6) | 4 | 1 | 1 | 0 | +2 UC015 |
| D11 / 10-11 | P09 Floor/Area deactivate (6) | 4 | 2 | 0 | 0 | Chưa earned UC016 |
| D12 / 11-11 | P09 (2) + P10 RoomType create (4) | 4 | 2 | 0 | 0 | +3 UC016 |
| D13 / 12-11 | P10 (2) + P11 type deactivate (4) | 4 | 1 | 1 | 0 | +2 UC017 |
| D14 / 13-11 | P11 (4) + buffer (2) | 2 | 2 | 0 | 2 | +3 UC019; **35/256=13.67%; 14 completed UCs** |
| D15 / 14-11 | P12 department create (6) | 5 | 1 | 0 | 0 | +2 UC024 |
| D16 / 15-11 | P13 department update (6) | 5 | 1 | 0 | 0 | +2 UC025 |
| D17 / 16-11 | P14 room create (6) | 5 | 1 | 0 | 0 | +2 UC020 |
| D18 / 17-11 | P15 room update (6) | 5 | 0 | 1 | 0 | +2 UC021 subject final SQL below |
| D19 / 18-11 | P16 final SQL/integration (4) + defects (2) | 0 | 2 | 2 | 2 | Review all receipts; no failed test carried as complete |
| D20 / 19-11 | Defect buffer / rework / blocked-task replacement | 0 | 0 | 0 | 6 | Không cam kết gain buffer |
| D21 / 20-11 | Defect buffer / final delivery review | 0 | 0 | 0 | 6 | **43/256=16.80%; tối đa 18 completed UCs** |
| **Tổng** | **108 planned +18 buffer** | **70** | **24** | **14** | **18** | **126 giờ** |

Critical path: approved read/security contracts → P01/P02 → P03; structure/concurrency approval → P05 → floor/type foundations → reference-safe CRUD → final SQL review. Department chain P12→P13 and room chain P04+P05+P10→P14→P15 có thể tách thực hiện nhưng integration tuần tự. Trước phần P04 ở D6 là go/no-go writes; D7 review lại: không có approved concurrency thì chuyển sang reads/evidence, không clear gates bằng build.

Fallback: (a) chỉ ba read/account UCs UC023/011/141 được duyệt và hoàn tất: +7 points, **19/256=7.42%**; (b) chỉ UC023 full được duyệt: **14/256=5.47%**; (c) không duyệt external contracts: giữ baseline **12/256=4.69%**, báo technical slices separately. Nếu mục tiêu bắt buộc80–85%, cần thay đổi thời hạn/nguồn lực hoặc **phê duyệt một scope khác với mẫu số công khai**, không gọi scope subset là toàn DEV1. Mốc 814h là estimate full scope, không là cam kết approved final SRS.

## 6. Phân việc cho Codex trong task triển khai sau

Không tạo agent/worktree trong Task59. Parallelization chỉ sau explicit approval checkout và file ownership; tổng 126h vẫn là tổng effort người review/implementation, không 126h cho mỗi agent.

| Lane đề xuất | Ownership file riêng | Dependency / integration |
|---|---|---|
| A: P01 rooms read | Controllers/RoomsController.cs; Services/Rooms/RoomDirectory*; DTOs/Rooms/Directory*; verification/RoomDirectory | Disposable DB riêng; không sửa Inventory/Maintenance/RoomBlock writers. Merge/review tuần tự sau contract approval |
| B: P02/P03 access account | Services/Users/UserAccessReview*; DTOs/Users/Access*; riêng lần tích hợp UsersController/DetailDto | Không chạy cùng agent sửa account files; shared evaluator/API visibility review riêng; không fake CurrentUser |
| C: P04/P06–P09/P12–P13 structure | Services/HotelStructure/; DTOs/HotelStructure/; StructureControllers; verification/HotelStructure* | Giao cụ thể floor/branch/dept files, không hai agent cùng sửa common service. P05 owner duy nhất |
| D: P10/P11/P14/P15 | RoomTypesController/Services/RoomTypes/DTOs; room mutation files khác lane A | Chờ type/floor/model approval; RoomsController chung lane A phải tích hợp tuần tự, không edit chồng |
| Integration owner | Program.cs, shared security, DbContext/schema nếu được duyệt riêng | Không lane nào tự sửa; tổng hợp diff/approval → build → targeted tests → receipts → review một thay đổi mỗi lần |

Mỗi agent ở isolated checkout được duyệt, artifact prefix và trial database khác nhau, canonical startup gate; không dùng existing rehearsal worktrees làm scratch, không xóa artifacts. DEV2 interface chỉ read/review/handoff, sửa module DEV2 do DEV2 thực hiện. Không chứng minh global race/deactivation acceptance bằng standalone User-lock probe.

## 7. Task code đầu tiên — có thể bắt đầu phần độc lập ngay

**P01a / UC023: internal Room Directory read query + real SQL query proof, 3h, không HTTP/DI.** Đây là phần đầu của P01 (6h), không một UC mới và **gain=0** tới khi P01b đạt full approved flow. Chọn trước UC011 vì model Room.branchId đã có và query không cần chọn permission reviewer đang unresolved. Task59 chưa thực hiện P01a.

- Read existing [Room](../../src/HotelOps.Api/Entities/Room.cs), [RoomStatusHistory](../../src/HotelOps.Api/Entities/RoomStatusHistory.cs), [RoomBlock](../../src/HotelOps.Api/Entities/RoomBlock.cs), Branch/Floor/Area/RoomType và DbContext. Không truy vấn production; không suy diễn status lifecycle/release hay assignee từ historical refs.
- Proposed **new** files: `src/HotelOps.Api/Services/Rooms/RoomDirectoryQuery.cs`, `src/HotelOps.Api/DTOs/Rooms/RoomDirectoryProjection.cs`, `scripts/verification/RoomDirectory/` focused query SQL harness. Internal projection/query input, chưa phải approved public API DTO. Không sửa generated mappings, Program, resolver hay DEV2; controller mới chỉ ở P01b sau approval.
- Scope ngay: async AsNoTracking projection theo explicit branch selector, deterministic floor/room order, nullable joins, count/page; raw stored status + source/history references đã lưu, không tự tính occupancy hoặc reason thiếu. Query là internal primitive, không tự authorize và không được gọi bởi HTTP tới khi P01b có verified permission boundary.
- DoD P01a: canonical disposable trial + startup Compatible; two-branch/duplicate room-number/sort-tie/null-Area/inactive-reference fixtures, scoped SQL filtering/count, correct history/reference projection, cancellation, no persistent mutation. Không coi branch parameter là authority. Build solution sau C# edits; test solution nếu có relevant discovered tests; thực thi harness riêng và ghi receipts, git diff --check. Chưa được gọi UC023 PASS.
- Dependency P01b 3h: approve Task20A D3/D5/D7 + full detail/history contract và shared DI impact; new `Controllers/RoomsController.cs`, final response/query DTOs và authorized service registration. Use existing Branch:ROOM_VIEW handler với server-side permission decision trước count/query, no admin/Owner fallback, no current-user spoofing. SQL+HTTPS revoked/expired/restricted/session/grant scope/filter injection/count-leak tests; no-store và correct status codes. Nếu 3h estimate không đủ cho full reason/history/auth, dùng buffer hoặc dời earned UC; không rút DoD thành list-only.

## 8. Validation và release limits

- Task58 historical source evidence: [writer acceptance](UserAccountDeactivation/TASK58_DEV1_WRITER_ACCEPTANCE.md), [accepted receipt](UserAccountDeactivation/task58-writers-accepted.local.json): 112 SQL PASS, 83 observed waits, 12 operations. Profile329/reauth165/other suites trong report là scoped regression. Build Task58 PASS, 1 NU1900 vulnerability-feed warning; không che warning. `dotnet test` Task58 exit0 không có discovered test project trong solution, không gọi là unit tests PASS.
- Task59 **build/test/API/SQL: NOT RUN** vì read-only audit/docs, không thay C#. Không khẳng định COMPLETE_VERIFIED đồng nghĩa rerun mọi alternative/production deployment hôm nay; đây là mức credit từ production implementation + relevant existing executable evidence, có regression risk khi code đổi.
- Validation Task59 **PASS**: 75 UC đúng scope, 256 points/790 UC-hours, 21×6=126h, 30 local links hợp lệ; **384/384 checksums giữ nguyên**, index/HEAD/bốn worktrees giữ nguyên; `git diff --check` và cached check exit0. Git vẫn báo sáu LF→CRLF notices trên các source có trước task; không rewrite chúng. Chi tiết trong ignored `obj/task59-validation.local.json`; không sửa Task51–58 artifacts để thêm verdict.
- UC006 vẫn PARTIAL: runtime contracts/release acceptance giữ nguyên; không endpoint/SA57/gate bypass. DEV2 full integrated/global lock-order acceptance không được suy từ Task58 standalone/fault suites. Future account-revoke/structure/room mutations phải bổ sung actual producer races trước release khi applicable.

**Delivery verdict: kế hoạch tăng tốc có thể thực thi theo từng approval; mục tiêu80–85% toàn DEV1 trong126h không được workload hỗ trợ.** Ưu tiên kế tiếp: P01 Room023; P02/P03 Access011+User141; P04 floor/area create; P05/P06–P15 approved structure CRUD; sau đó hoàn thiện account004/010 và housekeeping vertical slice thay vì tiếp tục UC006 audit.

## 9. Task61 update — 30/10/2026

| UC | Previous status | New status | Evidence / gain |
|---|---|---|---|
| UC141 (weight2) | PARTIAL | **PARTIAL** | [Task61 acceptance](UserList/TASK61_UC141_ACCEPTANCE.md): actual Program.cs HTTPS+SQL184 PASS/8 DMV waits; Detail234/Profile329 regression PASS; auth foundations167 PASS. **Gain0**, vì full step6 roles/effective permissions phụ thuộc UC011 chưa được duyệt |

Đã sửa actual read-boundary gap: UsersController-only filter retains shared Users namespace
trước Session/grants/projections, reuses SqlSessionPolicy và Global USER_VIEW, consistent
count/page trong request, final expiry/revocation checks trước serialization. Không thêm fields,
BRANCH/OWN visibility, DI/middleware/catalog/schema hoặc public access-report endpoint.

Verified baseline giữ **4 COMPLETE_VERIFIED / 12 points, 12/256=4.69%**; UC141 vẫn nằm trong
5 partial của Task59. Không cập nhật tiến độ bằng assertion count hoặc reliability work.
Task60 UC023 internal list có SQL evidence nhưng chưa full HTTP/detail/reason/history acceptance;
không cấp verified points cho UC023, không dùng sự thay đổi taxonomy của internal slice để đổi
mẫu số75/256. Estimated implementation equivalent giữ24.25/256=9.47%; mốc80/85 vẫn cần thêm
193/206 verified points như baseline.

Measured elapsed Task61 từ preflight đến report snapshot: **26.4 minutes**,
bao gồm tool/test waits, không phải logged human-hours; không trừ trực tiếp khỏi estimated
790 UC-hours còn lại. Runtime/source/artifact preservation và build/test details ở acceptance.

**Next implementation priority: UC011 → finish UC141 step6**, sau khi chốt existing permission,
reviewer scope/self/inactive-target/field visibility. List/detail contract hiện tại không cho phép
tự mở effective access report. Giữ UC141 PARTIAL tới khi dependency được triển khai và toàn UC
đạt acceptance; không tiếp tục tạo internal-only feature chỉ để tăng số file.

## 10. Task62 update — 30/10/2026

Task62 cho phép chọn best-fit V1 khi requirement im lặng, ghi **ASSUMED_V1**, không invent grants
hoặc giảm security. [UC011 report](AccessReview/TASK62_UC011_IMPLEMENTATION.md) ghi rõ USER_VIEW+
ROLE_MANAGE, global/branch scope intersection, clipped report, self cùng gate, conditional OWN,
assigned vs effective và per-role provenance. Không role-name bypass/catalog/schema change.

| UC | Previous | Current | Evidence / verified gain |
|---|---|---|---|
| UC011 (weight3) | BLOCKED_EXTERNAL | **COMPLETE_VERIFIED (V1)** | Production GET /api/users/{id}/effective-access; SQL reviewer scope and bulk report; +3 |
| UC141 (weight2) | PARTIAL | **COMPLETE_VERIFIED (V1)** | Step6 GET /api/users/{id}?includeAccess=true dùng cùng UC011 projection; original list/minimal-detail/filter/sort/page preserved; +2 |

[Final HTTPS/SQL receipt](AccessReview/task62-accepted-final.local.json): **293 PASS, 16 observed SQL
waits** (8 UC011 +8 existing Task61 boundary). Detail234/Profile329 regression PASS. Auth foundations
Permission43/Authorization61/Resource49/Hardening21 =174 PASS (doubles rõ phạm vi; no --live).
Build solution/harness PASS; NU1900 vulnerability-feed warnings còn. dotnet test exit0 không có
discovered tests; các console suites đã chạy thực tế, không suy unit-test PASS từ exit0.

Current exclusive DEV1 snapshot: **6 COMPLETE_VERIFIED, 4 PARTIAL, 44 NOT_IMPLEMENTED,
21 BLOCKED_EXTERNAL =75 UC**. Verified **17/256=6.64%** (+5 points); remaining239.
Estimated implemented equivalent =24.25−1.5+2+3 =**27.75/256=10.84%**, không phải verified.
80% cần205 total/+188 verified points; 85% cần218 total/+201. Shared scope denominator260
không đổi (17/260=6.54%). Không cấp điểm UC023 từ internal-only SQL và không giảm scope SA57.
UC011/141 rows giảm tổng estimated UC-hours còn lại từ790 xuống774; đây là backlog estimate,
không phải tiết kiệm nhân lực hay actual logged hours. Kế hoạch lịch sử §4–§8 chưa được rebaseline.

Production list vẫn Global USER_VIEW; branch support mới chỉ dedicated Access Review với cả hai
permission, không biến list filter thành authority. OWN reviewer chưa có mapping/grant phù hợp,
fail closed. Future SRS/Reception/DH revisions và deployment/load/principal acceptance vẫn riêng;
không unresolved mandatory requirement trong V1 đã chọn. UC006 runtime/release gates không đổi,
không HTTP Deactivate hay SA57 activation, không DEV2/source/schema change.

**Next implementation: UC014 Create Floor/Area**, bắt đầu contract/actual branch-permission/schema/
uniqueness/audit/concurrency extraction rồi triển khai production service + targeted SQL/HTTPS.

Measured Task62 preflight-to-validation elapsed **14.8 minutes**, bao gồm tool/test waits,
không phải human/billable hours hoặc chứng minh lại estimate toàn backlog. Preservation PASS:
398/406 file giữ nguyên, 8 edits đúng phạm vi có recoverable originals, 406 backup hashes hợp lệ;
HEAD/index/branch/bốn worktrees không đổi. Chi tiết `obj/task62-validation.local.json`.

## 11. Task63 update — UC014 production acceptance, 30/10/2026

**UC014 NOT_IMPLEMENTED → COMPLETE_VERIFIED_V1**, cả Floor và Area. Hai POST production
`/api/branches/{branchId}/floors` và `/api/branches/{branchId}/areas`; DTO riêng, MASTER_DATA_MANAGE
SQL global/branch scope, active/same-branch parent, normalized branch-unique codes, retained
User→session/grants→Branch→Floor locks, atomic CREATE audit và final authority check.
Assumptions V1/HTTP/errors trong [Task63 report](HotelStructure/TASK63_UC014_IMPLEMENTATION.md).
Không thêm grant/schema/entity/migration hoặc sửa DEV2. Branch lock bảo vệ shared Floor/Area code
namespace; existing DB unique indexes được kiểm thử độc lập. Duplicate name không tự bị cấm.

| Evidence | Result |
|---|---|
| [UC014 final production HTTPS + SQL](HotelStructure/task63-accepted-final.local.json) | **215 PASS /23 DMV waits**; cả hai operations, concurrent duplicates, parent deactivate/delete races, revocation/expiry/cancel, audit rollback, actual SQL1222/-2/1205, explicit fresh retries |
| [Room Directory regression](HotelStructure/task63-room-regression.local.json) | **117 PASS /11 SQL waits**, internal SQL scope, UC023 vẫn PARTIAL |
| Auth foundations | Permission43/Authorization61/Resource49/Hardening21 =**174 PASS**; doubles ghi rõ, không main-DB --live |
| Build / test / diff | Solution+harness builds PASS; NU1900 vulnerability-feed warning còn. dotnet test exit0/no discovered tests; explicit suites đã chạy. Diff/whitespace/preservation checks trong ignored obj |

Verified gain **+2**, từ17 lên **19/256=7.42%; 7/75 COMPLETE_VERIFIED**. Full scope còn237
verified points. 80% cần205 total/+186; 85% cần218 total/+199. Shared denominator260 giữ nguyên
(19/260=7.31%). Không credit cho SQL wait/assertion count, fixtures hoặc remediation riêng.

UC023 **vẫn PARTIAL như Task60 và context Task63**: chỉ internal query, chưa full production
HTTP/detail/current reason/history. Dòng BLOCKED_EXTERNAL còn sót từ Task59 được đồng bộ thành
PARTIAL, **không tăng verified points**. Current taxonomy: **7 complete /5 partial /43 not implemented
/20 blocked =75 UC**. Đây là taxonomy reconciliation của slice đã có, không phải UC023 mới hoàn tất.
Estimated implemented equivalent theo mô hình cũ29.75/256=11.62% tách riêng verified; không cộng
fraction mới cho UC023. UC014 row giảm estimated UC-hours774→768, không suy actual staffing savings
hoặc rebaseline lịch sử §4–§8 từ wall-clock một task.

HTTP tests dùng root Program.cs/DI/middleware, real SQL login/session và CSRF. Direct SQL fault cases
dùng cùng production service và persisted fixture sessions; Audit THROW/intentional inverse deadlock
chỉ nằm trong harness, không production hooks/DDL. Không dùng chúng để đóng UC006/global acceptance.
Middleware session touches/login metadata vẫn bật; POST-only full User snapshot không thay đổi.

**Next implementation: UC013 View Branch Structure**, full scoped branch/floor/area/room/department
tree/selectors với SQL/HTTPS, không chỉ các lists. UC015 update cần kiểm tra concurrency/version
contract với schema Floor/Area thực tế; không tự thêm production rowversion migration.

UC006 runtime/release gates giữ nguyên; không HTTP Deactivate/SA57, không HotelOpsDB hoặc Git mutations.

Measured Task63 preflight-to-validation elapsed **26.4 minutes**, gồm tool/test waits, không phải
human-hours hoặc chứng nhận tốc độ toàn backlog. Preservation PASS:417/419 file nguyên vẹn,
2 intended edits có recoverable originals, 419 backup hashes đúng; HEAD/branch/upstream/index/bốn
worktrees giữ nguyên. Chi tiết ignored `obj/task63-validation.local.json`.

## 12. Task64 update — UC013 production acceptance, 30/10/2026

**UC013 NOT_IMPLEMENTED → COMPLETE_VERIFIED_V1**, gain **+2**. Production
`GET /api/branches/{branchId}/structure` provides paginated physical tree expansion, selected-node
details, active/inactive filters and management department viewing. Nullable Area.FloorId and
required Room.FloorId follow actual schema; inconsistent/foreign links never leak. Root data query
is Branch +Count +Page, no N+1. No generated mapping/schema or business write.

**ASSUMED_V1 explicitly approved by developer:** ROOM_VIEW alone permits basic physical
Branch/Floor/Area/Room within SQL branch scope; no administrative descriptions/departments or write
rights. MASTER_DATA_MANAGE alone permits management read including departments. No requirement
to hold both; no new grants or role-name bypass. In-flight management expiry cannot leak management
data through a surviving basic grant. Existing SQL session/User-first authorization retained.
Full contract/examples/source list in [Task64 report](HotelStructure/TASK64_UC013_IMPLEMENTATION.md).

| Evidence | Result |
|---|---|
| [Production HTTPS/SQL receipt](HotelStructure/task64-accepted-final2.local.json) | **374 assertions PASS =215 UC014 +159 UC013**, 34 DMV waits =23 +11; hierarchy/scope/fields/paging/empty nodes, writer-first, actual UC014 count/page phantom, revocation/expiry/downgrade, real HTTPS cancel, SQL1222/-2/51064 and clean rollback |
| [UC023 regression](HotelStructure/task64-room-regression.local.json) | **117 PASS /11 waits**, separate disposable canonical trial, internal query only; **UC023 remains PARTIAL**, no verified gain |
| Auth foundations | Permission43/Authorization61/Resource49/Hardening21 =**174 PASS**, documented doubles/loopback scope, no main-DB --live |
| Build / test / diff | Solution +target harness builds PASS. NU1900 feed warnings remain, no C# warnings/errors. dotnet test exit0/no discovered test project; explicit console suites above executed. git diff --check PASS |

Current exclusive DEV1: **8 COMPLETE_VERIFIED (including V1) /5 PARTIAL /42 NOT_IMPLEMENTED
/20 BLOCKED_EXTERNAL =75 UC**. Verified **21/256=8.20%**, previously19/256=7.42%; remaining235.
80% needs205 total/+184; 85% needs218 total/+197. Shared scope unchanged260:21/260=8.08%.
Estimated implemented equivalent under historical model31.75/256=12.40% is separate from verified;
UC013 row reduces estimated remaining UC-hours768→762, not measured staffing savings or a rebaseline
of historical schedule §4–§8. No extra credit for assertions, fixtures, waits or UC023 internal read.

Final UC013 trial `HotelOps_Canonical_20261030_003457_da90ba3f_Fresh`; unchanged canonical sources,
approved guarded rowversion prerequisite and startup compatibility check precede all regressions.
Read transaction uses SERIALIZABLE locking, not SQL SNAPSHOT isolation; actual UC014 writer waits
until read completes, then persists once. Direct fault fixtures are separate from real HTTPS cases.
Early harness failures and all trials/logs are retained; no safety predicate or production behavior
was relaxed to pass. Local `obj/task64-*` records build/test/preservation evidence.

No mandatory blocker remains within the approved UC013 V1 boundary. Final SRS/catalog revisions,
deployment/load and future writer acceptance remain separate. No DEV2/source/schema/RBAC changes,
no UC006 gate/Deactivate/SA57 changes, no HotelOpsDB or Git mutations.

**Recommended next: UC015 Update Floor/Area.** First finalize GBR005 stale-write/version handling
against current schema (no mapped Floor/Area rowversion), then reuse UC014 validation/audit/lock order
and this read API. Do not introduce a schema migration or omit stale-write checks by assumption.

Preflight-to-preservation validation snapshot **26.6 elapsed minutes**, including tool/test waits,
not human-hours. Preservation PASS: **425/428 protected files unchanged,3 intended edits with all
428 backup hashes valid**; HEAD/branch/upstream/index/four worktrees unchanged. Originals, failed
attempts, disposable trials and prior artifacts retained; details `obj/task64-validation.local.json`.

## 13. Task65 update — UC015 production acceptance, 30/10/2026

**UC015 NOT_IMPLEMENTED → COMPLETE_VERIFIED_V1**, cả Floor và Area, gain **+2**.
Production `GET/PUT /api/branches/{branchId}/floors/{id}` và `/areas/{id}`: SQL MASTER_DATA_MANAGE
scope, Active target/branch, code/name/description/reason và Area parent hợp lệ, immutable IDs/branch/
status/FloorNumber. UC013 ROOM_VIEW read policy không đổi. Parent chuyển không làm thay Room/Asset
references; incompatible Room.FloorId bị409. Legacy foreign-parent Area không được lộ qua edit GET.

**ASSUMED_V1 concurrency:** schema không có Floor/Area rowversion; strong ETag SHA-256 của full row
state +latest append-only entity audit identity, checked under retained branch/target locks. Atomic
save consumes revision cả no-op; A→B→A không phục hồi old token. Không thêm schema, timestamp giả,
blind overwrite hoặc automatic retry. Audit retention/future producers phải giữ anchor/lock protocol;
không chứng nhận arbitrary unaudited privileged SQL. [Contract/source/HTTP examples](HotelStructure/TASK65_UC015_IMPLEMENTATION.md).

| Evidence | Result |
|---|---|
| [Final real HTTPS/SQL receipt](HotelStructure/task65-accepted-final2.local.json) | **589 assertions PASS =215 UC015 +215 UC014 +159 UC013**; **53 DMV waits =19 +23 +11**. Success/validation/scope/ETag/ABA, no lost update, duplicate/create-update races, parent-state race, revocation, actual HTTPS cancel/expired session |
| Post-UPDATE safety | **14 schedules**, both Floor/Area: SQL audit THROW51065,1222,-2,real1205,cancel,session expiry,grant expiry. Exact row/parent/ETag/audit restored; fresh retry commits once, stale replay denied |
| [UC023 regression](HotelStructure/task65-room-regression.local.json) | **117 PASS /11 SQL waits**, internal query; **PARTIAL unchanged**, no verified gain |
| AuthN/AuthZ | **174 PASS**:43 Permission/61 Authorization/49 Resource/21 Hardening; documented doubles/loopback scope. Actual SQL login/cookie/session coverage in receipt above |
| Build / test / diff | Solution +target harnesses PASS; NU1900 vulnerability-feed warning remains, no C# warnings/errors. dotnet test exit0/no discovered tests; explicit console suites executed. git diff --check PASS |

Current DEV1: **9 COMPLETE_VERIFIED (including V1) /5 PARTIAL /41 NOT_IMPLEMENTED /20 BLOCKED_EXTERNAL
=75 UC**. Verified **23/256=8.98%**, previous21/256=8.20%, remaining233. 80% needs205 total/+182;
85% needs218 total/+195. Shared denominator260 unchanged:23/260=8.85%. No point for assertion/wait
counts or UC023 internal read. Historical estimated implemented equivalent33.75/256=13.18% remains
separate from verified; UC015 row reduces estimated remaining UC-hours762→756, not actual staffing
savings or schedule rebaseline.

Final trial `HotelOps_Canonical_20261030_005426_4f3d446c_Fresh`, canonical sources unchanged +approved
guarded trial rowversion prerequisite; startup compatibility checked first. Direct production-service
fault fixtures are distinguished from real HTTPS. Intentional inverse test blocker causes1205 only
to prove rollback/retry, not to close global Users→feature/UC006 acceptance. Failed first harness
attempt and successful intermediate/final receipts/trials preserved.

No mandatory UC015 V1 blocker remains. Final SRS/deployment/load, audit retention changes and future
producer acceptance remain separate. No HotelOpsDB, generated schema/entity, RBAC/DEV2 or UC006
release-gate changes; no HTTP Deactivate/SA57 or Git mutations.

**Recommended next: UC016 Deactivate Floor/Area**, implement exact GBR035 active Room/child Area/
Asset/open Incident/Task guards and coordinate current reference writers before mutation; reuse
conditional edit infrastructure without assuming all references are either historical-safe or blockers.

Preflight→preservation snapshot **16.3 elapsed minutes**, including SQL/tool waits, not human-hours.
**436/436 backup hashes valid;433 protected files unchanged;3 intended edits** with originals kept.
HEAD/branch/upstream/index/four worktrees unchanged. Source/new-file whitespace/local links and
receipts checked in ignored `obj/task65-validation.local.json`; no prior artifact removed.

## 14. Task66 — UC016 GBR035 and writer coordination (historical snapshot)

UC016 previous **NOT_IMPLEMENTED**, now **PARTIAL; mutation BLOCKED**. Production HTTPS POST for
Floor/Area validates SQL session/MASTER_DATA_MANAGE branch scope, confirmation, GBR012 reason,
strong If-Match and current dependencies.409 lists actual IDs/counts; empty target503 explicitly
requires writer coordination. No successful deactivation or atomic DEACTIVATE audit is claimed.
[Contract, source, precise handoff and evidence](HotelStructure/TASK66_UC016_IMPLEMENTATION.md).

[Final targeted receipt](HotelStructure/task66-accepted.local.json): **94 UC016 checks +2 startup checks**,
actual root HTTPS, SQL authorization and persisted fixtures. Two DMV-observed target-lock waits
exercise authoritative stale reread and cancellation. Two actual DEV2-service defect reproductions:
IncidentService accepts inactive Area; AssetService commits stale active-Area placement after an
independent fixture deactivation. These are **confirmed defects**, not writer acceptance PASS.

[Regression recheck](HotelStructure/task66-regression-recheck.local.json): UC014215+UC013159+UC015215
=589 PASS,53 waits/14 UC015 faults. UC023117/11 waits and Auth174 PASS. One preceding UC015
Area session-expiry run failed with SQL-2; unchanged sequential recheck passed, root cause unproven.
Solution build/diff PASS; NU1900 feed warning remains. UC016 mutation/fault acceptance BLOCKED.

Gain **0/3**; verified remains **23/256=8.98%,9/75 UC**; remaining233 points. Current taxonomy:
**9 COMPLETE_VERIFIED (including V1),6 PARTIAL,40 NOT_IMPLEMENTED,20 BLOCKED_EXTERNAL=75**.
UC023 classification remains PARTIAL. Weight, denominator, estimates and historical snapshots are
not reduced to reward rejection checks. UC016 remaining8h unchanged pending integration scope.

Next: DEV2 MoveAsync and CreateIncidentAsync location eligibility/retained-lock corrections,
MaterialRequest receiving-location coordination review, OperationalTask COMPLETED contract; then
conditional soft deactivation +atomic audit, success HTTPS and both race directions/fault suites.
No HotelOpsDB/schema/catalog/DEV2 source/UC006 release gate changes or Git mutation.

## 15. Task67 — UC023 completed HTTP read workflow (historical snapshot)

UC023 **PARTIAL → COMPLETE_VERIFIED_V1**. Reuses Task60 RoomDirectoryService, adds production
GET `/api/rooms`, `/api/rooms/{roomId}`, `/api/rooms/{roomId}/history`. Explicit ROOM_VIEW SQL
branch/global scope; no role-name exemption, bound-ALL is not global, OWN denied. Raw persisted
status, validated filters/sort/pagination, room details, recorded current reason and same-room
verified Task/Inspection/Block references, paginated history and working history link. Missing/
stale/untyped evidence is reported truthfully, not fabricated. [V1 contract and source](RoomDirectory/TASK67_UC023_COMPLETION.md).

| Evidence | Result |
|---|---|
| [Final production HTTPS/new SQL](RoomDirectory/task67-accepted.local.json) | **227 UC023+2 startup=229 PASS**,4 DMV waits. Actual API/login/cookie/SQL authorization; explicit global ALL tested using isolated trial-only roles, not production grants |
| [Existing SQL regression](RoomDirectory/task67-sql-regression.local.json) |117 PASS/11 waits; original query/session/count/page/rollback scope. Internal suite alone does not certify HTTP |
| [Structure regression](RoomDirectory/task67-structure-regression.local.json) |590 PASS=216 UC014+159 UC013+215 UC015;53 waits/14 UC015 faults. Per-created-row audit assertion count varies with fixture race winner |
| AuthN/AuthZ |174 PASS, documented existing doubles/loopback scope; live SQL permission coverage in HTTP receipt |
| Build/test/diff | Solution and targeted harnesses PASS; NU1900 vulnerability-feed warnings remain. dotnet test exit0/no test projects; console suites explicitly executed. Diff/new-file checks and preservation verified |

Gain **+2**, previous23/256=8.98% → **25/256=9.77%**, **10/75** verified UC. Remaining231 points;
80% needs205 total/+180,85% needs218 total/+193. Current taxonomy **10 COMPLETE_VERIFIED (including
V1),5 PARTIAL,40 NOT_IMPLEMENTED,20 BLOCKED_EXTERNAL=75**. Shared denominator260 unchanged:
25/260=9.62%. Historical fractional estimates are not reclassified as verified work. UC023 remaining
estimate6h→0; other estimates unchanged, not a claim of actual developer-hours saved.

UC016 **stays PARTIAL**, its Task66 runtime blockers and DEV2 dependency unchanged; no UC006 gate
or SA57 activation. No HotelOpsDB/schema/DEV2/catalog-source/Git mutation. Recommended next:
**UC017 Create Room Type**, using actual global RoomType schema and confirmed permission/validation/
audit/concurrency requirements; do not invent BranchId ownership for a global catalog.

## 16. Task68 — UC017 SRS-first branch implementation, candidate migration (historical snapshot)

UC017 **NOT_IMPLEMENTED → PARTIAL**, gain **0/2**. Developer explicitly rejected dropping branch,
standard cleaning minutes or default checklist to fit the legacy global schema, and authorized a
separate compatibility/rollback SQL candidate tested on disposable trials only. This supersedes
Task59/67's global-only implementation recommendation for UC017, not the historical schema facts.
[Contract, source, migration and release dependencies](HotelStructure/TASK68_UC017_IMPLEMENTATION.md).

Production POST `/api/room-types` requires branchId/code/name/standardCleaningMinutes/defaultChecklistId,
with existing MASTER_DATA_MANAGE SQL branch authority. No new global/NULL-branch write, no role-name
grant, no pricing or fake checklist. Candidate adds nullable branch/cleaning/default FK fields,
branch-code uniqueness and legacy-preserving constraints; before deployment UC017503 while legacy
reads remain compatible. Actual successful trial creates verify published real template/criterion,
persisted operational fields, atomic audit and no changes to original Room/RoomType rows. Room
Directory/structure readers exclude foreign-branch type bindings under the candidate schema.

| Evidence | Result |
|---|---|
| [Final UC017](HotelStructure/task68-accepted.local.json) |130 UC017+2 startup=132 PASS,10 DMV waits,7 post-INSERT audit/1222/-2/1205/cancel/session/grant fault schedules; fresh retry once/replay409 |
| Candidate forward/rollback | Idempotent forward preserves legacy IDs/scalars/Room FKs; empty candidate rollback restores original schema; populated rollback refuses data loss. Unknown/damaged schema fails closed; no auto DDL/deployment |
| [UC023 legacy SQL](HotelStructure/task68-legacy-read.local.json) |117 PASS/11 waits on **unpatched** schema |
| [UC023 scoped HTTPS](HotelStructure/task68-scoped-rooms.local.json) |227 UC023+2 startup=229 PASS/4 waits on migrated disposable schema |
| [UC013/014/015 scoped regression](HotelStructure/task68-scoped-structure.local.json) |589 PASS/53 waits/14 UC015 faults on migrated schema |
| Auth/build/test/diff |174 foundation checks PASS with documented doubles/loopback scope; live auth in HTTP receipt. Solution/harness builds PASS; NU1900 remains. dotnet test exit0/no test project; explicit suites executed. Final preservation/diff proof in obj/task68-validation.local.json |

Classification stays **PARTIAL until migration/deployment integration**, as explicitly required by
the developer. Review production rollout/backup/maintenance/rollback, Database First index/mapping
refresh, eligible real templates and any legacy ownership/code cutover; do not infer/backfill legacy
branch IDs. Candidate scripts reject HotelOpsDB and were never integrated into startup/release.
Verified **10/75,25/256=9.77% unchanged**, remaining231 points; taxonomy **10 COMPLETE_VERIFIED
(including V1),6 PARTIAL,39 NOT_IMPLEMENTED,20 BLOCKED_EXTERNAL=75**. No denominator/estimate
reduction or UC017 points while deployment is pending. UC016 remains PARTIAL; UC023 remains verified.

No HotelOpsDB, production/canonical schema change, generated-entity rewrite, DEV2 source change,
UC006 gate change, commit/push/merge/reset/clean/worktree deletion. Next: reviewed UC017 deployment
integration; then UC018 Update Room Type using the SRS branch/default-checklist and legacy contract.

## 17. Task69 — UC017 deployment integration and final acceptance (current snapshot)

UC017 **PARTIAL → COMPLETE_VERIFIED_V1**, gain **+2**. [Final report/source/contract/recovery](HotelStructure/TASK69_UC017_DEPLOYMENT_ACCEPTANCE.md).
The existing canonical runner has an explicit opt-in -Uc017 stage using a guarded deployment entry
point, transactional forward+strict postflight, version markers, SHA256 receipts and safe rollback.
No startup mutation, shared/production execution or destructive legacy conversion. Global records/
Room FKs remain valid; new types are branch-owned, and SQL rejects cross-branch new-type assignments.
Controlled read-only V1 metadata coexists with the six-column legacy projection; no generated-file
overwrite or DEV2 mapping change.

| Evidence | Result |
|---|---|
| [Actual canonical workflow](CanonicalDatabase/evidence/HotelOps_Canonical_20261030_181243_928419f1.json) | Fresh/RBAC Upgrade/Authentication Upgrade PASS; all module receipts, schema comparison, CHECKDB/CHECKCONSTRAINTS PASS |
| [UC017 production HTTPS/SQL](HotelStructure/task69-accepted.local.json) |163 UC017+2 startup=165 PASS;12 actual deployment schedules,10 DMV waits,7 post-INSERT audit/1222/-2/1205/cancel/session/grant fault cases; strict inputs, scoped creation/checklist/uniqueness, cross-branch rejection and atomic audit |
| Migration/legacy recovery | Real legacy RoomType/Room values/FKs preserved; repeat apply, empty rollback, real mid-DDL failure/full rollback/recovery, populated rollback refusal and populated reapply preserving business/audit rows PASS |
| [UC023 legacy SQL](HotelStructure/task69-legacy-read.local.json) |117 PASS/11 waits; all mapped table rows restored |
| [UC023 deployed HTTPS](HotelStructure/task69-scoped-rooms.local.json) |227 UC023+2 startup=229 PASS/4 waits |
| [UC013/014/015 deployed regression](HotelStructure/task69-scoped-structure.local.json) |590 PASS/53 waits/14 UC015 faults |
| Auth/build/test/diff |174 foundation checks PASS with documented doubles/loopback scope; live SQL auth verified by HTTP receipts. Solution/harness builds PASS,2 NU1900 warnings remain. dotnet test exit0/no test project; console suites executed. Preservation proof: obj/task69-validation.local.json |

Verified **11/75**, **27/256=10.55%**, previous25/256=9.77%; remaining229 points. Taxonomy
**11 COMPLETE_VERIFIED (including V1),5 PARTIAL,39 NOT_IMPLEMENTED,20 BLOCKED_EXTERNAL=75**.
Shared denominator260 unchanged:27/260=10.38%. To reach80% requires205 total/+178;85% requires218/+191.
UC017 remaining estimate6h→0 is a planning update, not measured hours saved. Historical estimates
and prior snapshots remain historical; no deferred workflow or UC006 release gate receives credit.

UC016 **stays PARTIAL** with existing DEV2 blockers; UC023 stays verified. Production rollout is
NOT RUN and requires separate reviewed target/backup/restore, the existing auth/schema prerequisites,
real eligible checklist data and paused writers. API trials apply the unchanged approved Task26
rowversion prerequisite explicitly; UC017 never migrates authentication or fabricates templates.
No HotelOpsDB access, DEV2 source change, generated-file overwrite, UC006 gate change, commit/push/
merge/reset/clean or artifact/worktree deletion. Next implementation: **UC018 Update Room Type**,
retaining branch/legacy/checklist scope, concurrency, atomic audit and reference safeguards.

## 18. Task70 — Hotel Structure Batch 1 (31/10/2026)

Supersedes the next-task recommendation in §17. [Batch report](HotelStructure/TASK70_BATCH1_IMPLEMENTATION.md)
records scope, all remaining Hotel Structure candidates, contracts, assumptions, source inventory,
SQL/HTTPS schedules and limitations. S70 = production controller/DTO + existing UC017 service DI,
SQL session/MASTER_DATA_MANAGE branch gates, SERIALIZABLE transaction/catalog lock, atomic audit
for writes and strong state/audit/count ETag for UC018. No schema/DI/catalog/generated/DEV2 edits.

| UC | Weight | Final classification | Independent acceptance |
|---|---|---|---|
| UC018 Update Room Type |2|COMPLETE_VERIFIED_V1|64 PASS:55 HTTP/persistence +9 final SQL session/grant/cancel assertions; code/checklist validation, immutable ownership, stale/concurrent If-Match, audit fault rollback and historical FK/version preservation |
| UC020 Create Room |2|COMPLETE_VERIFIED_V1|55 PASS:46 HTTP/persistence +9 final SQL assertions; valid branch/floor/area/type, DIRTY/INIT history/audit atomically, duplicate race and rollback |
| UC142 View Room Types List |1|COMPLETE_VERIFIED_V1|49 PASS:40 HTTP/persistence +9 final SQL assertions; scope/search/status/sort/pagination/count/detail, legacy global-only read and no writes |

[Final batch receipt](HotelStructure/task70-accepted.local.json):345 PASS =168 independent UC checks
+12 batch setup +163 UC017 regression +2 startup. UC017 includes10 DMV waits,7 post-INSERT fault
schedules and12 actual deployment calls. [UC013/014/015 regression](HotelStructure/task70-structure-regression.local.json):
590 PASS =216+159+215. [UC023 regression](HotelStructure/task70-rooms-regression.local.json):229 PASS
=227+2 startup. Auth foundation174 PASS =43+61+49+21, preserving its test-double/loopback limits.
**Total final checks:1338 PASS**, excluding earlier attempts and canonical catalog assertions.

Exact solution build PASS,0 errors,2 NU1900 warnings (NuGet vulnerability feed unavailable).
Harness build PASS. `dotnet test HotelOps.slnx --no-restore` exit0 but no discovered test projects:
NOT RUN as test-framework evidence; console suites above actually executed. Diff check PASS.
Trial-only SQL/production HTTPS verified; production rollout/UI/future cleaning/inspection writers
NOT EXECUTED. Earlier failed logs/trials retained; no HotelOpsDB access or Git mutation.

Verified progress **11→14/75 (18.67% UC count)**; points **27→32/256=12.50%**, gain+5, remaining224.
Taxonomy: **14 COMPLETE_VERIFIED (including V1),5 PARTIAL,37 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75**.
No UC017/023 double count; UC016 remains PARTIAL, UC006 gate unchanged. Historical sections retain
their original snapshots and are not the current totals. Next batch: UC019+UC021+UC012, subject to
their exact requirements and writer/reference guards; do not assume branch movement/legacy conversion.

## 19. Task71 — Accelerated Batch 2 (31/10/2026)

Supersedes §18's next-batch recommendation. [Task71 report](HotelStructure/TASK71_BATCH2_IMPLEMENTATION.md)
records exact requirements, developer-approved DH designation V1, API/source inventory and limitations.
S71 reuses SQL MASTER_DATA_MANAGE/session/branch/catalog transactions. Manager Users are locked with
actor in ascending order before feature locks, with active branch role/membership and commit-time
eligibility rereads. Designation never changes RBAC. No schema/generated/DI/DEV2 edits.

| UC | Weight | Classification | Independent evidence |
|---|---:|---|---|
| UC019 Deactivate Room Type |3|COMPLETE_VERIFIED_V1|50 PASS: confirmation/reason/If-Match, active-room guard/list, soft deactivation, audit/rollback, deactivation versus room-create race |
| UC021 Update Room Information |2|COMPLETE_VERIFIED_V1|50 PASS: editable GET/conditional PUT, active references/uniqueness/reason, Room identity/status/open cleaning/history preservation, foreign-parent legacy refusal, concurrency/audit/rollback |
| UC024 Create Department |2|COMPLETE_VERIFIED_V1|53 PASS: scoped optional branch DH, multi-department designation, manager initial/final eligibility, actual competing-manager lock wait, legacy schema and atomic audit/rollback |
| UC025 Update Department |2|COMPLETE_VERIFIED_V1|51 PASS: scoped versioned update, immutable branch/code, manager disable/role/membership races, no RBAC mutation, audit/rollback and legacy schema |

[Final receipt](HotelStructure/task71-complete.local.json):**404 PASS** =2 startup +5 legacy compatibility
+181 Task70 regression/setup +216 Task71 batch. Independent four-UC evidence totals204 checks; common
setup/legacy-schema checks excluded from that subtotal. Each UC has SQL late session/grant/cancel
schedules; departments additionally manager role expiry/membership revocation/account disable.54
post-write assertions +2 real manager-lock assertions; HTTPS has real audit-trigger faults and races.

[Full Task71/UC017 receipt](HotelStructure/task71-accepted.local.json):560 PASS, of which **163 UC017**
checks reused without double-counting its Task70/Task71 repetitions. Later source delta only tightened
UC021 legacy reference filtering and harness checks; final404 receipt certifies that delta. The399-check
intermediate receipt and failed attempt remain preserved. [UC013/014/015](HotelStructure/task71-structure-regression.local.json):
590 PASS; [UC023](HotelStructure/task71-rooms-regression.local.json):229 PASS. Auth foundation174 PASS
=43+61+49+21, retaining its documented doubles/loopback limits. Represented nonduplicated suite slices:
**1560 PASS =404+163+590+229+174**; no sum of repeated full-run counts to inflate evidence.

Exact solution build PASS,0 errors,2 NU1900 warnings; final harness build PASS,0 errors,2 NU1900.
`dotnet test HotelOps.slnx --no-restore` exit0/no discovered test projects: NOT RUN as framework evidence;
the console suites above actually executed. Canonical final Fresh/Upgrade/AuthUpgrade CHECKDB/schema
comparison PASS after SQL Express RESOURCE_SEMAPHORE wait. Diff/source preservation checked separately.

Verified progress **14→18/75 =24% UC count**, **32→41/256 =16.02% weighted**; delta+4 UC/+9 points,
**+3.52 percentage points** weighted, remaining57 UC/215 points. Taxonomy: **18 COMPLETE_VERIFIED
(including V1),5 PARTIAL,33 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75**. UC016 stays PARTIAL; UC006 unchanged.

## 20. Task72 — Batch 3: historical reads and versioned inspection checklists

[Task72 report](HotelStructure/TASK72_BATCH3_IMPLEMENTATION.md): UC038 View Room Turnover
History (2), UC048 View Room Inspection History (2), UC039 Create Room Inspection Checklist
(2), UC040 Update Room Inspection Checklist (3): **COMPLETE_VERIFIED_V1**. History/private
evidence208 assertions PASS; checklist282 PASS. Real HTTPS/SQL/DMV, source authorization,
SQL-backed CHECKLIST_MANAGE/BRANCH, real rowversion stale protection, Draft/publish/retire,
immutable historical content, atomic audit, genuine DDL/audit rollback, User retention and
actual HTTP cancellation verified. Post-extension batch regression567 + retained structure590
and Room229 from the first phase; AuthZ174 foundation assertions separately use doubles.
Build/diff PASS; NU1900 remains; no test-framework projects discovered.

The [checklist proposal](HotelStructure/TASK72_CHECKLIST_CONTRACT_PROPOSAL.md) received
conditional developer approval after the initial two-UC report. Implemented the approved
extension only on disposable trials: nullable ownership/no legacy content backfill, same-branch
multi-type bridge, criterion_group and SQL rowversion required by SRS, controlled Database
First projections/trigger compatibility, optional canonical -Checklists integration with
fresh/upgrade integrity comparison. No generated entities/DEV2 source or UC006 gate changes.
Deployment does not grant HK_SUPERVISOR automatically; the existing permission/scope must
be explicitly configured in SQL. Trial grant activation/revocation is acceptance evidence.
No shared/production DB deployment. UC012/022/026 and operational-task blockers remain.

Task71 **18/75,41/256=16.02%** → **22/75=29.33% UC;50/256=19.53% weighted**.
Delta **+4UC/+9points/+3.52pp**; remaining53UC/206points. Taxonomy **22 COMPLETE_VERIFIED
(including V1),5 PARTIAL,29 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75**. UC016 remains PARTIAL.
Final preservation/receipts/source hashes and elapsed wall time for both phases are in
`obj/task72-checklist-validation.local.json`; original20m24s phase receipt is retained.
No HotelOpsDB/shared/production deployment or Git mutation. Historical snapshots in earlier sections
remain historical. Batch3 candidates: full reviewed UC012, UC048 and checklist ownership/version
contract for UC039/040; do not declare their unresolved flows complete.

## 21. Task73 — Batch 4: inspection execution and defects

[Task73 report](Housekeeping/TASK73_BATCH4_IMPLEMENTATION.md): selected UC029(3),041(3),
042(3),043(3),046(5). Developer conditionally approved necessary disposable-only V1 schema,
compatibility projections/mapping, actual rowversion and explicit SRS workflow states. Coarse
legacy statuses/content remain preserved; workflow_status is opt-in, not bulk backfilled.
Checklist branch/applicability/immutable historical versions and existing SQL permissions
remain enforced. No automatic HK grant, DEV2 source edit, UC006 gate change or production DB.

**UC042/043 COMPLETE_VERIFIED_V1**. Real HTTPS/SQL acceptance:225 workflow assertions,
93 planning assertions +282 checklist regression=600 PASS; separate post-Housekeeping
UC038/048 regression208 PASS. AuthN/AuthZ foundation43+61+49+21=174 PASS with documented
doubles, not live SQL certification. Fresh/Upgrade/AuthUpgrade CHECKDB/constraints/schema
comparison, strict final type/index/FK/trigger verification and populated legacy upgrade/
replay/genuine DDL failure/recovery/empty rollback/populated refusal PASS. Real rowversion,
assigned-only writes, immutable assigned versions (including legacy templates), file/source
privacy, audit/metadata/private-file rollback, concurrent defect stale loser412 and actual
HTTP cancellation/DMV release verified. Checklist publication now uses SQL timestamp under
its existing SQL effective-time guard; exact original concurrent publication criterion passes.

**UC029/041/046 PARTIAL**: independent business assignment/inspection/rework writes and
selected-defect links implemented/SQL-tested, but required notification delivery/retry is
not DEV2-approved/integrated. API explicitly returns NOT_INTEGRATED; no fake message/outbox
counted as delivery. No points for these partial UCs. UC028/037/044/045 and time-bound
cleaning flows are not safe replacements without room-state/DEV2/time/item contracts.

Task72 baseline22/75,50/256=19.53% → **24/75=32% UC;56/256=21.88% weighted**.
Delta **+2UC/+6points; +2.34375pp (~2.34pp)** computed before rounding percentages.
Remaining **51UC/200points**. Taxonomy **24 COMPLETE_VERIFIED(including V1),8 PARTIAL,
24 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75**. UC016 remains PARTIAL. Five-complete target
not achieved; no acceptance weakened or UC038/039/040/048 counted again.

Final sources/receipts/pre-existing713-file preservation/HEAD/index/four-worktree proof
and complete-batch wall time are recorded in `obj/task73-validation.local.json`.
Build/diff PASS; NU1900 remains. No test-framework projects discovered; dotnet test exit0
is not counted. Recommended Batch5: obtain DEV2 delivery/retry contract and verify its
adapter before upgrading the three PARTIAL UCs; then reviewed room/time prerequisites.

## 22. Task74 — Independent progress workflows, 31/10/2026

[Task74 report](Housekeeping/TASK74_BATCH5_IMPLEMENTATION.md),
[conditionally approved contract](Housekeeping/TASK74_CONTRACT_PROPOSAL.md),
[final HTTPS/SQL receipt](Housekeeping/task74-accepted.local.json).

UC032 **Update Cleaning Progress (2)** and UC109 **Update Task Progress (2)** are
**COMPLETE_VERIFIED_V1**. Production read/save/private-download APIs enforce SQL-backed
CLEANING_EXECUTE/TASK_EXECUTE and current assignment/branch, explicit lifecycle phase,
0–100 progress, distinct immutable cleaning snapshots/task criteria, notes/evidence,
actual rowversion and atomic audit. No workday/time event or room-status transition;
100% does not complete work or bypass missing mandatory criteria. Progress schema approval
does not resolve notification or room lifecycle contracts.

K74/T74: same existing single-project service/DTO/controller/transaction infrastructure;
explicit audited initialization, six additive tables, nine FKs, two rowversions, seven guards;
legacy fields/history untouched. Existing parent/assignment writers advance revision;
opted-in premature completion is SQL-rejected. Four controlled DEV1 mappings are compatible
before/after optional trigger deployment; generated entities/main DbContext/DEV2 unchanged.

Final acceptance **287 PASS**, including setup, UC012 subset and shared regression;
**87 UC032 +89 UC109** per-UC assertions. Real HTTPS/SQL success, unauthorized/forbidden,
cross-branch/foreign evidence/criterion rejection, invalid/missing/phase cases, persisted
audit, actual concurrent stale-write races, post-write grant-expiry/audit rollback and
DMV-confirmed cancellation PASS. One targeted shared regression covers UC013/023/142,
UC038/048, immutable checklist consumer and UC042 private-evidence helper. No duplicate UC credit.
Fresh/populated legacy upgrade/replay/DDL fault/empty rollback/recovery/populated rollback refusal
PASS. Final Fresh/Upgrade/AuthUpgrade metadata parity and CHECKCONSTRAINTS PASS.

S74 UC012 **PARTIAL**: create/read/conditional metadata update verified, but GBR033 deactivation
and physical Branch rowversion contract are not fully accepted. No deactivation success added.
UC028/026 reviewed as **BLOCKED_EXTERNAL**: shared room/block lifecycle or dependency-writer
protocol unresolved, not safe replacements. UC006 excluded; UC016/029/041/046 unchanged.

Task73 **24/75,56/256=21.88%** → Task74 **26/75,60/256=23.44%**;
gain **+2 UC/+4 points/+1.5625pp**, remaining **49 UC/196 points**.
Taxonomy **26 complete,9 PARTIAL,19 NOT_IMPLEMENTED,21 BLOCKED_EXTERNAL=75**.
Only COMPLETE_VERIFIED_V1 gains counted; 4–5-complete target not achieved.

Build/diff PASS; NU1900 vulnerability-feed warning remains. Solution has no discovered
test-framework tests; no dotnet test credit. Source/receipt hashes, preservation checks and
measured preflight-to-finish time in `obj/task74-validation.local.json`; report states the
measurement excludes initial unrecorded reading. No shared/production DB/HotelOpsDB access
or Git mutations. Batch6 should resolve notification/room/time contracts before selecting
another full dependent batch; do not weaken acceptance to manufacture independent UCs.
