# Task67 — UC023 Room Directory completion

30/10/2026, Asia/Saigon. **COMPLETE_VERIFIED_V1**: production HTTPS list, detail/current-status
evidence and paginated recorded history; SQL scope/session consistency; targeted acceptance and
regressions PASS. UC016 remains PARTIAL; its services, writers and release dependencies are unchanged.

## A. Requirements and V1 decisions

[UC023 §2.12](../../../docs/requirements/srs_sections/02_use_case_specifications.md) requires a
permitted logged-in actor; room number/floor/type/current status; branch/floor/area/type/status/
active/keyword filtering; floor/room-number ordering; empty results; selected-room details,
current reason/source and links to history; no business writes. GBR001/002/015 and SYS-PAR11
require SQL scope, pagination20/max100 and stable ordering. Reviewed [change notes](../../../docs/requirements/SRS_CHANGE_NOTES.md),
draft Permission Matrix, actual mappings/entities, [Task60](README.md), [Task64](../HotelStructure/TASK64_UC013_IMPLEMENTATION.md),
[Task65](../HotelStructure/TASK65_UC015_IMPLEMENTATION.md) and [Task66](../HotelStructure/TASK66_UC016_IMPLEMENTATION.md).
The SRS is historical draft, not a signed final role assignment. Current Task67 explicitly supplies
ROOM_VIEW and permits defensible ASSUMED_V1 decisions; no permission/catalog assignment is changed.

| Earlier gap | Selected ASSUMED_V1 / implemented behavior |
|---|---|
| D3 status | Return **exact stored operationalStatusCode**, no derived occupancy/lifecycle or invented display enum. Supported filters: AVAILABLE,OCCUPIED,DIRTY,CLEANING,INSPECTION,READY,OUT_OF_SERVICE from SQL CHECK |
| D5 branch | Required positive branchId for ordinary list/detail/history. Explicit allBranches=true is a separate list mode, mutually exclusive with branchId. Only EvaluateGlobalAsync(ROOM_VIEW) can authorize it; a branch-bound ALL assignment is not global. No automatic multi-branch union or role-name exemption |
| Permissions | Existing ROOM_VIEW SQL paths only, including current role assignment/membership/branch/time. MASTER_DATA_MANAGE alone, SYS_ADMIN/OWNER names and unsupported OWN do not grant room access. Canonical catalog currently has BRANCH ROOM_VIEW grants; code does not create global grants to satisfy an actor list |
| D7 filter/sort | Existing literal RoomNumber substring search, trim blank, max200; FloorId/AreaId/RoomTypeId positive; raw status/tri-state isActive. Default sort=floor: numbered floors before null, FloorNumber/FloorCode/FloorId/RoomNumber/RoomId. Whitelist floor,roomNumber,roomType,status; descending boolean. Text uses SQL collation and lexical room number, not invented natural ordering. Every order ends in a stable ID tie-break |
| Paging | page1/pageSize20 defaults; size1–100; reject Int32 offset overflow; beyond-end200 with empty items and filtered totalCount. History has separate same-limit paging, ChangedAt descending then HistoryId descending |
| Detail | Room list projection +room description +currentStatusEvidence +a working scoped relative HistoryUrl. No credential/personnel/assignment/admin fields; related Room description is the only added descriptive field |
| Current reason | Latest **recorded** room_status_history by ChangedAt/HistoryId is usable only if ToStatus matches current SQL Room status. RECORDED carries that row's reason/time/source; null reason stays null. No records→NOT_RECORDED. Latest row disagrees→OUT_OF_SYNC, no backfilled reason or state override |
| Source | CLEANING_TASK/INSPECTION/ROOM_BLOCK IDs are returned only after batch SQL verification of the corresponding entity's same RoomId. Foreign/dangling source ID is suppressed. Other source bindings, including ambiguous MAINTENANCE TriggerId, remain UNRESOLVED with no ID; no guessed Incident-vs-Work mapping or extra source-module permission |
| History | Read actual room_status_history only, never synthesize INIT/events from current state, audits or active task rows. Empty records→items[]/totalCount0. Coverage explicitly RECORDED_STATUS_EVENTS_ONLY, not a guarantee that every historical transition was recorded. ChangedBy and unrelated source contents are excluded |
| Inactive/corrupt references | Active branch authority is required. isActive defaults null, so historical inactive Rooms/parents/types remain visible within that branch. Legacy Floor/Area cross-branch or mismatched hierarchy rows are excluded, including global mode; they never expose foreign names |
| Errors | Invalid/missing branch/filter/page/sort/ID400; anonymous/invalid/revoked/expired SQL session401; permission/scope403; authorized-branch missing/foreign Room404; provider/unavailable authority503. Restricted-cookie pipeline can reject403. No room existence lookup before SQL branch permission |

Missing records/source bindings are truthful data-availability results allowed by Task67, not fabricated
events or a claim that maintenance/history producers are complete. ROOM_VIEW grants no writes and
does not authorize other module detail APIs merely because a source reference is shown.

## B. HTTP contract

| Method / route | Query / response |
|---|---|
| GET /api/rooms | branchId OR allBranches=true; optional floorId,areaId,roomTypeId,operationalStatusCode,isActive,search,sort,descending,page,pageSize. RoomDirectoryResponse(items,page,pageSize,totalCount) |
| GET /api/rooms/{roomId} | required branchId; RoomDirectoryDetailDto(room,description,currentStatusEvidence,historyUrl) |
| GET /api/rooms/{roomId}/history | required branchId; optional page/pageSize; RoomDirectoryHistoryDto(items,page,pageSize,totalCount,coverage) |

For example `/api/rooms?branchId=1&operationalStatusCode=READY&pageSize=20`, then open the selected
Room's detail with its returned branchId and follow historyUrl. Branch IDs are examples, never
hard-coded authority. Existing cookie authentication, production SQL session and HTTPS are required.
GETs require no CSRF mutation token and expose no POST/PUT/DELETE. Controller data responses use
no-store; static model-binding400 responses follow the existing ASP.NET validation convention.

## C. Source changes

- New [RoomsController.cs](../../../src/HotelOps.Api/Controllers/RoomsController.cs): three GETs, HTTP validation/status/no-store/generic provider errors.
- Extend [RoomDirectoryService.cs](../../../src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs): reuse existing ListAsync and authorization/transaction boundary; add DetailAsync/HistoryAsync, explicit global mode, sorting and bounded source batches. No service replacement or new architectural layer.
- Extend [RoomDirectoryContracts.cs](../../../src/HotelOps.Api/DTOs/Rooms/RoomDirectoryContracts.cs): validated list options, dedicated detail/history/evidence/source DTOs.
- [API Program.cs](../../../src/HotelOps.Api/Program.cs): one scoped RoomDirectoryService registration, same scoped DbContext/auth infrastructure; no other composition change.
- New [Task67RoomCases.cs](../HotelStructure/Task67RoomCases.cs) and extend existing [HTTPS bootstrap](../HotelStructure/Program.cs) with --task67. Reuses production API host, real login and certificate pinning; no new test project/framework.
- Existing [SQL harness](Program.cs): same117 checks, update obsolete “no controller” assertion to require authentication on the approved controller; receipt/console explicitly describe this as internal-list regression, not full HTTP acceptance.
- [Delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md): UC023 completion and verified progress only.

Generated entities, DbContext mappings, schema, RBAC catalog, DEV2 source and UC006/UC016 runtime
files are untouched. No new role grant exists in production source/configuration.

## D. SQL/HTTPS and concurrency evidence

| Execution | Actual result |
|---|---|
| [Final HTTPS/new-query receipt](task67-accepted.local.json) | **227 UC023 assertions +2 startup=229 PASS**,4 DMV-confirmed waits. Root production Program.cs HTTPS, real credential login/cookies/persisted sessions/SQL permission/EF queries. List/detail/history, empty/missing/stale history, all supported source batches, foreign polymorphic reference suppression, all/branch/bound-ALL/OWN, filters/sort/paging/validation, revoked session/grant, expiry and no sensitive fields |
| New consistency/fault paths | HTTPS detail waits on actual Room writer and rereads committed description; canceled HTTP releases retained User before feature blocker release; real SQL command timeout→503/no partial response; history count range blocks concurrent append, consistent page returned and next request sees append. Final session/grant time expiry after history count withholds materialized history and cleans transaction/tracker |
| Read-only | Every persisted Room column identical before/after successful/denied functional HTTP reads. No business audit/history created by reads. Session metadata updates from normal auth middleware are distinct from room business mutations |
| [Existing Task60 SQL regression](task67-sql-regression.local.json) | **117 PASS /11 DMV waits**: actual existing production service/accessor/session/permission source; count/page, filters, FK isolation, time expiry, writer waits, cancellation, SQL1222 and injected query/authority failures; all mapped rows restored. This suite alone does not certify HTTP |
| [UC013/014/015 regression](task67-structure-regression.local.json) | **590 PASS =216 UC014 +159 UC013 +215 UC015**,53 waits,14 post-UPDATE UC015 fault/expiry schedules. Per-created-entity audit assertions depend on fixture race winners, so total can differ from the previous589; checks are not weakened |
| Auth foundation | **174 PASS**:43 PermissionService,61 Authorization,49 ResourceAuthorization,21 Hardening. Their documented doubles/loopback scope is unchanged; live SQL authorization evidence is in the HTTPS receipt above |
| Build | dotnet build HotelOps.slnx PASS,0 errors; final --no-restore build1 NU1900 warning. Both existing harness builds PASS,2 NU1900 each. No C# warnings or warning suppression |
| dotnet test | Exit0; solution has only API project/no test project. Explicit console suites above were executed; no fabricated unit-test result |
| Diff / preservation | git diff --check plus new-file whitespace/local-link checks PASS. SHA256 backup/index/worktree proof: ignored obj/task67-validation.local.json |

All SQL uses **disposable canonical trial databases**, exact instance/name/Task17 marker checks,
unchanged approved guarded trial users.row_version patch and successful startup schema gate before
queries. Final HTTP trial: `HotelOps_Canonical_20261030_163135_33e395be_Fresh`; internal SQL trial:
`HotelOps_Canonical_20261030_162434_372bb7d2_Fresh`; structure regression:
`HotelOps_Canonical_20261030_162822_0225330a_Fresh`. HotelOpsDB is never opened.

ALL/OWN acceptance adds separate **trial-only roles/assignments** referencing existing ROOM_VIEW.
This proves real SQL decisions for explicit grants and branch-bound ALL without changing any
canonical role's grant or assigning a production permission. These fixtures are not deployment
recommendations. No synthetic permission data source or authentication bypass is used in HTTP tests.
Direct-service history schedules use persisted isolated SQL sessions and production accessor,
PermissionService/EfPermissionDataSource/SqlSessionService, separately identified from HTTP tests.

Existing locks remain User→Session/grants→authorized branch/resources. Source projections never
load source actors after feature locks. SERIALIZABLE guarantees one request's authority/count/page/
reason/source view, not stable pagination across separate requests or universal writer deadlock
freedom. No global writer/UC006 gate is cleared. Default timeout is not increased for acceptance.

Validation history: initial new harness compile CS0136 (tuple name shadowed a cookie variable) was
fixed; subsequent builds/SQL/HTTPS all pass. One overlapping harness rebuild emitted transient
MSB3026 apphost-copy retries while the earlier harness exited; build succeeded, and the subsequent
sequential rebuild has only NU1900. NU1900 is the unavailable NuGet vulnerability feed, not a hidden
compile failure. Intermediate214/226-check receipts and all trial artifacts/logs are retained.

## E. Classification, limits and progress

Mandatory UC023 V1 list/detail/reason/source/history-link, scoped authorization, HTTPS and SQL
acceptance/regressions are implemented and verified: **COMPLETE_VERIFIED_V1**. No mandatory V1
blocker remains. The truthful missing/out-of-sync/unresolved evidence response is part of the
approved best-fit contract, not a promise to implement missing source workflows or reconstruct data.

Gain **+2 points**, prior23/256→**25/256=9.77%**, **10/75** verified UC. UC016 stays PARTIAL; no points
or writer acceptance awarded to it. No SA57 activation or UC006 release-gate change. Final signed
SRS/catalog changes, future status producers, external unaudited SQL and production load acceptance
remain separate from this verified read use case.

Recommended next: **UC017 Create Room Type**, whose specification is §2.6; implement its confirmed
validation/permission/audit/concurrency requirements using the existing global RoomType model,
without inventing BranchId ownership. DEV2 coordination for UC016 continues as a separate dependency.
