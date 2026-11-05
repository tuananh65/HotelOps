# Task77 — Shared time participant / DEV2 UC062 integration gate

**A1 APPROVED, IMPLEMENTED, REGISTERED, SQL/HTTPS COMPONENT VERIFIED (96 checks).** DEV1 FE12 owns time authority. DEV2 owns PM occurrence identities, target/branch resolution, current assignment, source authorization, lifecycle, schedule advance and notification. This is a local handoff document, not an external message. **PM occurrence adapter remains disabled/fails closed; UC062 end-to-end readiness is not certified.**

Current implementation: `src/HotelOps.Api/Services/WorkTime/SqlWorkTimeTransactionParticipant.cs`, same scoped DbContext, constructor/DI graph verified. Supports CLEANING/REWORK/TASK source consistency with actual SQL positive coverage; INCIDENT/MAINTENANCE_OCCURRENCE unresolved. Scheduled Workday Start and End primitives exist without public Workday endpoints. User's strict A1 approval excludes public permission bindings/notifications/Pause All/PM identity changes. Latest report: [Task77 A1](TASK77_WORKDAY_FOUNDATION.md).

## API and transaction boundary

Exact implemented interface matches [IWorkTimeTransactionParticipant.cs.proposed](proposal/IWorkTimeTransactionParticipant.cs.proposed) byte-for-byte. One scoped `HotelOpsDbContext`, connection and explicit transaction are mandatory. No independent transaction, implicit commit, HTTP time callback, in-memory persistence or maintenance-only duplicate time table.

1. DEV2 may perform no-lock discovery of source IDs/current owner to determine the complete lock set. Discovery is never authority. Begin the scoped context transaction.
2. Call `LockUsersAsync` with actor + every affected old/new owner **before any feature/location lock**. Participant uses existing `UserEligibilityLock`: users sorted ascending, `UPDLOCK,HOLDLOCK`, fresh SQL eligibility. Acquire transaction-owned time serialization locks in the same user order. No Session-owned app lock.
3. Retain Working workday/time range locks for those users. Every time writer, including future corrections and Workday End, must use this protocol. Implementation accepts ReadCommitted/Serializable with retained User/UPDLOCK/HOLDLOCK boundaries; unique filtered indexes are the independent concurrent-insert backstop. Actual two-connection Working/Open races and independent unique-index rejection PASS; PM writer/location races remain NOT RUN.
4. DEV2 now locks its location/source resources in its reviewed order; corrective Incident precedes MaintenanceWork. PM must document schedule/run/work target order. Re-read current owner, active references, branch, expected revision and source state after waiting. If discovery changed owner to someone outside the acquired user set: rollback and restart discovery/locking, never acquire a new earlier User lock late.
5. DEV2 validates source action using SQL-backed permissions and current resource ownership; passes server-established `WorkTimeOperation`. `ActorUserId` is validated actor, `OwnerUserId` executor whose interval changes. These may differ for authorized supervisor reassignment/closure; opening work requires the executing owner. Never use arbitrary client IDs/PerformedBy as ownership proof.
6. Call `OpenIntervalAsync` or `CloseIntervalAsync` in the same context/transaction. Participant requires prior locked user set, rechecks branch membership and time invariants, captures one SQL UTC time after lock waits and writes time + existing `AuditLog` (`CREATE`/`UPDATE`) in the transaction. It never calls Commit.
7. DEV2 updates occurrence/work/schedule and audit/publication intent, rechecks source permission/session/eligibility before commit as required. Commit once. On any failure, rollback **all** source/time/schedule/audit changes and discard tracked state before a fresh retry. No success after partial SaveChanges.

`LockUsersAsync` holds locks until caller commit/rollback. Caller must not release/reuse the participant after completing the transaction; implementation must reject wrong/missing transaction and missing locked owner set. A scoped participant is not a transferable proof of authorization.

## Open/close semantics

- Open: owner active/not deleted/employment active, active branch/access, authoritative current ownership and source transition validated; one Working Workday in the same branch, no global open interval. Insert immutable server opened_at plus equal effective_started_at; no supplied timestamps. Work item identity must remain stable across resume/correction.
- Global one-open constraint is on **user_id only**, not branch/module/item. Concurrent start in cleaning and PM cannot both succeed. Workday one-Working constraint is also user-only.
- Close: match interval ID, source kind/ID, owner, branch, workday and expected rowversion; interval still open and workday still Working. Record server closed_at/closed_by and equal initial effective_ended_at. A repeated close returns state conflict; it never creates a second close/audit or silently succeeds as a different operation. Retry after unknown commit outcome requires source-revision reconciliation, not blind repeat.
- Locking reads owner eligibility without making a blanket opening decision. New starts require an eligible owner; closure removes responsibility and still requires an active authorized actor/current source action. Legacy inactive-owner closure needs an explicitly authorized source policy; the time primitive does not invent an administrator bypass.
- All intervals for a user must be overlap checked while holding User/time locks. This includes historical correction changes, not just open rows. Closed effort comes from effective closed ranges; original server ranges remain immutable evidence. Open range is provisional and separate.
- Workday End rejects an open interval. Pause All belongs to source lifecycle adapters; time service cannot silently close intervals or change PM state alone. Missing adapter means reject/rollback, not pretend Pause All succeeded.

## Work-item identity

Proposed discriminators represent SRS items: CLEANING, REWORK, INCIDENT, MAINTENANCE_OCCURRENCE, TASK. They are proposed storage codes, not approved business states. Source kind/ID is polymorphic; no fake FK to five tables. Service/adapters must prove SQL existence, unambiguous owner, branch and current revision under retained locks.

Existing PM `PreventiveMaintenanceRun.RunId` is a **candidate**, not automatically an accepted execution-occurrence key: Run has nullable Work link and historical PerformedBy, no proved current assignee. DEV2 must confirm its occurrence key and authoritative assignee/branch resolution. Until then MAINTENANCE_OCCURRENCE start is disabled/fails closed. Do not store IncidentId as an alias of RunId or infer ownership from historical performer.

## Error contract (proposed)

| Code | Meaning / caller action |
|---|---|
| TIME_TRANSACTION_REQUIRED / TIME_LOCK_SET_REQUIRED | Programmer/contract error; abort; no user-visible SQL details |
| TIME_ACTOR_INVALID / TIME_SCOPE_DENIED | Invalid session/actor or forbidden source/branch; HTTP401/403 at owning endpoint |
| TIME_WORKDAY_REQUIRED | No matching Working workday; conflict409 |
| TIME_ALREADY_WORKING / TIME_INTERVAL_ALREADY_OPEN | Competing Workday/interval start; conflict409, authorized existing item reference only |
| TIME_STATE_CONFLICT | Ended Workday, already closed interval, mismatch after discovery; rollback409 |
| TIME_STALE | Expected revision differs; endpoint412, missing required precondition428 |
| TIME_SOURCE_UNRESOLVED | Missing contract schema (SQL207/208), ambiguous source mapping or unsupported module; no successful mutation, caller rollback;503 at owning endpoint |
| TIME_BUSY | SQL deadlock/lock timeout/unique race; rollback, bounded full-transaction retry or conflict; no retry of a single detached statement |

SQL exceptions2601/2627 must be translated only for the known one-Working/one-open indexes; unrelated integrity violations are not “already started”.1205/1222/timeout must not leak provider text. Cancellation propagates after rollback; do not swallow it into success.

## User deactivation compatibility

Existing `ApprovedDev1ResponsibilityQueries` retains `WORKDAY_CONTRACT` unresolved. **Do not clear that marker or enable Deactivate in this milestone.** Future time-read adapter must report a Working day as a blocker even when no interval is open; any open interval blocks as well. Ended day does not remove assignments, pending work or handover obligations. Missing/contradictory data fails closed.

Workday.department_id records operational context, not a new Department Head authorization boundary. Existing Department Head branch-wide scope is preserved; do not equate it to User.DepartmentId or a single department manager.

Time start/resume, future deactivation and role/scope removal must serialize on the existing User-first retained lock. Lock protocol alone is not a complete deactivation gate. Predicate/source adapters require separately reviewed integration and SQL races; no changes to those gates are included in Task77 proposal.

## Required acceptance after approval

Real HTTPS/SQL tests on a new guarded trial: scheduled/unscheduled day; reason and notification intent; own/branch/source permission; same-user concurrent Workday starts; same-user cross-module interval starts; revoked membership/assignment/session during lock wait; End vs Start; close vs reassignment; zero/negative/overlap and stale correction; SQL trigger/audit failure with full rollback; PM occurrence+interval+schedule atomicity; correction SoD and original evidence retention. ShiftAttendance and catalog fingerprints must remain unchanged by time actions.

Actual A1 checks:96 component SQL/HTTPS checks, including fail-closed missing-schema legacy case, populated legacy migration/rollback/recovery, same-user Working/Open races proven by DMV, global cross-module exclusion, source ownership/branch, SQL server-time bounds, stale/invalid state, inactive actor, actual SQL audit failure/rollback, and caller source+Workday+interval+audit atomic rollback. Actual scoped HTTPS caller uses production Cookie/SQL session/RBAC components. Production Program startup/authentication adds7 checks separately. Unscheduled delivery/retry, correction SoD workflow and PM occurrence+interval+schedule integration remain NOT RUN. Pure math422 and isolated security199 are reported separately and never replace that missing acceptance.
