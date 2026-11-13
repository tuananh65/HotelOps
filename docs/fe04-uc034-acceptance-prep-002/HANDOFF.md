# UC034 isolated SQL / HTTPS acceptance handoff — PREPARED, NOT AUTHORIZED
Owner: DEV1 FE04. Starting source: db12691295ffe0382b005c686ac7478326b0c0d6.
Producer attribution contract: R05 95f7422e284bc81bcfcf9f460dace52022de5cc7.
No live target, lease, execution identity or dependency acceptance is granted by this file.

## 1. Runnable source boundary

Three compiled test-only files provide:
- CleaningCompletionAcceptanceHarness.RunAsync(case, ct): every business invocation calls CompleteCleaningAsync.
- CleaningCompletionWriteFault / CommitFault: case-specific injection with observable Fired markers.
- CleaningCompletionAcceptanceHarnessTests: no-SQL safety/selector tests, not domain acceptance.

There is deliberately no SQL-auto-running Fact or environment flag that grants approval.
The approved runtime host must instantiate CleaningCompletionAcceptanceRuntime and call RunAsync
sequentially for the enum cases; only the two concurrency cases run two independent command contexts.
The runtime host/central authority adapter is an owner-supplied integration dependency, NOT implemented
as a permissive local file or a mock success callback. A later approved host can call this API without
rewriting the FE04 business command or case callbacks.

Runtime inputs:
1. Externally issued lease ID/owner/expiry and exclusive lease held for this target/run.
2. Exact data source/database, completion database user and DISTINCT fixture database user.
3. Full source + Notification contract commit SHA; optional FE05 consumer SHA for UC034-only runs.
4. Required ValidateExternalLeaseAsync callback that verifies the authoritative lease is still held,
   exact permitted operations, accepted source/assembly/catalog/fixture hashes and contract decisions.
   A syntactically valid metadata envelope is never authority.
5. Two context factories, one per identity, adding ALL supplied interceptors to real UseSqlServer options.
   Configuration/environment are supplied from the owner-approved isolated runtime; harness sets no flag.
6. Approved branch code and private evidence directory. No credentials in repo/arguments/receipts.

Context guards validate target before opening a connection, then actual USER_NAME().
Authority is rechecked before context creation and before each EF provider commit.
Run budget is 90 seconds; at least two lease minutes must remain; no lease renewal is implemented.
Fixture helper batches use their existing internal T-SQL commits (outside EF commit interception):
owner approval must explicitly cover those append-only fixture batches and hold the external exclusive
lease for the complete bounded run. This harness does not itself acquire a SQL/central lease or prove
lease enforcement inside a raw T-SQL batch. No expiry time is edited.

## 2. Target and substrate requirements — NOT_GRANTED

Current app gates accept only genuine FS015/FS016 isolated Development installations:
- HotelOps_Canonical_FS015_Cleaning_V1_Fresh, or
- HotelOps_Canonical_FS016_Rework_V1_Fresh,
with existing owner/Resume/Closure/source markers and actual installed objects.
These are gate constraints, NOT allocation or reuse instructions.
Fresh R05's database name is rejected. Do not rename a DB, spoof markers, use ENVNOTIF001,
reuse an expired lease/trial, overwrite a named database or relax a source gate.

ENV + Project Leader must select/provision a genuinely fresh isolated instance/host and
gate-compatible target with approved canonical substrate, OR approve a separate future gate/composition
strategy. PREP-002 does neither. Existing object identity/marker/name checks must pass naturally.
Any installer whose own interlock conflicts with the proposed target needs its owner's approved
deployment strategy; do not patch/strip installer guards in this handoff.

Required substrate: database-first schema + approved RBAC catalog + Housekeeping/Checklist/Progress,
Work-Time, FS013 cycle/execution/notification ledger, FS014 Resume, FS015 Closure
(and FS016 if selected), plus immutable R04 attribution table/procedure/trigger/6 trusted FKs.
Validate original 18-file R05 substrate manifest and every additional installed module/catalog hash
against owner-approved deployment receipts. No DDL/grants/provisioning is performed here.

Command identity: authenticated least-privileged principal with exact read, update/insert and procedure
EXECUTE rights required by this source. In particular completion writes task/log/evidence/readiness/audit
and current provisional notification operation/publication/intent tables; capture requires EXECUTE on
fe04_capture_cleaner_attribution. Attribution direct INSERT/UPDATE/DELETE stays denied.
Do not copy UC029's native-assignment permission profile: direct graph DENYs in that profile conflict
with UC034's current direct staging path. Notification/Security owners must approve a compatible
UC034-only profile or an accepted participant/wiring before shared acceptance.
No sysadmin/db_owner, shared DENY removal, fake login or metadata-wide grant is implied.

Fixture identity: separately approved scoped INSERT/UPDATE rights for synthetic users, roles/access rows,
shift/checklist/room/type/task/progress/workday/session setup and case-specific changes. It is not used
for CompleteCleaningAsync, proof snapshots or final command verification. No shared role/permission
catalog changes, historical repair, DROP/reset/reseed or automatic fixture cleanup.

## 3. Exact positive fixture / session prerequisites

Append a new fixture for EVERY case; never borrow a historical task:
- Active existing branch + active same-branch HK department; existing active HK_STAFF/HK_SUPERVISOR
  roles with approved CLEANING_EXECUTE OWN and supervisor CLEANING_VIEW BRANCH/GLOBAL paths.
- Fresh active/nondeleted employee, account and employment ACTIVE; explicit nonrevoked branch access,
  current HK_STAFF role; eligible active shift template / PLANNED shift spanning now / ASSIGNED staff.
- Fresh active floor/room (DIRTY), own room type/checklist and nonempty required cleaning item snapshot.
- Task Status IN_PROGRESS; workflow IN_PROGRESS; ActualStartAt set; ActualEndAt null.
- Sole ACTIVE assignment to actor, EndedAt null, AssignedAt not future; exact current-cycle pointer.
- Required checklist items Done; matching strong task/progress rowversion ETags.
- No unresolved, unreconciled or linked blocker; no effective RoomBlock.
- One WORKING workday and exactly one open CLEANING interval, same actor/branch/task, valid rowversion.
- All original assignment/progress contributors are the same actor for the single-cleaner attribution case.
- Fresh owned supervisor, active role/branch grant and eligible persisted recipient query result.

Existing CreateFixtureAsync builds a synthetic ON_HOLD fixture with a historical closed interval.
The harness creates a memory-only random credential for ONLY that new actor, uses production
PasswordCredentialService + CredentialLockoutService + AuthenticationLoginService + SqlSessionService,
and requires successful committed nonrestricted CreatedSqlSession. It then calls real StartCleaningAsync
to resolve the explicit blocker/open a valid interval, completes checklist and captures both ETags.
No ISqlSessionService success double and no synthetic FAILED Inspection/source authority are used.

Production CookieTicket.Create and CurrentUserAccessor are used with the real SQL session.
This is trusted direct-service composition, not encrypted-cookie/middleware/HTTPS acceptance.
Never serialize/log SessionReference, claims, raw credential, password or connection string.

## 4. Case callbacks and SQL oracles — all SQL cases NOT_RUN

21 cases: Authorized, ValidEvidence, Replay, ConflictingReplay, SameKeyConcurrent,
DifferentKeyConcurrent, MissingAssignment, WrongAssignee, StaleTask, StaleProgress,
ChecklistIncomplete, ActiveBlocker, NoInterval, InvalidEvidence, AttributionFailure,
CompletionAuditFailure, ReadinessFailure, NotificationPublicationFailure,
NotificationIntentFailure, BeforeCommitFailure, LostCommitAcknowledgement.

- PrepareCaseAsync changes ONLY new fixture rows. Snapshot is taken AFTER setup and BEFORE command.
  Missing/wrong owner expects Forbidden under the pinned HK_STAFF OWN permission catalog.
- All command scenarios use CompleteCleaningAsync, including every fault and concurrency case.
- Attribution fault targets EXEC fe04_capture_cleaner_attribution.
- Completion audit fault requires INSERT audit_logs AND a COMPLETE_CLEANING payload parameter,
  excluding the earlier Work-Time audit. Readiness/publication/intent each have separate selectors.
- Fault tests require the callback Fired flag; an earlier unrelated failure cannot count as PASS.
- BeforeCommitFailure rejects EF precommit; LostCommitAcknowledgement faults only AFTER the first
  provider commit, then requires durable recovery/replay without a second business write.
- Valid PNG evidence is included in the positive evidence/lost-ack cases and post-evidence write faults.
  Precommit-fault case omits evidence because production deliberately preserves files after
  commitAttempted for ambiguous outcomes; do not assert an unapproved deletion policy.
- Snapshot compares task/assignment/checklist/progress/version/cycle/blocker/interval/attribution/
  readiness/audit/operation/publication/intent/attachment/evidence/room/Inspection state.
  Rejected/faulted commands must preserve snapshot and leave no new owned evidence file.
- Success requires PENDING_INSPECTION, actual end/note, closed interval, one COMPLETED log,
  immutable attribution matching assignment/progress/time/completion revision, audit/readiness,
  persisted mandatory publication/intents and VerifyClosureAsync proof.
- Exact replay preserves full snapshot; changed payload is Duplicate; same-key concurrent calls
  return identical receipts; distinct-key concurrent calls yield one success and one stale result.
- No Inspection is created, no READY is emitted and the synthetic room's persisted DIRTY status stays.
  These are executable assertions awaiting SQL, not current PASS claims.

Run sequentially and store case ID, exact source/assembly hashes, target/principal identity, lease/
fixture provenance, actual result/SQL counts, fault Fired and snapshot/proof hashes in controlled receipts.
Session/credentials must be redacted; attach failed attempts too. No mock observer or historical R05
component receipt may be relabeled full-command acceptance.

## 5. Exact dependencies to pin

| Dependency | Observed source identity | Acceptance boundary |
| --- | --- | --- |
| FE04 command/replay source | 397ff6c2c08bb946756c196e2f4382a9e3f3f524; accepted source checkpoint db12691295ffe0382b005c686ac7478326b0c0d6 | Application source unchanged by PREP-002 |
| Original-cleaner producer/reader/SQL | 95f7422e284bc81bcfcf9f460dace52022de5cc7; ancestor d37e99f42eaaf3089eb510c6298296a0f0743faa | Candidate contract preserved; runtime installation needs separate approval |
| Existing UC034 provisional closure publisher | R05 95f7422e284bc81bcfcf9f460dace52022de5cc7 | CLEANING_TASK_COMPLETED v1 and caller transaction StageClosureAsync / VerifyClosureAsync / recipient source rules; NOT shared accepted |
| Notification PR #40 candidate | 54d75a634005afb8d42704f761a820ed16b1b9d4, read-only local branch inspection | Its approval bundle says CONTRACT_ACCEPTED=NO / SQL_ACCEPTANCE_READY=NO; assignment-focused and expired Writer/lease. Not a UC034 approval or replacement participant |
| Accepted UC034 Notification participant/profile | NOT_PINNED | Notification/Security owner must supply exact compatible revision/contract/permission and acceptance receipt, preserving one caller transaction/ledger/guard |
| FE05 actual consumer | NOT_PINNED | FE05 owner supplies exact caller revision + authorization/transaction/null-handling receipt. No need to rewrite R05 reader |

Do not merge PR #40 or other dependency histories into this PR. Do not assume source publication
equals acceptance. If the approved shared participant is incompatible with the current concrete
provisional publisher, an explicitly scoped FE04 adapter/composition task is required; this harness
does not fake compatible success.

FE05 backend signature unchanged:
Task<CleanerAttribution?> ReadInspectionCleanerAttributionAsync(
    DbConnection connection, DbTransaction transaction, long inspection, long branch, CancellationToken ct);
Caller authorizes actor/Inspection/branch first, uses SAME scoped physical Serializable transaction,
owns commit/rollback, treats null as unavailable/ambiguous/stale/ineligible. No latest-assignment fallback,
primary election, completion-revision substitution or implicit UC035/036 authority.
FE05 consumer SHA is optional for isolated UC034-only cases; it remains mandatory for separately
claiming FE05 consumer integration acceptance. Do not block independent UC034 tests on all FE05 work.

## 6. HTTPS handoff — NOT_RUN

Requires separately approved matching API binary/source, owned HTTPS port/process, trusted TLS
(no bypass), actual runtime principal and app-local secrets, fixture login, cookie/session and fresh CSRF.
Never reuse historical port 7029 or its process/identity/lease by inference.
Exercise GET .../execution then POST .../complete with strong If-Match, original ProgressETag,
GUID Idempotency-Key, note and optional evidence. Assert response ETag/receipt, 400/401/403/409/412/428/503
as applicable AND persisted SQL effects; positive/replay/concurrency must match direct-service oracles.
Cookie/CSRF tests and no-SQL boundary tests do not substitute for real HTTPS execution.

## 7. Immediate owner handoff

Project Leader + ENV: issue/approve exact fresh target, principals, external exclusive lease validator
and provenance, provisioning/catalog/fixture receipts, private storage and HTTPS runtime plan.
Notification owner + Security: pin an accepted UC034 event/participant/profile (do not reuse expired
UC029 Writer/lease or remove DENYs). FE05 owner: pin consumer only for actual consumer integration.
FE04: compose the approved host with the existing harness, run sequential SQL cases, then actual HTTPS,
publish new evidence and failures. Until then SQL/HTTPS remain NOT_RUN and UC034 is NOT ACCEPTED.