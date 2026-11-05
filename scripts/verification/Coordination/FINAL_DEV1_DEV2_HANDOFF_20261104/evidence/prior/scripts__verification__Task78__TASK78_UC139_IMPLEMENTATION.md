# Task78 — B1.1 implemented: scheduled own-workday public API

## Current result after explicit B1.1 approval

**B1.1 scheduled public API implemented and verified; full UC139 remains BLOCKED_EXTERNAL.**
Approved ATTENDANCE_RECORD own Start/Current/End and SHIFT_VIEW own eligible shifts.
No new permissions/grants or schema changes. A1 participant reused unchanged.

### Production files

Added `src/HotelOps.Api/Services/WorkTime/WorkdayPermissionResourceResolver.cs`,
`Services/WorkTime/WorkdayService.cs`, `Controllers/WorkdaysController.cs`,
`DTOs/WorkTime/WorkdayRequests.cs`. Existing OwnWorkdaySnapshotReader reused unchanged.
Changed only Program.cs among existing production files: exact approved three DI additions
and resolver registration replacement. Build and DI resolution/same-instance isolation
passed BEFORE applying the patch; reverse `git apply --check` passes after activation.
No middleware/config/other registration, entity/DbContext, DEV2 source or UC006 gate edits.

Four routes are active:

| Route | Binding and behavior |
|---|---|
| GET `/api/branches/{branchId}/workdays/current` | ATTENDANCE_RECORD; own Working snapshot200/none204 |
| GET `/api/branches/{branchId}/workdays/own-shifts` | SHIFT_VIEW; actor's eligible current-date assignments only |
| POST `/api/branches/{branchId}/workdays` | ATTENDANCE_RECORD; `{shiftAssignmentId}`;201 with DTO/ETag/Location |
| POST `/api/branches/{branchId}/workdays/{workdayId}/end` | ATTENDANCE_RECORD; `{pauseAll:false}` and quoted base64 eight-byte `If-Match`;200 DTO/new ETag |

Actor comes exclusively from ICurrentUserAccessor; protected ticket and SQL validation
must match that actor/session. No actor/timestamp request properties accepted. Missing,
invalid/revoked sessions401; restricted sessions403; permission/ownership/scope403;
invalid input400; stale rowversion412; duplicate Start/open interval conflict409;
unavailable branches/store503. Open interval End returns `MSG31_PAUSE_ALL_REQUIRED`.

Service begins same-DbContext Serializable transaction, obtains A1 User/time locks, then
validates real SQL session, active account/employment and approved SQL resource permission.
Permission/session/source reads retain locks to commit. Revalidation happens after waits;
no stale middleware snapshot alone authorizes mutation. Resolver checks ownership before
scope evaluation, including BRANCH/ALL, and delegates all unrelated resource types to the
original CleaningTaskResourceResolver. Scoped concrete alias uses the existing A1 interface
instance; no second participant is constructed for the service.

### Acceptance actually executed after approval

**159 checks PASS** in [production HTTPS/SQL receipt](task78-b11-https-sql3.local.json),
database `HotelOps_Canonical_20261104_063549_81d4bc64_Fresh`:

- Actual production Program process, real TLS/cookie credential login, SQL sessions/RBAC;
  startup/readiness, all four public routes and production OpenAPI verified.
- Own shifts/current/scheduled Start/End persisted and inspected independently through SQL;
  actor/branch/server-time bounds, atomic CREATE/UPDATE audits and changed rowversion.
- Anonymous/invalid/restricted/revoked session, missing ATTENDANCE_RECORD/SHIFT_VIEW,
  cross-branch/foreign assignment/cross-user End, invalid JSON actor/timestamp, CSRF,
  missing/weak/stale If-Match and duplicate Start refused.
- Concurrent production Start yielded exactly201+409 and one Working row.
- Real SQL open interval: End409 preserved Working day, open interval and IN_PROGRESS source.
  Interval closure in fixture uses A1 trusted backend participant, not an extra public endpoint.
- Permission/session revoke committed while actual HTTP request waited on A1 application lock;
  DMV proved blocking, post-wait checks returned403/401 with no workday.
- Actual SQL audit-table retained lock blocked audit write after Workday INSERT. READUNCOMMITTED
  inspection proved inserted-but-uncommitted row; normal SQL command timeout returned503;
  outer transaction rolled back Workday and left no false workday audit. No test trigger,
  schema alteration, fake notification or mocked persistence used.
- Unscheduled reason required400; reason supplied503; Pause All503; neither implemented.
  Existing RBAC catalog/grants and ShiftAttendance fingerprints preserved.
- Included21 adapter/scope checks:12 real-SQL comparisons against original cleaning delegate,
  and9 ownership checks using synthetic OWN/BRANCH/ALL grant paths with real SQL resource
  ownership. These21 are component checks, not21 extra live grant configurations. Actual
  HTTPS tests use existing SQL OWN grants; no catalog permission/role-grant modifications.

Additional regression **199 PASS**:93 authorization with production fallback mode,
43 PermissionService,14 CurrentUserAccessor,49 resource authorization. These use isolated
collaborators and are reported separately from159. Relevant runners were rebuilt against
current API. Pre-activation DI probe PASS with ValidateOnBuild/ValidateScopes, same instance
and separate-scope isolation. Production startup + actual endpoint resolution subsequently PASS.

`dotnet build HotelOps.slnx` PASS0 errors,2 NU1900 warnings; harness final build PASS0 errors,
2 NU1900 warnings (initial restore build4 entries). Vulnerability feed unavailable.
DI verification harness emitted ASP0000 for intentionally constructing its test ServiceProvider;
no production ASP0000 warning. `dotnet test HotelOps.slnx --no-restore` exit0/no framework
cases: NOT AVAILABLE, not framework tests PASS. `git diff --check` PASS.

### Failed attempts and remaining issue

First two disposable trials were retained, not reset/dropped:
`HotelOps_Canonical_20261104_062644_e9f68a23_Fresh` and
`HotelOps_Canonical_20261104_063341_c44f2751_Fresh`.
Restricted credential with10-minute remaining expiry hit pre-existing short-expiry UTC/local
cookie bug: login200, next request401; handler logged `Ticket expired`. Accepted trial uses
a credential lasting beyond the normal15-minute restricted-session cap and proves403 denial.
Short-expiry case is **not fixed or reported PASS**; see
[exact finding and unapplied candidate](SHORT_TEMPORARY_EXPIRY_AUTH_FINDING.md).
Shared authentication changes remain outside B1.1. First DI runner attempt lacked restore
assets; restore/build/probe succeeded before Program activation. All failed logs retained.

No unscheduled notification delivery, durable inbox acceptance or Pause All transition/rollback
was implemented/executed: **NOT RUN / BLOCKED** for these full-UC flows. The503 fail-closed
branches were executed. DEV2 recipient/outbox/maintenance contracts in B1 scope remain pending;
no DEV2 source edited or automatic handoff message sent. UC111 correction/time log and UC138
shift CRUD prerequisites are unchanged.

### Ledger, preservation and next step

[UC_LEDGER_TASK78.json](UC_LEDGER_TASK78.json): UC139/111 BLOCKED_EXTERNAL; UC138
NOT_IMPLEMENTED. **26/75 UC;60/256 points=23.4375%; delta0 UC/0 points**. All75 existing
classifications/weights/earned points preserved; only UC139 evidence and task metadata updated.
Task77 ledger stays unchanged. Full UC139 cannot be completed by scheduled slice alone.

Receipt [TASK78_B11_VALIDATION.json](TASK78_B11_VALIDATION.json) records file hashes,
expected existing-path changes, root HEAD/index/four-worktree preservation and elapsed time
from saved B1.1 preflight. Earlier Task78 review receipt preserved separately as
TASK78_REVIEW_VALIDATION.json. No HotelOpsDB/protected database accessed; three owned
disposable trials retained; no commit/push/merge/rebase/reset/clean.

Next UC139 work is the approved branch-level DH recipient + DEV2 transactional outbox/inbox
contract and source-specific Pause All participants (DEV1 cleaning/rework/task; DEV2 maintenance).
The short-expiry authentication fix is a separate exact shared-auth review, not silently bundled.

---

# Historical Task78 review before user approved B1.1

The following sections preserve the earlier review state and do not describe current activation.

# Task78 — UC139 contract closure / public API activation gate

Date: 04/11/2026. DEV1, `feature/BE1-database-integration`.
**Status: independent read component implemented; B1.1 awaiting explicit approval;
public UC139 API NOT IMPLEMENTED; full UC139 remains BLOCKED_EXTERNAL.**

## 1. Verified baseline and requirements

Read AGENTS.md, Task77 report/A1 receipt/approval scope/DEV2 contract, current delivery
ledger, SRS reading guide/change notes, UC139 (§12.3; source lines11743–11829),
GBR136–143 (lines16126–16133), actual A1 participant/interface, EF mappings, Program DI,
CurrentUserAccessor/PermissionService/data source and actual notification entities.
The stored SRS/matrix remains historical draft; current task explicitly confirms UC139
behavior but does not approve a new permission binding or branch organization relation.

A1 source remains in the current working tree, not a new commit. Existing registration
is interface-to-SqlWorkTimeTransactionParticipant. Workdays/WorkIntervals are distinct
from ShiftAttendance. A1 marker `Task77-v1` was read from exactly
`HotelOps_Canonical_20261103_162910_76025447_Fresh`; no DDL or data writes were performed.
Task77's96 SQL/HTTPS,7 production-host,26 User SQL,199 security,422 arithmetic checks
remain **historical baseline evidence**, not Task78 executions.

## 2. B1 decisions and approval request

The concrete request is [B1_APPROVAL_SCOPE.md](B1_APPROVAL_SCOPE.md), with
[B1.Program.di.patch](B1.Program.di.patch). Patch applicability checked with
`git apply --check`; **patch not applied**. This is textual applicability, not compilation
or DI resolution of the proposed service/resolver, which are not yet implemented.

Proposed binding: ATTENDANCE_RECORD for own scheduled Start/current/End; SHIFT_VIEW
for own today's shifts. Existing five actor-role OWN grants support reviewing that
binding, but no code assumes approval and no permission/catalog/grant is changed.
OWN grants cannot be satisfied through EvaluateBranchAsync: the existing PermissionService
requires a trusted resource adapter. A delegating adapter is proposed so cleaning resolution
is preserved. No authorization core/interface change is requested.

Shared change proposed is **Program.cs only**: three registrations plus replacement of
one resolver registration with a wrapper. Existing A1 participant registration stays;
concrete alias returns its same scoped instance. No DbContext/entity/schema modification.
Activation is conditional on implementation, build and same-instance DI checks after approval.

Unscheduled start requires validated reason, correct no-shift determination, atomic
notification intent and delivered readable inbox. Existing Department.ManagerUserId is
not an approved branch-level DH relationship. Notification entities alone do not establish
an outbox, dispatcher, retry semantics or inbox API. No reusable notification adapter or
work-item pause participant was found in actual working-tree services.

Ordinary End with open interval must409 MSG31. Full Pause All must pause/hold the actual
source, preserve history/responsibility/audit, close server-time interval and End atomically.
A1 CloseInterval validates IN_PROGRESS source; a future pause participant must account for
that ordering rather than setting source state first and making Close fail. No maintenance
state is mutated, and no guessed PAUSED/HOLD transition is implemented.

## 3. Actual production change in this task

One new DEV1 file:
`src/HotelOps.Api/Services/WorkTime/OwnWorkdaySnapshotReader.cs`.

- `ReadAsync(long branchId, CancellationToken)` derives actor exclusively from
  ICurrentUserAccessor. No client user IDs or timestamps.
- Rejects missing/restricted identity; checks SQL account/employment/deletion status,
  active branch and current branch access using SQL server clock.
- Reads only own Working day/open interval using AsNoTracking and DTO projections;
  UTC output, base64 rowversion, no EF entity returned. No business/audit writes.
- Query is a presentation component, **not a permission/session authorization boundary**
  or an authoritative transition precondition. Eventual API must authorize/revalidate SQL
  session and writes must re-read under A1 locks. Multi-query output is not promised to be
  an atomic snapshot under concurrent transitions.
- No route or DI registration: protects the unapproved binding. Existing A1 scheduled
  Start/End is reused in the proposed orchestrator, not reimplemented.

No existing production file changed, no shared edits applied, no DEV2 edits. New verification
source: `OwnWorkdayReaderVerification.cs`; temporary build project is ignored under
`obj/Task78Reader`, outside production solution architecture. It is a SQL component runner,
not a framework test project or production endpoint.

## 4. Executed verification

| Check | Actual result / limit |
|---|---|
| `dotnet build HotelOps.slnx --no-restore` | PASS;0 errors,1 NU1900 warning (NuGet vulnerability feed unavailable) |
| Component runner build | PASS;0 errors,4 NU1900 warnings |
| Actual A1 SQL read component | **8 PASS**, [local receipt](task78-reader.local.json); real SQL, identity collaborator is a double, no HTTPS/permission acceptance |
| Negative read cases | Missing identity, restricted identity, invalid/inaccessible branch, unknown account rejected |
| Positive read case | Eligible actor/current-none agrees with independent SQL; no tracked entities; workday content unchanged |
| Populated Working-day/open-interval read | **NOT RUN**: chosen existing eligible fixture has no Working day; no fake fixture/acceptance claimed |
| Security, current production fallback mode |93 authorization +43 permission +14 current-user +49 resource = **199 PASS**; isolated collaborators, not live business SQL |
| Default authorization mode |61 PASS separately; not added to199 to inflate count |
| `dotnet test HotelOps.slnx --no-restore` | Exit0 with no framework cases; **NOT AVAILABLE**, not framework tests PASS |
| `git diff --check` | PASS |
| B1 DI patch check | PASS, not applied. Initial one-context patch failed applicability; regenerated with normal context and rechecked successfully. Python helper unavailable; PowerShell/apply_patch used, no source overwritten |

New logs: `obj/task78-build.local.log`, `task78-reader-build.local.log`,
`task78-reader-sql.local.log`, `task78-authorization-fallback-regression.local.log`,
`task78-{permission,current-user,resource}-regression.local.log`,
`task78-framework-tests.local.log`. Initial no-build default authorization run61 was followed
by rebuilding/current fallback run93; only actual fallback93 is used in199.

### Mandatory UC139 acceptance still NOT RUN in Task78

Scheduled public Start, unscheduled start/reason, delivered DH inbox notification,
public missing-permission/concurrent starts, ordinary public End/no-open and open-rejection,
explicit Pause All/rollback, public cross-user/branch and permission/session revoke,
public atomic audit/workday consistency: **NOT RUN**, public routes not activated.
A1's historical component tests do not satisfy these missing public cases.

## 5. DEV2 coordination

Local handoff in B1 scope specifies notification event/dedup key, transaction ownership,
recipient ambiguity, dispatch/retry/failure handling, durable readable inbox acceptance,
source pause participant, lock ordering and rollback. No messages were sent automatically.
DEV2 must supply an approved notification/outbox participant and maintenance pause/identity
contract. DEV1 still owns cleaning/rework/task pause implementation, pending exact source
transition/history review. Unsupported kinds fail closed; one-open invariant remains A1 SQL.

## 6. Ledger and exact next action

UC139 **BLOCKED_EXTERNAL**, UC111 **BLOCKED_EXTERNAL**, UC138 **NOT_IMPLEMENTED**.
No UC upgraded. **26/75;60/256=23.4375%; delta0 UC/0 points; remaining49 UC/196 points**.
Task77 ledger and delivery plan are preserved; Task78 receipt records unchanged classifications.
UC111 still needs own time-log/correction/approved permission/notification lifecycle;
UC138 still needs shift/roster CRUD and its full acceptance, not ShiftAttendance substitution.

Next: user reviews **B1.1 proposed existing-permission binding and exact Program.cs diff**.
After approval, implement scheduled public API slice and real TLS/SQL acceptance without
claiming full UC139 until notification and Pause All alternatives also pass. No blanket
approval is requested for schema, outbox or maintenance.

Preservation, hashes and elapsed time: [TASK78_VALIDATION.json](TASK78_VALIDATION.json).
Measured interval starts at saved preflight; initial document review predates that clock and
was not separately timed. Full-turn elapsed cannot be claimed exactly. No HotelOpsDB or
protected/shared DB accessed; no commits/push/merge/rebase/reset/clean/branch/worktree changes.
