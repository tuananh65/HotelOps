# Task66 — UC016 Deactivate Floor/Area

30/10/2026, Asia/Saigon. **PARTIAL — mutation BLOCKED by production writer coordination.**
This delivery implements the production HTTPS rejection/readiness path and actual GBR035 SQL
queries. It does **not** implement or certify a successful Active→Inactive transition. No dormant
mutation switch, test bypass, schema migration or DEV2 source change is introduced.

## A. Requirements and contract

[UC016 §2.5](../../../docs/requirements/srs_sections/02_use_case_specifications.md) requires an
authenticated Administrator with applicable permission, an Active target, dependency validation,
confirmation and reason, conditional update, soft deactivation and atomic audit. History and IDs
remain intact; an inactive location must not be selectable for **new** records. Exceptions list
the dependencies and stop the operation. [GBR035](../../../docs/requirements/srs_sections/05_requirement_appendix.md)
explicitly includes active Rooms, active child Areas, active Assets and open Incidents/tasks.
GBR012 requires at least **10 trimmed characters** in the reason. GBR005 requires stale-write
rejection; GBR007/008 require state and audit to commit together; GBR009 prohibits physical deletion.

The Permission Matrix remains draft, per [change notes](../../../docs/requirements/SRS_CHANGE_NOTES.md).
Reuse the existing MASTER_DATA_MANAGE mapping of UC014/015, server-side CurrentUserAccessor,
protected cookie and persisted SQL session. Global/branch SQL scope is enforced, unsupported OWN
denied, no role-name exemption or new grants. ROOM_VIEW grants no write access.

| V1 decision | Implemented behavior/evidence |
|---|---|
| Confirmation | Required true; reason trimmed,10–1000,no control characters. Unknown JSON properties rejected;32KiB body limit; normal CSRF pipeline |
| Concurrency | Existing UC015 GET edit response supplies the same strong ETag. Required If-Match, missing428, malformed400, stale412; checked after retained target lock. No rowversion column invented |
| Scope | SQL actor→session/grants→Branch UPDLOCK/HOLDLOCK→Floor/Area UPDLOCK/HOLDLOCK→authoritative state/revision→dependencies→final auth check. Owned SERIALIZABLE transaction, no tracked entity state |
| Locations | Floor includes its Areas and Rooms; Area includes its Rooms. Current Asset physical placement includes Room/Area, never asset_location_history. Incidents include direct Room/Area and current Asset placement |
| Active Asset | IN_USE/UNDER_REPAIR block; RETIRED is historical. Unknown non-RETIRED lifecycle also blocks, never silently safe |
| Incident | OPEN/IN_PROGRESS/RESOLVED block. CLOSED/CANCELLED are terminal in IncidentService; RESOLVED can return to IN_PROGRESS |
| Maintenance | Linked work other than CLOSED/CANCELLED blocks, including COMPLETED awaiting verification/rework. Based on MaintenanceWorkService.Workflow.cs |
| OperationalTask | TODO/IN_PROGRESS/BLOCKED block. CANCELLED is historical. COMPLETED remains **TASK_COMPLETION_CONTRACT_UNRESOLVED**, separately identified; schema alone does not prove rework/verification has ended. This preserves the earlier approved provisional policy |
| Dependency response |409 includes kind, exact count and ascending first100 IDs per kind; count exposes truncation. Fixed number of projected queries, no entity serialization/N+1. No names, actor identity, credentials or operational descriptions exposed |
| Empty target |503 STRUCTURE_WRITER_COORDINATION_REQUIRED. This is a release dependency, not a business assignment predicate. No UPDATE, DEACTIVATE audit or successful response is possible |
| Cancellation/provider failure | CT propagated; SQL1205/1222/-2 maps to existing503 without automatic retry. Returning a rejection disposes/rolls back the read transaction and releases retained locks |

ASSUMED_V1: JSON confirmation field, maximum reason length, bounded dependency ID samples,
subtree/current-asset location interpretation and conservative COMPLETED Task classification.
These choices do not override explicit GBR035 or claim a final signed SRS/Permission Matrix.

## B. Production source

- [DeactivateStructureRequest.cs](../../../src/HotelOps.Api/DTOs/HotelStructure/DeactivateStructureRequest.cs): strict request and dedicated dependency response.
- [StructureDeactivationDependencies.cs](../../../src/HotelOps.Api/Services/HotelStructure/StructureDeactivationDependencies.cs): actual EF SQL dependency predicates.
- [HotelStructureUpdateService.cs](../../../src/HotelOps.Api/Services/HotelStructure/HotelStructureUpdateService.cs): DeactivateAsync, retained transaction/auth/revision checks, explicit fail-closed release dependency.
- [HotelStructureUpdateController.cs](../../../src/HotelOps.Api/Controllers/HotelStructureUpdateController.cs): production HTTPS routes and errors.
- [Task66StructureCases.cs](Task66StructureCases.cs), existing [Program.cs](Program.cs): real HTTPS, persisted fixtures and actual DEV2 production-service defect reproduction, selected with --task66.

No additional DI registration is necessary: the existing scoped UC015 service is reused. No change
to Program.cs in the API, DbContext/entities, schema/catalog, UC006 gates or DEV2 modules.

### Endpoints — explicitly incomplete mutation contract

`POST /api/branches/{branchId}/floors/{id}/deactivate`

`POST /api/branches/{branchId}/areas/{id}/deactivate`

Body: `{"reason":"Location no longer used","confirmation":true}`; supply strong If-Match from
existing management GET of that node, cookie and CSRF header. Existing GET returns editable DTO
and ETag; no new read route conflicts.401 session,403 scope/permission,404 missing/foreign target,
409 inactive/dependencies,412 stale,428 missing precondition,400 invalid input,503 coordination/provider.
**Neither POST can currently succeed.** They are safe diagnostic/rejection endpoints, not a usable
finished deactivation workflow. Framework-generated input400 responses contain static validation
details; controller responses use no-store, with no entity or provider-secret disclosure.

## C. Writer inventory and precise DEV2 handoff

| Writer / source | Observed behavior and risk | Required correction / exact acceptance |
|---|---|---|
| DEV1 HotelStructureCreationService.CreateAreaAsync/CreateAsync | User→Branch U→Floor retained; parent active checked after wait. Serializes with UC015/016 branch lock | Keep this order. After an eventual successful Floor deactivation, blocked creator must reread and reject; before it, newly committed active child must block UC016 |
| DEV1 HotelStructureUpdateService.UpdateAreaAsync/UpdateFloorAsync | User→Branch U→parent Floor→target; authoritative reread/ETag, rejects inactive parent/target | Preserve ordering and same revision anchor. Actual existing create/update regression runs with Task66 |
| DEV1 BranchStructureService / RoomDirectoryService | Read-only, no responsibility producer | Existing consistent-read/authorization regressions; do not label them writers |
| DEV1 Room/OperationalTask write workflows | No production Room create/update/activation or OperationalTask writer found in Services/Controllers search; mapped entities alone are not an implementation | Future writer acceptance must acquire compatible branch/location locks, reread active ancestry and use atomic audit. External jobs/imports are not certified by repository search |
| DEV2 **AssetService.AssignLocationAsync/TransferLocationAsync→MoveAsync**, Services/Assets/AssetService.Location.cs | Asset U→late actor read→ordinary active Room/Area AnyAsync→Asset FK UPDATE. Location eligibility is not retained. Can commit after target becomes inactive | Discover branch/current+destination IDs outside locks only as hints; lock User participants ascending, Branch U, Floor/Area/Room ancestry deterministically **before Asset**; lock/reread Asset and compare discovery/current linkage, validate destination and ancestry active under retained locks, write/history/audit/commit. Test both writer-first and location-deactivation-first, cancel,1222,1205, no partial writes |
| DEV2 **IncidentService.CreateIncidentAsync/ValidateCreateAsync**, Services/Incidents/IncidentService.cs | Validates Room/Area existence+branch before transaction, never IsActive; later Asset→number allocation→INSERT. Even sequential creation at inactive Area succeeds | Move authoritative location eligibility inside transaction under User→Branch/location→Asset→Incident allocation order. Require active location/ancestry for new records. Test inactive Room/Area rejection and both real concurrent schedules, stale linkage and rollback |
| DEV2 **MaterialRequestService.Save→ValidateContent**, Services/MaterialRequests/MaterialRequestService.Commands.cs and MaterialRequestService.cs | Users→Inventory catalog gate/request→ordinary receiving Room/Area active read. No retained common structure lock; potential late feature→location inversion if patched locally | Incorporate branch/location acquisition **before catalog/request locks**, reread discovered target IDs after locking, retain active checks to commit. Prove new receiving records cannot select inactive structure, both schedules/cancel/rollback. Not added as a GBR035 blocker without a business requirement |
| DEV2 MaintenanceWorkService.ReturnWorkAsync/ExecuteCorrectiveAsync; CreateWorkAsync in Services/Maintenance/MaintenanceWorkService.Create.cs | Completed corrective work and its Incident already block current predicates; ReturnWork restores IN_PROGRESS, verify closes. CreateWorkAsync locks Incident then reads actor; requires OPEN Incident, which blocks UC016. Existing User concurrency hotfix does not certify structure-lock coordination | Review CreateWorkAsync and every reopen/restore path against retained branch/location protocol before clearing UC016 integration. SQL proof required; Task55A User-lock evidence is not structure acceptance |
| External/import/job | Repository search cannot prove absence of privileged SQL/external producers | Deployment owner must inventory production producers; do not treat unknown writers as accepted |

Canonical proposed integration order: **Users ascending → Branch U → Floors ascending → Areas ascending
→ Rooms ascending → Asset → Incident → MaintenanceWork / other feature locks → authoritative reread
→ write/audit → commit**. Discover IDs first, then reject/retry stale discovery without taking earlier
locks late. This is a handoff proposal requiring coordination with existing DEV2 feature orders,
not a claim that global lock-order acceptance has passed. Holding reader locks in UC016 alone cannot
fix a writer whose eligibility read happened before those locks, nor a writer that accepts inactive
locations after UC016 has committed.

## D. Executable verification

All SQL connections below use disposable canonical trials with instance/name/Task17 identity checks,
the unchanged approved guarded trial users.row_version prerequisite and startup compatibility gate.
HotelOpsDB is never opened. Tests execute root production Program.cs HTTPS/cookie/login/SQL/CSRF,
with direct production DEV2 services only for the explicitly identified defect reproductions.

| Execution | Result and limits |
|---|---|
| [Final Task66 receipt](task66-accepted.local.json) | **94 UC016 checks +2 startup checks=96 PASS**. Active Room/child Area/Asset, RESOLVED Incident, unverified COMPLETED Maintenance and TODO Task positive cases; inactive Room/retired Asset/closed Incident+Maintenance/cancelled Task negatives. COMPLETED OperationalTask separately unresolved. Anonymous401, revoked grant403, expired session401, cross-branch403,404/invalid ID, inactive409, strict payload/confirmation/reason/If-Match, no disclosure |
| Target lock/concurrency | Two actual DMV-confirmed HTTPS waits: concurrent SQL target writer wins and request412 after authoritative reread; waiting cancellation propagates. Two simultaneous no-dependency attempts both503, without mutation. Production actor/branch/target locks reused, no fake authorization |
| DEV2 writer acceptance | **FAIL — defects confirmed by SQL**. Actual CreateIncidentAsync creates OPEN Incident at inactive Area; actual AssignLocationAsync crosses a pause before Asset UPDATE and commits an Area FK after fixture deactivation. Neither fixture UPDATE is a UC016 success. Source DEV2 unchanged |
| [UC014/013/015 regression recheck](task66-regression-recheck.local.json) | **589 PASS =215 UC014+159 UC013+215 UC015**,53 DMV waits and14 actual post-UPDATE UC015 fault/expiry schedules; original assertions/timeouts preserved |
| [UC023 regression](task66-room-regression.local.json) |117 PASS,11 DMV waits; internal production query only, UC023 remains PARTIAL |
| AuthN/AuthZ foundation |174 PASS:43 PermissionService,61 Authorization,49 ResourceAuthorization,21 Hardening. Existing doubles/loopback scope explicitly retained; live SQL auth evidence is in Task66/structure receipts |
| Build | Solution PASS,0 errors; NU1900 NuGet vulnerability-feed warnings remain (final --no-restore solution build:1 warning; targeted harness builds:2). No C# warning or suppression |
| dotnet test | No test project in solution; console SQL/HTTPS suites executed explicitly. Command exit recorded in obj/task66-test-exit.local.txt; do not treat an empty dotnet test run as unit-test coverage |
| Diff / preservation | git diff --check PASS. Original backups and current unrelated files compared by SHA256; proof in ignored obj/task66-validation.local.json |
| UC016 success / state audit atomicity / post-UPDATE audit failure,1222,-2,1205 | **BLOCKED/NOT RUN for UC016 mutation**. Actual soft-deactivation UPDATE and DEACTIVATE audit are intentionally absent; UC015 fault regression does not certify a UC016 transition |

Final Task66 database: `HotelOps_Canonical_20261030_160249_52647412_Fresh`. Regression recheck:
`HotelOps_Canonical_20261030_160012_5f1394be_Fresh`. All earlier trials/receipts/logs preserved.

Execution history is not hidden: obj/task66-acceptance.local.log first stopped on a harness cache
assertion for framework-generated validation400; static validation disclosure is checked separately
from controller no-store. obj/task66-targeted.local.log exposed missing independent RoomType fixture;
Task66 now creates its own RoomType/Department fixture in the trial. obj/task66-final.local.log failed
UC015 Area session-expiry with provider -2/Unavailable after UPDATE rather than SessionInvalid.
The sequential unchanged-timeout/assertion recheck above passed including that schedule. That
timeout is **not reproduced and root cause remains unproven**; it is a residual intermittent test/
environment concern, not silently reclassified as a passing original run. No production timeout was
increased or behavior changed to hide it. Final Task66 receipt succeeds independently of regression
using --task66-only; --task66 still runs the combined suites.

## E. Completion and next work

UC016 moves from NOT_IMPLEMENTED to **PARTIAL**, with mandatory mutation BLOCKED. Verified points
gained **0**; DEV1 remains **9/75,23/256=8.98%**. UC023 remains PARTIAL. Next: integrate the precise
DEV2 location-writer corrections, settle COMPLETED OperationalTask terminal contract, then implement
the actual conditional Active→Inactive UPDATE plus atomic DEACTIVATE audit and run both schedule
directions and post-UPDATE fault suites before awarding UC016's3 points. No success path may be
enabled solely because dependencies are empty or existing UC015 tests pass.
