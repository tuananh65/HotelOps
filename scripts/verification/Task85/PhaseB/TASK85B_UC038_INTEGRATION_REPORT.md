# Task85-B — UC038 View Room Turnover History

**RESTORED & REVERIFIED — production source/approved DI, actual startup and146 HTTPS/SQL checks PASS on the approved disposable trial.**
UC038 earned2 historically, not a new UC or points.26/75,60/256=23.4375% unchanged.

## Source restored safely

Five new DEV1 files under `src/HotelOps.Api/`:

- Controllers/TurnoverHistoryController.cs — only3 GET routes: `/api/rooms/{roomId}/turnover-history`,
  `/{id}`, `/{id}/evidence/{attachmentId}`. HTTPS/Cookie/no-store; error mapping/file response only.
- Services/HotelStructure/TurnoverHistoryService.cs — list/detail/date/result/count/page,
  stable newest+CleaningTaskId ordering; explicit relational cycles, assignment/progress/inspection/
  versioned criteria/defects/rework/cancellation audit/evidence projections.
- Services/HotelStructure/TurnoverHistoryBoundary.cs — scoped current actor + SQL session/eligibility,
  retained Serializable transaction/User-first locks, existing catalog applock and final permission gate.
- Services/HotelStructure/TurnoverHistoryFiles.cs — private source-authorized attachment resolution,
  traversal/reparse-point/size/MIME guards, asynchronous read; never return storage/provider URL.
- Services/HotelStructure/TurnoverHistorySchema.cs — read-only legacy/V1 compatibility probes and
  optional metadata projection; no DDL, generated entity or global EF mapping change.

Historical source `cf8fb020362f8f64192e7f2f14a10547cebe5b91`:
OperationalHistoryService.cs / OperationalHistoryFiles.cs / OperationalHistoryController.cs,
RoomTypeBatchService.cs boundary and read-only ChecklistSchema/HousekeepingSchema probes.
Selective extraction avoids restoring giant RoomTypeCreationService writer dependency closure.
Existing OperationalHistoryDtos.cs reused byte-for-byte. UC048 endpoints and business writers excluded.
Nested evidence links rewritten to the3 restored turnover routes, not missing inspection-history routes.
Private criterion group uses EF unmapped SQL read projection; no ChecklistCriteria DbSet was added.

## Contract and authorization

SRS historical draft §4.11 (split specifications:3324 onward), Change Notes, Task83 historical
source/accepted208-check receipt (shared UC038/048, not208 new UC038 tests), Task84 integration/
validation and PhaseA handoff reviewed. No pending role or room-state transition is invented.
Actual catalog permissions remain CLEANING_VIEW **and** INSPECTION_VIEW for returned room branch.
Actor/ticket/session must match, active account/employment and current SQL-backed scope required.
ALL/global and BRANCH evaluations use actual room branch; OWN never promoted to branch history.
No new permission, role grant, resolver semantics or Authentication Foundation change.

CycleId is persisted CleaningTaskId, inspection/rework linked by exact CleaningTaskId/RoomId.
No MAX(Id)/timestamp used to infer cycle identity. Latest result is selected only among inspections
already linked to that exact room/cycle with stable time/ID tie-breaker. No final completer inferred.
Legacy missing links excluded; absent cancellation audit returns null rather than an invented reason.
Missing legacy criterion group/location/link metadata is not fabricated; optional V1 fields read only
after matching schema probes. Historical versions and inactive/unanswered criteria retained.

## Shared approval

User explicitly approved only Program.di.patch: one AddScoped<TurnoverHistoryService>() after
RoomTypeDirectoryService. Applied and verified removing that line reproduces pre-task Program text;
DI_APPROVAL_APPLIED.json records before/after hashes. No other existing file edited.
Controller moved from reviewed proposal only after approval; Program registrations/middleware otherwise preserved.
Proposed DI checks26 PASS before applying; integrated-source checks26 PASS after applying.
Same scoped A1 interface/concrete participant and context retained, no circular dependency observed.

## Verification currently executed

- Production build before and after DI: exit0,0 errors,2 NU1900 warnings each. `build.log`,
  `build-integrated.log`; outputs isolated under PhaseB/artifacts, no pre-existing binaries replaced.
- `di-integrated-receipt.json`:26 DI/invalid-input/EF translation/controller boundary checks PASS;
  synthetic host services and direct controller invocation, **not live HTTPS or SQL acceptance**.
- Security:199 checks (93/43/14/49), isolated collaborators; current-source post-DI reruns logged.
- UC037 Task82 boundary regression14; UC142 Task84 DI/static/boundary regression19.
  These are component checks, supplemented by actual UC037 pagination and UC142 list/detail HTTP/SQL regression in the final146-check run.
- Framework `dotnet test` exits0 with no cases: **NOT AVAILABLE**, not tests PASS.
- HTTPS harness builds0 errors/4 NU1900; source at HttpsProgram.cs and fixture/acceptance fragments.
  Two initial DI harness attempts failed (unused InvalidReference enum case; missing harness hosting
  environment registration). Corrected proposal/harness only, original failure logs retained.
- **146 final real HTTPS/SQL checks PASS**, exit0: `https-sql-accepted-receipt.json`,
  `https-sql-accepted.log`, `Task85B.HttpsResume3Verification.csproj`. This includes actual
  Program startup/schema gate, credentials/CSRF/cookies/SQL sessions; list/detail/filter/page
  identities; empty room/result; exact room/cycle and corrupt-link exclusion; stored checklist
  version/inactive+unanswered criteria/defects/rework/recorded cancellation; private PDF bytes,
  deleted/unrelated/traversal denies; branch/OWN/restricted/no-permission; role/session revocation,
  inactive account/employment; UC037/142 regression.146 are assertions, not146 independent UCs.
- Additional `field-oracle-receipt.json`: **26 real authenticated HTTPS/SQL checks PASS**,
  comparing cleaning type, exact SQL timestamps/assignee IDs, checklist version number/identity,
  inspection results, rework reason/assignee/completion time and attachment metadata to stored
  values; also empty page and invalid route/filter room/cycle checks. Separate from146, no new UC.
- DMV confirms real HTTPS detail blocked on uncommitted progress writer; after rollback the
  original note is returned. Exact business/audit fingerprints remain unchanged after reads,
  inactive-room restore and rollback probes; catalog/master-data fingerprints unchanged.
- Current catalog has **zero ALL grants for CLEANING_VIEW/INSPECTION_VIEW**. Positive operational
  ALL acceptance is NOT_APPLICABLE, not a guessed grant. Actual SYS_ADMIN403 proves no operational
  bypass. Existing PermissionService ALL semantics covered only by isolated regression.
- Legacy Checklist/Housekeeping optional V1 extensions (criterion group/location/link-table
  attachment fields) are absent in this trial: that optional schema branch is NOT_RUN. The
  required UC038 fields are verified through actual legacy relationships; no synthetic metadata.

## SQL approval, execution and retained trial

No existing trial may be reset/reused for new fixtures on the basis of Task82/84 permissions.
User approved the separate request for exactly `HotelOps_Canonical_20261104_133500_c03885b1_Fresh` on
`REDACTED_LOCAL_SQL_HOST\SQLEXPRESS`. Prepare-Task85BTrial.ps1.proposal verifies prerequisite hashes,
existing Prepare-Database verifies instance/nonexistence; rowversion/UC017 V1 prerequisites check
the exact new target/Task17 marker. SQL_SCOPE_REVIEW.proposal.json records source hashes unchanged
from12 reviewed Task84 scripts. Fresh canonical RBAC seed retained; no role_permissions edits.
Fixture/test writes limited to new Task85B identities and turnover fixtures/new SQL sessions.
Initial sandbox connection failed SSPI before opening SQL; authorized execution outside the
sandbox succeeded. Instance/nonexistence checks passed before creation; no previous trial accessed.
RETAINED_TRIAL.json verifies instance/database/Task17 marker,12roles/62permissions/140grants,
25 fixture users/19 role assignments (including one final field-oracle identity),3rooms/3cycles.
Trial retained, not reset or dropped.

Three failed live harness executions are retained, **not reported PASS**:

1. `https-sql.log`:38 checks before one wrong SYS_ADMIN200 expectation; API403 was correct.
2. `https-sql-resume.log`:29 checks before branch user403 because added fixture roles were cleared
   before SaveChanges; corrected harness persistence, no production/RBAC fix.
3. `https-sql-final.log`:119 checks before wrong timelinePage-object assertion; actual Task82 DTO
   has integer TimelinePage. Corrected assertion to page2/limit1/totals and array sizes.

Each continuation validated exact retained fixture counts and created separate authentication
identities; original business fixtures and failed identities remain. No database reset, deleted
fixture or permission-catalog modification. Do not add38+29+119+146 as independent final coverage.

## Preservation and next action

Preflight records branch/upstream/HEAD/index/full status/four worktrees.29,420 existing files
captured with SHA256+mtime before build. First postflight ran during bounded DI approval and
must not be treated alone as final Program preservation proof. Final postflight uses baseline
with **only approved Program hash/mtime substituted**; original baseline kept intact.
PhaseA, Task82 and Task84 sources/artifacts remain protected by baseline hashes.
Final file scan:29,420 expected existing files,0 changes/0 read errors after substituting only
the approved Program hash/mtime. Scan finished before final SQL continuations, which wrote
only new PhaseB artifacts and the approved trial; final focused hashes and Git status checked
again afterward. Final evidence/outcomes and elapsed time: TASK85B_VALIDATION.json.

Required current-catalog UC038 acceptance is complete on the retained trial. This is runtime
restoration, not deployment to a shared/production database or another2 earned points.
Do not begin Task86 automatically; review the recommendation in TASK85B_RUNTIME_PROGRESS.md.
