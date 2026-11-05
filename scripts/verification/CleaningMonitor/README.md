# Task75 continuation — selective recovery and UC037 read-only implementation

Ngày: 03/11/2026, Asia/Saigon. Baseline accepted Task74:26/75 UCs,60/256 points=23.4375%. Scope changed by explicit developer approval: only one shared edit, `AddScoped<CleaningMonitorService>()` in Program.cs; preserve all existing registrations/middleware, DbContext/schema, shared authorization and DEV2 code.

## 1. Exact recovery / integration

[RECOVERY_RESULTS.json](../Task75Review/RECOVERY_RESULTS.json) is the exact path/blob/hash record. Original recovery inventory226 paths was compared with the working tree: zero drift. Recovered15 missing non-shared source files:14 DTOs and HotelStructureCreationService, all initially byte-identical to cf8fb02. No existing source file was overwritten. The recovered service is unregistered; restoring it is not new UC014 completion or runtime exposure.

Also recovered missing DEV1_21_DAY_DELIVERY_PLAN.md from cf8fb02 and minimally updated its current snapshot and UC037 row for this continuation; historical Task74 sections remain. Recovery inventory now includes16 paths;210 remain deferred. New CleaningMonitor service/controller/DTO and verification project are implementation files, not snapshot restores.

Program.cs received exactly the approved single service registration relative to its pre-existing fallback patch. Concrete dependencies are HotelOpsDbContext, ICurrentUserAccessor, IPermissionService, ISqlSessionService and TimeProvider, all already registered. Production HTTPS requests successfully instantiate and execute the service, proving runtime DI resolution without a dependency cycle.

Deferred: existing source edits; all partial DbContext mappings, optional model/schema packages, PermissionGrantRules/PermissionService changes; controllers/services requiring those mappings or DI; UC006 internal readiness; existing verification harness changes and historical documentation not needed by this slice. No blanket restoration, shared schema authorization expansion or controller with missing DI was introduced.

## 2. DEV2 dependency separation

[DEV2_HANDOFF.md](DEV2_HANDOFF.md) contains the local proposed notification adapter, real delivery/retry/idempotency/failure acceptance and room lifecycle/effective-block writer coordination. UC029/041/046 remain PARTIAL; UC044/045 remain NOT_IMPLEMENTED. No assignment core was reimplemented and no notification delivery was fabricated.

## 3. Independent UC selection

Selected safe implementation slice: **UC-037 Monitor Cleaning Tasks — GET list/detail only**, weight2. Full UC remains PARTIAL because its mandatory Cancel flow recalculates room state and notifies the assignee. The read subset is explicitly not treated as an independently completed UC.

No four fully independent completable UCs were identified within the current constraints. Exact SRS flows were checked, not inferred from titles:

- UC030/031/033/034/035/036 require Workday/WorkInterval or room lifecycle effects; UC035 explicitly opens an interval after validating Working workday.
- UC022 includes deactivation/reactivation, active parent rules, open incidents/assets/blocks and room status recalculation; shared writer/lifecycle coordination is not resolved.
- UC044/045/047 require current inspection/rework cycles, effective room blocks and mandatory notification.
- UC103 includes notification, recorded-time cancellation guard and stop-series semantics. UC107 includes Decline notification; Accept-only is not full completion.
- UC138 includes employee notifications; shift-definition CRUD alone is not full completion.
- UC114/115/116/117/123 include notification, with UC116 response notifications and UC123 withdrawal notification confirmed directly in SRS.
- Other ledger items remain blocked by existing schema/workflow/contract or release gates. UC006/UC016 were not selected. Do not reinterpret unavailable contracts as silent requirements to grant ASSUMED_V1 completion.

## 4. Production behavior / ASSUMED_V1 details

- GET `/api/branches/{branchId}/cleaning-monitor`
- GET `/api/branches/{branchId}/cleaning-monitor/{kind}/{id}`, kind=cleaning|rework.

HTTPS, normal validated cookie/SQL session and actual branch/global CLEANING_VIEW grant required. OWN-only is not promoted to supervisor branch access; SYS_ADMIN/OWNER do not bypass catalog grants. Branch authorization occurs before count/data lookup, foreign IDs remain scoped and invalid sessions/restrictions retain existing middleware behavior.

List includes cleaning/rework kind and typed ID, room/floor, raw recorded status, optional stored workflow state, priority, current assignee, due/overdue, latest progress/blocker and description. Queries use only existing tables and optional Task73 columns discovered through read-only metadata; no shared EF mapping changes. No write-back during reads. Rework deadline is null/overdue unknown on legacy schema without its due_at; never borrow a parent deadline or relabel a coarse status as an approved SRS state.

Filters: inclusive hotel-calendar FromDate/ToDate, floor, recorded/workflow status, assignee, priority, overdue, kind and literal keyword search; default today in Asia/Ho_Chi_Minh, default20/max100 page size. ASSUMED_V1 bounded date range366-day difference; cleaning occurrence uses scheduled-start then creation, rework uses requested-at. Due ascending, null last, ID/kind tie-breaker. For nonterminal rows, unknown due is excluded from both overdue filter values; terminal rows are always not overdue per GBR055. These technical query conventions do not finalize room lifecycle business states.

Details show parent cleaning assignment/progress history and a minimal audit projection, each latest100 with total counts. Rework progress is not invented: item progress stays null, and the detail's progress timeline is the linked cleaning task's history. No raw EF entities, audit JSON payloads, credential fields or private attachment keys are returned. Assignment/cancel action links to unavailable routes are not fabricated.

Missing/invalid filter400, absent or out-of-branch source404 after branch authorization, missing scope403, session401; unsupported transient SQL/schema failures503. All responses use no-store. No Cancel route exists. Shared enum/status/catalog values remain unchanged.

Security/read consistency: own SERIALIZABLE read transaction, SQL session checks and permission checks before and after queries. This retains scope reads across count/page/detail. No SaveChanges or business/audit mutations occur; normal authentication/session middleware may touch its session as before. A business audit event is not added for GET.

## 5. Per-UC real HTTPS / SQL evidence

Accepted receipt: [task75-accepted.local.json](task75-accepted.local.json), ignored local evidence; **120 checks PASS** on `HotelOps_Canonical_20261103_075634_d5401ae2_Fresh`. Evidence includes mixed bootstrap/problem/SQL assertions, not120 independent UCs or all possible HTTP scenarios.

Harness reuses the existing UserDirectory guarded preparation and HTTPS/cookie patterns: server+DB+trial extended-property check, empty personnel/business precondition, existing guarded Task26 user-rowversion prerequisite; launches actual API assembly through production Program.cs with a pinned ephemeral HTTPS certificate. Password/cookies/PFX password remain in memory; certificate file belongs to the invocation and is removed. No HotelOpsDB connection, SQL mock or fake notification adapter.

Verified: real CSRF/login/SQL sessions for six identities; anonymous401; missing grant/cross-branch/admin-no-bypass/OWN-only/restricted403; list/count/paging/empty/search/assignee/kind/status/overdue/floor isolation; invalid/date/injection-like filters400; detail/parent assignment/progress/blocker/audit; missing/foreign source404; no Cancel endpoint; revoked membership/role403 and SQL session401. Exact business/audit fingerprints unchanged by read requests; master/catalog fingerprint unchanged by the suite.

Legacy schema branch is exercised. Optional Task73 workflow_status/rework due_at query branch is **NOT VERIFIED**; no schema was added to manufacture that coverage. HTTP cancellation behavior and full timeline pagination are **NOT VERIFIED**; current timeline has a documented bounded latest100 window.

## 6. Concurrency / rollback

Real competing SQL transaction temporarily updates a cleaning task. DMV confirms actual HTTPS reader waiting behind that writer. After writer rollback, response200 with original IN_PROGRESS state; uncommitted cancellation never leaks. No simulated sleep hook substitutes for SQL wait evidence. Read-only endpoint performs no business mutation, so no business-write rollback claim is made; Cancel/lifecycle transaction acceptance remains absent.

## 7. Build and shared regression

- `dotnet build HotelOps.slnx`: PASS,0 errors,2 NU1900 warning entries.
- CleaningMonitor harness build: PASS; NU1900 vulnerability-feed warnings remain.
- Actual HTTPS/SQL harness:120 checks PASS.
- One shared regression set after implementation: Authorization production fallback93; PermissionService43; CurrentUserAccessor14; ResourceAuthorization49, all PASS. These runners use their documented isolated doubles; they do not replace the real SQL acceptance above. Logs in obj/task75-*-regression.local.log.
- `dotnet test HotelOps.slnx`: exit0; no framework test project/cases discovered in API-only solution. Framework tests **NOT AVAILABLE**, not PASS.
- `git diff --check`: final result in TASK75_VALIDATION.json.

No regression claim is made for unrecovered Task74 endpoints. Restored DTOs/service compile in the real API, but the recovered unregistered Floor/Area service is not certified through HTTP in this continuation.

Intermediate failures retained/disclosed: normal sandbox SQL setup failed SSPI; approved Windows-identity execution succeeded. First fixture used an unsupported audit action and hit existing CK_audit_logs_action; corrected to existing CREATE, failed trial retained, new trial created without reset. One orchestration check stopped after successful trial setup because it read stale LASTEXITCODE; harness then ran on that still-unused trial. No source/schema guards disabled and no failed result counted as acceptance.

## 8. Classifications and progress

UC037: NOT_IMPLEMENTED → **PARTIAL**. No new COMPLETE_VERIFIED_V1 UC. Task75 target4–5 additional verified UCs **not achieved**; remaining full-flow contracts are documented instead of awarding partial credit.

Updated totals: **26 complete,10 PARTIAL,18 NOT_IMPLEMENTED,21 BLOCKED_EXTERNAL=75**. **26/75 UC;60/256 points=23.4375%**, delta0UC/0points/0percentage points, remaining49UC/196points. The original review reconciliation JSON remains the historical baseline; updated delivery plan and UC_LEDGER_TASK75.json reflect this continuation.

## 9. Timing / preservation

Continuation preflight start: `2026-11-03T14:06:49.7348710+07:00`; final measured end/duration, exact created paths and preservation checks are recorded in TASK75_VALIDATION.json. Review-only8m15s from the prior turn is separate. Elapsed includes approval, failed attempts, schema prerequisite setup, build, real HTTP/SQL and documentation; it is not measured throughput for full UC completion.

HEAD/index/four worktrees and unrelated changes preserved. Only authorized existing source edit is one Program.cs DI line; pre-existing fallback/OpenAPI/Health/WeatherForecast patch remains. No generated entity/DbContext/shared authorization/DEV2 source or release script changed. Three disposable new trials were created; failed and successful trials retained. Final review corrected the GBR055 terminal-without-deadline case and verified it on the third fresh trial; intermediate115-check acceptance remains historical evidence. No main/shared DB access or Git mutation.

## 10. Recommended next batch

Resolve/review DEV2 notification adapter and shared room lifecycle protocol, then complete UC029/041/046 and UC044/045 from existing snapshot foundation without repeating assignment core. Meanwhile selectively review the dependency-closed baseline restoration of services/controllers/mappings/DI; do not apply210 deferred paths wholesale. UC006/UC016 gates remain intact.

No additional shared approval is inferred beyond the single accepted DI line. If future work needs mappings, status/schema changes or DEV2 writers, prepare the exact patch and owner approval first.
