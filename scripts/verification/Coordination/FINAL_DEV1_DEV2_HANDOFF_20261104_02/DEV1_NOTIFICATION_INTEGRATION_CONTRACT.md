# Notification — source integration contract

Thiết kế theo quyết định COORD-FINAL-01; executable B01/B02/C01 còn REVISE. Không có publisher/outbox/worker implementation được tìm thấy trong current DEV1 source và các worktrees đã đọc trong capture trước (không phải rescan source Codex B hiện tại). A01 ACCEPTED là trạng thái chủ dự án xác nhận, không phải chứng nhận rằng local namespace/signatures đã có. DEV2 không phải chờ thêm discovery DEV1 để viết revised diffs; cần gắn đúng A01 source của mình vào submission.

## Source dictionary

Tất cả current file hashes/blobs: evidence/CURRENT_SOURCE_IDS.json. S là immutable snapshot cf8fb02 như evidence manifest. Historical IDs: assignment **H-A**=1b9f0d49b1a8d4a7435db421733c6472e6864b11; controller **H-C**=f2b771332a2cdb8942c809bfcd339c6d68df6550; batch **H-B**=8aba8703a8e26808af57208cdd7439ceb43c099e. Historical source không thuộc activated runtime.

## Sáu producers — actual state/signature/route

| UC | Writer/controller/method và lines | HTTP + permission | Trạng thái current |
|---|---|---|---|
| UC029 Assign Cleaning Task | S Services/Housekeeping/HousekeepingAssignmentsService.cs:32 `Task<RoomTypeBatchResult> AssignCleaningAsync(SessionReference ticket,long sid,long branch,long task,string tag,AssignCleaningRequest input,CancellationToken ct)`; H-C:29–31 AssignCleaning | POST /api/branches/{branchId}/cleaning-tasks/{id}/assign; CLEANING_ASSIGN | HISTORICAL_ONLY writer; PARTIAL ledger |
| UC037 Monitor Cleaning Tasks | Services/Housekeeping/CleaningMonitorService.cs:22 ListAsync(ticket,sessionId,branchId,CleaningMonitorQuery,ct); :65/:69 DetailAsync(ticket,sessionId,branchId,kind,id,[CleaningMonitorTimelineQuery,]ct); CleaningMonitorController.cs:16–23 | GET /api/branches/{branchId}/cleaning-monitor và /{kind}/{id}; CLEANING_VIEW branch/global | GET IMPLEMENTED, Cancel writer MISSING; UC PARTIAL |
| UC041 Assign Room Inspection | H-A:56 `Task<RoomTypeBatchResult> AssignInspectionAsync(SessionReference ticket,long sid,long branch,long task,AssignInspectionRequest input,CancellationToken ct)`; H-C:32–34 | POST /api/branches/{branchId}/cleaning-tasks/{id}/inspections; actual historical binding INSPECTION_PERFORM | HISTORICAL_ONLY writer; PARTIAL |
| UC044 Approve Room Inspection | Không có approve controller/service/writer trong current hoặc S đã kiểm | Route/binding MISSING, không đặt permission mới từ UC title | NOT_IMPLEMENTED |
| UC045 Reject Room Inspection | Không có reject controller/service/writer trong current hoặc S đã kiểm | Route/binding MISSING; record defect/finish inspection không phải Reject | NOT_IMPLEMENTED |
| UC046 Request Cleaning Rework | H-A:90 `Task<RoomTypeBatchResult> RequestCleaningReworkAsync(SessionReference ticket,long sid,long branch,long inspection,string tag,RequestReworkRequest input,CancellationToken ct)`; H-C:35–37 | POST /api/branches/{branchId}/inspections/{id}/reworks; INSPECTION_PERFORM | HISTORICAL_ONLY writer; PARTIAL |

Prefix current/historical paths Services/Controllers là src/HotelOps.Api/. Không đổi INSPECTION_PERFORM thành permission giả INSPECTION_ASSIGN. Current Monitor không có cancellation side effect, SaveChanges hoặc notification hook.

## Transaction, audit và publisher insertion point

H-B BatchAsync:22–60: clean same scoped HotelOpsDbContext; Begin Serializable :32; sorted actor+additional users :35; SQL session/permission Gate :36; eligibility :37; Branch UPDLOCK/HOLDLOCK :40; RoomTypes transaction applock :45; schema guard :47; writer callback :48; final gate/eligibility :50–51; **single Commit :53**. Rollback bằng await-using disposal khi non-success/throw, không có inner producer commit. BatchAuditAsync :193–201 ghi actor/branch/entity/before/after/changed fields và SaveChanges :199.

| Producer | Business SaveChanges / raw write / audit | Same-transaction hook và missing |
|---|---|---|
| UC029 | H-A:47–48 persist CleaningAssignment; :49 workflow ASSIGNED/legacy PENDING; :51 audit; :53 CLEANING_ASSIGN gate parameter | Sau authoritative assignment/workflow/audit, trước H-B:53. Missing current writer restoration + lifecycle + publication/replay integration |
| UC037 | Current ReadAsync :119 Begin, :120/:122 Gates, :123 read-only Commit; không business SaveChanges/audit | Không chèn publisher vào GET. Cancel writer phải được DEV1 thiết kế riêng, không có exact runtime hook hôm nay |
| UC041 | H-A:80–82 InspectorUserId + VersionId + inspection SaveChanges; :83 workflow; :85 audit | Sau audit trước H-B commit; missing runtime, assigned-inspector notification và source read |
| UC044 | Không SaveChanges/Commit/audit conclusion writer | Future atomic current inspection pass + source completion/room recalculation + audit + intent; chưa có file/hunk được duyệt |
| UC045 | Không rejection/defect/rework conclusion boundary | Future failed/rework-required + defect linkage + room/effective-block + audit + intent; chưa có hook |
| UC046 | H-A:108 SaveChanges rework; :109 workflow; :110 defect links; :112 audit | Sau audit trước H-B commit, cùng context; missing runtime/lifecycle/durable intent |

S InspectionExecutionService.ExecuteInspectionAsync:28 chỉ hỗ trợ read/start/save/finish/defect. Finish :90 chuyển PENDING_CONCLUSION/legacy IN_PROGRESS, **không** PASSED/FAILED; assigned InspectorUserId phải bằng actor. Không gán UC044/045 cho method này.

## Event → identity → recipient → permission/read → hook

Event labels là mô tả nghiệp vụ, không phải notification_type/event enum đã seed.

| Event | Immutable source identity | Recipient authoritative | Source-read endpoint / quyền | Integration / thiếu |
|---|---|---|---|---|
| UC029 assignment | CleaningTaskId/current cycle + new CleaningAssignmentId/revision | Persisted AssigneeUserId ở chính assignment vừa tạo; actor+target eligibility locks | Current GET cleaning-monitor/cleaning/{id}, CLEANING_VIEW branch/global; HK_STAFF quyền đọc route này chưa chứng minh | H-A:51→H-B:53; cần recipient-readable own route/binding và replay |
| UC037 cancellation | CleaningTask/current cycle + cancellation revision | Assignee tại cancellation boundary nếu có; không latest assignment khi delivery | Monitor GET hiện có; Cancel action permission chưa được bind | Cancel writer/room coordination/audit missing |
| UC041 inspection assigned | InspectionId + CleaningTaskId + checklist VersionId/revision | Persisted RoomInspection.InspectorUserId; eligibility + SoD | Historical GET /api/branches/{b}/inspections/{id}/execution, INSPECTION_PERFORM/exact inspector; **absent current** | H-A:85→H-B:53; cần current read endpoint |
| UC044 inspection approved | InspectionId + exact cleaning/rework cycle + conclusion revision | Actual final completer của cycle: MISSING persisted authoritative fact | Không có current inspection conclusion read endpoint đã chứng minh cho recipient; Monitor không tự thay thế | Conclusion + final-completer contract + lifecycle missing |
| UC045 inspection rejected | Như UC044 với failed conclusion/defect identity | Actual final completer + designated eligible supervisor; không broadcast các eligible supervisors | Recipient-specific read binding/route chưa có evidence | Writer + immutable recipient facts + room/rework missing |
| UC046 rework requested | ReworkId + InspectionId + CleaningTaskId + selected defect links/revision | Persisted CleaningRework.AssigneeUserId sau eligibility | Current GET cleaning-monitor/rework/{id} cần CLEANING_VIEW; assignee own access chưa được xác nhận | H-A:112→H-B:53; runtime/lifecycle/retry missing |
| UC139 unscheduled | WorkdayId + actor + branch + stable operation GUID | Một primary eligible branch DH | Current own-only workday GET không cho DH đọc user khác | Unscheduled writer, cross-user read, recipient resolver và publisher adapter missing |

## Persisted evidence: không suy diễn final completer

Current CleaningTask có ActualEndAt nhưng không CompletedBy; CleaningRework có AssigneeUserId/CompletedAt nhưng không final-completer field; CleaningProgressLog.LoggedBy là tác giả progress. WorkInterval.ClosedBy có thể là actor pause/hold, không là final business completer. AssignmentStatus COMPLETED không chứng minh ai thực hiện final transition sau reassignment/rework.

RoomInspection.InspectorUserId là persisted assigned-inspector fact. H-A EligibleInspector:20–27 chứng minh eligibility (active account/employment/membership/branch role, validity), không chọn unique supervisor recipient. DEV1 phải cung cấp reviewed completion identity theo current cycle/rework và source-read khi implement UC044/045; DEV2 không tự lấy latest assignment hoặc Department.ManagerUserId để lấp gap.

## B01 revised DDL checklist dựa actual inbox

Current SQL/mapping: notification_recipients có PK recipient_id; notification_id bigint, user_id bigint, delivery_channel varchar(30) default IN_APP, delivery_status varchar(30) default PENDING; UQ index_42 **(notification_id,user_id,delivery_channel)**. NotificationRecipient.cs không chứa operation/replay fields. notifications.notification_type varchar(60), SourceType varchar(60), SourceId nullable bigint, ActionUrl text; source reference này chưa tự xác minh quyền.

DEV2 revised B01 phải có actor+GUID uniqueness, fingerprint version/canonical bytes, immutable business result/publication, intent dedup, row/version or claim concurrency và durable retry scheduling. Actual composite inbox FK có thể dựa existing unique triple; nếu chọn binding có recipient_id hoặc key khác thì phải cung cấp exact referenced unique key/DDL. Không khai FK tới composite key chưa tồn tại. Nullable pre-delivery linkage phải có all-or-none/status rules; DELIVERED bắt buộc linkage không-null, đúng recipient/source publication và inbox row thực sự tồn tại.

Đây là constraints/checklist cho DEV2 tạo exact DDL, **không phải tự duyệt tên cột/state/DDL chưa được cung cấp**. Cần kiểm negative FK, forged DELIVERED, duplicate event/recipient/channel, race claim/crash/restart, idempotent installer/legacy conflicts và rollback/recovery trên disposable SQL đã được duyệt riêng.

## Replay, retry và explicit-user read

Operation reservation tạo/khóa trong caller transaction. Cùng actor/key + cùng canonical request trả original authorized result; khác fingerprint conflict; mất response/ambiguous commit phải resolve durable operation, không chạy lại business mù. Authorization hiện tại được re-evaluate khi replay; không replay dữ liệu source bị revoke chỉ vì key tồn tại. Không biến unknown/DB outage thành SOURCE_READ_DENIED.

Worker dùng publication/source/recipient facts immutable; re-evaluate recipient eligibility và source-read theo explicit recipient identity, không HTTP ambient actor của producer. IPermissionService hiện chỉ dùng ICurrentUserAccessor; EfPermissionDataSource nhận userId nhưng không phải complete recipient authority evaluator. Không fake HttpContext/session hoặc mượn actor grants. Shared explicit-user evaluator/source adapter và historical readable routes cần exact approval/test riêng.

C01 activation cần một scope gắn đúng approved producer + authoritative business operation + DbContext/physical transaction, không bật mandatory notification cho mọi SaveChanges/Commit toàn API. Login/session-touch/audit độc lập không tự trở thành producer. Scope completion phải xác minh actual business result/source revision và publication persistence, không tin client flag, count in-memory hoặc một SaveChanges đã chạy. Rollback/savepoint phải phục hồi guard state tương ứng SQL; ambiguous commit resolve bằng durable operation/replay, không tự coi đã rollback để chạy lại writer. Current sáu UCs chưa có live notification writer đủ điều kiện activation: Monitor GET không là writer và các writers còn lại missing/historical.

JOB03 claim/retry dùng delivery_intents; reservations/publications không là queue thứ hai. Postcommit transient failures retry với backoff≤60 phút; sáu terminal reasons theo decisions document. Không purge ledger/backfill V1. Business/audit/mandatory intent cùng transaction; notification delivery không được gọi network trong business locks.

## Quy tắc song song bổ sung — có hiệu lực cho bản handoff này

Codex B đang làm các UC DEV1 độc lập trong worktree riêng. COORD-FINAL-01 chỉ kiểm chứng current source của worktree DEV1 chính và shared contracts liên quan DEV2. Không bắt đầu implementation thuộc Codex B; không lấy source từ worktree B làm current runtime DEV1.

Danh sách worktrees được khám phá lại bằng `git worktree list --porcelain` tại cả preflight và postflight, không dùng số lượng hoặc danh sách đường dẫn cố định. `evidence/PARALLEL_PREFLIGHT.json` và `evidence/PARALLEL_POSTFLIGHT.json` ghi exact path, HEAD, branch, index SHA256 và status từng worktree. Bản này chỉ đọc metadata Git ở các worktree ngoài DEV1 chính; không capture lại source của Codex B.

Không sửa/reset/clean/checkout/chuyển branch/đồng bộ/di chuyển/xóa worktree Codex B. Không ghi Git config hoặc index. Hoạt động đồng thời của B, nếu có, được ghi riêng; không nhận là thay đổi do COORD-FINAL-01 và không tuyên bố đã kiểm chứng implementation của B.

Trước tích hợp Codex B phải có bàn giao exact diff, base HEAD/branch, danh sách file và SHA256; review conflicts với toàn bộ local patches DEV1/shared contracts hiện tại; xin approval riêng cho phạm vi tích hợp cụ thể. Chỉ tích hợp và chạy verification phù hợp sau approval. Cùng HEAD không có nghĩa cùng working-tree source. Không tự động đồng bộ branch hoặc áp dụng diff của B.

Các kết luận về tìm kiếm ở worktrees trong bản capture trước chỉ là evidence lịch sử. Bản handoff này không khẳng định source của worktree B vẫn sạch hoặc không có publisher tại thời điểm B đang làm việc. Source/runtime được bàn giao vẫn là current DEV1 chính, đã đối chiếu hash trước/sau. Thay đổi source được dựa vào trong DEV1 chính phải dẫn tới `SNAPSHOT_INCONSISTENT`; thay đổi metadata của B chỉ được ghi nhận riêng khi không làm thay đổi source được dựa vào.

Không thay đổi approval boundary: business/architecture decisions đã chốt không tự cấp quyền áp dụng shared DDL/EF/DI/RBAC/coordinator. DEV2 có thể triển khai các thành phần nội bộ thuộc ownership theo contract; shared diff vẫn phải có file-specific review và approval riêng.

