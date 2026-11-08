# FE05-FAST-003 — PARTIALLY_VERIFIED

**FE04_DEPENDENCY_STATUS: NOT_READY**

## Baseline / scope

Fetched và xác minh `origin/develop=bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c` (PR #19).
Worktree mới `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST003`, branch
`feature/dev1-fe05-failed-rework`. Không reset/merge/cherry-pick. FAST001/002 vẫn giữ
nguyên tại branch `feature/dev1-fe05-execution-history`, commit `3502ef74e7d8ce7172823c4fe750689b4095259c`.
Không copy frontend/auth/router vào backend candidate. Worktree INSP/FE04 không bị sửa.

## Code thực tế

- Reuse nguyên validator INSP-002; reuse loader INSP-004A và điều chỉnh hẹp để trả source
  có explicit persisted `room_cleaning_cycle_state.cleaning_task_id` khớp Inspection/task/room.
  Reuse `LifecycleInstalled` của FE04, không suy luận current cycle từ latest ID/time.
  Source đọc assigned version kể cả Retired, criteria/results/defects và cả direct/bridge
  attachments. Reuse D4 marker/rowversion; không tạo hash/revision authority khác.
- `InspectionConclusionSql.RejectAsync`: caller SERIALIZABLE transaction, existing
  validator, conditional Inspection rowversion/state/source update → FAILED/COMPLETED/FAIL,
  server inspected_at/reason, conditional task PENDING_INSPECTION → REWORK_REQUIRED.
  Không tự commit. SQL guards phát hiện thay đổi source/cycle/revision và task phase conflict.
- `InspectionConclusionSql.CreateReworkAsync`: persisted FAILED source, branch/room/task/
  current-cycle/revision, valid selected FAIL defects, no open Rework; tạo real identity,
  task/inspection/assignee/due và relational defect links trong cùng caller transaction.
  Đây là **SQL primitive**, không tự cấp authorization, assignee eligibility hoặc proof authority.
- `InspectionConclusionService`: existing BatchAsync/user/session/branch/catalog boundary,
  assigned-inspector Reject authorization và final gate; typed source previews, command
  validation, existing audit calls và unconditional fail-closed integration boundary.
- `InspectionReworkSourceService`: authorized persisted FAILED source facts/current-cycle/
  revision/defect/evidence reads. Response luôn `authorityEstablished=false`; không phải
  receipt cho FE04. UC046 existing route được chuyển vào preflight này, chặn legacy path
  trước đây có thể tạo Rework chỉ từ FAILED row rồi trả NOT_INTEGRATED.
- D4 accepted patch được tích hợp nguyên definitions/install/verification/recovery từ
  worktree INSP; Attachment partial mapping `UseSqlOutputClause(false)` được thêm vào
  target hiện tại, bảo toàn các mappings FE04. Không sửa generated entities hoặc SQL schema.
  Chỉ existing installers được chạy trên isolated trial; không deployment/shared SQL.

## APIs và DTO

Prefix `/api/branches/{branchId}/inspections/{id}`:

| Method / suffix | Contract / behavior |
|---|---|
| GET `/reject-source` | `RejectInspectionPreview`: persisted inspection/branch/room/task/version/state, ETag, defect/evidence IDs, `mutationAvailable=false`, dependency codes |
| POST `/reject` | `{reason,confirmed}`, strong `If-Match` + existing GUID `Idempotency-Key` convention; source/authorization/state validation, sau đó 503 `INSPECTION_INTEGRATION_REQUIRED` |
| GET `/rework-source` | `ReworkSourcePreview`: persisted FAILED source, current task, version, ETag, defects/evidence, `authorityEstablished=false`, dependency codes |
| POST `/reworks` (existing) | Existing `{assigneeId,dueAt,defectIds,reason}` + If-Match; 400 invalid subset, 409 invalid source/open Rework, 412 stale, 503 unavailable authority; **không trả 201** khi mandatory handoff chưa có |

HTTPS/cookie/CSRF conventions giữ nguyên. INSPECTION_PERFORM và branch SQL gates được
reuse; Reject còn yêu cầu đúng assigned inspector. Source reads không cấp INSPECTION_VIEW
cho worker hoặc nới OWN access. Collection/source DTO không bind EF graph từ client.
Preview không phải mutation authority; public writes vẫn load/recheck dưới locks.

## Transaction / concurrency / replay

Existing user/session → branch → catalog lock order; explicit current-cycle lock retained
trong caller SERIALIZABLE transaction. Existing lifecycle/catalog serialization được reuse.
Conditional root rowversion là stale authority dưới SQL locks; validator ETag comparison
chỉ là một lớp validation. D4 direct evidence metadata update đã chứng minh invalidation.
Root/task/identity/defect link changes rollback cùng caller; không tạo retry framework.
SQL core concurrency kiểm chứng một Reject winner, một conditional conflict; không tuyên
bố deadlock-free, đặc biệt child-first attachment writers vẫn có thể deadlock.

`Idempotency-Key` mới được validate theo convention hiện hữu. **Durable successful replay
NOT_IMPLEMENTED / NOT_RUN** vì ledger/staging Inspection chưa accepted. Repeated public
Reject chỉ chứng minh cùng trả 503 và không đổi source; không tính làm durable replay PASS.
Core duplicate Rework guard đã tested, nhưng không thay thế operation receipt.

## Evidence thực tế

- Backend solution build và WriteTests build: **PASS, 0 warning/error**.
- Loader unit/read-orchestration doubles: **19/19 PASS**; tách khỏi SQL verification.
- SQL core: **16 unique cases PASS qua 3 lượt**: lượt đầu 6 PASS rồi lỗi ownership probe
  của harness chưa bind transaction; sửa harness, giữ 6 receipts và chạy tiếp 9 ca PASS;
  thêm riêng một ca failure-after-root-update rollback PASS. Có FE04 existing
  `ReworkSourceDigestAsync` đọc 32-byte digest trên Rework/defect rows thật trong test tx.
- Focused HTTPS: **10/10 PASS** với real ASP.NET/SQL/cookie/CSRF, không interception.
  Bao phủ trusted reads, 503 fail-closed/duplicate key, input/missing tag, stale evidence,
  persisted failed facts không được promote thành authority, selected defects, invalid
  phase, unknown source, 401/403/branch isolation, actual wrong-cycle pointer và persistence.
- Production successful Reject/Rework commit + notification/projection + durable replay:
  **NOT_RUN / BLOCKED**, không lấy SQL-core fixture làm kết quả production.
- `dotnet test HotelOps.slnx --no-build`: exit 0; solution chỉ có API, không discover tests.
  Không tính làm unit test credit. Không rerun 45 domain tests/24 D4 tests/historical suites
  không bị ảnh hưởng; source reuse/integration không được tính là test PASS mới.

SQL Server local `DESKTOP-JI8U50T\SQLEXPRESS`, version 16.0.1000.6; trial mới
`HotelOps_Canonical_20261108_110524_fc68d7da_Fresh`, owner
`3e531db0-1455-4db4-bb41-c9c982b8da98`, 0 users trước fixtures. UUID/identity được kiểm
tra trước mutations. Existing guarded D4 và lifecycle installation receipts được giữ.
Owned processes đã dừng, không drop/reset trial nào. Public API persistence: first
Inspection vẫn PENDING_CONCLUSION; second FAILED là **core-only synthetic concurrency
fixture không có audit/receipt authority**; Rework count=0 và conclusion audit count=0.

## Exact blockers / FE04 compatibility

1. **DEV2 staging/retry:** integration source chỉ có provisional Cleaning adapters. Event
   constraints trong `fs016-candidate/01_Rework.sql` cho CLEANING_TASK_ASSIGNED,
   CLEANING_BLOCKER_REPORTED, CLEANING_TASK_COMPLETED, REWORK_SUBMITTED; chưa có accepted
   Inspection Failed/Rework Requested contract, mandatory recipients, caller-transaction
   staging, durable operation result/recovery và delivery/retry semantics cho hai UC này.
   Không sửa DEV2 code hoặc tạo publisher/outbox khác, flags không bật.
2. **Persisted authority binding:** `fs016_rework_source_proofs.origin` CHECK chỉ cho
   `SYNTHETIC_TEST_ONLY`; `ReworkLifecycleService.ExecuteReworkAsync` cũng yêu cầu literal
   đó. Không ghi fake origin hoặc đổi FE04 consumer. Cần FE04/shared DB owner chốt compatible
   persisted real writer receipt/revision/selected-defect digest và authorized worker reads.
   Supersession/durable receipt semantics chưa được thay bằng một authority tự tạo.
3. **Room lifecycle projection:** SRS UC045 yêu cầu room Rework Required trừ Blocked,
   nhưng current `CK_rooms_operational_status` chỉ có AVAILABLE/OCCUPIED/DIRTY/CLEANING/
   INSPECTION/READY/OUT_OF_SERVICE. Không invent mapping thành DIRTY hoặc enum mới.
   Current readiness event là journal, chưa có accepted atomic room projection participant
   để hoàn thành postcondition. UC044 cũng phụ thuộc shared Ready/RoomBlock participant;
   không triển khai writer khác để bỏ qua boundary này.

FE04 structural SQL reads/digest đã exercised; authoritative UC035/036 integration vẫn
**NOT_READY**, không phát AUTHORITATIVE_INSPECTION_HANDOFF_READY.
Ước tính task **55%**, UC045 **60%**, UC046 **55%**; không UC ACCEPTED.
