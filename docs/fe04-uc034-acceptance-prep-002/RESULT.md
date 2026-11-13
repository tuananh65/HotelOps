# FE04-UC034-ACCEPTANCE-PREP-002 — RESULT

Owner: DEV1 FE04. Ngày: 2026-11-11.
**Acceptance preparation/source: VERIFIED. SQL/HTTPS: NOT_RUN. UC034: NOT ACCEPTED.**
Draft PR creation còn BLOCKED bởi GitHub authentication/installation, chưa có PR URL.

## 1. Remote, ancestry và review base

- Worktree: D:/SEP490/Projects/HotelOps-DEV1/obj/fe04-uc034-full-command.
- Branch: feature/dev1-fe04-uc034-full-command.
- Starting HEAD (local + remote VERIFIED): db12691295ffe0382b005c686ac7478326b0c0d6.
- Exact R05 base (local + remote VERIFIED): 95f7422e284bc81bcfcf9f460dace52022de5cc7,
  branch feature/dev1-fe04-original-cleaner-binding.
- Required ancestor: d37e99f42eaaf3089eb510c6298296a0f0743faa.
- PREP-002 harness/source commit: 70db8cbbc4ad72cf7858125dd7e1b04b39142385.
- Final publication HEAD được báo sau documentation commit; không tự ghi SHA tự tham chiếu.
- Initial R05→001 diff: 5 file, đúng task 001. R05 là ancestor, không kéo dependency branch khác.
- PR base đề nghị: feature/dev1-fe04-original-cleaner-binding; KHÔNG tự target develop.
  R05 dependency vẫn cần acceptance riêng trước merge; Draft PR không thay thế quyết định đó.

## 2. Exact source diff

PREP-002 thêm đúng 7 file:
1. tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceHarness.cs
2. tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceCallbacks.cs
3. tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceHarnessTests.cs
4. docs/fe04-uc034-acceptance-prep-002/SCOPE.md
5. docs/fe04-uc034-acceptance-prep-002/HANDOFF.md
6. docs/fe04-uc034-acceptance-prep-002/RESULT.md
7. docs/fe04-uc034-acceptance-prep-002/source-hashes.json

Không sửa application source, generated EF/schema/mapping, Program.cs, Notification/FE05 writer,
shared auth, flags hoặc source task 001. PR R05→final gồm 12 file: 5 file task 001 + 7 file PREP-002.
Exact allowlist đã ghi trước sửa: [SCOPE.md](SCOPE.md). Source/blob/TRX hashes: [source-hashes.json](source-hashes.json).

Main FE04 và R05 source giữ nguyên. Notification worktree có independent dirty path
scripts/verification/FE04NotificationRuntime/APPROVAL_PACKAGE.json khi kiểm tra cuối;
không stage/sửa/revert file đó. Kết luận dependency dùng exact committed report/SHA và public PR API,
không coi nội dung owner đang sửa là accepted contract.

## 3. Acceptance harness readiness

**COMPILED / NO-SQL CONTROLS VERIFIED; runtime execution còn GATED.**
21 case-specific CompleteCleaningAsync cases đã chuẩn bị, gồm positive/valid evidence, missing/wrong
assignee, task/progress stale, checklist/blocker/interval/evidence rejection, attribution/audit/readiness/
publication/intent faults, precommit failure, lost commit acknowledgement, exact/conflicting replay và
same/different-key concurrency.

Điểm sửa so với coverage cũ: không dùng ReportCleaningBlockerAsync cho completion fault/concurrency.
Completion audit callback chỉ fault COMPLETE_CLEANING, không đụng Work-Time interval audit trước capture.
Mọi fault case phải chứng minh callback Fired, rồi kiểm tra snapshot rollback.
Positive checks gồm actual end/note, interval closure, attribution revision binding, audit/readiness,
persisted mandatory publication/intents/proof, no READY và no Inspection creation.

Fixture/session code dùng production authentication/login/session/accessor; không dùng session-success
double. Fixture identity tách khỏi completion runtime identity, được kiểm tra actual USER_NAME().
Không có SQL-auto-running Fact. Owner composition phải cung cấp context factories + authoritative
external lease/pin validator; source metadata/file/local callback permissive không cấp authority.

## 4. SQL fixture/session và runtime requirements

Chi tiết executable callbacks, exact prerequisites và owner contract:
[HANDOFF.md](HANDOFF.md).

Positive fixture: active branch/HK department/user/grants/role/shift; sole active assignee và current
cycle; IN_PROGRESS + actual start/no end; required checklist hoàn tất và strong ETags; resolved blocker;
WORKING workday + một matching open interval; eligible HK supervisor; single-cleaner source history.
Fresh fixture được append cho từng case; real committed nonrestricted SQL session từ actual login.
Không sửa role/permission catalog, không backfill hoặc dùng historical Inspection authority.

Chưa có target, execution identities, exclusive lease/provenance hoặc fixture ownership được duyệt.
Không dùng ENVNOTIF001, expired trial, fake marker/name hoặc tự provision/renew lease.
Hiện gates chỉ chấp nhận genuine FS015/FS016 isolated installations; fresh R05 bị từ chối.
ENV/Project Leader phải chọn fresh gate-compatible target/host với approved deployment strategy,
hoặc approve một task strategy/composition riêng; PREP-002 không đổi gate/installer/DDL.

Lease checks trước context và EF commit; raw fixture helper batches có internal T-SQL commit,
nên owner phải approve toàn bộ batch và giữ exclusive lease cho bounded run. Harness không tự
acquire SQL lease và không giả vờ chứng minh lease enforcement bên trong raw batch.

## 5. Notification / FE05 source identities

- FE04 command source: 397ff6c2c08bb946756c196e2f4382a9e3f3f524; checkpoint được PL chấp nhận:
  db12691295ffe0382b005c686ac7478326b0c0d6. App source không đổi trong PREP-002.
- Attribution producer/reader/SQL: 95f7422e284bc81bcfcf9f460dace52022de5cc7.
- Existing provisional UC034 closure publisher: cùng R05 SHA; CLEANING_TASK_COMPLETED v1,
  caller-owned StageClosureAsync/VerifyClosureAsync và existing ledger. NOT shared accepted.
- Notification PR #40: public API VERIFIED open Draft, base develop,
  head 54d75a634005afb8d42704f761a820ed16b1b9d4.
  Exact committed report ghi CONTRACT_ACCEPTED=NO / SQL_ACCEPTANCE_READY=NO và expired Writer/lease.
  Đây không phải UC034 participant acceptance, không được tái sử dụng target/lease bằng suy luận.
- Accepted UC034 Notification participant/profile SHA: **NOT_PINNED**.
  Native UC029 graph DENYs không tương thích current UC034 direct staging; cần owner-approved profile
  hoặc compatible participant/FE04 wiring, không tự bỏ DENY hoặc tạo competing ledger.
- FE05 actual consumer SHA: **NOT_PINNED**. Giữ R05 reader signature, same physical Serializable
  caller transaction, authorization trước call và null/fail-closed. Không yêu cầu owner viết lại reader.
  Consumer integration acceptance tách riêng; không chặn independent UC034-only SQL cases vì full FE05
  chưa xong. Nếu đưa consumer SHA vào run envelope, phải pin full valid revision và owner receipt.

## 6. Validation — execution VERIFIED

- dotnet build HotelOps.slnx: **PASS, 0 warnings / 0 errors**.
- dotnet test HotelOps.slnx: **exit 0**, API-only solution, không discover regression project.
- Explicit focused regression project: **177 PASS / 0 FAIL / 1 SKIP**,
  trong đó **45 no-SQL harness/control tests mới PASS**.
- Receipt: TestResults/uc034-acceptance-prep-002/focused-final.trx (local ignored).
- Intermediate focused run: 176 PASS / 0 FAIL / 1 SKIP, trước khi thêm independent-FE05 guard case.
- SQL/live opt-in tắt trong test process. 1 SKIP là existing opt-in service/SQL theory group.
- git diff --check: PASS. Git advisory LF→CRLF được ghi nhận; không đổi EOL configuration.
- 21 SQL case outcomes: **0 PASS / 0 FAIL / 21 NOT_RUN**.
- HTTPS business acceptance: **NOT_RUN**.
- Không claim SQL syntax/query/permissions, actual rollback/replay/concurrency, staging/delivery hoặc
  HTTP acceptance từ no-SQL guard/selector tests. SQL strings đã đối chiếu schema/source nhưng chưa chạy.

## 7. Draft PR preparation / authentication blocker

Desired base: feature/dev1-fe04-original-cleaner-binding @ 95f7422e284bc81bcfcf9f460dace52022de5cc7.
Desired head: feature/dev1-fe04-uc034-full-command (published final HEAD).
Draft PR URL: **NOT_CREATED / BLOCKED** tại thời điểm ghi report.

GitHub CLI không có; host-only và repository-scoped git credential helper không trả credential cho REST.
Public GitHub API đọc được PR #40, nhưng không cấp quyền tạo PR.
Đã kiểm tra không có open PR trùng head, rồi gửi yêu cầu tạo đúng R05 base/head với draft=true;
GitHub POST /pulls trả HTTP 401, nên xác minh Draft PR NOT_CREATED. Git push vẫn thành công;
không suy từ Git transport authentication rằng REST API đã được cấp quyền.
User đã chọn cài/kết nối GitHub; trạng thái plugin kiểm tra gần nhất vẫn installed=false.
Đã dùng [plugin-management skill](C:/Users/Hax/.codex/plugins/cache/openai-curated-remote/plugin-management/0.1.0/skills/plugin-management/SKILL.md)
để tìm/đề xuất GitHub. Không tự coi lựa chọn text là kết nối thành công; cần hoàn tất installation/
account authorization. Đây là authentication capability blocker, không phải rejection của approval review.

Prepared title:
fix(housekeeping): harden UC034 replay and prepare full-command acceptance

Prepared Draft PR body:
UC034 replay now rejects incomplete, unsupported and malformed persisted receipts through the existing
contract failure response. The R05 completion/attribution path and safety gates are preserved.
Adds a test-only 21-case CompleteCleaningAsync acceptance harness, separate fixture/runtime identities,
external lease checks and precise failure callbacks; no application activation or owner infrastructure edits.
Validation: solution build 0 warnings/errors; explicit focused tests 177 passed, 1 SQL theory group skipped.
SQL/HTTPS remain NOT_RUN; shared Notification and FE05 consumer acceptance are not inferred.
Review against the exact R05 candidate branch only. No merge/deploy/schema/production authorization.

Compare/review link (KHÔNG phải PR URL):
https://github.com/tuananh65/HotelOps/compare/feature%2Fdev1-fe04-original-cleaner-binding...feature%2Fdev1-fe04-uc034-full-command?expand=1

## 8. Remaining blockers / next executable actions

1. GitHub installation/account authorization: user hoàn tất kết nối; Codex tạo Draft PR với exact base/head
   và kiểm tra actual PR file list, draft flag, SHA. Không tạo trùng nếu owner đã tạo PR.
2. ENV + PL: approve exact fresh target/identities, held exclusive lease validator, trusted fixture/
   source/catalog/assembly provenance, storage/TLS/runtime host. Không tự dùng expired/shared trial.
3. Notification + Security: pin approved UC034 staging/transaction/guard/profile SHA, giải quyết compatibility
   với direct staging hoặc accepted FE04 adapter scope. Không yêu cầu viết lại source đã có.
4. FE05 owner: pin actual consumer/caller + authorization/transaction receipt khi chạy consumer integration.
5. FE04: sau approvals, compose runtime host với harness hiện có, chạy sequential 21 cases rồi HTTPS;
   ghi receipt mới/failure/remaining NOT_RUN, không dùng R05 lịch sử thay execution.

Preparation/source ready; integration runtime chưa ready; UC034 **NOT ACCEPTED**.