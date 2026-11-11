# [HOTELOPS DEV1 FE04]
# FE04-UC034-FULL-COMMAND-001 — SPRINT RESULT

Ngày: 2026-11-11. Owner: DEV1 FE04.
**SOURCE_VERIFIED trong phạm vi source + no-SQL; UC034 acceptance: BLOCKED.**
Không coi source-ready là INTEGRATION_READY hoặc ACCEPTED.

## 1. Identity và phạm vi

- Worktree: `D:/SEP490/Projects/HotelOps-DEV1/obj/fe04-uc034-full-command`.
- Branch: `feature/dev1-fe04-uc034-full-command`.
- Exact base R05: `95f7422e284bc81bcfcf9f460dace52022de5cc7`.
- Required ancestor R03: `d37e99f42eaaf3089eb510c6298296a0f0743faa`, VERIFIED ancestry.
- Source/test commit: `397ff6c2c08bb946756c196e2f4382a9e3f3f524`.
- Final documentation HEAD được trả trong completion report sau commit; không ghi SHA tự tham chiếu chưa tồn tại.
- Allowlist đã ghi trước sửa: [SCOPE.md](SCOPE.md).
- Không sửa main worktree, R05 worktree hoặc Notification PR #40 source.

Exact changed files so với base (5 files, không xóa):
1. `src/HotelOps.Api/Services/Housekeeping/CleaningClosureBoundary.cs`
2. `tests/HotelOps.Dev1.RegressionTests/CleaningCompletionCommandTests.cs`
3. `docs/fe04-uc034-full-command-001/SCOPE.md`
4. `docs/fe04-uc034-full-command-001/RESULT.md`
5. `docs/fe04-uc034-full-command-001/source-hashes.json`

SHA256 byte/normalized và Git blob identity của source/test, cùng identity các dependency giữ nguyên và TRX receipt: [source-hashes.json](source-hashes.json).
Manifest không tự hash chính nó; RESULT là documentation commit sau source commit, không thay source đã test.

## 2. Implementation reused và thay đổi mới

Tái sử dụng toàn bộ command UC034 tại R05: controller/DTO, session/execute permission,
sole current assignment/current cycle, workflow/ETag, required checklist/progress,
blocker/work interval, completion note/time/evidence, PENDING_INSPECTION,
audit/readiness, mandatory staging, durable replay và bounded commit recovery.

Gia cố replay: receipt thiếu/rỗng/sai JSON, incomplete operation hoặc unsupported
fingerprint version trả fail-closed `DURABLE_RESULT_CONTRACT_INVALID` (Unavailable,
controller mapping 503), thay vì lỗi JSON không được xử lý. Matching fingerprint
không đủ để xác nhận durable completion. Payload conflict vẫn Duplicate/409.
Replay thành công vẫn phải kiểm tra persisted mandatory staging trước outer commit.
Không thêm endpoint/DTO/status/permission hoặc đổi Notification/Work-Time gates.

R05 attribution capture giữ nguyên, chỉ gọi completion mới sau task/log mutation,
trước audit/staging/commit; không gọi từ blocker hoặc replay. Không fallback latest
assignment, không chọn primary cho nhiều cleaner, không backfill. Attribution
contract/source/SQL và rework authority reader không có diff so với base.

Source review xác nhận BatchAsync sở hữu Serializable transaction; capture,
audit/readiness/staging ở trong cùng callback, trước commit. Lỗi mandatory write
ngăn success và thoát outer transaction. Đây là review source, chưa phải SQL rollback proof.

## 3. Build và focused tests — VERIFIED execution

- `dotnet build HotelOps.slnx`: **PASS, 0 warning / 0 error**.
- `dotnet test HotelOps.slnx`: **exit 0**, API-only solution, không discover regression project; không tính test credit từ lệnh này.
- Chạy regression project trực tiếp, filter:
  `CleaningCompletionCommandTests|CleaningClosureBoundaryTests|CleaningClosureWorkflowTests|CleanerAttributionTests|CleaningLifecycleContractTests|NotificationCandidateTests`.
- **132 PASS / 0 FAIL / 1 SKIP**, gồm **40 case mới PASS**.
- Receipt local ignored: `TestResults/uc034-full-command-001/focused.trx`.
- Skip là opt-in full service/SQL theory group; không gọi đó là một SQL scenario đã chạy.
- SQL/live opt-in được xóa chỉ trong test process; không sửa configuration hoặc bật flag.
- `git diff --check`: PASS. Git advisory LF→CRLF có xuất hiện; không đổi EOL config.
- Không có build/test warning được ghi nhận trong các lệnh thành công trên.

## 4. Evidence theo scenario

| Scenario | Kết quả / giới hạn |
| --- | --- |
| Invalid/mismatched/restricted actor/session | PASS, gọi actual CompleteCleaningAsync; không tạo SQL command/transaction |
| Pre-existing/dirty/ambient transaction context | PASS, actual command từ chối trước SQL; không provider commit |
| Receipt JSON thiếu/hỏng, incomplete/version sai | PASS, production replay parser/rules |
| Replay scope/assignment/operation/lifecycle/readiness không khớp | PASS ở component; không phải durable DB replay |
| Fingerprint đổi progress/task/branch/evidence/action | PASS ở component; không phải concurrent SQL command |
| Invalid envelope, If-Match/HTTPS/session/CSRF, receipt contracts | PASS trong existing focused tests |
| Invalid evidence metadata/encoding/MIME/extension | PASS, actual production evidence validator qua reflection; không substitute validator hoặc SQL/file write |
| Mandatory staging/proof thiếu caller transaction | PASS, actual provisional publisher từ chối trước SQL |
| Fresh R05 không được closure gate cho phép | PASS, existing exact-target gate vẫn fail-closed |
| FE05 reader sai physical connection/transaction/isolation/ambient context | PASS trước SQL, provider transaction double |
| Authorized completion; missing/wrong assignee trong DB | NOT_RUN full command SQL |
| Stale task/progress ETag; incomplete checklist; active blocker; invalid interval trong DB | NOT_RUN full command SQL; existing opt-in workflow suite giữ nguyên |
| Attribution capture fault; audit/readiness/mandatory staging/commit fault | NOT_RUN sau actual domain mutation |
| Transaction rollback; same-key/conflicting replay; concurrent/version conflict | NOT_RUN full command SQL |
| Pending Inspection không tạo READY/conclusion | PASS receipt boundary + VERIFIED source review; actual DB effects NOT_RUN |

No-SQL tests dùng synthetic receipt và transaction double, connection cấm tạo SQL command.
Không dựng mock successful Notification/FE05 authority để tuyên bố acceptance.
Các scenario chưa chạy được ghi rõ thay vì suy từ historical R05 PASS.

## 5. SQL / HTTPS

**NOT_RUN**: không có approved isolated target, execution identity, exclusive lease,
fixture ownership hoặc quyền live mutation trong chỉ thị này. Không kết nối HotelOpsDB,
ENVNOTIF001 hoặc shared trial; không provision, DDL, bật gate/worker/producer hay deploy.
Không thay target name/marker hoặc nới FS015/FS016 gates để đưa R05 qua gate.

R05 23 PASS/2 NOT_RUN, regression 308 PASS/11 NotExecuted và seeded SQL 6 PASS
là **REPORTED historical component evidence**, không phải receipt execution của task này.

## 6. FE05 compatibility handoff

Exact reusable contract trong source commit nêu trên, unchanged từ R05:

```csharp
Task<CleanerAttribution?> ReadInspectionCleanerAttributionAsync(
    DbConnection connection, DbTransaction transaction,
    long inspection, long branch, CancellationToken ct);
```

FE05 caller authorize actor/Inspection/branch trước khi gọi. Dùng cùng scoped
HotelOpsDbContext, physical connection và caller-owned Serializable transaction;
không ambient transaction/independent commit. FE05 sở hữu commit/rollback.

Reader kiểm tra inspection/task/room/branch/current cycle, persisted completion
source và cleaner eligibility. Null = unavailable/ambiguous/stale/ineligible:
fail-closed. Giữ CompletionRevision đã capture, không thay bằng current rowversion;
WriterTransactionId là completion transaction, không phải FE05 current transaction.
Không mở rộng result thành UC035/036 authority proof. Không sửa FE05 business writer.

## 7. Notification và blockers theo owner

Standalone baseline này vẫn dùng provisional `CLEANING_TASK_COMPLETED` v1,
existing operation/publication/intent ledger và closure gate. Accepted shared
Notification participant/delivery chưa được pin hoặc tích hợp bởi task này.
Không sửa PR #40 hoặc coi trạng thái lịch sử 06R1 là kết luận về source owner mới nhất.

| Gate còn lại | Responsible owner / bằng chứng cần |
| --- | --- |
| G5 Full command SQL acceptance | FE04 + Project Leader: approved target/identity/lease/fixtures; attribution + audit/readiness/mandatory staging rollback, replay/concurrency, least privilege |
| G6 Real HTTPS acceptance | FE04 + environment owner: actual auth/CSRF/ETag/response và persisted effects trên target được duyệt |
| G7 Notification integration acceptance | Notification owner + FE04: exact accepted event/participant/source SHA và caller transaction/staging/guard/delivery receipts |
| G8 FE05 actual consumer acceptance | FE05 owner: exact caller/source SHA, authorization, same-transaction reader/null handling và integration receipt |

Không yêu cầu owner viết lại source đã có; cần pin và tích hợp đúng accepted revision.
Multi-cleaner policy chưa được duyệt tiếp tục null, không tự mở business alternative.

## 8. Readiness và tiến độ có căn cứ

Bốn delivery gates đã hoàn tất: G1 baseline/allowlist; G2 reused command + scoped
replay fix; G3 build/focused no-SQL evidence; G4 exact source identity/FE05 handoff.
Bốn acceptance gates G5–G8 còn mở như bảng trên.

**50% (4/8 gates của checklist task này)**, không phải phần trăm FE04/UC034 hoàn thành
nghiệp vụ, test coverage hoặc UC credit.

- SOURCE_READY / SOURCE_VERIFIED: **YES, scoped source + no-SQL only**.
- INTEGRATION_READY: **NO**.
- UC034 ACCEPTED: **NO**.
- Full-command acceptance: **BLOCKED** bởi G5–G8.
- Runtime activation / merge / deploy: **NOT_GRANTED / NOT_RUN**.

## 9. Draft PR preparation và next executable action

Draft title: `fix(housekeeping): fail closed on invalid UC034 durable replay receipts`.

Draft description: UC034 replay previously deserialized persisted JSON directly.
Reject incomplete/version-incompatible/corrupt receipts through the existing contract
failure response while preserving fingerprint conflict, staging verification and R05
attribution. Build passed (0 warnings/errors); focused tests 132 passed, 1 SQL group
skipped. SQL/HTTPS/Notification/FE05 acceptance remains unverified; gates unchanged.

Review diff phải là exact five-file delta từ R05, không lấy toàn bộ R05 dependency
history làm thay đổi task này. Chưa quyết định PR base mặc định là develop; dùng
exact R05-based diff cho review, không merge.

Next executable action: Project Leader phê duyệt riêng isolated full-command target,
execution identity, exclusive lease, fixture ownership và gate strategy; pin accepted
Notification/FE05 dependency SHA. Sau đó FE04 chạy G5 và G6, phối hợp owner nhận G7/G8
receipts, giữ shared/production OFF. Không sửa safety gates trong task này.