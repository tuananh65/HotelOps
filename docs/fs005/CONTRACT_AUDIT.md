# Contract and security audit

Reviewed current controller/service/DTO, Program DI, PermissionService/EfPermissionDataSource, HotelOpsDbContext cleaning mappings (assignments/progress/rework/tasks), generated entities, frontend screens and existing transport. Requirements: SRS FE04 UC028–038, especially UC037; Change Notes remain pending for Reception/Department Head. FS002/003/004 and UC_LEDGER_SUMMARY distinguish historical credit from current runtime. Source priorities and immutable Codex A handover references are retained in docs/fs002/README.md and docs/fullstack-integration/HANDOVER_READ_MANIFEST.json.

Only source change in controller: remove NonController under explicit user approval. Exactly two declared public action methods, both HttpGet. No anonymous attribute, added action, DI/security/config/schema/grant changes. Existing Program scoped registration resolves through actual HTTPS startup and successful list/detail. Regression inspects MVC ActionDescriptors, exact routes, cookie scheme and absence of route duplicates. Other four pending recovery controllers remain undiscovered.

| Method | Active route | Input / output |
| --- | --- | --- |
| GET | /api/branches/{branchId:long}/cleaning-monitor | CleaningMonitorQuery / CleaningMonitorPage |
| GET | /api/branches/{branchId:long}/cleaning-monitor/{kind}/{id:long} | cleaning or rework + positive ID; CleaningMonitorTimelineQuery / CleaningMonitorDetail |

Read gate: HTTPS; protected Cookie ticket; actual actor/session IDs match, restricted fails closed; SQL session validation and EvaluateBranchAsync(CLEANING_VIEW, branch). EvaluateBranchAsync validates active user/branch and existing SQL permission/access paths. OWN is never promoted to branch access. Serializable read transaction retains session/permission locks across counts/page/detail and revalidates before commit. No entity response, business mutation, new audit or notification. Authentication/session middleware retain normal behavior including session touches.

SQL: fixed identifier fragments chosen by COL_LENGTH metadata; values are SqlParameters. Branch predicates apply to both UNION arms. Rework inspection/room/cleaning lineage joins agree. Read-only EF timelines use AsNoTracking, async and cancellation. Task date uses scheduled_start_at or created_at; rework requested_at. Maximum date span366 days, search150 chars, status ASCII alphanumeric/underscore, page arithmetic guarded. Sort due null-last then due/id/kind. Optional schema SQL errors become503, not fabricated defaults of readiness. Null overdue remains unknown. Displayed percent is latest persisted log, not a state transition.

Responses: 200 DTO, 400 filter/HTTPS, 401 invalid session, 403 permission/scope, 404 absent scoped item, 503 dependency/query unavailable. Empty result is200. Global/view grants do not create own-task discovery or execution grants. Monitoring audit/timeline exposure uses the existing approved CLEANING_VIEW semantic; no new permission binding introduced.

Limitations: existing query uses TOP1 active assignment and does not diagnose duplicate active assignment integrity; latest cleaning log/parent timelines are not rework-specific execution state. No new scale/performance, revocation-under-lock race, or every optional-schema permutation acceptance claimed. No current-cycle/readiness inference. Source review found no active create/start/complete/reassign cleaning HTTP writer; assignment/rework request endpoints exist but do not supply full lifecycle/notification acceptance.
