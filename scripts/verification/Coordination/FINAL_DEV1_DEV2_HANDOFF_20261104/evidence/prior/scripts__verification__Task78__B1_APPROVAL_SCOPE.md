# UPDATE — User APPROVED B1.1 STRICTLY BOUNDED

Approved binding and exact Program.cs patch applied after implementation/build/DI checks. Scheduled public API acceptance159 PASS; full UC139 remains BLOCKED_EXTERNAL. See TASK78_UC139_IMPLEMENTATION.md and TASK78_B11_VALIDATION.json. Original review proposal retained below for traceability; NOT APPLIED labels below describe its pre-approval state.

---

# Task78 — bounded B1.1 approval, NOT APPLIED

This request covers scheduled own-workday public API activation only. It does not
approve notification schema, organization mapping, maintenance mutations or full UC139 completion.

## One proposed existing-permission binding

Approve `ATTENDANCE_RECORD` for **own** Start/current/End Workday, and `SHIFT_VIEW`
for **own** today's eligible shift assignments. Use existing SQL role grants/scopes;
no catalog/grant edits, new permissions, role-name shortcuts or ShiftAttendance writes.
The five UC139 actor roles already have ATTENDANCE_RECORD/OWN in
`scripts/database/release/02_HotelOps_Upgrade_Auth_To_RBAC.sql`; this is evidence
for the proposal, not approval or proof of current database grants.

Even a BRANCH/ALL path never permits acting on another employee. Actor is exclusively
`ICurrentUserAccessor`, with current SQL session/account/employment/branch/access revalidation.
Restricted sessions fail closed. Mutations revalidate session and permission after
A1 user/time locks; caller owns the same DbContext transaction and commits audit atomically.

| Exact file / signature or change | Ownership / schema impact | Required evidence before activation |
|---|---|---|
| `src/HotelOps.Api/Program.cs`: apply only [B1.Program.di.patch](B1.Program.di.patch) | Shared DI: three additions; replace one existing resolver registration with delegating wrapper. No middleware/config/schema change. Preserve A1 registration and scoped participant instance. | Build, same-instance DI and cycle checks; existing cleaning resolver regressions; production startup |
| `src/HotelOps.Api/Services/WorkTime/WorkdayPermissionResourceResolver.cs`: `ResolveAsync(string, PermissionResource, long, CancellationToken)` | DEV1 feature adapter implementing existing shared interface. Bind proposed codes only to `workday_self_branch` (resource ID is branch ID), `workday` and `shift_assignment`; resolve ownership/branch from SQL. Unsupported types delegate unchanged to existing CleaningTaskResourceResolver. No schema. Approval needed for semantic authorization binding. | OWN/BRANCH/ALL own-only; malformed/foreign resource; branch membership/permission revoke; cleaning delegation |
| `src/HotelOps.Api/Services/WorkTime/WorkdayService.cs`: `StartAsync(long branchId, StartWorkdayRequest, CancellationToken)`, `CurrentAsync(long branchId, CancellationToken)`, `EndAsync(long branchId, long workdayId, byte[] expectedVersion, bool pauseAll, CancellationToken)`, `OwnShiftsAsync(long branchId, CancellationToken)` | DEV1 orchestration using approved A1 participant's existing scheduled Start/End primitives. No interface/entity/DbContext change. Service receives concrete participant alias to same scoped A1 instance. | Real SQL/TLS, authenticated session, revoke during lock wait, concurrent starts, audit failure/rollback, cross-user/branch, server time |
| `src/HotelOps.Api/Controllers/WorkdaysController.cs` and `DTOs/WorkTime/WorkdayRequests.cs` | DEV1 endpoints below; no controller business logic. Feature DTO rejects unknown JSON properties. No user/actor/timestamp input. | DTO/status/routes, HTTPS/cookie/CSRF, no EF entity response; no verification-only driver in production |

Proposed routes (all require valid Cookie auth, HTTPS and normal restricted-session/CSRF handling):

- `GET /api/branches/{branchId}/workdays/current`: ATTENDANCE_RECORD, own current day; 200 DTO or 204 none.
- `GET /api/branches/{branchId}/workdays/own-shifts`: SHIFT_VIEW, own today's eligible assignments only.
- `POST /api/branches/{branchId}/workdays`: ATTENDANCE_RECORD, scheduled body `{shiftAssignmentId}`; 201 DTO.
- `POST /api/branches/{branchId}/workdays/{workdayId}/end`: ATTENDANCE_RECORD, If-Match eight-byte rowversion, explicit `{pauseAll:false}`; 200 DTO.

Unscheduled starts and Pause All **remain unavailable** until their mandatory adapters are
approved and implemented; validate request, return a documented 503 dependency-unavailable
problem without mutations. Ordinary End with an open interval returns409 MSG31 and describes
the Pause All requirement, not a claim that Pause All is currently available. Other proposed
responses:401 invalid/revoked session;403 missing permission/restricted/foreign scope;400 invalid
input;412 stale rowversion;409 already Working/invalid state;503 unresolved schema/dependency.

Do not apply the DI patch before WorkdayService/resolver exist, build passes and actual DI
resolves. These classes are proposed signatures, not implemented or build-tested artifacts.
Approval of B1.1 allows implementation and tests of these exact files/semantics only.

## Separate DEV2 coordination; not part of B1.1 approval

- In-app notification adapter: event keyed by `(WorkdayId, event kind, recipient)`; same
  scoped DbContext/transaction; no independent commit/network send. DEV2 must provide durable
  outbox deduplication, dispatch/retry/poison/failure handling and readable inbox delivery.
  Proposed event payload: server event ID, workday ID, branch, executor, server-start timestamp,
  validated reason and approved recipient ID; redact credentials and request/session data.
- Branch-level Department Head relationship is unresolved. `Department.ManagerUserId` is
  department-level legacy data, not approved branch-wide routing. No role broadcast or guessed ID.
  DEV2/team must name authoritative relation, active/branch eligibility, ambiguity/no-recipient
  handling and whether recipient snapshot or dynamic resolution governs retry.
- Pause All participant proposal: `PauseForWorkdayEndAsync(WorkTimeOperation operation,
  long intervalId, byte[] expectedVersion, CancellationToken ct)` on caller-owned transaction.
  It must lock/re-read authoritative source, validate ownership/branch/current state and preserve
  history/responsibility/audit. It must coordinate the interval close **before** any source-state
  mutation that would invalidate A1's IN_PROGRESS source validation, then commit source pause,
  interval close and workday End together. No duplicate close by orchestrator and adapter.
- Lock order: user IDs ascending, A1 time locks, authoritative source locks in agreed deterministic
  order; fresh permission/session checks after waits. Outer caller alone commits. Any source,
  interval, audit or delivery-intent failure rolls back all mandatory mutations.
- CLEANING/REWORK/TASK belong to DEV1, but current working tree has no reusable pause participant.
  Exact target states/history fields must be verified against each UC/schema before implementation;
  this document approves none. INCIDENT/MAINTENANCE_OCCURRENCE remain DEV2/fail closed.
- Deduplication cannot be inferred from a successful HTTP retry. Retrying an entire failed SQL
  transaction must not create duplicate notification/inbox records; terminal business conflicts
  are not transient retry successes. Notification acceptance requires delivered, readable inbox
  data; outbox enqueue alone never passes full UC139.

No notification/outbox interface is implemented by this proposal. DEV2 code is not modified
and this handoff has not been sent to anyone automatically.
