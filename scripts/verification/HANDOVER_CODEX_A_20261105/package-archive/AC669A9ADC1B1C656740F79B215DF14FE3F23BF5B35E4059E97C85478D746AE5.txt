# Room / Workday / Branch — shared contract

Quyết định nghiệp vụ được chốt bởi COORD-FINAL-01; implementation/shared diffs dưới đây chưa được tự phê duyệt. Current source hashes/blobs và numbered excerpts trong evidence/. Không kết nối SQL để khẳng định trạng thái database đang chạy; SQL vocabulary dưới đây lấy từ release scripts/mappings.

## Room lifecycle: ba chiều và compatibility

Current `Entities/Room.cs:18` chỉ có OperationalStatus, không persisted Occupancy + Readiness riêng, không CurrentCleaningCycleId/current inspection FK. SQL release :3459 cho phép AVAILABLE/OCCUPIED/DIRTY/CLEANING/INSPECTION/READY/OUT_OF_SERVICE. EF `HotelOpsDbContext.cs:2195–2202` default AVAILABLE trong khi SQL :2250 default DIRTY. Không tự sửa một phía hoặc coi default cũ là approved canonical state.

| Legacy value | Điều có thể nhận diện | Không được suy diễn |
|---|---|---|
| OCCUPIED | Legacy occupancy hint | Không chứng minh Readiness/EffectiveBlock |
| DIRTY / CLEANING / INSPECTION | Legacy housekeeping hint | Không tự khẳng định Vacant hoặc không block |
| READY | Ready candidate legacy | Không đủ để check-in nếu thiếu passed **current** inspection/no effective block |
| OUT_OF_SERVICE | Legacy availability restriction | Không chứng minh identity/lifetime của actual RoomBlock |
| AVAILABLE | Legacy trạng thái/default không đủ ba chiều | Không map mặc nhiên thành Vacant+Ready |

DEV2 exact migration phải nêu mapping dữ liệu cũ, default/EF consistency, ambiguous legacy data fail-closed, current-cycle identity và compatibility/rollback. Không tạo migration trong task này.

RoomInspection lưu RoomId + nullable CleaningTaskId + InspectorUserId + checklist VersionId; CleaningRework lưu CleaningTaskId + InspectionId + assignee. Cần validate cùng Room/current cleaning/rework cycle. Một pass inspection cũ hoặc MAX(inspected_at)/latest assignment không xác lập passed current inspection. Historical workflow mapping PASSED→legacy COMPLETED/PASS, FAILED→COMPLETED/FAIL có trong S housekeeping SQL, không chứng minh current conclusion writer tồn tại.

RoomStatusHistory có room/source trigger type/id, from/to, actor/time/reason; SQL :3447–3457 vẫn dùng legacy vocabulary. Coordinator cần history đủ giải thích occupancy/readiness/block decisions cùng business+audit transaction; exact history/projection diff chưa approved.

## Effective RoomBlock và Reception

RoomBlock source có BlockId, RoomId, nullable IncidentId, BlockedAt/By, ExpectedReleaseAt, ReleasedAt/By/ReleaseNote. Effective unreleased state căn cứ ReleasedAt IS NULL, không tự giải phóng theo ExpectedReleaseAt. Closed/Cancelled incident chỉ release rows có chính IncidentId, không release manual/incident khác. Cần active/authoritative room lineage và retained source locks.

**Conflict actual:** release SQL :1998 UX_room_blocks_active là UNIQUE(room_id) WHERE released_at IS NULL. Nó không hỗ trợ đồng thời các incident blocks V1 trên cùng Room. Exact revised indexes cần enforce một unreleased row/(RoomId,IncidentId) và một manual unreleased row/Room, xử lý duplicate legacy, null semantics, concurrency và recovery. EF RoomBlock mapping không khai filtered index này; không dùng EF model đơn lẻ để kết luận SQL cardinality.

Current Services search không tìm RoomBlocks/RoomStatusHistories mutation path. IncidentService.ChangeIncidentStatusAsync :20–132 cập nhật status/event/audit và Commit, không release RoomBlock/coordinator call. Maintenance Verify/Cancel cũng chưa chứng minh block-release integration. Current Reception check-in/out/move controller/writer không tìm thấy. Đây là missing runtime integration, không phải quyền DEV2 tự sửa DEV1 Reception.

V1 check-in: Active + Vacant + Ready + no effective block, kiểm lại dưới transaction/locks. Occupied + Blocked là trạng thái hợp lệ để cảnh báo; không tự check-out hoặc Ready phòng đang occupied. Check-out/move phải có actual source/cycle/history contract và một outer commit; coordinator không commit độc lập.

## UC139: executable A1/B1.1 đang có

Current routes trong Controllers/WorkdaysController.cs:16–37:
- GET /api/branches/{branchId}/workdays/current — ATTENDANCE_RECORD, own Working day 200/204.
- GET /api/branches/{branchId}/workdays/own-shifts — SHIFT_VIEW, own eligible current hotel-date assignments.
- POST /api/branches/{branchId}/workdays — scheduled Start, ShiftAssignmentId.
- POST /api/branches/{branchId}/workdays/{workdayId}/end — If-Match strong Base64 8 bytes, PauseAll=false.

Actor lấy CurrentUserAccessor. WorkdayPermissionResourceResolver:15–43 từ chối foreign day/assignment **trước** scope evaluation, kể cả ALL/BRANCH; non-Workday types delegate Cleaning resolver. WorkdayService Execute :94–114 Begin Serializable, LockUsers/time first, validate SQL session/account/employment and permission trước action, rồi Commit nếu success. Không gán final gate của BranchAdministrationService cho WorkdayService: code paths khác nhau.

StartAsync :23–27 unscheduled chỉ kiểm nonblank/max1000 rồi trả 503 UNSCHEDULED_NOTIFICATION_UNAVAILABLE; chưa thực hiện min10 thành successful unscheduled behavior. EndAsync :60 trả 503 PAUSE_ALL_UNAVAILABLE; End thường có open interval bị A1 từ chối. Không bypass bằng đóng interval trực tiếp trong End.

A1 SQL tại Task77/proposal/01_WorkTime.forward.sql: WORKING/ENDED; unique filtered UX_workdays_one_working(user_id); work_intervals global UX_work_intervals_one_open(user_id) WHERE closed_at IS NULL; actual rowversion và UTC server clocks. ShiftAttendance không là WorkInterval. DDL lưu work_item_kind CLEANING/REWORK/INCIDENT/MAINTENANCE_OCCURRENCE/TASK nhưng runtime RequireOperation :201–207 chỉ nhận CLEANING/REWORK/TASK. Không coi PM đã được bật vì CHECK cho phép kind.

## A1 interface DEV2 có thể reuse

Exact interface: `Services/WorkTime/IWorkTimeTransactionParticipant.cs:8–28`:

```csharp
Task LockUsersAsync(IReadOnlyCollection<long> actorAndOwnerIds, CancellationToken cancellationToken);
Task<WorkTimeIntervalResult> OpenIntervalAsync(WorkTimeOperation operation, CancellationToken cancellationToken);
Task<WorkTimeIntervalResult> CloseIntervalAsync(WorkTimeOperation operation, long intervalId,
    byte[] expectedRowVersion, CancellationToken cancellationToken);
```

WorkTimeOperation(ActorUserId,OwnerUserId,BranchId,WorkItemKind,WorkItemId) là trusted backend record, không HTTP DTO. Concrete SqlWorkTimeTransactionParticipant có StartScheduledWorkdayAsync/EndWorkdayAsync, không nằm trong interface ba methods. Program alias concrete/interface dùng cùng scoped instance.

LockUsers :28–55 sorted distinct IDs; UserEligibility UPDLOCK/HOLDLOCK trước per-user WorkTime exclusive transaction applock, rồi Working/Open UPDLOCK/HOLDLOCK. Không mở rộng lock set hoặc đổi transaction giữa các calls. Transaction :183–189 yêu cầu current same DbContext local SQL ReadCommitted/Serializable; tracked lockedTransaction ID phải khớp. Open/Close kiểm Working day, branch/owner/source, global-open interval, authoritative source executing state, SQL UTC, expected version; SaveChanges+audit, **không Commit**. Caller sở hữu source permission/locks/lifecycle.

## B1.2 exact dependency và Pause All sequencing

B12 path/hash/approval ở evidence manifest. B12 file chứa proposed records/IBranchDepartmentHeadResolver/IWorkdayNotificationParticipant/IWorkdayPauseParticipant, **không phải source interface đã tồn tại**. A01 mới được accepted theo request hiện tại không tự biến các proposed B12 types thành runtime.

Unscheduled flow cần stable operation GUID/fingerprint/replay, reason trim≥10 (A1 SQL đã có constraint), no eligible own shift, actual department/branch identity, new Workday + atomic audit/publication/intent. Một primary eligible DH từ branch relationship, không manager column/global fallback/broadcast. user_roles có branch/validity/IsActive nhưng không role-primary field; UserBranchAccess.IsPrimary không là DH selector. Với contract một DH/branch, zero/multiple eligible distinct DH phải fail closed; DEV2 đưa exact resolver/selection relation vào diff review. Recipient source-read cross-user không được mượn own Workday GET.

Pause All chỉ commit khi mọi supported item được pause hợp lệ:
1. Outer owner xác thực actual actor/session, own day/rowversion, source action permission; discover complete lock set trước khi lấy locks.
2. Giữ same scoped A1 participant/context, sorted users/time locks; re-read authoritative interval kind/item/owner/branch/revision và source/history dưới retained locks.
3. Module adapter gọi A1 CloseInterval **khi source vẫn IN_PROGRESS**, rồi áp dụng approved source pause/hold + history/audit, reason end of shift; giữ assignment/progress. Không stage source status sớm để A1 SaveChanges flush ngoài ý muốn; không duplicate close.
4. EndWorkday sau khi tất cả adapter thành công; publication/intents theo actual event contract; một outer commit. Source/policy/session/version/interval/audit/mandatory intent failure rollback toàn bộ.

DEV2 chịu Maintenance/PM adapter và exact occurrence/run identity; không duplicate time model, không PM PAUSED giả. Current ReturnWork/StartWork điều khiển corrective state không thay adapter Pause All. Rework pause transition cũng chưa chứng minh trong current schema/source; thiếu một supported kind thì toàn Pause All không accepted. Extension RequireOperation/SourceAsync cho PM là shared participant impact cần exact reviewed diff, không phải sửa DEV2 domain bằng DEV1.

## UC012 / GBR-033 — exact dependency handoff

BranchAdministrationService DeactivateAsync :90–106 hiện chỉ read/check/return; known blockers 409, còn lại 503. BranchDeactivationDependencyReader:17–55 luôn CanDeactivate=false/ScopeResolutionComplete=false. Không bật khi thấy count=0.

| Dependency | Authoritative table/predicate | Writer owner | Current locking | Required coordination | Evidence | Status |
|---|---|---|---|---|---|---|
| Active Rooms | rooms.BranchId + IsActive | DEV1 Room writers | Current Floor/Area writer giữ User→Branch U/H; Room writer current missing | Room create/reactivate/move phải giữ active target branch; không lấy Floor/Area proof cho Room | HotelStructureCreationService:52–65; Entities/Room | PENDING_DEV1_WRITER |
| Warehouse stock | StockMovements→Warehouse.BranchId; SUM(SignedBaseQty) theo warehouse+item !=0; distinct warehouse, kể cả inactive warehouse/negative integrity case | DEV2 Inventory | Catalog→warehouse/stock app locks; SaveWarehouse active branch check ordinary Any, không retained branch guard đã chứng minh | SaveWarehouse new/transfer/reactivate, stock posting/receipt/transfer destination giữ active branch; existing-history operations phân biệt khỏi new work | InventoryService.cs:90–128; Masters:64–104; Documents/PostCore; ReceiptIntegration | PENDING_DEV2_CONFIRMATION |
| Operational/Cleaning/Rework/Inspection tasks | Task.BranchId; cleaning/rework→Room; inspection Room + exact cleaning-cycle lineage | DEV1 | Historical batch User→Branch→RoomTypes; current writers missing | All create/reopen/reassign/transfer affected entrypoints reviewed; closed items không tự tính open | Reader/Classify, H-A/H-B | BLOCKED_DEV1_RUNTIME |
| Open incidents | incidents.BranchId, OPEN/IN_PROGRESS; RESOLVED unresolved GBR033 meaning | DEV2 FE06 | Create validation before tx; check Branch tồn tại, không IsActive | Validate active Branch under retained lock **inside** create transaction; status reopen must same protocol | IncidentService:228–232,319–322; status:20–132 | CONFIRMED_GAP |
| Corrective Maintenance | MaintenanceWork.Incident.BranchId hoặc consistent Department.BranchId, actual open states | DEV2 FE06 | optional assignee User→Incident U/H→Work U/H; no shared active-branch lock | CreateWork/Start/ReturnWork/Assign, authoritative branch re-read, active retained guard theo semantics new responsibility/open work | Maintenance Commands:25–95; Create:11–61; Workflow | PENDING_DEV2_CONFIRMATION |
| PM occurrence | Actual run/work scope qua Schedule.Room/Area/Asset và work identity, chỉ actual open work | DEV2 FE06/07 | Không có current complete PM participant evidence | Supply occurrence identity, source branch conflict/orphan handling, exact open predicate; **không block mọi due/active schedule** | PreventiveMaintenanceSchedule/Run + A1 reject PM kinds | PENDING_DEV2_SOURCE |
| Internal requests | internal_requests.BranchId + approved open-state mapping | DEV1 FE13 | Writer current chưa có | Future create/reopen/routing giữ branch protocol; DRAFT/COMPLETED meaning chưa tự bỏ khỏi checker | Reader/Classify; entity/status constraints | PENDING_DEV1_WRITER |
| Material requests | material_requests.BranchId; active approval/issue/remainder semantics | DEV2 FE09 | User locks→Inventory Catalog→Request U/H→cycle; Participant checks affiliation, không source auth | Save/Command/Issue và linked reopen phải revalidate active branch; chốt FULLY_ISSUED/REMAINDER_CLOSED/legacy terminal mapping | MR Commands:12–61,86–191; service:24–74 | PENDING_DEV2_CONFIRMATION |
| Replenishments | material_replenishments.Warehouse.BranchId; REQUESTED/PARTIALLY_RECEIVED open; FULLY_RECEIVED chưa chốt terminal | DEV2 FE10 | Catalog→sorted MR requests→warehouse/stock; ordinary branch-active query | Create/Receive/linked Receipt/Post/Close scope và same transaction; terminal/history không tự đổi vì branch inactive | ReplenishmentService:16–21,46–151; ReceiptIntegration:14–23 | PENDING_DEV2_CONFIRMATION |

Incident counterexample: deactivate có thể commit, branch vẫn tồn tại, rồi CreateIncident hiện tại vẫn qua Any(BranchId). Serializable scan của DEV1 chỉ chặn insert trong transaction scan; không ngăn writer chạy sau commit nếu không kiểm IsActive. Vì vậy cần cả writer cooperation và branch dependency/update transaction.

DEV2 submission: exact file/method changes cho các hàng FE06–10, source-resolved branch/affected sorted sets, retained guard placement trước final mutations, reload after waits, transaction ownership/rollback và cả hai SQL race orders. DEV1 review Branch integration tương ứng; không yêu cầu DEV1 duyệt lại DTO/service/test nội bộ không đổi shared contract. Không sửa writer trong handoff này.

## Quy tắc song song bổ sung — có hiệu lực cho bản handoff này

Codex B đang làm các UC DEV1 độc lập trong worktree riêng. COORD-FINAL-01 chỉ kiểm chứng current source của worktree DEV1 chính và shared contracts liên quan DEV2. Không bắt đầu implementation thuộc Codex B; không lấy source từ worktree B làm current runtime DEV1.

Danh sách worktrees được khám phá lại bằng `git worktree list --porcelain` tại cả preflight và postflight, không dùng số lượng hoặc danh sách đường dẫn cố định. `evidence/PARALLEL_PREFLIGHT.json` và `evidence/PARALLEL_POSTFLIGHT.json` ghi exact path, HEAD, branch, index SHA256 và status từng worktree. Bản này chỉ đọc metadata Git ở các worktree ngoài DEV1 chính; không capture lại source của Codex B.

Không sửa/reset/clean/checkout/chuyển branch/đồng bộ/di chuyển/xóa worktree Codex B. Không ghi Git config hoặc index. Hoạt động đồng thời của B, nếu có, được ghi riêng; không nhận là thay đổi do COORD-FINAL-01 và không tuyên bố đã kiểm chứng implementation của B.

Trước tích hợp Codex B phải có bàn giao exact diff, base HEAD/branch, danh sách file và SHA256; review conflicts với toàn bộ local patches DEV1/shared contracts hiện tại; xin approval riêng cho phạm vi tích hợp cụ thể. Chỉ tích hợp và chạy verification phù hợp sau approval. Cùng HEAD không có nghĩa cùng working-tree source. Không tự động đồng bộ branch hoặc áp dụng diff của B.

Các kết luận về tìm kiếm ở worktrees trong bản capture trước chỉ là evidence lịch sử. Bản handoff này không khẳng định source của worktree B vẫn sạch hoặc không có publisher tại thời điểm B đang làm việc. Source/runtime được bàn giao vẫn là current DEV1 chính, đã đối chiếu hash trước/sau. Thay đổi source được dựa vào trong DEV1 chính phải dẫn tới `SNAPSHOT_INCONSISTENT`; thay đổi metadata của B chỉ được ghi nhận riêng khi không làm thay đổi source được dựa vào.

Không thay đổi approval boundary: business/architecture decisions đã chốt không tự cấp quyền áp dụng shared DDL/EF/DI/RBAC/coordinator. DEV2 có thể triển khai các thành phần nội bộ thuộc ownership theo contract; shared diff vẫn phải có file-specific review và approval riêng.

