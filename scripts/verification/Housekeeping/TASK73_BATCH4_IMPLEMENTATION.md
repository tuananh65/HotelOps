# TASK73 — Accelerated Batch 4: Housekeeping and Room Operations

31/10/2026, DEV1, branch `feature/BE1-database-integration`.
**Final: UC042/043 COMPLETE_VERIFIED_V1; UC029/041/046 PARTIAL.** Target five complete
not reached because required notification delivery/retry has no approved/integrated DEV2
adapter. Independent work was implemented and verified; no enqueue/audit counted as delivery.

## A. Selected UCs, weights and acceptance requirements

| UC | Exact SRS title | Weight | Required scope/dependencies | Classification |
|---|---|---:|---|---|
| UC029 | Assign Cleaning Task | 3 | Same-branch active HK staff/department/shift, workload, due, assignment/state, concurrency/audit and notification retry | PARTIAL |
| UC041 | Assign Room Inspection | 3 | Pending inspection/reinspection cycle, eligible inspector/SoD, effective applicable Published checklist, one open inspection, due/audit/notification | PARTIAL |
| UC042 | Perform Room Inspection | 3 | Assigned inspector Start, Save Draft, PASS/FAIL/photos, mandatory/defect checks, distinct Pending Conclusion, optimistic concurrency and audit | COMPLETE_VERIFIED_V1 |
| UC043 | Record Inspection Defects | 3 | Assigned inspector; Inspecting/Pending Conclusion; failed criterion, description/severity/location/photos, immutable concluded state and audit | COMPLETE_VERIFIED_V1 |
| UC046 | Request Cleaning Rework | 5 | Failed source, selected defects, eligible on-shift assignee, due, one open rework, Pending Acceptance, audit/notification | PARTIAL |

Read AGENTS, plan, SRS §4.2/5.3–5.5/5.8, Permission Matrix, GBR005/007/008/010/014/
016/046/056/058–061/064, Change Notes, Task70–72 reports, entities/DbContext and existing
Room/RoomType/Structure/checklist/history/harness code. No newer signed SRS/matrix found;
Reception/DH unresolved changes not implemented. No UC006 or UC016 selection and no reuse
of UC038/039/040/048 completion credit. UC016 remains PARTIAL.

Preflight713 existing dirty/untracked files copied and SHA256-hashed before edits;
HEAD/index/branch/four worktrees recorded in `obj/task73-session.local.json` and its snapshot.
Developer conditionally approved the concrete [contract](TASK73_CONTRACT_PROPOSAL.md),
prioritizing independent UC042/043 and explicitly retaining notification-dependent partials.

## B. Implementation and ASSUMED_V1

Single API project preserved. Controllers handle HTTP only; DTOs hide EF entities and
private storage. Reuse RoomTypeCreationService partial/transaction infrastructure; no new
DI or architecture projects. Async SQL/CancellationToken/AsNoTracking, SQL session/permission/
branch isolation/final gate, sorted retained actor/target User locks, SERIALIZABLE feature
reads and bounded existing catalog applock. No role-name authorization bypass or new grants.

Conditionally approved opt-in schema: nullable workflow_status and real rowversion on
cleaning task/inspection/rework; inspection/rework due_at; nullable defect room_location;
rework→selected defects, result→attachments and defect→attachments relational bridges.
Coarse legacy status stays compatible; no bulk legacy conversion. Existing COMPLETED
cleaning alone never means Pending Inspection. Generated entities untouched: unmapped
Database First SQL projections and trigger-compatible partial mappings only.

Eight SQL guards enforce owned inspection cycle/version, one open inspection, result
uniqueness/version/phase, defect phase/failed criterion, bridge source ownership, and immutable
assigned checklist items/version (including legacy generic templates). Result/defect writes
advance parent inspection rowversion. Raw8-byte rowversion ETag is a quoted16-hex value,
separate from older aggregate-hash tags; required If-Match rejects stale writes412.

UC042 implements Start→Inspecting, Save Draft without conclusion, and Finish→Pending Conclusion.
Mandatory criteria need PASS/FAIL; any failed result needs a described defect. Result editing
cannot silently mark a criterion PASS while recorded defects remain. Existing assigned version
is retained after retirement or RoomType default changes. UC043 requires failed criterion from
that exact active version, minor/major active severity, description/location, and optional
private evidence. Defects allowed in Inspecting/Pending Conclusion, refused after conclusion.
History/execution DTOs expose location and explicit result/defect evidence IDs; no FileUrl/key.

UC029/041/046 independent business writes persist assignment/due/selected defects and atomic
audits. Responses explicitly say `notification: NOT_INTEGRATED`. No notification/outbox rows
were fabricated; no DEV2 source modified. Their missing delivery/retry remains a mandatory
acceptance gap. See [local DEV2 handoff](TASK73_DEV2_NOTIFICATION_HANDOFF.md).

ASSUMED_V1 where SRS lacks storage/HTTP specifics:

- Existing CLEANING_ASSIGN for cleaning assignment; INSPECTION_PERFORM for inspection
  assignment/execution/defect/rework, as proposed and conditionally approved. Read selectors
  use CLEANING_ASSIGN or INSPECTION_VIEW. Every permission is SQL-backed with branch scope;
  HK role eligibility is an additional business condition, never an authorization shortcut.
- Canonical HK department code/role codes are resolved through SQL, no hard-coded IDs.
  PARTIAL assignment/rework eligibility uses current valid scheduled shift window with
  existing SQL PLANNED/ASSIGNED records; this is not workday/attendance verification.
  Planning exposes raw shift statuses and both all-open and UTC-window workload, not a
  fabricated on-shift boolean. Default window is UTC day; branch-calendar workload refinement
  belongs to completion review of the PARTIAL assignment workflow, not verified UC credit.
- Prospective assignees/inspectors active, effective branch role/membership; eligibility
  and future due time rechecked at commit. Inspector planning conservatively excludes
  recorded cycle assignments/progress/rework involvement because work-interval proof is absent.
- SRS states live in nullable workflow_status, with compatible coarse mirrors documented
  in the [migration README](../../database/release/housekeeping/README.md). Legacy unassigned
  PENDING can opt in only through explicit `adoptUnassignedLegacy=true` and no active
  assignment; no automatic inspection/rework adoption. Missing authoritative workflow fails
  closed. Fixture completion/rejection/reopening is not a claimed UC034/044/045/047 endpoint.
- Criteria outcomes exact PASS/FAIL; required reasons, UTC future due, location≤250,
  note/description bounds. Upload: JSON Base64, ≤5 files per operation, ≤10MiB each,
  JPG/PNG/PDF magic/MIME/extension match. Request bound72MiB accommodates Base64 encoding.
- Private operator-controlled Evidence:PrivateRoot (default ContentRoot/App_Data/evidence),
  server-generated keys, no client path/public FileUrl, source-authorized Task72 download.
  On failed/canceled SQL transaction remove only files created by that invocation.

## C. HTTP endpoints

Authenticated HTTPS/no-store. Writes use existing CSRF middleware; anonymous write401 is
verified with valid anonymous CSRF bootstrap, while missing CSRF independently returns400.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/branches/{branch}/cleaning-tasks/{id}/assignment-options` | Scoped staff/workload/raw shift planning + rowversion when installed |
| GET | `/api/branches/{branch}/cleaning-tasks/{id}/inspection-options` | Inspector/SoD planning and published default checklist |
| POST | `/api/branches/{branch}/cleaning-tasks/{id}/assign` | Assign eligible housekeeper, If-Match; notification NOT_INTEGRATED |
| POST | `/api/branches/{branch}/cleaning-tasks/{id}/inspections` | Create assigned inspection/version/due,201; notification NOT_INTEGRATED |
| GET | `/api/branches/{branch}/inspections/{id}/execution` | Assigned inspector's execution DTO/rowversion |
| POST | `/api/branches/{branch}/inspections/{id}/start` | Assigned→Inspecting, If-Match |
| PUT | `/api/branches/{branch}/inspections/{id}/results` | Save Draft results/private evidence, If-Match |
| POST | `/api/branches/{branch}/inspections/{id}/finish` | Validate and enter Pending Conclusion, If-Match |
| POST | `/api/branches/{branch}/inspections/{id}/defects` | Create failed-criterion defect/location/evidence, If-Match |
| POST | `/api/branches/{branch}/inspections/{id}/reworks` | Create selected-defect rework,201/If-Match; notification NOT_INTEGRATED |

Reuse `/api/inspection-history/{id}/evidence/{attachmentId}?branchId=...` for private
download (INSPECTION_VIEW/source authorization). Errors400 invalid/reference/CSRF,401
session,403 permission/owner/scope,404 missing/hidden,409 invalid phase/duplicate,412 stale,
428 missing If-Match,503 unavailable schema/gate/contention. No Room lifecycle endpoints added.

## D. Per-UC acceptance

| Check | UC042 | UC043 | UC029/041/046 independent partials |
|---|---|---|---|
| Success | Start, Save Draft, failed-with-defect and all-Pass Finish; no auto-approval | Defect in Inspecting and Pending Conclusion; exact location/photo links | Real cleaning assignment, inspection assignment/version/due and selected-defect rework persisted |
| Authentication/authorization | Anonymous401, denied403, SQL-authorized other supervisor denied own execution | Anonymous/denied/assigned-owner checks | Denied/foreign inspector/cleaner SoD and explicit notification outcome |
| Branch/missing | Foreign branch403, missing inspection404 | Foreign403; unknown/foreign criterion rejected400 | Foreign branch403; source/target eligibility |
| Invalid/business rules | Invalid outcome/item/file, incomplete mandatory and Fail-without-defect rejected; result edit after finish409 | Empty description/location, invalid severity/item, >5/oversize/MIME/extension/path invalid; passed criterion400; concluded409 | Wrong staff role/SoD, duplicate inspection/rework and invalid defect selection |
| Persistence/audit | Exact version/result/evidence and distinct state, server audit | Description/location/severity/relational file link and root revision | Atomic business/audit; delivery NOT_INTEGRATED |
| Rollback | Audit failures on Start/Save/Finish; file/metadata cleanup | Defect/location/bridge/file/audit rollback | Rework audit fault rolls back row and links |
| Concurrency | Stale Start412; real HTTP cancellation/SQL lock release on Save | Concurrent same-rowversion defect200/412, one row; real canceled HTTP | Existing shared sorted User/feature lock boundaries; notification acceptance NOT EXECUTED |

No full completion asserted for partials: notification retry/delivery and remaining planning/
form refinements need integration review. Completed UCs do not depend on claiming the upstream
notification delivered; they execute legitimate assigned-inspection inputs independently.

## E. SQL/HTTPS evidence

- [task73-accepted.local.json](task73-accepted.local.json): byte-identical preserved copy
  of successful `task73-attempt3.local.json`, **600 PASS** on
  `HotelOps_Canonical_20261031_160120_8acb3323_Fresh`.
  Actual production Program.cs/Kestrel, pinned ephemeral HTTPS, real credentials/cookie/
  SQL sessions/SQL permissions. No synthetic production auth or test-only routes.
- Breakdown600 =282 existing checklist regression +93 planning +225 workflow assertions.
  Receipt's historical `planningChecks=318` field includes both new planning/workflow;
  `workflowChecks=225`, so pure planning=93. Shared bootstrap/security assertions are not
  counted as independent full business scenarios per UC. No dotnet test credit.
- [task73-history-regression.local.json](task73-history-regression.local.json):208 PASS
  UC038/048 after Housekeeping extension. [task73-room-regression.local.json](task73-room-regression.local.json):
  229 PASS UC023 on the extension. Final RoomType/checklist consumers are additionally
  exercised by checklist282 and workflow public UC018 default change/legacy-version use.
- Canonical final full module run in `obj/task73-final-run.local.txt`, log
  `obj/task73-final-canonical.local.log`: Fresh/Upgrade/AuthUpgrade CHECKDB, constraints,
  seed replay and schema comparison PASS. New modules opt-in through `-Housekeeping`.
- Strict final column type/nullability/rowversion/index/FK/trigger verification all three
  targets: `obj/task73-shape-final-{Fresh,Upgrade,AuthUpgrade}.local.json`, all success.
- Successful receipt `housekeepingDeployments`: real post-column DDL failure, recovery,
  replay, populated-legacy preservation, empty-extension rollback and populated refusal.
  Raw legacy state/content fingerprint unchanged; no bulk ownership/state conversion.
  A new trial is used after fixture/validation failure, not destructive reset.

## F. Concurrency, transaction and filesystem rollback

SERIALIZABLE source/role/membership/branch reads, sorted retained User locks, common catalog
serialization and final session/grant/eligibility checks. Rowversion changes on aggregate
updates and SQL result/defect changes. Concurrent same-version defects have one200/one412
and one persisted row. Actual DMV waits observed for target row contention; canceled HTTPS
Save/Defect transactions release SQL locks before blocker rollback and preserve root revision.

Injected post-write audit failure proves SQL root/results/defect/location/links/attachment
metadata/audit rollback; owned private files removed. File storage is not a distributed
transaction: a process crash before SQL commit can leave orphan private bytes without an
authorized metadata source. No public serving, no access through source downloader, and no
startup/bulk cleanup. Offline recovery must review and remove only confirmed orphans.

New assignments freeze exact criterion/version even for legacy template; SQL immutability
rejection verified. Retired version already assigned remains executable. New selections
still require active/effective Published/applicable checklist. Room operational status and
DEV2 block/lifecycle writers were not modified or implicitly certified.

## G. Shared regression, build and warnings

Checklist UC039/040 + UC017/018 consumer paths282; UC038/048 history208; UC023 room229.
These are targeted suites; no unrelated full structure/account regression repeated.
PermissionService43, Authorization61, ResourceAuthorization49, AuthorizationHardening21
PASS (**174**, explicitly documented test doubles separate from live SQL/HTTPS evidence).

`dotnet build HotelOps.slnx` **PASS**; verification harness build **PASS**.
NU1900 remains: unreachable NuGet vulnerability feed, no successful vulnerability scan claimed.
`dotnet test HotelOps.slnx --no-restore` discovers no test-framework projects (solution only
API): framework tests **NOT RUN/unavailable**, not counted. Console suites above executed.
`git diff --check`, untracked text whitespace and preservation checks **PASS** in final validator.

Failures retained, not counted: first fixture duplicated ACTIVE assignment despite existing
UX_cleaning_assignments_active (fixture fixed, index preserved); concurrent checklist publish
regression failed with Draft retained. Diagnostics were added without relaxing200/412;
publication now uses SQL timestamp, the same authority as its effective-time SQL guard.
Final race reports actual200/412. Clock-source discrepancy was a suspected cause, not a
captured original SQL error. Housekeeping uses its own raw-rowversion ETag validator rather
than the older66-character hash-tag validator. No failed/intermediate run counted PASS.
Canonical CHECKDB waited RESOURCE_SEMAPHORE on SQL Express; retained integrity checks,
no server settings/session killing/old-database cleanup. All final integrity runs completed.

## H. Blockers and replacement review

Mandatory DEV2 notification delivery/retry contract is absent/unapproved/unintegrated for
UC029/041/046 (GBR010/UC124). No fake Notification/Recipient or outbox persisted. Independent
business writes are available with explicit NOT_INTEGRATED result, but these UCs earn zero
points. Local [handoff](TASK73_DEV2_NOTIFICATION_HANDOFF.md) specifies required integration
and real delivery/failure/restart/retry/idempotency/source-authorization acceptance.

No safe full replacement found without another unresolved contract: UC028/037/044/045
need GBR044/DEV2 room-block/status coordination (UC037 also cancellation notification);
UC031/033–036 need workday/work intervals; UC032 needs distinct per-cleaning-item storage,
not silently repurposed inspection criteria. Task/internal-request/shift candidates retain
their documented status/recurrence/acknowledgment/time/notification blockers. UC016's DEV2
blockers have no new resolution and UC006 remains excluded. No acceptance weakened for five.

## I. Classification and file scope

UC042/043: **COMPLETE_VERIFIED_V1** for complete described backend execution/defect flows.
UC029/041/046: **PARTIAL**, business/selector subset verified; notification and complete
integration acceptance outstanding. No completed UC038/039/040/048 counted again.

Created: HousekeepingPlanningController, HousekeepingWorkflowController; Housekeeping DTOs;
HousekeepingPlanningService, HousekeepingAssignmentsService, InspectionExecutionService;
HousekeepingSchema; module SQL/runner/README; Task73PlanningCases/WorkflowCases; contract,
handoff and report. Modified: controlled Checklist partial mapping (trigger OUTPUT), history
DTO/service (location/photo links), ChecklistService SQL publication timestamp, optional
canonical integration, harness Program, Task72 race diagnostics and delivery plan. Exact
paths/hashes in `obj/task73-validation.local.json`.

Generated entities/auth core/DEV2 source unchanged. No shared/production SQL/HotelOpsDB,
new RBAC grants, UC006 gate change, branch/worktree change, commit/push/merge/rebase/reset/clean.
HEAD/index/four worktrees and all unrelated pre-existing713-file hashes preserved.

## J. Updated progress

Task72 **22/75,50/256=19.53%** → Task73 **24/75 (32% UC),56/256=21.88% weighted**.
Gain **+2 UC/+6 points**, exact weighted delta2.34375 percentage points (~2.34pp), computed
before rounding displayed percentages. Remaining **51 UC/200 points**.
Taxonomy24 COMPLETE_VERIFIED(including V1),8 PARTIAL,24 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75.
UC016 stays PARTIAL. Only COMPLETE_VERIFIED(_V1) counted; denominator75/256 unchanged.
Delivery plan §21 updated. Five-complete objective not achieved; three partials not credited.

## K. Actual complete-batch duration

Recorded preflight start **2026-10-31T08:20:31.6600850Z** (15:20:31 Asia/Saigon).
Exact final end and elapsed seconds in `obj/task73-validation.local.json`. Includes approval
interval and SQL/build/API waits; not a claim of uninterrupted coding. Initial reading
before recorded preflight is excluded. No invented start/end or effort estimate.

## L. Recommended Batch 5

Obtain DEV2-approved delivery/retry adapter contract, integrate and verify end-to-end before
upgrading UC029/041/046 individually; finish their selector/calendar/notification UX contracts.
Then resolve room lifecycle/block protocol before approval/rejection/cleaning status flows,
and workday/interval/cleaning-item models before time-bound cleaning UCs. Keep UC006 release
gate and UC016 classification unchanged until their own mandatory acceptance is satisfied.
