# UC139 B1.2 — one bounded contract review; NOT APPROVED / NOT APPLIED

Do not reapprove A1/B1.1. No schema/RBAC/middleware/config change is included here.
No new permission or independent notification queue is proposed.

## Task79 closure — DEV2 checkpoint confirmed by user

Branch: `feature/DEV2-coordination-contracts`. Worktree at checkpoint:
`C:\Users\Admin\Documents\GitHub\HotelOps_DEV2_Coordination`.
HEAD: `4a187320643d0c82f232c6b9dd5a667b8401c280`.
Draft: `scripts/verification/Coordination/NOTIFICATION_DELIVERY_RETRY_CONTRACT_DRAFT.md`.
Draft blob: `dc5ceb10121f752351e56e4b7150277c96271e1c`.

**User-confirmed status: DRAFT_READY_FOR_JOINT_REVIEW, not joint-approved. DEV2 has not
implemented INotificationPublisher, durable outbox, replay adapter or notification worker.**
There is no confirmed implementation commit to fetch/checkout/cherry-pick. No further
publisher search in cf8fb02 is needed. The external worktree path and blob are unavailable
in this environment; draft contents have **not** been read or technically verified here.
Checkpoint/status information is attributed to the user's coordination confirmation.

B1.2 remains BLOCKED_EXTERNAL. All signatures below are DEV1 review proposals, not the
actual unpublished DEV2 interface and not an approved integration contract. No adapter,
queue, DI/schema/RBAC change or maintenance mutation is authorized by this closure.

## Evidence and gates

- `docs/coordination/HotelOps_Dev2_Backend_Sync_RBAC_2026-10-20.md` confirms exactly one
  branch-bound DH, responsible for all branch departments. Existing catalog code is
  DEPT_MANAGER; numeric role/user/branch IDs must not be hardcoded.
- Earlier local searches found no publisher implementation. The user has now confirmed
  that implementation has not begun at the DEV2 checkpoint; this is a known external
  dependency, not a missing commit to keep searching for. No fetch/checkout performed.
- Read historical `cf8fb02:scripts/verification/Housekeeping/TASK73_DEV2_NOTIFICATION_HANDOFF.md`
  directly: it explicitly says delivery/retry is a proposal, not integrated. Not restored.
- A1 schema has no client-operation identity column/store. A notification dedup key alone
  does not prove stable replay of the Workday business result. Publisher operation mapping
  and retention must be confirmed; no ad hoc audit-log scan or new operation table is applied.
- SRS cleaning transition IN_PROGRESS->ON_HOLD (UC033), task IN_PROGRESS->PAUSED (UC110),
  and mandatory reason 'end of shift' (UC139) are identifiable. Rework state catalog does
  not establish a pause/hold transition; cannot silently invent REWORK.ON_HOLD or complete it.

## Exact proposed feature contracts (source not created yet)

Target `src/HotelOps.Api/Services/WorkTime/WorkdayB12Contracts.cs`:

```csharp
public sealed record UnscheduledWorkdayCommand(Guid OperationId, long ActorUserId,
    long BranchId, string Reason); // actor supplied only by authenticated orchestration
public sealed record WorkdayNotificationFact(Guid OperationId, long WorkdayId,
    long BranchId, long DepartmentId, long ActorUserId, long RecipientUserId,
    DateTime OccurredAtUtc, string Reason);
public interface IBranchDepartmentHeadResolver
{
    Task<long> ResolveSingleAsync(long branchId, CancellationToken ct);
}
public interface IWorkdayNotificationParticipant
{
    Task EnqueueUnscheduledAsync(WorkdayNotificationFact fact, CancellationToken ct);
}
public sealed record WorkdayPauseCommand(Guid OperationId, long ActorUserId,
    long BranchId, long WorkdayId, long IntervalId, byte[] ExpectedIntervalVersion,
    string WorkItemKind, long WorkItemId);
public interface IWorkdayPauseParticipant
{
    string WorkItemKind { get; }
    Task PauseAndCloseAsync(WorkdayPauseCommand command, CancellationToken ct);
}
```

All are trusted backend contracts, not HTTP DTOs or permissions. Participants use the
same scoped DbContext and existing A1 time participant; explicit caller-owned transaction,
no inner transaction/commit, network notification call or memory persistence.

### Exact proposed production files and impacts

| File under `src/HotelOps.Api/` | Change after bounded approval / ownership |
|---|---|
| `Services/WorkTime/WorkdayB12Contracts.cs` | Add contracts above; DEV1-owned feature boundary reused by DEV2. Review cross-developer contract before creation. |
| `Services/WorkTime/BranchDepartmentHeadResolver.cs` | Implement ResolveSingleAsync from **exactly one distinct active DEPT_MANAGER branch assignment**, active role/user/employment and current branch membership, assignment validity by SQL time. This is a proposed explicit interpretation of the approved one-DH-per-branch relationship; NOT a role broadcast. Zero/multiple candidates fail closed. No Department.ManagerUserId or global-role fallback. |
| `Services/WorkTime/WorkdayNotificationParticipant.cs` | Implement feature adapter using DEV2's actual INotificationPublisher, same transaction. **Cannot finalize constructor/call signature until actual publisher source and joint contract are supplied. Do not create/modify INotificationPublisher.** |
| `Services/WorkTime/SqlWorkTimeTransactionParticipant.cs` | Add concrete `Task<Workday> StartUnscheduledWorkdayAsync(long actorUserId, long branchId, long departmentId, string reason, CancellationToken ct)` primitive; no A1 interface change, scheduled/Open/Close/End methods preserved. SQL clock/uniqueness/User locks/audit as A1. Reject any eligible current-day assignment under retained reads. |
| `Services/WorkTime/WorkdayPauseCoordinator.cs` | Add `Task PauseAndEndAsync(long actorUserId, long branchId, long workdayId, byte[] expectedWorkdayVersion, Guid operationId, CancellationToken ct)`; resolve authoritative open interval, exact-kind adapter, pause+close then existing End. Zero/duplicate/missing adapters fail closed before mutation. |
| `Services/WorkTime/CleaningWorkdayPauseParticipant.cs`, `OperationalTaskWorkdayPauseParticipant.cs` | New DEV1 adapters for actual ON_HOLD/PAUSED transitions, existing history and responsibility, source locks/current assignment. Existing source permission must be checked. No maintenance/rework fallback or silent status-only update. |
| `Services/WorkTime/WorkdayService.cs` | Integrate only approved unscheduled/pause participants and durable operation replay; keep scheduled flow/own-only authorization unchanged. Existing `StartAsync` signature preserved. Proposed End overload adds `Guid? operationId` before CT; old B1.1 signature retained for ordinary End. |
| `DTOs/WorkTime/WorkdayRequests.cs`, `Controllers/WorkdaysController.cs` | Add nullable OperationId UUID field to Start/End bodies, mandatory only for unscheduled/Pause All. No actor/timestamp input; existing4 routes unchanged. Conditional new field must be reviewed; not active today. |
| `Program.cs` | Shared DI gate: add scoped IBranchDepartmentHeadResolver->BranchDepartmentHeadResolver; IWorkdayNotificationParticipant->WorkdayNotificationParticipant; WorkdayPauseCoordinator; two IWorkdayPauseParticipant registrations for the two implemented DEV1 kinds. **No patch is applied or requested blindly:** actual factory diff can only be finalized once DEV2 publisher DI and full supported-kind contracts are known. Existing A1/B1.1 registrations must remain byte-preserved. |

No Rework pause adapter is proposed as executable until its exact authorized status/history
transition and source permission are supplied. DEV2 owns incident/maintenance/PM adapters;
no DEV2 file edit is requested. Approval of the listed feature shape does not approve guessed
publisher signatures, an incomplete Pause All release or unspecified shared DI additions.

## Notification event proposal to add to the actual joint contract

Proposed logical event `workday.unscheduled.started.v1` (NOT a seeded notification type):
payload = WorkdayNotificationFact above; safe workday action path must be own/branch authorized.
Event key = `(actor, branch, operationId, eventKind)`; inbox dedup key additionally includes
approved recipient. No passwords/session tokens or client actor/recipient IDs in payload.
Event label must be mapped to DEV2's approved event/type registry; do not insert a guessed
NotificationType/status or map this into an unrelated existing event.

Recipient resolution: pre-read candidate DH, lock actor+candidate IDs ascending, then re-resolve
under retained organizational reads. Changed candidate requires full rollback/retry, never
extending A1's already fixed lock set out of order. Employee operational DepartmentId comes
from the authoritative active user department in the same branch; unresolved context fails
closed rather than choosing an arbitrary department. DH remains branch-wide.

Unscheduled Start requires no eligible own assignment for hotel-local current date (same
eligibility as B1.1 Own-Shifts), validated nonblank reason <= existing1000-character column,
new Workday IsUnscheduled=true/WORKING, SQL timestamps and atomic CREATE audit plus durable
notification intent. Publisher must participate in this transaction and roll back on failure.

Replay must persist operation->business result mapping and normalized request fingerprint
(actor, branch, action, reason/context). Same key+same request returns original result without
a new Workday/event even if original day later Ended; same key+different payload409. Concurrent
replay serialized by User/time locks; rollback leaves no successful replay record. Mapping
cannot disappear earlier than the approved replay retention window. **If actual DEV2 publisher
does not provide the required durable operation/replay facility, stop and produce a separate
schema/store review; schema changes are not included in this request.**

Delivery is owned by DEV2: durable dispatch, transient retry/backoff/restart, terminal failure
visibility and dedup across crashes. Full UC139 acceptance requires a readable inbox item,
not just successful EnqueueUnscheduledAsync. No fake publisher is production-ready evidence.

## Pause All atomic protocol

1. Validate actor's real SQL session, ATTENDANCE_RECORD own day, employment/branch and
   expected Workday version after User/time locks; fresh state after any lock wait.
2. Read global open intervals for actor; >1 is integrity failure (A1 SQL unique index remains).
   Identify exact kind/key, owner, branch, current assignment and authoritative source revision.
3. Require source action permission: CLEANING_EXECUTE or TASK_EXECUTE where binding exists.
   REWORK binding/transition and MAINTENANCE/PM participants require specific coordination;
   never promote ATTENDANCE_RECORD into source execution rights.
4. Exact source participant retains source/history/version locks. Under those locks call A1
   CloseInterval while source still IN_PROGRESS (A1 source validation requires that), then apply
   approved pause/hold plus history/audit, reason 'end of shift', preserve assignment/progress.
   Source and close have one owner; coordinator does not perform a duplicate close.
5. Only after participant succeeds, call existing EndWorkdayAsync. One outer commit. Any
   permission/revision/source/history/interval/audit failure rolls back **every mutation**.

Lock order: user IDs ascending -> existing A1 per-user application/time locks -> durable
replay mapping -> branch/role/context -> source and history in deterministic order ->
notification intent. Actual DEV2 lock order must be reconciled before integration. No HTTP
callbacks, detached transactions, independent context or maintenance status mutation by DEV1.

## Required acceptance before full UC139 completion

- Unscheduled reason validation, no-shift verification, unique approved branch DH, missing/
  ambiguous/inactive/foreign recipient, durable readable inbox, retry/restart/dedup/failure.
- Same operation replay, payload conflict, concurrent replay, response-loss retry after day End,
  rollback without replay/intents, original business result identity retained.
- Real source pause for **every supported kind**, preserved history/progress/assignment,
  same-transaction maintenance participant, unsupported-kind refusal, stale source/interval/day,
  cross-user/branch, permission/session revoke during waits, audit/publisher/source fault rollback.
- A1/B1.1 scheduled endpoints/security regression, build and diff check, disposable SQL only.

## Dependency handoff and resumption gate

Task79 is closed with P0 fixed/verified and B1.2 explicitly blocked. DEV1 has independently
prepared exact feature signatures/files, event facts, recipient/replay decisions, source
transition constraints, lock/transaction order and acceptance matrix above. These review
artifacts do not constitute runtime implementations or request blanket approval.

DEV2 handoff must provide all of the following before integration resumes:

1. Joint-approved publisher/replay contract and exact namespace/signatures; caller-owned
   DbContext/transaction participation and reconciled lock order, no inner commit.
2. Source implementation at an identifiable commit, plus explicit approved integration
   scope. The draft/checkpoint HEAD alone is insufficient evidence of implementation.
3. Approved UC139 event/type mapping, branch-DH recipient relationship, durable operation
   mapping/retention, idempotency/payload-conflict and retry/failure/recovery semantics.
4. Durable readable inbox delivery evidence, including restart/retry/dedup and rollback
   publishing nothing. Enqueue-only or mocked publisher is insufficient.
5. DEV2-owned maintenance/PM pause participant with same-transaction source/history/time
   semantics and acceptance evidence; no direct maintenance status edits by DEV1.

DEV1 still needs specific approval/clarification for Rework pause state/history/permission
and the eventual exact shared DI diff. Cleaning/Task adapters can be prepared from their
authoritative source workflows after that bounded scope is agreed; do not call a subset
full Pause All. No B1.2 runtime changes until the relevant gates are satisfied.

When approved contracts, source and acceptance evidence arrive, compare them with this
proposal, finalize the smallest exact diff, then integrate and run full disposable SQL/HTTPS
acceptance. Preserve existing P0/B1.1; no rollback or reimplementation is needed.
