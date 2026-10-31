# Task64 — UC013 View Branch Structure

30/10/2026, Asia/Saigon. **UC013 COMPLETE_VERIFIED_V1**: production HTTPS endpoint, SQL authorization,
paginated structure expansion, node details, actual SQL concurrency/fault evidence and relevant
regressions PASS. The V1 visibility decision below was explicitly approved by the developer.

## A. Requirements and selected V1 rules

[UC013 §2.2](../../../docs/requirements/srs_sections/02_use_case_specifications.md) describes branch
selection, Floor/Area tree, rooms per Floor, departments, expansion, active/inactive filters, selected
node details and empty results. [GBR001/002/015 and SYS-PAR-11](../../../docs/requirements/srs_sections/05_requirement_appendix.md)
require a valid session, scoped permission, pagination default20/max100 and stable ordering.
[Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md), the draft Permission Matrix and
[current catalog](../../database/release/03_HotelOps_RBAC_Catalog_Seed.sql) were reviewed; the historical
SRS is not represented as a finalized permission assignment.

**Explicit developer decision, recorded ASSUMED_V1:** ROOM_VIEW independently permits **basic physical
Branch → Floor → Area → Room** viewing within SQL-authorized branch scope. It does not grant writes
or administrative data. MASTER_DATA_MANAGE independently permits management viewing, including
departments and Floor/Area descriptions. Neither path requires both permissions. No permission,
role assignment or write policy was added. This narrows the draft's Operations Manager department
visibility to the physical scope the developer approved, rather than silently expanding ROOM_VIEW.

| Rule | Implemented V1 contract |
|---|---|
| Actor and authority | Protected cookie ticket + CurrentUserAccessor + initial/final SQL session checks. Existing PermissionService evaluates MASTER_DATA_MANAGE global/branch, then ROOM_VIEW branch if management is denied; unavailable authority never falls back to a weaker path. No role-name bypass |
| Scope | Unbound ALL management can inspect any existing branch. Bound ALL and BRANCH stay within valid branch/membership. OWN has no structure resource mapping and cannot grant this branch read. Existing evaluator controls effective/expired/future assignments and active branch/membership |
| Basic fields | Branch ID/code/name/isActive; Floor ID/BranchId/code/name/floorNumber/isActive; Area additionally actual nullable FloorId; Room ID/BranchId/FloorId/AreaId/roomNumber/isActive. Floor/Area description is null; department requests return403 |
| Management fields | Same physical fields plus Floor/Area description; departments ID/BranchId/code/name/isActive. No manager IDs, staff, credentials, sessions, branch contact details, Room operational status/history or arbitrary entity serialization |
| Real relationships | Room has a required Floor FK and optional Area; Area may be branch-level with null FloorId. Root Areas are those branch-level nodes. Floor Rooms includes valid direct rooms and rooms in its Areas; no parent relationship is invented. Room retains its actual FloorId even inside a branch-level Area |
| Legacy inconsistency | Exclude Areas linked to another branch's Floor and Rooms with cross-branch Floor/Area or conflicting Area.FloorId. Explicit selection returns404. Do not repair data or expose foreign parent IDs/names |
| Expansion | ASSUMED_V1: one selected node and one child collection per request, sufficient to build the complete tree through paginated expansion. Root defaults to floors, Floor to areas, Area to rooms; Room/Department are leaves |
| Filter and empties | ASSUMED_V1: isActive omitted means both; true/false filters the requested child collection, not the selected header. Empty authorized branch/Floor/Area/filter/page returns200 with empty items and correct total; no fake nodes |
| Inactive branch | Global management can inspect archived structure. Scoped/basic paths remain denied for inactive branches by the existing evaluator. Inactive child/parent records within an authorized active branch remain readable; no active-only assumptions |
| Order and bounds | ASSUMED_V1: FloorNumber ascending, null last, then code/ID; Area/Department code then ID; RoomNumber then ID, using DB collation. Default20/max100, positive IDs/pages and Int32-safe offset. Beyond-last page returns empty items, retains total |
| Consistency | Owned SERIALIZABLE locking transaction; retained actor User → Session/grants → feature reads; final checks reuse retained authority. No personnel reads after feature locks. Count/page/header are one consistent read, not SQL SNAPSHOT isolation. No SaveChanges, audit insertion or business mutation |
| Final authority | Session/assignment expiry after waiting withholds the report. Management-to-basic downgrade discards the already materialized management report; a new request may receive basic data only |
| Cancellation/errors | RequestAborted propagated to SQL; disposal rolls back/releases locks. SQL1222/-2/1205 →503, no automatic retry or partial DTO. Other SQL/programming faults use existing generic500 middleware. No-store responses |

Existing session-touch/login metadata remain enabled in middleware. “Read-only” means no business
write by UC013; it does not disable the existing session activity policy. Serializable retention can
delay a concurrent structure/account writer; this is bounded by existing command/request timeouts.

## B. Usable production HTTP contract

**ASSUMED_V1 route**, compatible with UC014 POST routes:

```http
GET /api/branches/{branchId}/structure
GET /api/branches/{branchId}/structure?collection=areas
GET /api/branches/{branchId}/structure?collection=departments
GET /api/branches/{branchId}/structure?nodeType=floor&nodeId={floorId}
GET /api/branches/{branchId}/structure?nodeType=floor&nodeId={floorId}&collection=rooms
GET /api/branches/{branchId}/structure?nodeType=area&nodeId={areaId}&page=2&pageSize=20
GET /api/branches/{branchId}/structure?nodeType=room&nodeId={roomId}
GET /api/branches/{branchId}/structure?nodeType=department&nodeId={departmentId}
```

All lists accept `isActive`, `page`, `pageSize`. Branch node must omit nodeId; other nodes require
it. Invalid node/collection combinations are400. Root `areas` shows only branch-level Areas; expand
each Floor to get its Areas or Rooms. Each response returns Branch, visibility, selected node,
availableCollections, applied filter and the requested `{ items, page, pageSize, totalCount }` page;
unrequested collections are null. Leaves return their selected DTO with no child page. Paging and
the selected-node header let clients expand without loading an unbounded tree.

| HTTP | Behavior |
|---|---|
| 200 | Authorized structure/header/empty page; Cache-Control no-store |
| 400 | Invalid branch/node/query/pagination/type binding; VALIDATION_FAILED or standard validation ProblemDetails |
| 401 | Missing/invalid/revoked SQL session; SESSION_INVALID where controller/session middleware supplies it |
| 403 | Missing effective permission, out of branch scope, unsupported OWN, restricted session, or basic request for departments. No data envelope |
| 404 | Authorized management missing branch, or authorized branch with missing/foreign/inconsistent selected node; STRUCTURE_NODE_NOT_FOUND |
| 503 | Read contention/authority unavailable; STRUCTURE_READ_UNAVAILABLE for service result. No partial data |

GET uses the existing cookie/SQL-session pipeline over HTTPS. Existing CSRF protection still applies
to unsafe methods. No create/update/deactivate behavior is exposed through this endpoint.

## C. Task64 files

| File | Change |
|---|---|
| [BranchStructureDtos.cs](../../../src/HotelOps.Api/DTOs/HotelStructure/BranchStructureDtos.cs) | New dedicated query validation, selected-node DTOs and bounded collection DTOs |
| [BranchStructureService.cs](../../../src/HotelOps.Api/Services/HotelStructure/BranchStructureService.cs) | New SQL-scoped read/consistency/eligibility/projection path |
| [BranchStructureController.cs](../../../src/HotelOps.Api/Controllers/BranchStructureController.cs) | New authenticated GET and HTTP result mapping |
| [Program.cs](../../../src/HotelOps.Api/Program.cs) | One AddScoped registration; existing Task62/63 registrations preserved |
| [HotelStructure harness](Program.cs) | Optional --task64 extension; legacy UC014 invocation preserved |
| [Task64StructureCases.cs](Task64StructureCases.cs) / [Task64StructureConcurrency.cs](Task64StructureConcurrency.cs) | New production HTTPS cases and real SQL service/wait/fault acceptance |
| [Delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md) and this report | UC013 classification, evidence and progress |

No generated entity, DbContext, schema, RBAC, DEV2 service, UC006 predicate/release gate or previous
verification artifact was changed. No HTTP Deactivate, SA57 activation or Git mutation.

Preflight/final HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`, branch
`feature/BE1-database-integration`, upstream `origin/feature/BE1-database-integration` unchanged.
Index and four worktrees unchanged. Recoverable backup `obj/task64-preflight-20261030-071304`:
**428/428 original backup hashes valid;425 pre-existing files unchanged;3 intended existing-file
edits** (API Program.cs, harness Program.cs, delivery plan). All earlier uncommitted changes retained;
the remaining six delivery files are new. Validation snapshot at26.6 elapsed minutes from preflight,
including tool/test waits, not human-hours or evidence for the entire delivery-plan estimate.

## D. Executed HTTPS/SQL evidence

[Final receipt](task64-accepted-final2.local.json): **374 PASS assertions =215 UC014 +159 Task64
UC013**, **34 DMV-confirmed waits =23 UC014 +11 UC013**. Assertions include fixture logins and
invariant checks; they are not374 independent business scenarios. Production root Program.cs runs
as a real child API, ephemeral pinned HTTPS certificate, actual credential login, protected cookies,
SQL sessions and the normal middleware/DI/schema gate. No fake API host or mocked permission store.

Final trial: `HotelOps_Canonical_20261030_003457_da90ba3f_Fresh`. It was freshly initialized from the
canonical schema/catalog/reference/approved existing contract patches, then the **unchanged guarded
rowversion prerequisite** was applied. Trial instance/name/Task17 marker and startup compatibility
were checked before fixtures or regressions. HotelOpsDB was never connected to; canonical SQL source
files were not altered. Credentials/cookies/certificate passwords/connection strings are absent
from the acceptance receipt.

| Area | Actual evidence / result |
|---|---|
| HTTPS authorization | Anonymous401; no/ineffective/restricted grant403; actual Operations Manager ROOM_VIEW BRANCH/basic200, bound management200, independent room viewer200; cross-branch403; global management200. Role names never grant authority |
| Hierarchy/details | Multiple Floors/Areas/Rooms, branch-level Area, no-Area Room, required Floor FK, selected leaves, inactive nodes, empty branch/Floor/Area, department management path and basic denial PASS |
| List/validation | Floor ordering incl ties/null, Room ordering across pages, default20/max100, invalid/overflow/out-of-range queries400, beyond-last page, exact inactive Area/Room filters PASS |
| Leakage/writes | Recursive DTO allowlist, foreign/operational markers absent, basic descriptions null/departments denied. Functional HTTPS business/User/catalog/audit snapshots unchanged, no new sessions; normal activity touches allowed. Separate direct service/fault phase fingerprints **all mapped table rows**, including sessions/credentials/audits, unchanged |
| Writer first | Actual HTTPS waits on uncommitted Floor description; after commit returns authoritative new description, not an old EF entity. DMV lock wait captured |
| Revocation | Permission and session revokers following User-first order wait for active read; first read completes consistently, next HTTPS request403/401. SQL grants/session rechecked |
| Time expiry | Assignment expires while page waits: report withheld403. Management expires with ROOM_VIEW surviving: current report withheld403; next request basic only, no description |
| Cancellation | Real HTTPS RequestAborted while feature SQL waits; transaction locks disappear **before** blocker release. No business mutation |
| Read first | Pause between actual count/page; actual production UC014 CreateFloor with another SQL actor waits, then commits once after read. Count/items reflect pre-insert state; subsequent DB count observes insert |
| SQL faults | DMV-confirmed real LOCK_TIMEOUT1200 →1222, command timeout2s →-2; deliberate SQL THROW51064 query failure. No returned partial DTO/retained transaction/tracked state; subsequent normal read succeeds. Fault phase all-table fingerprint unchanged |
| No N+1 | Root feature projection exactly3 SQL commands: Branch +Count +Page, independent of children. Selected-node reads add one bounded header query; no recursive per-child lookup |

Direct service fixtures use persisted SQL sessions, protected ticket principals and production
CurrentUserAccessor/PermissionService/SqlSessionService. A request-context holder isolates the two
concurrent console calls; these direct fault/read-first cases are distinguished from real HTTPS.
Harness page barriers/SQL THROW are harness-only, not production hooks or schema changes. UC013
1205 has a fail-closed source handler; actual1205/retry evidence here belongs to **UC014 regression**,
not a claim that a new UC013 deadlock schedule was executed.

Early attempts/logs/trials are preserved under ignored `obj/task64-*`: initial count/page test failed
because constructing a second HttpContextAccessor in one console execution context cleared the first
fixture's AsyncLocal identity. Fixed by separate fixture request contexts. A subsequent SQL1222
assertion lacked the provider number because the error arose during materialization rather than
ExecuteReader; harness now observes typed EF QueryIterationFailed too, retaining only error numbers.
No production authorization/SQL behavior was weakened to obtain PASS. Final receipt above supersedes
those failed harness attempts; they were not reported as successful runs.

## E. Regression, limits and classification

- **UC014:**215 assertions/23 waits in the final run; original production create service unchanged.
  Includes duplicate/parent/scope races, audit rollback, SQL1222/-2/1205, explicit fresh retries.
- **UC023:**[117 PASS/11 waits](task64-room-regression.local.json), separate trial
  `HotelOps_Canonical_20261030_002749_eb7e5304_Fresh`. Remains **PARTIAL**, no Room Directory HTTP added.
- **Relevant AuthN/AuthZ:** Permission43 +Authorization61 +Resource49 +Hardening21 =**174 PASS**.
  These foundation harnesses use their documented doubles/loopback scope, not live main-DB --live.
  Real SQL cookie/session/revocation coverage is the UC013/014 receipt above.
- **Build:** `dotnet build HotelOps.slnx`, HotelStructure harness and RoomDirectory harness PASS.
  NU1900 warnings remain: vulnerability feed https://api.nuget.org/v3/index.json unavailable;
  no C# warnings/errors. No warning suppression or package changes.
- **dotnet test HotelOps.slnx:** exit0, **no discovered automated test project**. Do not infer unit
  test coverage from this; explicit console SQL/HTTPS suites above were executed.
- **Diff:** git diff --check PASS; new/untracked delivery source whitespace and preservation verified
  separately in ignored `obj/task64-validation.local.json`. Existing LF/CRLF notices retained.

**No mandatory blocker remains for this selected V1 UC013 contract.** Final SRS/Permission Matrix
ratification, deployment/certificate/load acceptance and future unimplemented external writers are
not certified by these local tests. Archived/basic and departmental visibility follow the explicit
V1 boundary above. No UC006/global release gate is closed by this task.

**UC013 COMPLETE_VERIFIED_V1; gain +2:19 →21/256 =8.20%, completed7 →8/75.**
UC023 remains PARTIAL; remaining235 verified points, unchanged full-scope denominator.

**Recommended next task:** UC015 Update Floor/Area, reusing this read API and UC014 conventions;
first select an explicit GBR005 stale-write/version contract compatible with existing Floor/Area
schema (no rowversion currently mapped). Do not silently add a migration or skip concurrency/audit.
