# Yêu cầu DEV2 — bàn giao A01 và duyệt exact Notification V1 integration cho UC029

**Người gửi:** DEV1 Room Operations. **Mục đích:** mở luồng Assign Cleaning Task an toàn, không viết lại Notification infrastructure. **Trạng thái:** REQUEST / chưa có approval mới, chưa gửi tự động. Baseline tích hợp DEV1 `a5b3f66e9732ff00035f0f5fc43971322e0122e6`, branch feature/dev1-room-ops, .NET10 single-project HotelOps.Api/SQL Server Database First.

## 1. Phần đã chốt, không yêu cầu DEV2 phê duyệt lại

COORD-FINAL-01 tại `28f0a5e4f582bc56ba101b48915d33baadeb7b95:scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104_02/DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md`: A01 interface ACCEPTED; A02 reported structurally complete; B01 architecture gồm notification_operation_reservations, notification_publications, notification_delivery_intents (chỉ bảng cuối là retry queue); caller-owned transaction; actor+stable GUID key/fingerprint; immutable publication/intents; mandatory stage failure rollback; delivery sau commit retry. JOB03 cadence5phút/backoff/max delay60phút, không purge reservation/backfill historical events V1. B01 executable DDL/B02 EF/C01 guard+DI phải REVISE, không được hiểu là đã duyệt.

DEV1 không thấy exact namespace/signature/blob A01/A02 trong baseline; đây là **UNKNOWN availability**, không phải bác bỏ interface đã accepted. Remote heads10 branch DEV2/BE2/develop2 đã đối chiếu bằng ls-remote ngày06/11/2026, cached source cùng SHA; bounded src path/symbol search chưa tìm publication infrastructure. Không kết luận về private/unpublished worktree của DEV2. Xin gửi đúng implementation/package, không yêu cầu tạo interface cạnh tranh vì DEV1 chưa có nó.

## 2. Điểm gắn producer thực tế

Existing active POST `/api/branches/{branchId}/cleaning-tasks/{id}/assign`, HousekeepingWorkflowController.AssignCleaning → `Services/Housekeeping/HousekeepingAssignmentsService.cs:RoomTypeCreationService.AssignCleaningAsync`. Task state PENDING_ASSIGNMENT→ASSIGNED (legacy PENDING); inserts CleaningAssignment ACTIVE; audit same transaction; response hiện `HousekeepingWriteDto.Notification = NOT_INTEGRATED`.

`Services/HotelStructure/RoomTypeBatchService.cs:BatchAsync` owns Serializable transaction, sorted UserEligibilityLock(actor+target), session/permission CLEANING_ASSIGN, eligibility, retained Branch, CatalogLock=HotelOps:RoomTypes:v1, source/version/active-assignment check, mutation/audit, final gate/eligibility, outer commit. Hook phải nằm trong chính transaction sau khi biết persisted AssignmentId/task revision + audit, trước commit. Không publish trong GET/SaveChanges toàn cục, không commit riêng hoặc send network trong transaction. No-source producer callback không đủ evidence.

## 3. Source/interface và payload cần DEV2 trả lời

| Thành phần | Yêu cầu cụ thể / proposed semantic contract |
| --- | --- |
| A01 actual API | Commit/path/blob/SHA256, namespace/signatures, constructor dependencies/lifetime, exact reserve/resolve-replay/stage/complete result methods and failure types; giữ accepted interface |
| DEV1 adapter | Đề xuất feature adapter nội bộ chuyển AssignmentPublicationFacts sang A01, cùng scoped DbContext và physical transaction. Đây là boundary đề nghị, không signature A01 đã có; DEV2 map sang actual API và chỉ rõ call sequence |
| Event catalog | Đề xuất logical event `CLEANING_TASK_ASSIGNED` V1 cho UC029; **chưa là seeded notification_type/enum**. DEV2 xác nhận actual existing name hoặc exact catalog change cần duyệt, schema version, recipient/channel policy |
| Immutable event facts | Server event/publication identity, operation GUID, actual actorId, branchId, taskId/current-cycle identity, roomId, persisted assignmentId, assigneeUserId, assignment server UTC, source event revision, due UTC; optional normalized reason chỉ khi policy approved; no session/credential/header blob |
| Recipient | Một AssigneeUserId của assignment vừa commit, không broadcast/Department.ManagerUserId/latest assignment khi retry; channel IN_APP theo existing schema |
| ActionUrl/read | Proposed authorized own-task header route do DEV1/DEV3 cung cấp; worker đánh giá explicit recipient identity, không producer HttpContext. Delivery không cấp quyền source; thông tin source header tối thiểu, không leaks audit/nhân viên khác |
| Result | Durable business result task/assignment identity + actual resulting revision + publication outcome. Staged khác Delivered; không hard-code DELIVERED khi enqueue. Replay returns original authorized result after current permission/read reevaluation |

Một assignment-revision change do reassignment có thể supersede event; unrelated progress update không tự mất recipient fact. DEV2 phải chỉ rõ source-specific supersession predicate; không dùng 'rowversion khác' cho mọi event. Terminal reasons đã chốt: SUPERSEDED, RECIPIENT_INACTIVE/NOT_FOUND/OUT_OF_SCOPE, SOURCE_READ_DENIED/NOT_FOUND; transient DB/auth/source outage phải retry, không terminalize thành denied.

## 4. Exact B01/B02/C01 package cần duyệt

- B01 DDL forward/verify/recovery/idempotent install+partial-version refusal trên actual existing schema; unique actor+operation key, versioned canonical request bytes/fingerprint, durable completed authorized result; immutable publication identity; intent uniqueness publication+recipient/channel+source transition; concurrency/claim/version/next-due/attempt evidence. Không thêm queue thứ tư hoặc purge reservation V1.
- SQL metadata FS007 đúng retained trial: **0/3 bảng B01**, existing notification_recipients PK recipient_id và UNIQUE(notification_id,user_id,delivery_channel). Delivered phải link durable đúng inbox recipient/source; composite FK chỉ được dùng với actual unique key. Nullable pre-delivery linkage all-or-none; forged DELIVERED/no inbox, duplicate intent và cross-recipient linkage phải bị reject. Không đòi FK tới key chưa tồn tại. Đây là checklist; DEV2 trả exact column/check/FK diff, không chạy DDL từ request này.
- B02 additive entities/DbContext partial, model↔actual SQL parity và EF insert behavior. Không sửa generated NotificationRecipient để ép navigation/principal key. Shared mapping changes phải liệt kê từng file/hunk/hash và review impact cả hai DEV.
- C01 activated-UC029 producer scope identity = approved operation + authoritative business action + same DbContext/physical transaction. Guard phải xác minh completed durable result/staged mandatory publication và persisted intent trước commit, không tin flags/count in-memory; reads/login/session-touch/audit độc lập không trở thành producers.
- Guard coverage: sync/async SaveChanges/Commit, raw ExecuteSql/ExecuteUpdate/ADO paths hoặc explicit unsupported bypass rejection; rollback/savepoint behavior, missing completion, wrong tx/context, exception/cancel, ambiguous commit+durable resolution. EF SaveChanges interceptor không tự bảo vệ DbTransaction.Commit bypass.
- DI exact Program hunk trên current baseline/hash trong SOURCE_EVIDENCE.json; same scoped participant/context, no cycle, no replacement auth/context registration. Hosted dispatcher/retry ownership thuộc DEV2. Không blanket activate cho tất cả writers.
- Joint lock sequence reservation↔Users/session/Branch/feature/source cần exact approved sequence + both-order SQL races. Existing revoke/global-admin ordering và maintenance/inventory locks không được 'giải quyết' bằng universal user-first assertion. Deadlock1205 là failure cần phân tích, không retry thành acceptance PASS.

## 5. Replay và failures cần thống nhất cụ thể

UC029 client stable Idempotency-Key GUID; fingerprint binds action/version, actor from session, branch/task, assignee, due UTC, normalized reason, adoption=false và original If-Match. Same key/same canonical command sau commit: current authorization then original result, không re-run state transition dù tag gốc cũ. Same key/different command409; commit chưa rõ phải resolve durable reservation; rollback chưa hoàn tất không được replay success. New key vẫn phải stale/state check bình thường. Không trả source data khi actor mất quyền chỉ vì reservation tồn tại.

Failure stage/publication/mandatory intent hoặc audit trước outer commit → rollback source/task/assignment/audit/reservation completion. Delivery network/inbox failure sau committed publication → giữ assignment, durable retry, MSG21/pending outcome theo API contract. Enqueue không đủ full notification acceptance: phải có inbox người nhận đọc được. Không yêu cầu DEV1 tự tạo queue, publisher hoặc independent transaction.

## 6. Acceptance và phản hồi cần gửi lại

DEV2 cung cấp exact ref+commit+diff/source SHA, approval receipt theo file/hunk, DDL/EF/DI compatibility report, unit/SQL races+rollback/replay/claim/crash/restart, transaction guard bypass tests, real inbox read với người nhận hợp lệ và denied/revoked recipient tests. Bảo toàn GET assignment-options, Cookie/CSRF và UC032; dùng proposed source-read adapter thật sau approval, không fake HttpContext.

DEV1 sẽ cung cấp producer facts, source-read/transition-specific supersession adapter sau review với DEV3; gọi accepted A01 đúng transaction, capture AssignmentId, response mapping và UI refresh/no unsafe retry. DEV2 cung cấp publisher/replay/store/inbox/dispatcher/guard integration package; chỉ shared changes cần joint approval, không yêu cầu DEV1 duyệt mọi DTO/service nội bộ DEV2.

Xin phản hồi: (1) exact A01/A02 source; (2) revised B01/B02/C01 package + pending approval owner; (3) mapping UC029 event/payload/result/read/supersession; (4) agreed lock/order/failure contract; (5) acceptance receipts hoặc explicit chưa triển khai. Không áp DDL/DI/RBAC hoặc gửi dữ liệu production theo yêu cầu này. New SQL writes chỉ được phép khi chủ dự án duyệt scope/hashes riêng.
