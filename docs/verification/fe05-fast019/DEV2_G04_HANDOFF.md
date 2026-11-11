# FAST019 — một handoff quyết định G01–G05 (chưa approved)

Ngày: 2026-11-11 (Asia/Saigon). Owner nguồn thay đổi: FE05/BE1. Đây là proposal để DEV2/FS013/PL duyệt, không phải event registration, schema acceptance hay runtime authority. UC041_STATUS=BLOCKED; UC044_STATUS=BLOCKED; SOURCE_CONTRACT_READY=false. G06 không thuộc thay đổi này và vẫn pending theo CONTRACT-011 do PL cung cấp; không có artifact CONTRACT-011 độc lập trong pin để xác nhận thêm.

## 1. Exact source pins và khác biệt phải giữ

| Nguồn | Revision | Nội dung được dùng |
|---|---|---|
| FE05 FAST018 | `940b26b06f4bd19c457cdfea71a81a6d2c59ae19` | Parent FAST019, writer assignment, approve composition, N01 integration đang có |
| DEV2 N01 published | `d60022b487b5b59e84d6551a0ee5162c9fd32164` | `Services/Notifications/Fe05OperationStaging.cs`, `Fe05NotificationContracts.cs`, N01 store/graph; đọc bằng git object, không merge |
| FE04 R05 | `95f7422e284bc81bcfcf9f460dace52022de5cc7` | `CleanerAttributionContract.cs`, `CleanerAttributionSource.cs`; reader/capture là authority attribution hiện có |
| FAST019 | Git commit chứa manifest đi kèm | Chỉ DTO/preflight/client UUID candidate + focused refusal/transport tests + handoff/evidence |

Các đường dẫn Services ở bảng trên nằm dưới `src/HotelOps.Api/`. Exact blobs ở mỗi pin được ghi trong `source-pins.json`. FAST018 và N01 published có cùng request shape bắt buộc InspectionId, nhưng **khác commit guard**: FAST018 giữ verifier cho từng reservation bằng suffix UUID, kiểm tra DomainFingerprint và poison khi verifier lỗi; N01 published dùng một graph key actor/operation. DEV2 phải chọn revision tích hợp và chứng minh replay không thay mất verifier trước đó. FAST019 không sửa/cherry-pick graph. Giữ nguyên source đã verified FAST018; không dùng lịch sử test làm evidence mới.

Đã đọc SRS draft §5.3 UC041 và §5.6 UC044, Change Notes, EF RoomInspection/CleaningTask và mappings. SRS là historical draft: chọn inspector và due time, một open Inspection/cycle, notify inspector; Passed notify người đã dọn, Ready trừ effective block. Không suy diễn quyền Reception/Department Head hay schema từ draft. Explicit supervisor của candidate cần được chấp nhận cùng assignment/reassignment business contract.

## 2. G01 — identity có trước CREATE và giới hạn hiện tại

**Exact existing operation identity có thể dùng:** `(authenticated actorUserId, operationId: UUID)` theo existing N01 `store.LockAsync(actor, operation)`. UUID tạo một lần khi review, giữ cho cùng intent/request; actor lấy từ validated session, tuyệt đối không nhận actor từ body. FE05 candidate thêm nullable `operationId` vào body của POST `api/branches/{branchId}/inspection-assignment-candidate/{taskId}`, `If-Match` là TaskRevision. Không đổi public AssignInspectionRequest hay bật candidate controller. Client cũ không có key chỉ còn preflight; key empty bị từ chối. Một identity không đại diện một InspectionId và không cấp quyền replay. Reload tạo review mới; sau unknown outcome UI chặn retry tự động. Reload với key mới **không** phải resolution của operation cũ; future production cần durable lookup trước new intent.

**Blocker được xác nhận:** `Fe05OperationRequest(OperationId, BranchId, InspectionId, RequestFingerprint, RequiredEvents)` và `Fe05OperationIdentity.Fingerprint` đòi InspectionId dương; owner coordination cũng truy cập persisted Inspection. Writer chỉ có `inspection.InspectionId` sau `SaveChangesAsync` CREATE. `Fe05NotificationKind` chỉ có Failed=1 và ReworkRequested=2. Dùng task ID dưới tên InspectionId, 0, sentinel, preallocated giả hoặc Reserve sau CREATE đều không hợp lệ. UUID transport không giải quyết resource binding này.

Đã đọc candidate SQL tại N01 pin: `scripts/database/notifications/N01Implement06/01_ForwardCandidate.sql` giữ existing operation fingerprint/result_json/completed; publication thêm FK actual Inspection và event/source CHECK cho Failed/Rework cùng legacy events. Assigned/Passed chưa có nhánh hợp lệ trong CHECK. Vì vậy chỉ thêm enum/profile cũng chưa đủ; DEV2 phải duyệt exact constraint/registration/schema path và mapping resource trước CREATE, không chỉ đổi client field.

**Reservation binding proposal, SOURCE-ONLY:** DEV2/FS013 chọn mở rộng chính existing reservation request/coordination/store để hỗ trợ resource trước CREATE, bằng tuple có tên rõ ràng `(branchId, roomId, cleaningTaskId=currentCycleTaskId, expectedTaskRevision, expectedCycleRevision)` dưới operation actor/UUID và UC041 initial-assignment discriminator. Không đề xuất một operation ledger/scheme khác, không áp dụng tuple này vào trường InspectionId. Task là existing durable ID; current cycle hiện được chứng minh bởi `room_cleaning_cycle_state(room_id, cleaning_task_id)` và row_version, không có cycle ID mới. RoomId/branch/current cycle phải được server load và so với request, không tin client. Sau CREATE, completion của **cùng reservation** phải bind bất biến actual InspectionId, post-create root revision và supervisor binding revision/audit; Stage/Verify chứng minh nó thuộc tuple reserved. API/interface/SQL layout cụ thể thuộc DEV2; FE05 không đưa DDL hoặc interface shared mới vào source.

**Fingerprint proposal, chưa là wire contract:** giữ domain request fingerprint tách khỏi N01 ledger/event fingerprint. Sau owner chấp thuận encoding, SHA-256 của UTF-8 compact JSON array có đúng thứ tự:

`["UC041", actorUserId, branchId, roomId, cleaningTaskId, currentCycleTaskId, taskRevision, cycleRevision, checklistVersionId, inspectorUserId, responsibleSupervisorUserId, dueAtUtc, trimmedReason, true]`

Định dạng: IDs là số nguyên signed-64 không exponent, ETag là strong quoted 16 hex uppercase, dueAtUtc là `yyyy-MM-ddTHH:mm:ss.fffffffZ` (server normalize UTC, reject Unspecified); string JSON escape theo encoding DEV2/FS013 duyệt, không Unicode normalize reason. UUID là key tách riêng, không nằm trong domain fingerprint, tương tự existing issuer convention. N01 vẫn phải bind UUID/actor/resource/event set trong ledger fingerprint. Đây là specification candidate cần owner chọn/duyệt, **không triển khai encoding hay hash UC041 mới** khi chưa được phép. Không dùng browser JSON hash làm authority.

ChecklistVersionId là version được load dưới catalog lock và so với expected ID; chưa có expected version row revision trong assignment DTO. Inspector/supervisor là explicit IDs; không có account rowversion được client chấp nhận. Retain actual Users rows/eligibility locks, recheck permission/branch/account/qualification trước commit. Initial CREATE chưa có Inspection/supervisor binding revision: phải ghi actual persisted post-create revisions vào result/receipt, không chế revision đầu vào. Reassignment cần expected persisted InspectionRevision + supervisor binding revision + previous inspector/supervisor + cycle/task/checklist binding được owner quyết định. **Reassignment vẫn closed**, không dùng initial-create key hoặc tự thêm semantics.

### Transaction sequence phải được DEV2/FS013 chấp nhận

1. Ngoài transaction chỉ discovery/hint đủ để xác định roster; không giữ hint như proof.
2. Caller mở một Serializable transaction, attach NotificationCommitGuard; eligible Users/prelock actor + selected inspector + explicit supervisor theo existing retained roster, reauthorize session/branch/catalog/task/current cycle/checklist/SOD, khóa parent/range theo protocol hiện có.
3. N01 Reserve bằng actor/UUID + approved pre-create binding **trước domain CREATE**. Completed replay: Verify durable graph + exact result/source/recipient authorization trước trả lại; không chạy mutation lại và không bị initial Pending eligibility che mất completed lookup. Adapter cần phân biệt replay với fresh eligibility, vẫn giữ authorization/source locks.
4. Fresh: existing assignment domain CREATE + supervisor bind + audits, cùng context/physical transaction. Bind actual generated InspectionId về reservation; kiểm tra open Inspection trong locked task/range, không chỉ lock theo UUID.
5. Stage bắt buộc `inspection.assigned` version 1 cho exact persisted selected inspector/current cycle, theo read policy đã duyệt; không producer/worker riêng.
6. Trusted completion/result + N01 receipt + Verify; guard kiểm tra lại toàn graph, actor/session, task/cycle/version/root/binding, Users/read eligibility và event recipients. Một guarded Serializable commit bởi caller duy nhất. Lỗi bất cứ bước nào: poison guard + rollback toàn domain/binding/audit/publication/reservation; participants không commit/rollback riêng.

### Replay/collision decisions

| Tình huống | Quy tắc đề xuất / acceptance còn thiếu |
|---|---|
| Cùng actor/key, cùng canonical request | Completed trả exact result/receipt sau Verify, không CREATE/audit/event lần hai |
| Cùng actor/key, khác task/cycle/version/inspector/supervisor/due/reason | Conflict, không overwrite fingerprint hoặc retarget reservation |
| Khác key cùng task/cycle, đồng thời | Serialize trên existing parent/cycle + open-Inspection range; tối đa một new open Inspection; người thua conflict/stale, không event dư |
| Khác actor cùng UUID | Existing store key có actor; vẫn tranh chấp cùng domain task/cycle, không bypass uniqueness |
| Incomplete reservation/rollback/deadlock/timeout | Không coi completed; current N01 báo incomplete rollback required. Không retry mutation tự động; client giữ key khi resolve unknown outcome |
| Cycle advance / root reassigned / user revoked | Fail closed khi current relationship/eligibility không còn chứng minh được; receipt cũ không cấp quyền source read mới |
| Reassignment | New explicit intent/key và expected root/binding revisions; semantics/version policy/recipient behavior chưa approved, NOT_RUN |

**Open approvals G01:** DEV2/FS013 chấp nhận pre-create resource representation và immutable completion binding; canonical encoding/fingerprint; exact event/profile registration; durable result/replay validation và lock ordering (kể cả completed replay); PL chấp thuận explicit supervisor + initial/reassignment business contract/schema. Existing schema/interface không đủ nên không thể bật writer trong FAST019.

## 3. G05 — reuse accepted-binding seam và exact dependency

Giữ nguyên `InspectionApproveComposition` và `IInspectionApproveAcceptedBinding`:

```csharp
Task RequireAcceptedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, CancellationToken ct);
Task<string?> ReserveOrReadCompletedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, CancellationToken ct);
Task StageRequiredAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string revision, CancellationToken ct);
Task<string> CompleteWithTrustedSqlReceiptAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string revision, CancellationToken ct);
Task VerifyCompletedAsync(HotelOpsDbContext db, InspectionIssuerOperation operation, string receipt, CancellationToken ct);
```

Operation hiện có: `InspectionIssuerOperation.Approve(key, actor, branch, inspection, expectedETag)`. Existing ASCII canonical là `FE05|1|UC044|actor|branch|inspection|UPPERCASE_UNQUOTED_ETAG|NULL`, SHA-256; key tách khỏi fingerprint. Không đổi canonical này. Binding phải giữ cùng DbContext/connection/physical Serializable transaction và cancellation. Không implicit transaction/commit trong participant.

**Exact existing sequence:** accepted gate → lock/authorize source (existing Users roster/prelock phải bao gồm authoritative R05 cleaner, hint chỉ dùng discovery) → ReserveOrReadCompleted → nếu replay: Verify + RequirePassedReceiptSource + commit guard; nếu fresh: `ApproveDomainInCallerTransactionAsync` (mandatory PASS/current-cycle/SOD/revisions, Inspection PASSED/COMPLETED/PASS, task COMPLETED, audit) → StageRequired(Passed, post-write revision) → CompleteWithTrustedSqlReceipt → validate persisted source/receipt/writer transaction → original `InspectionRoomProjectionAdapter.ApplyAsync` → VerifyCompleted/source/authorization guard → một caller commit. Không viết READY riêng, không chuyển projection lên trước durable completion để né proof.

**Dependency quan trọng:** Stage chạy trước completion/projection nên không được đòi READY hoặc completed conclusion receipt như điều kiện để stage event. Stage phải kiểm tra persisted uncommitted PASS root/task và R05 recipient dưới cùng transaction; receipt/trusted SQL issuer được xác thực ở completion, projection và final Verify. Nếu DEV2 profile đòi receipt ngay trong Stage thì contract tạo vòng phụ thuộc; owner phải xác nhận cách tách validation, không tự reorder composition. N01 receipt `FE05_N01_FS013_CANDIDATE_V1` không phải conclusion receipt `FE05_INSPECTION_CONCLUSION_V1`, và cả hai trước commit đều không chứng minh durability.

**Exact R05 original cleaner:** gọi transaction-only `ReadInspectionCleanerAttributionAsync` qua existing FE05 authorized reader/`InspectionR05CleanerSource`; bind `CleaningTaskId, RoomId, BranchId, AssignmentId, CleanerUserId, CompletionProgressId, CompletedAtUtc, CompletionRevision, WriterTransactionId`. Join persisted Inspection→task→room→current-cycle, exact completion assignment/progress, actual_end_at/completed_at và eligible cleaner. CompletionRevision là immutable completion snapshot, **không phải live task/cycle rowversion**. Re-read under locks và so retained identity/roster trước staging/guard. Thiếu/ambiguous/stale/foreign source phải từ chối; không latest cleaner, actor, Rework assignee hay tự capture/backfill attribution trong FE05.

**FE05-owned source còn thiếu sau approvals:** concrete accepted-binding adapter (chỉ implement seam trên), exact Passed locked source/recipient adapter dùng R05, roster discovery/prelock integration và durable replay mapping. Chỉ triển khai khi DEV2 supplies accepted event/issuer/receipt/staging interfaces + PL read decisions; hiện không thay ClosedInspectionApproveBinding, DI hay public writer.

**DEV2/FS013-owned dependencies:** register `inspection.passed` v1 và source policy trong existing graph; accepted staging/verification participants, immutable domain-result/N01 receipts; approved SQL issuer registration, object/module hash/execute authority và integration revision. Existing expected issuer string `FE05.UC044.UC045` version 1 là điều kiện consumer kiểm tra, không phải lời cho phép activate issuer. Không tạo trusted issuer/profile/SQL authority mới.

**FE04 proof/projection:** existing adapter kiểm tra registered issuer SQL module definition hash + execute-as owner, completed reservation exact actor/key/source/post-write revision/conclusion và trusted receipt; `READINESS_OVERLAY_V1` schema/READY support, mandatory criteria và effective block/current cycle/no-open-Rework phải hợp lệ. R05 attribution proof không thay conclusion receipt; conclusion receipt không thay N01 graph. EffectiveBlock giữ BLOCKED overlay, không hứa event nghĩa là phòng READY. Different-transaction FE04 consumer phải đọc và xác minh durable committed proof; đây là acceptance riêng chưa chạy. Optional criterion FAIL chưa được tự đổi thành PASSED read-policy hoặc quyền evidence; giữ mandatory-PASS rules hiện hữu, PL xác nhận final semantics.

**Open approvals G05:** DEV2 event/profile/source/stage/issuer/result transaction participation; FS013 durable completion + replay verification; PL event-specific cleaner read contract và optional-FAIL semantics; FE04 exact schema/proof/projection runtime prerequisites. Acceptance implementation + live successful commit/replay/concurrency/rollback/independent consumer đều NOT_RUN. Closed binding refusal PASS chỉ chứng minh từ chối.

## 4. G02/G03 — exact event-specific read decisions, chưa cấp quyền

| Event candidate | Exact relationship trước source read | Existing permission candidate |
|---|---|---|
| `inspection.assigned` v1 | recipient == persisted `room_inspections.inspector_user_id`, exact Inspection/task/room/branch/current cycle, current assignment revision; retained selected inspector không đủ nếu persisted root đổi | `INSPECTION_PERFORM` + eligible account/grant + applicable branch/resource scope; không tự grant `INSPECTION_VIEW` |
| `inspection.passed` v1 | recipient == exact R05 original cleaner trong cùng Inspection/task/current cycle, completion binding nguyên vẹn; state PASSED/COMPLETED/PASS và task COMPLETED | `CLEANING_VIEW` + eligible account/grant + branch + OWN-resource relation; cần PL approved PASSED summary policy riêng |

G02 không dùng recipient ID membership làm quyền. G03 không reuse `Own cleaning-task/current-cycle FAILED summary` trong existing OriginalCleaner descriptor, không mở rộng FAILED sang PASSED. OWN chỉ có nghĩa sau khi chứng minh exact relationship; GLOBAL/BRANCH grant vẫn không thay relationship. Thực thi account/session/permission/scope và exact relationship tại staging, final commit verification, inbox/source read/delivery theo graph DEV2; revoked/reassigned/cycle advanced phải deny/hide theo owner contract, không lộ source khác. Late delivery/cycle advance semantics cần DEV2/PL duyệt; không gửi payload cũ như một quyền mới. `acceptedConcreteBinding` tiếp tục null, private evidence=false.

**Minimal allowlist đề nghị:** display chỉ event type/version, fixed title + generic message (không tên/notes/room label). Authorized source summary có `inspectionId, branchId, roomId, cleaningTaskId, inspectionRevision, cycleRevision, workflowState`; Assigned thêm `checklistVersionId, dueAtUtc`; Passed chỉ `conclusion=PASSED`. AssignmentId/completionProgressId/completionRevision/cleaner ID/writerTransactionId giữ trong trusted validation metadata, không tự đưa vào display payload. Publication/operation IDs là graph metadata, không authorization. Không result details, failed result IDs, private evidence/URLs, notes, history, guest info hoặc arbitrary action URL. Source-link resolver phải tự reauthorize exact resource; không broaden existing detail endpoint chỉ để mở notification. Nếu cần summary endpoint mới, chỉ thêm sau read-policy approval.

## 5. G04 event registration checklist cho DEV2

| Quyết định | Đề nghị cần duyệt | Trạng thái |
|---|---|---|
| Assigned event | `inspection.assigned`, version 1, FE05 producer, one exact selected inspector, G01 resource→generated root binding, G02 policy/allowlist | PENDING, chưa enum/catalog/profile |
| Passed event | `inspection.passed`, version 1, FE05 producer, one exact R05 original cleaner, G03 policy/allowlist, G05 seam | PENDING, chưa enum/catalog/profile |
| N01 schema/interface | pre-CREATE task/cycle resource; immutable actual Inspection completion mapping; replay receipt/source validation; no fake root | PENDING DEV2/FS013, source-only |
| Issuer/storage/projection | exact approved issuer/hash/execute authority; domain + N01 receipt atomicity; FE04 schema/projection proof | PENDING owners, không DDL/runtime |
| Source revision | retain FAST018 graph fixes or DEV2-approved successor pin; tests for verifier preservation | PENDING DEV2, không overwrite |

DEV2 có thể duyệt/chỉnh từng row bằng exact revision + acceptance evidence; tài liệu này không tuyên bố G04 closed. Không sửa notification graph, không parallel notification system, không worker/producer activation, không UC047 rewrite.

## 6. Evidence boundary

Xem REPORT.md và logs đi kèm để biết kết quả thực chạy. Focused unit/transport/refusal/replay-key tests không phải authoritative SQL replay/concurrency acceptance. Các case bắt buộc còn NOT_RUN: two-connection different-key same-cycle assignment race; same-key lost-response durable replay; changed-fingerprint conflict trên SQL; reassignment/cycle-advance/revocation; mandatory-stage failure rollback tất cả tables; Passed stage/issuer/projection failure rollback; separate-transaction FE04 consumer; successful authenticated HTTPS mutation. N01 L1/L2 SQL NOT_RUN vì runtime chưa approved. Không shared SQL, merge/deploy/force-push, owner-source edits hoặc tự bật accepted binding.
