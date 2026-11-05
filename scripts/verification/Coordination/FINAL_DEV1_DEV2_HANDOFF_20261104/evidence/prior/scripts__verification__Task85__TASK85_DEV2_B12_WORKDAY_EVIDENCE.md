# Task85 — DEV2 B1.2 / Workday source evidence

Audit ngày 2026-11-04. Chỉ đọc production source; không chạy SQL/HTTPS mới.
Root `D:\SEP490\Projects\HotelOps`; branch `feature/BE1-database-integration`;
HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Có local/untracked runtime source;
HEAD riêng lẻ không tái tạo được runtime. Xem CURRENT_SOURCE_LINES.txt và preservation manifests.

## 1. B1.2 approval artifact

Exact path: `D:\SEP490\Projects\HotelOps\scripts\verification\Task79\B12_APPROVAL_SCOPE.md`.
SHA256: `B912CFCE7DCCF5B5AF1596958DEE2B6CA22204B61DB93F9A6B4F9E40965CA8E0`.
Trạng thái nội dung: **NOT APPROVED / NOT APPLIED**; proposal, không phải interface DEV2 đã implement.
Hash khớp TASK79_CLOSURE_VALIDATION.json; hash cũ trong TASK79_VALIDATION.json là phiên bản trước closure,
không phải hash hiện hành. A1/B1.1 đã được duyệt riêng; không suy approval đó sang B1.2.

Tìm tên/content trong root và ba worktree đăng ký khác (task11-dev2-rehearsal,
task12-pr7-resolution, HotelOps-AI-Loop) không thấy bản B12 thứ hai. Worktree checkpoint DEV2
`C:\Users\Admin\Documents\GitHub\HotelOps_DEV2_Coordination` không tồn tại tại môi trường này.
Không fetch hoặc checkout. Draft blob `dc5ceb10121f752351e56e4b7150277c96271e1c`
được Task79 ghi theo xác nhận người dùng, không phải nội dung joint contract đã đọc ở Task85.

## 2. Task79 receipts có thật

Tất cả relative paths dưới `D:\SEP490\Projects\HotelOps\scripts\verification\Task79\`.
PASS dưới đây là kết quả lịch sử được receipt ghi; không phải rerun Task85.

| Exact filename | PASS / FAIL / NOT_RUN và giới hạn |
|---|---|
| auth-expiry-before.local.json | 5 reproduction checks đạt kỳ vọng tái hiện bug: login200 rồi cookie401; không phải bản trước đã an toàn |
| auth-expiry-after.local.json | 43 PASS, 0 failed checks được ghi; production HTTPS, SQL session, login/logout/password rotation/expiry |
| boundaries-before.local.json | 21 assertions; 20 timezone models; 5 cookie-skew cases quan sát, không phải 5 framework test FAIL |
| boundaries-after.local.json | intermediate 21 checks; không cộng với final53 |
| boundaries-final.local.json | final53 PASS, 0 skew, 20 timezone models; deterministic policy, không phải53 HTTPS requests |
| user-sql-regression.local.json | 26 PASS, 0 failed checks được ghi; SQL provisioning/audit/rollback/concurrency, auth collaborators là doubles |
| TASK79_VALIDATION.json | tổng hợp43/53/26 và199 security (93+43+14+49), build0 errors/2 NU1900; không phải thêm321 independent live business checks |
| TASK79_CLOSURE_VALIDATION.json | closure, hash và dependency provenance; không thêm acceptance checks |

SQL catalog receipt: `HotelOps_Canonical_20261104_063549_81d4bc64_Fresh`.
`AuthExpiryLive.cs:28–31` cố định catalog, yêu cầu env `TASK79_TRIAL_SQL_SOURCE`.
Các receipt trên **không lưu DataSource instance**: instance của execution Task79 chưa chứng minh được
từ các receipt này; không suy từ trial Task84 hoặc user secrets hiện tại. Task85 không kết nối SQL để xác minh.
OS thực thi: SE Asia Standard Time. UTC OS acceptance **NOT_RUN**; notification và Pause All
acceptance **NOT_RUN**. Framework tests: no cases / NOT AVAILABLE, không phải PASS.
Một execution thất bại do stale assembly được giữ tại
`D:\SEP490\Projects\HotelOps\obj\task79-boundaries-after-stale-assembly.local.log`:
`UTC output contract failed`; không có completed check count trong log, không bịa số FAIL assertions.
Task79 report ghi rebuilt/rerun final53 PASS. NU1900 là vulnerability-feed warning được giữ nguyên.

## 3. Runtime UC139 hiện tại

Các line numbers là source hiện tại, dưới `src/HotelOps.Api/`.

| Flow | Controller / method / route | Service / status |
|---|---|---|
| Scheduled Start | Controllers/WorkdaysController.cs:24–26 Start; POST `/api/branches/{branchId}/workdays` | Services/WorkTime/WorkdayService.cs:22 StartAsync(long branchId, StartWorkdayRequest request, CancellationToken ct); IMPLEMENTED B1.1 |
| Current | controller:16–18 Current; GET same base `/current` | service:43 CurrentAsync(long branchId, CancellationToken ct); IMPLEMENTED own-only |
| Own shifts | controller:20–22 OwnShifts; GET same base `/own-shifts` | service:65 OwnShiftsAsync(long branchId, CancellationToken ct); IMPLEMENTED eligible own assignments |
| Unscheduled Start | same POST Start, ShiftAssignmentId=null | service:25–29 reason invalid400; otherwise503 UNSCHEDULED_NOTIFICATION_UNAVAILABLE; **MISSING business implementation**, no business/audit mutation |
| End | controller:28–42 End; POST same base `/{workdayId}/end`, strong If-Match8 bytes | service:50 EndAsync(long branchId,long workdayId,byte[] expectedVersion,bool pauseAll,CancellationToken ct); ordinary End IMPLEMENTED |
| Pause All | same End request PauseAll=true, no separate endpoint | service:60 returns503 PAUSE_ALL_UNAVAILABLE; **MISSING**, does not close intervals |

Controller requires Cookie authentication and HTTPS. Start/Current/End use ATTENDANCE_RECORD;
Own-Shifts SHIFT_VIEW. Actor from ICurrentUserAccessor, never DTO. WorkdayService:83–140
validates actual ticket/session, restricted flag, active account/employment, SQL-backed
resource permission and branch. Explicit actor equality in assignment/workday queries at:36–38,
57–59 prevents cross-user access even with BRANCH/ALL. Resolver is
Services/WorkTime/WorkdayPermissionResourceResolver.cs; delegates non-Workday resources to existing Cleaning resolver.

### Actual transaction / persistence boundary

WorkdayService constructor:16–18 receives scoped HotelOpsDbContext and concrete participant.
Execute:98 begins Serializable; :99 LockUsersAsync(actor); :100–108 revalidates session,
employment and permission **after lock wait**. :110 commits only2xx. No explicit RollbackAsync:
await-using disposal rolls back noncommitted return/exception. No SaveChanges in controller/service.
Primitive owns staged writes, **not transaction commit**:

- SqlWorkTimeTransactionParticipant.cs:58 StartScheduledWorkdayAsync(long actorUserId,
  long branchId,long shiftAssignmentId,CancellationToken ct): SaveChanges:92 (day), :95 (CREATE audit).
- :99 EndWorkdayAsync(long actorUserId,long branchId,long workdayId,byte[] expectedVersion,
  CancellationToken ct): conditional update plus audit; SaveChanges:120. Refuses open interval;
  WorkdayService maps TIME_INTERVAL_ALREADY_OPEN to409 MSG31_PAUSE_ALL_REQUIRED (:125).
- :124 OpenIntervalAsync(WorkTimeOperation operation,CancellationToken cancellationToken):
  SaveChanges:144/:147 interval+audit; :151 CloseIntervalAsync(operation,long intervalId,
  byte[] expectedRowVersion,CancellationToken cancellationToken): ExecuteUpdate then SaveChanges:175 audit.
- :183 Transaction checks same DbContext current SQL ReadCommitted/Serializable transaction;
  :191 validates fixed lock lease; no Begin/Commit/Rollback in participant. SQL time :239,
  audit branch/actor/source identity :241–244. Ownership is caller service/DEV2 outer writer.
- Lock order :28–54: sorted UserEligibility locks then transaction-owned per-user app locks;
  time rows UPDLOCK/HOLDLOCK :179–182, then authoritative source checks. Caller must take
  source locks/check source permission before interval mutation. SQL unique indexes in approved
  A1 enforce one WORKING workday and one OPEN interval/user; no ShiftAttendance substitution.

Program.cs:26–27 maps interface and concrete to **same scoped instance**, :29 WorkdayService;
no Task85 DI modifications. Interface comment says REVIEW PROPOSAL but actual interface and
SQL implementation exist; distinguish stale comment from executable source and A1 approval.

## 4. Exact reusable A1 contract and B1.2 gate

`Services/WorkTime/IWorkTimeTransactionParticipant.cs:4–24`:
WorkTimeOperation(ActorUserId,OwnerUserId,BranchId,WorkItemKind,WorkItemId);
LockUsersAsync(IReadOnlyCollection<long>,CancellationToken);
OpenIntervalAsync(WorkTimeOperation,CancellationToken);
CloseIntervalAsync(WorkTimeOperation,long,byte[],CancellationToken).
Results carry IntervalId/WorkdayId/server timestamps/RowVersion. Business permission remains caller duty.

DEV2 PM/maintenance adapter must supply authoritative current work-item/occurrence ID,
owner/branch/revision/status and source action permission, same scoped DbContext/participant,
same outer transaction, deterministic source/history locks after fixed sorted user/time locks.
Participant RequireOperation:195–201 currently supports only CLEANING/REWORK/TASK;
maintenance kinds fail TIME_SOURCE_UNRESOLVED. No approved adapter implementation inferred.
Pause source while retaining responsibility/history, close interval while source is still
IN_PROGRESS (SourceAsync:216–237 requires it), then End day. One outer commit; source/time/
audit/outbox failures roll back all. No direct DEV1 maintenance writes or independent inner commit.

B1.2 still needs joint-approved publisher/event/replay contracts + actual source and durable
inbox/retry/restart/dedup/rollback evidence, unique branch-DH recipient semantics, operation→result
replay retention/fingerprint, DEV2 PM Pause All participant, and approved rework pause transition.
B12_APPROVAL_SCOPE contains proposed signatures, **not installed adapters**. Event labels there
must be reconciled with DEV2 registry before use; no new catalog/RBAC/DI/schema approval implied.
UC139 remains BLOCKED_EXTERNAL;26/75,60/256=23.4375%, unchanged.
