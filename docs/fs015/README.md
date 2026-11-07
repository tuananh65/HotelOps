# FS-015 — UC033 / UC034 cleaning workflows

Status: **FS-015 PROVISIONAL V1; chưa đạt shared integration acceptance**; implementation/verification workflow V1 trên isolated trial đã hoàn tất. UC033/034 và UC031 vẫn **PROVISIONAL V1**, chưa shared integration acceptance, chưa cộng UC credit. FE-04 **NOT_READY_FOR_MERGE**. Baseline trước FS015 `d2f0d0faffe614c1fd89c5ebaa30a76c241395bc`, nhánh `feature/dev1-room-ops`, working tree sạch trước sửa. Không có commit/handover DEV2 mới được chỉ định; không audit lại repository hoặc FS001–013.

## UC và transaction

| UC | Implementation |
| --- | --- |
| UC033 Report Blocker | Current assignee/CLEANING_EXECUTE/branch, active BLOCKER reference + note; đóng đúng interval của Working workday, append BLOCKED progress log, insert state vào **sidecar FS014 hiện có**, workflow ON_HOLD, readiness PENDING_CLEANING + effective block, audit và mandatory supervisor staging cùng transaction. Không mở nguồn blocker thứ hai. |
| UC034 Complete | Current assignee, IN_PROGRESS/current cycle, Working workday/current interval, không blocker active/unreconciled/linked thiếu confirmation; snapshot bắt buộc hoàn tất, progress ETag khớp. Đóng interval, completion progress log với legacy status **COMPLETED**, task workflow **PENDING_INSPECTION** (legacy task status vẫn IN_PROGRESS), actual end/note, readiness PENDING_INSPECTION + effective block, audit/staging cùng transaction. Completion actor/time lưu ở progress log/audit/event; không invent completed_by column. |
| UC031 Resume | Giữ model FS014 và lock/interval semantics; cho phép đúng fresh trial FS015 có owner marker. Không suy diễn resolved cho Incident/Material Request. Actual start và interval history được giữ; Report → Resume → Complete đã kiểm tra qua API thật. |

Controllers chỉ HTTP/DTO. Dùng BatchAsync + HotelOpsDbContext/WorkTime participant; lock users tăng dần → WorkTime → branch/catalog, revalidate permission trước commit. Không sửa generated entities/mappings, global DI, shared configuration hoặc room current-cycle pointer. Không ghi READY, Inspection results hay implement UC035–048.

`If-Match` là task ETag. Complete còn yêu cầu `progressETag` trong body: item updates có rowversion riêng, không được bỏ qua optimistic concurrency của snapshot. Note 1–500 ký tự, tối đa 5 ảnh/PDF; reuse EvidenceUpload, validator, private storage và progress_evidence_links hiện có (JPG/PNG/PDF đúng signature/MIME/extension, tối đa 10 MiB/file). Reporter/time/source identity ở CleaningProgressLog; ảnh gắn với log nguồn. File rollback được dọn; nếu commit outcome chưa rõ thì giữ file để không xóa bytes của attachment đã commit, có thể cần dọn orphan thủ công sau xác minh.

Idempotency dùng **notification_operation_reservations hiện có**, key `(actor, GUID operation)`, SHA256 fingerprint V1 binds actor/branch/task/action/original task ETag/note/reason/progress ETag/photo digests. Replay reauthorize current owner và kiểm tra receipt identity/state/staging proof, trả receipt/ETag gốc, không mutation lần hai. Cùng key khác payload trả 409; stale task/progress ETag trả 412. Recovery sau commit/cancellation chỉ đọc durable receipt bằng context/transaction sạch và budget 10 giây, không thực hiện mutation mới. SQL transaction/audit/readiness/publication/intent failure rollback; không bỏ mandatory event để làm xanh tests.

## Notification V1 — PROVISIONAL_NOT_ACCEPTED

Mở rộng **ProvisionalCleaningNotification / ProvisionalCleaningDelivery hiện có**, cùng ba bảng operation/publication/delivery_intent; không có publisher, dispatcher infrastructure hoặc queue thứ hai. Không đăng ký hosted dispatcher. Delivery candidate phải được gọi explicit và có thể chọn publication cụ thể; test chỉ drain publication của fixture.

| Event name | Schema | Source identity / occurrence key | Recipient |
| --- | --- | --- | --- |
| CLEANING_BLOCKER_REPORTED | 1 | CLEANING_TASK; taskCycleId = current cleaning task; progressId là blocker source. `CLEANING:{taskId}:BLOCKER:{progressId}` | Active HK_SUPERVISOR role trong branch, active employment/account + explicit branch access + eligible CLEANING_VIEW BRANCH/GLOBAL grant. Không hard-code ID. |
| CLEANING_TASK_COMPLETED | 1 | CLEANING_TASK, task cycle + completion progressId. `CLEANING:{taskId}:COMPLETE` | Cùng supervisor scope, để tiếp tục phân công Inspection; không tạo inspection giả. |

Payload chứa schemaVersion, eventName, occurrenceKey, sourceType, taskId/taskCycleId/roomId/branchId/assignmentId/progressId, actorId, operationId/publicationId, occurredAtUtc và sourceETag. Không đưa password/token/photo bytes vào payload/audit. Notification receipt `PENDING_DELIVERY` là snapshot lúc commit, không tuyên bố đã giao hoặc shared accepted.

Recipients được đọc lại dưới transaction/locks và trước commit; không có authorized supervisor thì fail-closed. Delivery revalidate recipient role/access/read permission và source branch trước tạo inbox + recipient + linkage trong cùng transaction. Revoked/inactive/out-of-scope chuyển TERMINAL; transient DB error retry có backoff trên intent hiện có. Unique recipient/publication và transactional intent lock ngăn duplicate inbox. Component đã kiểm tra explicit delivery + lần gọi thứ hai EMPTY; chưa acceptance toàn bộ Notification Center.

Schema candidate mở event CHECK và thay unique assignment publication bằng filtered unique dành riêng CLEANING_TASK_ASSIGNED, thêm unique occurrence key cho closure; không thay SQL assignment cũ trên trial lịch sử. Trạng thái sender/recipient/audit trong ledger cùng transaction. Actor/role grants không được seed bổ sung dựa trên giả định permission mới.

## Isolated SQL và source changes

Chỉ target `HotelOps_Canonical_FS015_Cleaning_V1_Fresh`, `HotelOps.FS015.Owner=DEV1_FS015`, `HotelOps.FS015.Closure=PROVISIONAL_V1`; runtime còn yêu cầu Development và explicit `CleaningClosure:Candidate=FS015_PROVISIONAL`. Mặc định OFF; shared/production không mở. `New-CleaningTrial.ps1` dựng foundation từ frozen source manifest FS013, retarget exact guard trên DB mới; reuse FS014 blocker DDL và notification proposal cũ. Không sửa source SQL cũ hoặc ghi database shared/production/FS014. Không drop/reset.

Lượt provision đầu dừng ở script closure do T-SQL EXEC ghép QUOTENAME trực tiếp. Transaction rollback; sửa dùng biến @ddl rồi cài forward-only phần còn thiếu trên đúng trial mới. Không chạy lại destructive migrations hoặc reset history. Synthetic fixture setup trong transaction; mỗi case append room/user/task mới. SQL fixtures chạy tuần tự trong một xUnit collection; concurrency nghiệp vụ vẫn test chủ động bằng Task.WhenAll.

File tạo/sửa thuộc FS015:

- Backend: CleaningExecutionController.cs, CleaningLifecycleDtos.cs, CleaningLifecycleService.cs, CleaningClosureBoundary.cs, CleaningBlockerResolution.cs; Notifications/ProvisionalCleaningNotification.cs, ProvisionalCleaningDelivery.cs, **ProvisionalCleaningClosure.cs** mới.
- Frontend: api/dev1/cleaningExecution.ts, screens/housekeeping/CleaningExecutionPanel.tsx; tests/cleaning-execution.test.mjs và cleaning-execution.spec.ts. Form Report/Complete chỉ mở khi server capability PROVISIONAL_V1; dùng CSRF/If-Match/key hiện có, giữ nguyên command khi retry outcome chưa rõ. Complete hiển thị snapshot bắt buộc, upload qua validator đã có; stale response reload nguồn và giữ thông báo.
- Tests: CleaningResumeSqlTests.cs chia sẻ fixture và hỗ trợ marker/target FS015; **CleaningClosureWorkflowTests.cs**, **CleaningClosureLiveTests.cs** mới.
- SQL/provision: scripts/database/release/fs015-candidate/01_ClosureWorkflow.sql; scripts/verification/FS015/New-CleaningTrial.ps1.
- Documentation/evidence: README này, docs/unit-test-inventory/FE-04/UNIT_TEST_INVENTORY.md, evidence/ của FS015. Không tạo Tester ZIP/PR hoặc báo cáo trùng lặp.

## Tests thực tế / evidence

| Level | Kết quả và evidence |
| --- | --- |
| Backend build | PASS, 0 warning/0 error — evidence/build.log |
| Solution test | Exit 0; solution chỉ có API, không dùng làm số test — evidence/solution-test.log |
| Unit regression | 233 PASS / 0 FAIL / 0 SKIP — evidence/fs015-unit-verified.trx (lượt fs015-unit-final.trx cũng 233 PASS); 16 unit cases mới cho fingerprint, photos và malformed replay receipt |
| SQL component | 37 PASS / 0 FAIL / 0 SKIP: 23 closure + 14 Resume regression — evidence/fs015-sql-serialized-final.trx. Real service/EF/SQL, session validation double; fault audit/readiness/publication/intent và commit-ack là interceptor. |
| Node regression | 57 PASS / 0 FAIL — evidence/node-final.log; riêng cleaning-execution 11 PASS. Có prototype regression nền; không dùng prototype PASS làm backend acceptance. |
| Frontend build | PASS — evidence/frontend-build.log; giữ warning React Router use client / chunks >500KB |
| Browser interception | Lượt đầu 11 PASS/1 FAIL do select label; retest riêng Report 1 PASS sau accessible label. Ca stale Complete retest 1 PASS — evidence/browser-stale-retest.log. Tổng 13 case identities PASS qua lượt gốc + targeted retests, không phải một clean 13/13 run. Lượt stale đầu giữ tại browser-stale-final.log (GET count bị StrictMode thêm read, không phải duplicate POST). Không dùng interception làm real HTTP/SQL acceptance. |
| Live HTTPS/API/SQL | **1 PASS**, evidence/fs015-live-final.trx (lượt đầu cũng PASS, fs015-live-attempt1.trx). API process thật + trusted local TLS, login/cookie/CSRF thật; Report kèm PNG, replay, Resume, UC032 checklist API, Complete/replay, SQL intervals/publications/attachment, logout. Không bỏ TLS certificate validation, không ghi plaintext credential. API process owned đã dừng. |

Focused backend đầu 17 PASS/5 FAIL (evidence/fs015-workflow-attempt1.trx): completion log status sai CHECK, linked fixture ETag cũ, audit fault được wrap thành DB exception/recovery và commit interceptor phát sinh lại ở recovery. Sửa mapping/harness theo semantics thật, retest 22 PASS (fs015-workflow-retest1.trx). Không sửa constraint hoặc bỏ assertion nghiệp vụ. Lượt gộp SQL đầu 31 PASS/6 FAIL (fs015-sql-final.trx) do hai fixture classes ghi cùng catalog song song gây SQL deadlock; giữ receipt lỗi, serialize fixture collection rồi chạy lại. Không thêm retry vào business SQL hoặc nới assertions; concurrency Task.WhenAll giữ nguyên.

NU1900 vulnerability feed chưa truy cập được khi restore test project; browser có NO_COLOR/FORCE_COLOR warning, PowerShell stderr wrapper có NativeCommandError quanh npm notice dù native exit 0. Build C# không có warning/error mới. Không đo coverage. Workday được chuẩn bị bằng fixture foundation; **public UC139 HTTP NOT_RUN**, production least-privilege/shared deployment và live UI browser E2E với API thật chưa xác minh. Runtime logs của process live không được ghi để tránh lộ credentials; TRX chỉ lưu assertions/status.

## Remaining work / FS-016 / Git

Trial workflows không đồng nghĩa business/shared acceptance. Notification V1 chưa accepted DEV2; cần chốt publisher/event/recipient contract và deployment least privilege. Linked Incident/Material Request vẫn fail-closed nếu thiếu accepted source confirmation; không cho client tự gửi linked IDs để bỏ qua source authorization. Chưa có integration adapters cho alternative cross-module links. Cần review room readiness/history shared, Workday public deployment, rồi tiếp tục scope FE-04 UC035/036 và acceptance UC037/038 trong task riêng; không chuyển FE14/15 trước approval/merge FE04. Không tự gắn TESTER_READY/ACCEPTED.

Git source identity: source commit `eea52f7073d5667f7ccf079598eb683f6dea3424` (`feat(housekeeping): implement provisional blocker and completion workflows`), gồm 17 file source/tests/scripts. Project Leader đã trực tiếp phê duyệt commit/push FS015 lên `origin feature/dev1-room-ops` trong phiên Codex. Documentation/inventory và evidence được commit riêng; inventory dùng source SHA thực tế. Notification **PROVISIONAL_NOT_ACCEPTED**; shared/production feature flags **OFF**; FE-04 **NOT_READY_FOR_MERGE**; không cộng UC credit chưa nghiệm thu. Không force-push/Tester ZIP/Tester request/PR/merge.
