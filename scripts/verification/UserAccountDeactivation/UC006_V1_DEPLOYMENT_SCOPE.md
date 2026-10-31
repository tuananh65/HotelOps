# UC-006 — V1 deployment scope proposal (Task57)

Ngày 2026-10-30; source baseline `4a187320643d0c82f232c6b9dd5a667b8401c280`
và local DEV1 source Task51–56 được bảo toàn. **DRAFT FOR HUMAN REVIEW — chưa áp dụng**.
Tài liệu xác định việc có thể nghiệm thu ngay trên backend hiện có, không cấp phép Deactivate.
Source reachable không đồng nghĩa đã deployed, đủ authorization hoặc đạt production acceptance.

## 1. Quyết định đã duyệt và giới hạn

- **D05: APPROVED V1 POLICY**, theo chỉ dẫn developer Task57: Working Workday chặn
  Deactivate, kể cả không còn open interval. Employee phải thực hiện End trước Admin Deactivate.
- Ended Workday riêng lẻ không chứng minh source assignments đã release. Không auto-end
  trong Deactivate; không suy thêm Admin forced-end authorization. Employee không thể End
  là ngoại lệ vận hành cần approval riêng về actor/quy trình/audit, không cho direct SQL bypass.
- **Delivery priority: APPROVED**: ổn định chức năng backend đã triển khai trước; workflow
  SRS còn thiếu được giao task riêng. Hoãn delivery không xóa yêu cầu hoặc tự cho scope exclusion.
- D01–D04, D06–D07 vẫn chưa có formal approval mới. Giữ nguyên các technical/V1 PROVISIONAL
  approvals đã ghi ở [contract matrix](UC006_V1_CONTRACT_MATRIX.md), S7; không yêu cầu duyệt lại
  nguyên tắc block current management/actual responsibility/mandatory next action.
- D05 chốt policy, chưa cung cấp Workday model/writer. `WORKDAY_CONTRACT` và hai Unsupported
  không tự clear. Task57 không thay runtime, gate list, DI, HTTP, schema hoặc RBAC.

## 2. Actual source inventory và deployment boundary

Đã kiểm tra [Program.cs](../../../src/HotelOps.Api/Program.cs), toàn bộ
[Controllers](../../../src/HotelOps.Api/Controllers), services, entities/mappings, SQL release/
patches, test runners, file deployment trong checkout (kể cả tìm tên file ẩn ngoài build output).
`AddControllers` + `MapControllers` và DI tạo source route-to-service path khi startup schema
gate Compatible. Không khởi chạy app/SQL hoặc đọc secret; không kiểm tra deployment thực tế.

| ID | Workflow / production entry point trong source | Classification | Reachability và giới hạn bằng chứng |
|---|---|---|---|
| A01 | Incident/Maintenance: IncidentsController, MaintenanceWorksController → IIncidentService/IMaintenanceWorkService | IMPLEMENTED_AND_REACHABLE | DI scoped, mutation routes có thật; Assign/Return tạo hoặc khôi phục assignee duty. Chưa chứng minh deployment/authz readiness |
| A02 | Warehouse/Inventory: InventoryController → InventoryService/InventoryAccess | IMPLEMENTED_AND_REACHABLE | Warehouse Save tạo/đổi/activate manager; cookie/current-user/access gate có thật. Posting/capacity interfaces cũng được DI cho MR/Maintenance/Replenishment |
| A03 | Asset/Handover: AssetsController → IAssetService | IMPLEMENTED_AND_REACHABLE | Create pending receiver, Accept custody transfer; Reject/Retire và master/location mutations có thật |
| A04 | Material Request: MaterialRequestsController → MaterialRequestService | IMPLEMENTED_AND_REACHABLE | Save DRAFT, submit/resubmit/reapproval, request-information và latest approval cycle tạo/restore duty |
| A05 | Replenishment: ReplenishmentsController → ReplenishmentService + Inventory posting | IMPLEMENTED_AND_REACHABLE | Create/Close/Receive(link/post), ảnh hưởng shared Catalog/resource/User graph; không tự suy creator là current personal owner |
| A06 | Existing DEV1 account/auth: UserAccountCreation/Profile/Activation/BranchAssignment/RoleAssignment, AdminCredentialReset, Auth/AdminReauthentication controllers | IMPLEMENTED_AND_REACHABLE | SQL-backed account/session/access writers; Activation chỉ phục hồi account, không tạo task/manager assignment |
| B01 | UserAccountDeactivationService và concrete responsibility queries | IMPLEMENTED_BUT_NOT_REGISTERED | Có direct-call SQL harness; không controller/DI Deactivate. Business review và immutable release fuse trước mutation vẫn hoạt động |
| C01 | Cleaning/rework/inspection/defect/department-manager mutation workflows | SPECIFIED_BUT_NOT_IMPLEMENTED | Có entities, readers, authorization resource reader và predicate subsets; không tìm thấy workflow mutation entry point/service/job. Có SQL FK không đồng nghĩa writer đã triển khai |
| C02 | Internal Request, Operational Task, Shift, Workday, unscheduled day, request/shift acknowledgment, manager transfer, JOB04/JOB05 | SPECIFIED_BUT_NOT_IMPLEMENTED | Chi tiết model/legacy/deployment risk tại [deferred matrix](UC006_DEFERRED_WORKFLOWS.md); không loại khỏi business scope |
| D01 | Deployed API revisions/other services, SQL Agent/ETL/import/HR/admin SQL, live procedures/triggers, ignored publish artifacts | EXTERNAL_OR_DEPLOYMENT_UNKNOWN | Không có authoritative deployment/writer manifest được cung cấp; source absence không chứng minh production absence |

Đây là inventory theo family, không đếm family như một method được nghiệm thu. Hai dimensions
độc lập: A/B/C mô tả source; D mô tả phần deployment chưa biết của cả A và C.
Users/Health/Weather/Csrf và read/history/lookup routes đã rà soát nhưng không là writer mới
cho assigned responsibility. Session/credential writers vẫn thuộc shared lock-graph review.

### Jobs, SQL, import/export và packaging

| Surface đã kiểm tra | Source finding | Điều chưa được chứng minh / evidence cần lấy |
|---|---|---|
| Hosted/scheduled services | Không thấy AddHostedService, IHostedService/BackgroundService, Quartz/Hangfire hay timer job writer trong API; JOB04/05 vẫn được yêu cầu ở SRS §16.4/16.5 | Owner cung cấp scheduler/service/container inventory và version; không chỉ xác nhận “không có controller” |
| SQL procedures/triggers/jobs | Trong database/patches và scripts/database không tìm thấy CREATE PROC/PROCEDURE/TRIGGER hoặc sp_add_job tạo workflow tương ứng. Có raw SQL lock/number allocation trong service | DBA cung cấp reviewed metadata/export của deployed modules, triggers, SQL Agent steps, synonyms/cross-DB writers và quyền thực tế; repo scan không xác nhận deployed absence |
| Canonical/upgrade/patch | [Release README](../../database/release/README.md), canonical full schema/seed; auth + RBAC upgrade và bốn contract patches Inventory/MR/Replenishment/Maintenance. Đây là provisioning/upgrade writers, không scheduled API jobs | Exact applied package hashes và schema validation của disposable deployment copy; MR legacy vocabulary khác patched service vocabulary. Không chạy migration hoặc seed trong Task57 |
| Import/export | Không thấy business CSV/Excel/upload/bulk import/export entry point trong production API; ExportCredentialForProtectedTicket là ticket internal, không business export | External ETL/HR/import/admin scripts UNKNOWN. Test/provisioning SQL dưới scripts thực sự có direct writes; không coi tất cả script là online production writer hoặc bỏ qua khả năng tái sử dụng |
| Deployment configuration | Checkout không có .github pipeline, Docker/compose, tracked publish/IIS manifest cho production; launchSettings là local development. .gitignore loại publish/, *.pubxml, PublishScripts và output | Build/artifact hash + service/route exposure + DB principal permissions + scheduler manifests do deployment owner xác nhận; ignored/untracked deployment ngoài checkout vẫn UNKNOWN |

### Authorization dependency của backend đã reachable

MaintenanceWorks/Incidents/Assets/MaterialRequests/Replenishments controllers không có
`Authorize` như Inventory; các command DTO còn truyền `ActorUserId` từ client.
[Cookie registration](../../../src/HotelOps.Api/Security/HotelOpsCookieInfrastructure.cs) không
đặt fallback policy; [CSRF middleware](../../../src/HotelOps.Api/Middleware/ApiCsrfMiddleware.cs)
và RestrictedSession không thay thế server-side actor authorization. Inventory có cookie,
current user và `InventoryAccess.Authorize`. Vì vậy A01/A03/A04/A05 là source-reachable,
**không được dán nhãn production-authorized/PASS** từ service SQL tests. Cần DEV2 hoàn tất
actor/session/permission/scope/SoD và integration evidence theo permission baseline đã duyệt.
Không sửa DEV2 hoặc thêm responsibility predicate cho dependency này trong Task57.

## 3. Existing writer coverage có thể nghiệm thu ngay

Owner lock dùng [UserEligibilityLock](../../../src/HotelOps.Api/Services/Users/UserEligibilityLock.cs)
trên caller DbContext/transaction, distinct IDs ascending, UPDLOCK/HOLDLOCK, no tracking;
new/retained future owner phải nondeleted + account ACTIVE + employment ACTIVE.
Global-admin foundation lấy applock/Users universe trước feature locks; không nâng lên Users
universe sau feature lock. Source read cuối transaction/FK/actor/audit vẫn thuộc graph.

| ID / operations (từng operation phải có evidence) | Responsibility, owner, release | User locks → feature → authoritative reread | SQL evidence đã có / acceptance gap |
|---|---|---|---|
| E01 Maintenance AssignWorkAsync, ReturnWorkAsync | maintenance_works.assignee_id; PENDING/IN_PROGRESS/ON_HOLD current. Consistent COMPLETED/CLOSED/CANCELLED không current; Return COMPLETED → IN_PROGRESS khôi phục duty | [Commands](../../../src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Commands.cs): proposed assignee hoặc retained current assignee → Incident → Work; Reload pair, linkage/status/assignee drift rejection; eligibility sau SQL wait | Task55B ReturnWork real commit + actual Deactivate denial waits, stale/cancel/1222/1205/post-flush rollback PASS scoped. Assign và toàn bộ family chưa có complete DEV1 integrated receipt |
| E02 Maintenance Create/Start/Hold/Resume/SubmitResult/Verify/Cancel/CreateLog; Incident Create/Triage/ChangeStatus | CreateWork assignee NULL; Start/Resume dùng current assigned work; SubmitResult chưa Closed nhưng approved DEV2 predicate có lifecycle riêng. Incident reported_by không tự current assignee | Existing Incident/Work locks, authoritative pair/state guards; nhiều path RequireUser/detail/FK reads sau feature, chưa participant-complete global ordering | Existing IncidentMaintenance tests có source. Không coi tồn tại test hoặc PASS ReturnWork là execution/acceptance của các path này. Cần owner graph review và both-order SQL tests |
| E03 Inventory SaveWarehouse (create/update/manager remove/change/activate) | warehouses.manager_user_id + is_active. Active management block; legitimate manager replacement/removal hoặc warehouse inactive release theo guards; inactive old owner được phép release | [Masters](../../../src/HotelOps.Api/Services/Inventory/InventoryService.Masters.cs): discover old/new → sorted Users → Catalog → Warehouse; Reload manager + rowversion, proposed eligibility; drift reject, không acquire missing User sau Catalog | RC-D4 shared harness có cases old/new sorted, inactive, writer-first, inactive-first, manager drift; source chuẩn bị ≠ đã chạy trên DEV1 trial. Cần actual Deactivate/evaluator integration, NULL owners/activation/fault snapshots |
| E04 Asset CreateHandover, AcceptHandover → FinalizeHandover | PENDING to_user_id là receiver duty; custody IN_USE/UNDER_REPAIR thuộc custodian_user_id. Accept chuyển custody atomic, old custody release nhưng target vẫn có custody; Reject chỉ kết thúc pending receiver | [Handover](../../../src/HotelOps.Api/Services/Assets/AssetService.Handover.cs): create receiver → Asset; accept sorted From/To Users → Asset → Handover; Reload participants/PENDING/source custody + eligibility | RC-D4 source cover proposal/accept/reject + DMV barriers; chưa có DEV1 integrated SQL acceptance receipt cho family. Cần both orders, participant drift, stale tracking, audit rollback; history ACCEPTED không xóa current custody |
| E05 Asset Create/Update/AssignLocation/TransferLocation/ChangeCondition/RejectHandover/Retire | Master/location/history không tự tạo custodian (Create custodian NULL; master input không gán custody). Reject giữ source custody; Retired FK history không độc lập block | Asset-first actor/reference/detail reads, Reject không future-owner User lock. Retire có dependency checks, không đồng nghĩa toàn graph đã ordered | Cần release/no-owner/actor/FK joins proof với Users TABLOCKX và authorization. Không áp yêu cầu new-owner eligibility lên old owner đang được release; không sửa DEV2 |
| E06 MR Save(create/edit/submit/reapproval); Command(submit/resubmit/request-information/approve/reject/cancel/close-remainder) | DRAFT/NEEDS_INFORMATION requester_id current; greatest RevisionNo PENDING cycle + parent PENDING_APPROVAL assigned_approver_id current. Submit/request-info chuyển duty; resolved/superseded older cycle là history | [Commands](../../../src/HotelOps.Api/Services/MaterialRequests/MaterialRequestService.Commands.cs): actor/requester/selected approver Users → Catalog/request/cycle; RecheckDiscovery/Latest/Cycle reread participants/version/revision and no late extra User | Task55B classifier greatest-revision/history SQL PASS chỉ query. DEV2 --deactivation-coordination source có submit/request-info probes (standalone), không full actual UC006. Cần Save/content edit/reapproval/resubmit/latest selection drift + both orders/faults và SQL actor authz |
| E07 Other Inventory + MR Issue + Replenishment | Inventory SaveItem/Unit/Conversion/Threshold, SaveDraft/Post/CancelDocument/Dispatch/Receive, Create/SaveCounts/StocktakeAction, Create/PostAdjustment/Reverse/PostReversal, PostIssue/PostReceipt; MR Issue; Replenishment Create/Close/Receive. Không suy creator/requester thành duty ngoài approved contract; Catalog/resource/User/FK graph có thật | [Inventory Begin](../../../src/HotelOps.Api/Services/Inventory/InventoryService.cs) thường Catalog → Actor khi không prepareOwners; Issue/receipt/capacity integration có shared transaction/feature gates. Review caller/callee, rollback, không chỉ direct owner writer | Inventory/MR/Replenishment runners có source, không có Task57 execution. Cần complete graph/read assumptions, document/warehouse/stock order, no-owner/release paths và owner acceptance; không hứa không deadlock từ source inspection |
| E08 DEV1 CreateAsync/UpdateAsync/AssignAsync/AddAsync/ActivateAsync; reset/login/password/session/reauth workflows | Account/access/session, không new business assignment. Profile chỉ fullName/email/phone; Create không set manager. Role/capability không tự current work; Activation không tự restore task | Global-admin/Users-first foundation ở create/profile/role/reset; branch retained User protocol; Activation sorted actor/target Users → authority/session/version reread → conditional update/audit/final authority → commit | Task55B actual Activation both orders/stale/cancel/post-flush rollback và LastAdmin foundation có receipt; các account regression receipts Task51–55 được giữ. Chưa complete cross-module auth/session graph acceptance; không đổi nhãn thành Deactivate commit/LastAdmin poststate PASS |

Evidence: [ReturnWork receipt](task55b-returnwork-final.local.json),
[contract/account receipt](task55b-contracts-accepted.local.json),
[state-query receipt](task55b-gates-final.local.json),
[service-fault receipt](task55b-service-final.local.json).
Đây là kết quả Task55B trên disposable trial, không SQL mới của Task57.
Prepared suites: [RC-D4 source](../../../tests/Shared/ResponsibilityCoordinationChecks.cs),
[RC-D4 limitations](../../../tests/Shared/RESPONSIBILITY_COORDINATION_RC_D4.md),
[MR coordination source](../../../tests/HotelOps.MaterialRequests.IntegrationTests/DeactivationCoordination.cs),
[MR limitations](../../../tests/HotelOps.MaterialRequests.IntegrationTests/DEACTIVATION_COORDINATION.md).
Các suite DEV2 có exact machine/database guards và audit prerequisites; không tự bỏ guard,
đổi sang DB chung hoặc coi standalone status probe là actual Deactivate.

## 4. Draft scope amendment SA57 — human review, NOT ACTIVATED

**Đề xuất delivery baseline:** ổn định/kiểm thử A01–A06 và E01–E08 trước; giữ UC006 internal
unregistered/guarded. Deferred workflows trong matrix giữ nguyên SRS/business obligations,
giao task riêng với owner/representation/eligibility/locking/tests. Đây là phạm vi công việc
nghiệm thu backend hiện hữu, chưa là tập duty duy nhất được phép xét khi Deactivate.

Một UC006 V1 release hạn chế chỉ có thể được xem xét khi nhóm duyệt **D06** bằng văn bản,
chỉ rõ workflows nào chưa deployed, exact deployment revision/DB shape, dữ liệu legacy đã
được phân loại và mọi đường writer bị hạn chế bằng evidence. Chưa đề xuất loại trừ nào có
hiệu lực; chưa có đủ proof để đổi bất kỳ Unsupported/Unresolved thành NoBlocker.

Required deployment restrictions/controls đề xuất (cần deployment owner thực hiện/xác nhận):

1. Versioned API/artifact, route inventory, startup schema compatibility + applied patch
   fingerprints trên disposable copy; không lấy release schema cũ riêng lẻ làm current patched schema.
2. Inventory DB principals + effective write/EXECUTE rights, deployed procedures/triggers,
   Agent/ETL/jobs/imports/admin access. Deny/disable uncoordinated online ownership writers
   hoặc tích hợp cùng protocol bằng owner-reviewed change; không tự đổi quyền trong Task57.
3. Với legacy/current data: report theo target và cycle trên copy, phân loại unknown states,
   active assignments, manager FKs, pending receiver, shift timestamps; historical closure
   cần authoritative proof. Free text/zero modeled rows không đủ chứng minh Workday/ack an toàn.
4. Operations chuyển responsibility qua workflow đã có quyền và audit hợp lệ; nếu thiếu
   workflow release thì giữ account deactivation BLOCKED và giao remediation task, không
   update trực tiếp account/manager/status để qua query. D05 employee End và kiểm tra từng
   source assignment; employee không End được cần separately approved exception.
5. DEV2 server-side identity/scope/SoD readiness cần hoàn tất trước khi gọi A01/A03/A04/A05
   là production-supported; external gateway assumptions phải có cấu hình/evidence cụ thể.
6. Mọi writer mới/import/job/schema upgrade phải quay lại inventory + locks + test coverage;
   không cho deploy bổ sung đường tạo duty ngoài baseline đã review mà vẫn giữ acceptance cũ.

## 5. Acceptance plan và ba pending release gates

Các test dưới đây là **required/NOT RUN Task57**, không gate mới hoặc kết quả PASS mới.
Mỗi actual operation: writer-first commit → contender đợi User (DMV waiter/owner/SPID) →
concrete evaluator thấy current blocker; standalone inactive-first → writer đợi trước feature,
reread từ chối; stale assignee/manager/custody/revision/version/role drift; cancellation,
bounded lock timeout, deadlock cleanup/fresh retry, failure sau SQL business/audit flush →
whole rollback. Phải kiểm tra aggregate/session/credential/access/history trước/sau.

| Existing release gate (PENDING) | Có thể nghiệm thu trên existing backend ngay | Deferred implementation acceptance | Deployment UNKNOWN / review cần thiết |
|---|---|---|---|
| DEV1_WRITER_COORDINATION | E08 actual account/auth operation inventory + SQL both orders/session/authority/LastAdmin tests; phân biệt account writers với business assignment writers | C01/C02 future writers, D05 day/end/interval, task/request return/reopen, manager transfer, JOB05: tests sau task riêng; không dựng fake writer để đóng | DEV1 external/import/HR/direct SQL scope và legacy data proof; owner review inventory/evidence; deferred scope amendment chưa approved nên không clear toàn gate |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | E01–E07 method coverage với actual DEV1 evaluator/service-denial retained-lock lifetime; Warehouse/Asset/MR both orders + drift/faults; existing ReturnWork receipt tái dùng đúng scope; server-side actor authz acceptance | JOB04 preventive generation/default eligibility cần task riêng; future modules không được chứng nhận bởi ReturnWork | Deployed DEV2 writers/patches/permissions + owner acceptance; scoped standalone PASS không full UC006. D07 promotion boundary vẫn proposal, không tự clear |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Complete E01–E08 caller/callee graph gồm global applock/Users universe, sorted Users, Catalog/Incident/Work/Asset/Warehouse/document/stock locks, ordinary actor/FK/detail reads. Bounded both-order SQL traces xác nhận không inverse cycle theo isolation đã ghi | Future request/task/shift/day/ack/jobs phải tích hợp canonical order trước deployment và bổ sung tests | Effective deployment jobs/procs/import paths + source/plan/isolation review bởi DEV1/DEV2/DB owner; chưa có whole-graph proof, gate vẫn PENDING |

Closing criteria/evidence dùng [contract matrix §7](UC006_V1_CONTRACT_MATRIX.md) trong phạm vi
được duyệt; Task57 không chấp thuận D07. Sau business/deployment resolution và reviewed release
source promotion hợp lệ mới có thể chạy actual internal UC006 success: INACTIVE + new version +
revoke all sessions + one atomic audit; unchanged credentials/roles/memberships/assignments;
audit failure/LastAdmin rollback. Endpoint vẫn là task riêng. Không thể báo full acceptance hiện nay.

## 6. Các approval / handoff cụ thể còn cần

| Item | Deliverable để người chịu trách nhiệm review | Hiện trạng |
|---|---|---|
| Product/DEV1 D01–D04 | Request closure precedence, exact manager release, future roster boundary, typed receiver/cycle binding; giữ approved broad rules | Chưa formal approve các đề xuất cụ thể; không hỏi lại D05 hoặc approvals S7 |
| Scope D06 / SA57 | Chọn explicit deployment-limited scope nếu cần UC006 trước missing modules; kèm denied write paths + legacy assessment và owner signoff | DRAFT, chưa có exclusion hoặc activation |
| Deployment/DB owner | Artifact/schema/job/module/permission inventories và copy-data assessment; xác nhận không có hidden writer cho từng deferred workflow | UNKNOWN, không truy cập HotelOpsDB để thu thập trong task này |
| DEV2 owner | Authorization readiness; E01–E07 integration/fault receipts và feature-first actor/read graph disposition; JOB04 backlog | ReturnWork hotfix đã tích hợp, không còn pending merge; broad acceptance vẫn pending |
| Release owners D07 | Chấp thuận acceptance boundary/promotion sequence và từng evidence set trước reviewed source change | Ba gates vẫn PENDING; build/predicate PASS không tự đóng |
| Operational exception (chỉ khi phát sinh) | Employee không thể End Workday: actor/SoD/audit/recovery process approval riêng | Không cấp Admin forced-end trong Task57 |

**Verdict: PARTIAL** — baseline proposal và existing-backend acceptance plan có thể giao việc;
UC006 mutation/release vẫn BLOCKED, không scope cut, không runtime bypass.
