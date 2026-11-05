# Task74 — Accelerated Batch 5: independent use cases

2026-10-31, DEV1, `feature/BE1-database-integration`.
**Result: 2 COMPLETE_VERIFIED_V1 (UC032/109), 1 PARTIAL (UC012), 2 reviewed candidates BLOCKED_EXTERNAL (UC028/026). Target 4–5 complete was not achieved; no partial credit awarded.**

## A. Selected UCs and weights

| UC | Exact SRS title | Weight | Acceptance / dependency | Final classification |
| --- | --- | ---: | --- | --- |
| UC032 | Update Cleaning Progress | 2 | In Progress + current assigned housekeeper; room-type cleaning item states, 0–100 progress, notes/photos, rowversion, audit; no implicit completion | COMPLETE_VERIFIED_V1 |
| UC109 | Update Task Progress | 2 | Current assignee; In Progress or explicit Paused; completion criterion results, notes/private evidence, rowversion/audit; no implicit completion | COMPLETE_VERIFIED_V1 |
| UC012 | Update Branch Information | 2 | Read/create/update plus branch deactivation/GBR033; latter requires safe dependency writer protocol | PARTIAL |
| UC028 | Create Cleaning Task | 3 | Cleaning item foundation now available; Pending Assignment, one open cycle, GBR043/044 room/block precedence/history/atomic room side effects still unresolved | BLOCKED_EXTERNAL |
| UC026 | Deactivate Department | 5 | GBR042 active users, warehouses, open tasks/requests, assets and concurrent producers need coordinated guards | BLOCKED_EXTERNAL |

Read AGENTS, delivery plan, SRS UC012/026/028/032/037/044/045/047/107/109/138, applicable Permission Matrix/GBR rules, Change Notes, Task70–73 reports, existing entities/DbContext/Room/RoomType/Housekeeping/checklist services and harnesses. No newer signed final matrix found; no pending Reception/DH permission changes invented. Numeric order was not used to infer titles.

Excluded UC006/016/029/041/046 as requested. Completed baseline UCs are regression only, never counted again. Replacement review found UC037/044/045/047/107/138 require notification; UC031/033–036/108/110 need unimplemented time contracts. No safe additional full replacement was established in this batch.

## B. Implemented endpoints

All routes use real cookie/SQL sessions, HTTPS, CSRF on writes and scoped SQL grants. DTOs do not expose EF entities or storage keys.

| Method / route | Behavior |
| --- | --- |
| POST `/api/branches` | UC012 subset: global MASTER_DATA_MANAGE, normalized unique code, mandatory name/address/contact validation, fixed SYS-PAR-10 timezone, Active create/audit |
| GET `/api/branches/{id}/edit` | UC012 subset: global scoped metadata and strong aggregate revision ETag |
| PUT `/api/branches/{id}` | UC012 subset: If-Match, validated metadata/reason, immutable code/timezone/activation, atomic update/audit |
| POST `/api/branches/{branch}/room-types/{id}/cleaning-items` | Foundation: MASTER_DATA_MANAGE; create distinct cleaning definitions once, no overwrite of existing definitions |
| POST `/api/branches/{branch}/cleaning-tasks/{id}/progress-initialization` | Foundation: CLEANING_ASSIGN; explicit audited adoption, copy immutable snapshots from actual RoomType, no read-time conversion |
| POST `/api/branches/{branch}/operational-tasks/{id}/progress-initialization` | Foundation: TASK_MANAGE; explicit audited criterion initialization for an existing started task, not a Start/Pause operation |
| GET / PUT `/api/branches/{branch}/cleaning-tasks/{id}/progress` | UC032 read/save with SQL CLEANING_EXECUTE/current assignment, checked items, notes, photos and actual rowversion ETag |
| GET / PUT `/api/branches/{branch}/operational-tasks/{id}/progress` | UC109 read/save with SQL TASK_EXECUTE/current assignment, criterion PASS/FAIL, notes/photos, explicit IN_PROGRESS/PAUSED state |
| GET `/api/branches/{branch}/{cleaning-tasks\|operational-tasks}/{id}/progress/evidence/{attachment}` | Reauthorize source/current owner and typed evidence relationship; bounded private file read; no arbitrary URL access |

Missing If-Match = 428; invalid DTO/tag/result/file = 400; invalid/missing session = 401; missing grant/branch/current ownership = 403; management initialization missing source = 404; missing evidence = 404; stale revision = 412; invalid phase/duplicate initialization = 409; absent/incompatible optional schema = 503. With OWN-only grants, an unresolved missing execution resource is masked as 403; no invented global execution bypass.

## C. Source changes and ASSUMED_V1

Developer conditionally approved [the concrete contract](TASK74_CONTRACT_PROPOSAL.md) in this session, including additive trial schema, compatible mapping, evidence links, required criteria and concurrency. Only DEV1 scope implemented.

Created runtime files:

- `src/HotelOps.Api/Controllers/BranchMaintenanceController.cs`, `WorkProgressController.cs`.
- `src/HotelOps.Api/DTOs/HotelStructure/BranchMaintenanceDtos.cs`, `DTOs/Housekeeping/ProgressDtos.cs`.
- `src/HotelOps.Api/Services/HotelStructure/BranchMaintenanceService.cs`, `Services/Housekeeping/WorkProgressService.cs`.
- `src/HotelOps.Api/Data/ProgressSchema.cs`.

Created deployment/verification files:

- [Progress package](../../database/release/progress/README.md): `01_Progress.sql`, `02_Verify.sql`, `03_Rollback.sql`, `Run-Progress.ps1`, `README.md`.
- `scripts/verification/HotelStructure/Task74BranchCases.cs`, `Task74ProgressCases.cs`.
- This report and `TASK74_CONTRACT_PROPOSAL.md`; local receipts/logs referenced below.

Modified existing files:

- `Services/HotelStructure/RoomTypeBatchService.cs`: optional private feature permission callback; existing callers retain default behavior. Reuses retained User → Branch → catalog, SERIALIZABLE business read/write, initial/final session/grant checks and transaction handling.
- `Services/Housekeeping/InspectionExecutionService.cs`: existing upload routine delegates to a typed private helper; original INSPECTION source behavior preserved and regression verified.
- `Data/HotelOpsDbContext.Checklists.cs`: four DEV1 entities disable bare SQL OUTPUT for optional parent/assignment triggers; works before/after deployment. Generated entities and main DbContext unchanged.
- Harness `HotelStructure/Program.cs`: `--task74`, private evidence root, combined receipt; no new test project.
- [Delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md): classifications, current snapshot and §22.

Approved storage: six additive typed tables; nine trusted FKs; six checks; unique item/source/order keys; two log indexes; two actual rowversion columns; seven triggers. Cleaning definitions and snapshots are distinct from inspection checklists. Task criteria preserve identity while results/notes change. Evidence uses existing CLEANING_TASK/TASK types with typed log FKs; no shared enum/catalog expansion. Existing parent/assignment writes advance revision, preventing stale writes through legacy paths. Opted-in COMPLETED writes are rejected when required cleaning items or task criterion results are missing; legacy rows without adoption are untouched.

ASSUMED_V1 for details not prescribed by SRS:

- Explicit management initialization is required; no bulk adoption or GET side effects. Labels/order/required flags are frozen per task; this batch does not add unrelated template CRUD or task state-transition APIs.
- Current assignment must be unique, active, non-ended and effective at server time, even when a caller has broader authority. SQL `PermissionGrantRules`/grant paths are reused; no runtime role grants or new permission codes. Trial fixture role assignments are test setup only.
- PAUSED is an explicit approved progress record state with a compatible existing parent; never inferred from BLOCKED/ShiftAttendance. No Workday, interval or time-event implementation is claimed.
- Progress may be updated within 0–100; 100% does not fill missing required results or complete work. Timeline/evidence detail is bounded to the latest 100 entries; individual upload at most five files, existing 10 MiB JPG/PNG/PDF policy.
- Private downloads recheck source type/id, typed link, branch and current ownership before reading storage. Unsupported metadata/format/paths are rejected; SQL failure/cancellation cleans only files owned by that invocation.
- Branch metadata subset uses the existing audit-backed aggregate ETag pattern without inventing a Branch row_version column. Literal/full UC012 schema/deactivation acceptance is not claimed. SYS-PAR-10 is `Asia/Ho_Chi_Minh`; update never reactivates a branch.

## D. Acceptance per UC; concurrency and rollback

[Final accepted receipt](task74-accepted.local.json) is a byte-identical copy of `task74-progress-attempt10.local.json`, SHA256 `B7C9DB1EA0AF05204050A2E992E4C973127AB6A306B08C822D24B0A9E50432F2`.
**287 PASS total**, including bootstrap, deployment checks, UC012 subset and shared regression; not 282 distinct full use cases. Within this receipt, **87 UC032 assertions and 89 UC109 assertions** are the contiguous per-UC acceptance phases.

| Acceptance | UC032 | UC109 |
| --- | --- | --- |
| Production success/read/save | PASS, frozen cleaning snapshots and checked state | PASS, own task criterion results; explicit Paused save also PASS |
| AuthN/AuthZ | Anonymous 401, absent grant 403, non-executor/admin assumption rejected | Same, current SQL assignee required |
| Cross branch / source links | Foreign branch 403; foreign attachment download 404 | Foreign branch 403; actual foreign task criterion rejected 400; foreign evidence 404 |
| Invalid/missing | Percent -1/101, unknown item, missing/invalid tag/CSRF, arbitrary URL, bad base64/path rejected; management missing source 404 | Same; invalid criterion/source rejected; OWN missing resource masked 403 |
| Business rules | In Progress only; source phase change rejects; 100% preserves state and missing mandatory item | In Progress/explicit Paused only; closed source rejected; no auto-resume/completion; missing mandatory criterion remains visible |
| Persistence/audit | SQL progress/result/evidence and actor/branch audit verified | SQL parent percent + criterion/log/evidence/audit verified |
| Optimistic concurrency | Two actual concurrent HTTPS saves: one 200, one 412 | Same |
| Legacy writer compatibility | Raw parent change advances ETag; stale request rejected; RoomType change preserves writable original cleaning snapshots | Same; assignment changes also invalidate revision/access |
| Source identity / required result | SQL snapshot rewrite and premature completion rejected | SQL criterion rewrite and premature completion rejected |
| Audit failure rollback | Real audit CHECK failure after business/evidence writes; revision/progress/file state restored | Same |
| Temporal final gate | Valid initial grant expires during real SQL pause; final 403 rolls back writes/revision/files | Same |
| Cancellation | DMV-confirmed SQL wait; actual HTTPS cancellation releases transaction locks before blocker rollback; revision unchanged | Same |

Existing unique-current-cleaning-assignment constraint remains enabled and rejects duplicate owner. Where ambiguity is representable, execution fails closed; changing the current owner immediately revokes former owner's access. Raw cross-source evidence relink is SQL-rejected. No mocked success, fake notification or enqueue-as-delivered credit.

UC012 subset: production create/read/conditional update, SQL persistence/audit, anonymous/denied/scoped-global rejection, cross-branch management rejection, malformed/unknown fields, missing source, missing/stale tag, audit fault rollback, real concurrent edit and duplicate-create races all PASS. Deactivation success/dependency concurrency remains NOT IMPLEMENTED / NOT VERIFIED; no complete points.

## E. SQL / HTTPS and deployment evidence

- Actual API `Program.cs`, local HTTPS certificate pinned by harness, credential login/cookie/CSRF and SQL-backed sessions/grants. No mock server or in-memory provider.
- Final Fresh API trial: `HotelOps_Canonical_20261031_173727_aa1dae75_Fresh`. Preparation uses unchanged guarded `UserDirectory/Prepare-Database.ps1`, UC017/checklist/Housekeeping module runners and Task26 trial-only users.row_version prerequisite. Original SQL source guards retained.
- Baseline canonical full run: `HotelOps_Canonical_20261031_164630_c840a2d2`; Fresh/Upgrade/AuthUpgrade baseline schema comparison, CHECKDB/CHECKCONSTRAINTS PASS. Log `obj/task74-progress-canonical.local.log` and canonical evidence folder; this baseline run precedes the separate progress package acceptance.
- Progress package: real post-table DDL fault rollback (tables/marker absent), fresh apply, replay, Verify, empty Rollback/reapply, populated legacy upgrade preservation and populated Rollback refusal PASS. Exact receipt paths are included under `deployments` in the final accepted receipt.
- Hardened package Apply/Apply/Verify/Rollback/Apply/Verify independently PASS on `...164630_c840a2d2_Upgrade` and `..._AuthUpgrade`; `obj/task74-accepted-{Upgrade,AuthUpgrade}-*.local.json`.
- Final postflight: `obj/task74-shape-parity.local.json` — equal progress columns/FKs/checks/indexes on final Fresh + Upgrade + AuthUpgrade, hash `A6D3B800F7160879C595F2615CACE33422BF371895C5D4D1AFC92FCD35226918`; exact trigger bodies verified by runner; CHECKCONSTRAINTS PASS on all three. No production deployment readiness inferred.
- Unknown/partial package hashes fail closed. Empty rollback removes guards/indexes/tables atomically; populated rollback refuses data loss and requires reviewed forward recovery. No legacy bulk conversion.

Failed validation attempts were retained, not counted as passes. Early setup/harness runs hit SSPI without Windows escalation, absent trial/auth prerequisites, an incorrect `published_at` fixture field, attempted legacy-to-branch RoomType conversion (correctly rejected), missing SYS_ADMIN business permission, EF OUTPUT incompatible audit-pause fixture, existing unique-current-assignment rejection, and @@SPID smallint fixture cast. Fixtures/setup were corrected; no production guard was disabled. Logs: `obj/task74-*-attempt*.local.log`, `obj/task74-progress-diagnostic.local.log`. Attempt7 passed before cancellation additions; attempt9 passed cancellation before the final snapshot compatibility fix; attempt10 is the final complete acceptance receipt. Diagnostic service invocation is not substituted for HTTPS evidence.

## F. Targeted shared regression / build

One final combined targeted regression after new writes: UC013 scoped structure; UC023 Room directory; UC142 RoomType list; UC038 turnover history; UC048 inspection history; UC039/040 published criterion consumer preservation; UC042 real Start/Save private evidence/Finish using the refactored upload helper. All PASS in the final receipt. No duplicate historical full regression credit or UC re-counting.

`dotnet build HotelOps.slnx`: **PASS**, 0 errors. Harness build: **PASS**, 0 errors. Existing NU1900 warnings remain because NuGet vulnerability data cannot load from `https://api.nuget.org/v3/index.json`; no new C#/EF warnings in final build. `git diff --check`: **PASS**; Git emits pre-existing LF/CRLF notices on unrelated dirty files.

`dotnet test HotelOps.slnx --no-restore --list-tests`: exit0, no discovered framework tests. Solution contains only the API project. **Test-framework tests unavailable; no dotnet test credit.** Executed console HTTPS/SQL harness and migration checks are the acceptance evidence.

## G. Blockers / scope boundaries

1. UC012 GBR033 deactivation requires no active rooms/warehouses with stock/open tasks/incidents/requests, plus retained producer coordination. Existing DEV2 location/incident work remains unresolved; no deactivation mutation added. Full physical Branch rowversion contract also not approved in this batch.
2. UC026 GBR042: `MaintenanceWorkService.CreateWorkAsync` currently validates DepartmentId against incident branch without `IsActive`; no accepted common department-deactivation writer protocol. No DEV2 writer modification or unsafe success path introduced.
3. UC028: SQL Room status currently uses coarse DIRTY/CLEANING/INSPECTION/READY/etc.; full SRS Pending Cleaning/Rework/Blocked projection and effective RoomBlock writer coordination not resolved by progress schema approval. Creating items alone does not implement room-status/history side effects.
4. UC016 concurrency blockers and UC029/041/046 notification delivery/retry stay unchanged. Other notification/time-dependent UCs were not used as false independent replacements.

AGENTS ownership preserved: no DEV2-owned source, shared RBAC catalog, generated entity, main DbContext, UC006 gate, app configuration or production/shared DB changes. Approved controlled DEV1 partial mapping only. No commit/push/merge/rebase/reset/clean, branch/worktree switch or deletion. Final preservation hashes/HEAD/index/worktrees in `obj/task74-validation.local.json`; unrelated baseline files remain unchanged.

## H. Classifications

UC032/109 **COMPLETE_VERIFIED_V1** for the entire described progress workflows. They do not require notification or room-status/time transitions in their SRS flows. UC012 **PARTIAL**; UC028/026 **BLOCKED_EXTERNAL**, no implementation success or points claimed for missing workflows. Foundation initialization APIs are not separately counted UCs.

## I. Updated progress

Task73 **24/75,56/256=21.88%** → Task74 **26/75,60/256=23.44% weighted**.
Delta **+2 UC/+4 points**, exact **+1.5625 percentage points**. UC completion count **34.67%**; remaining **49 UC /196 points**. Denominators unchanged. Classification:26 complete (baseline accepted aliases included),9 partial,19 not implemented,21 blocked external=75. Only new COMPLETE_VERIFIED_V1 earns progress.

## J. Actual elapsed time

Recorded preflight start: **2026-10-31T09:33:46.9921535Z** (16:33:46 Asia/Saigon).
Measured interval through final validation/report: **01:09:18 (hh:mm:ss)**; end UTC **2026-10-31T10:43:05.0987400+00:00**.
Includes approval wait, failed attempts and SQL/API/build execution; not uninterrupted coding time. Initial reading before recorded preflight is excluded, so exact elapsed from the user's first Task74 message is **not measurable from available evidence**. Machine-readable timestamps/duration: `obj/task74-validation.local.json`.

## K. Recommended Batch 6

Coordinate and verify DEV2 notification delivery/retry adapter to finish UC029/041/046 separately; do not assume enqueue is delivery. In parallel, approve the shared room lifecycle/effective-block protocol before UC028/044/045, using the new cleaning-item foundation. Obtain explicit Workday/WorkInterval contract before time-dependent cleaning/task UCs. Keep UC006 and UC016 release/deactivation gates unchanged; no further safe 4–5 complete independent batch is promised without those contracts.
