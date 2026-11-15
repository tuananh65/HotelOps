# FASTTRACK-142A checkpoint100 — create operational work from an internal request

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=0545212f76b3a9c00368fb82e7908b1e8ca561d6
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

## Business result

UC102/UC118 now share a usable Create New Operational Task path. From an authorized request, the manager receives current source prefill, completes task criteria/outcome/timing and explicitly chooses Required or Optional. The Tasks screen also offers a scoped request selector and Save and Assign. Task, immutable definition/criteria, explicit source/link metadata, audit and request version commit together. Creation never closes the request.

Current DEPT_MANAGER, IR_PROCESS, exact user/branch/current receiving-department mapping and IR_VIEW are checked independently from target TASK_MANAGE/TASK_VIEW. Both transaction gates revalidate authority, including idempotent retries after routing. Missing capability/mapping fails closed. No authority is inferred from User.DepartmentId or Department.ManagerUserId. Source If-Match, CSRF, Serializable transaction and stable creation keys remain enforced. Manual creation JSON/hash compatibility has a regression test; generated entities and shared configuration are unchanged.

## Direct evidence

[Final trial3](checkpoint100-trial3-runtime.json) reports success=true. Real Edge → trusted HTTPS → disposable SQL verifies request-page prefill/creation/reloaded Required link and Tasks source selection → Optional link → Save and Assign navigation. SQL confirms both persisted links, source provenance and unchanged request status. The trial checks missing mapping, missing target permission, cross-branch denial, CSRF, required If-Match, explicit completion metadata, stale version, same-key retry, changed-payload conflict, concurrent creation (201/412), route reauthorization and injected-failure rollback of task/definition/criteria/link/audit/source revision.

[Cleanup](checkpoint100-trial3-cleanup.json) confirms DROPPED_AND_BACKUP_REMOVED. The runtime report's cleanup=false means whole-clone disposal was delegated to the runner; the separate cleanup receipt is authoritative. Its inherited outer scope label says service-only; the nested linkedCreationBrowser and matching SQL assertions provide the actual public-path evidence. Raw evidence is not rewritten.

Trial1 failed because the synthetic fixture assumed NORMAL priority existed. The fixture now creates it only when absent and rejects an incompatible/inactive existing entry. Trial1 cleanup initially hit the other-session guard; a later identity-checked cleanup succeeded. Trial2 passed the request-page path and cleanup. Trial3 additionally verifies the final Tasks source selector. No SQL constraint, FK, security guard or timeout was relaxed.

Validation executed on final source: dotnet build HotelOps.slnx PASS (0 warnings/errors); dotnet test HotelOps.slnx exits0 but discovers no test project; focused actual regression project 66 PASS; frontend focused transport/history browser tests 25 PASS; TypeScript and production build PASS. Vite still reports upstream use-client directives and a large bundle. ModelProbe has the existing three EF1002 synthetic-fixture warnings. npm ci was not rerun: dependencies and lockfile did not change.

## Remaining acceptance

WORKFLOWS_COMPLETED=[ad-hoc operational task creation from an internal request, explicit completion requirement, durable link and assignment handoff]
UI_API_SQL_E2E_VERIFIED=[request screen Create New; Tasks optional source selector; Required/Optional persistence; Save and Assign navigation]
EXACT_REMAINING_GAPS=[UC118 cleaning/incident/material targets and legacy-link reconciliation; UC102 recurring series linked to requests has unresolved JOB05/UC120 policy; UC117 remaining full acceptance; UC119 handover and UC120 closure dependencies; JOB06 finite archive policy/implementation]
NEW_BLOCKERS=[linked recurring business contract awaiting decision; no new blocker for the delivered ad-hoc path]
NEXT_BATCH=[finish UC117 full acceptance; connect remaining UC118 target types; implement UC120 only after mandatory-work and handover dependencies are authoritative]
SPRINT_TIME_REMAINING=approximately21h42 at2026-11-15T20:08Z; original deadline2026-11-16T17:50Z unchanged.

No whole UC was newly completed. Prior C71 recurrence and C99 Link Existing evidence remains historical, not newly executed evidence. The sprint continues; no shared or production migration, seed, deployment or SQL mutation occurred.

Changed areas: task DTO/service/persistence/controller; canonical task and request creation UI; focused regression tests; isolated runner/browser/probe; matrix and this receipt. No generated EF entity or new production schema change.
