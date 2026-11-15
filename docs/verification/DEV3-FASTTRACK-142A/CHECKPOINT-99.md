# FASTTRACK-142A checkpoint99 — link existing operational work

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=5ec2e0f990ec2ccafa5a489b0d145d8f60d35f45
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

## Business result

UC118 now has a usable Link Existing Operational Task path in the canonical internal-request screen. The manager selects a currently visible open task in the same branch and explicitly chooses Required for completion or Optional. Successful save persists that choice and audit, advances the existing request ETag and reloads the linked task with its canonical destination. Linking does not close the request or rewrite the task's original SourceType/SourceId.

The service requires current session, IR_VIEW on the request, active DEPT_MANAGER plus IR_PROCESS and the exact current user/branch/receiving-department mapping. Target TASK_VIEW is checked independently using existing global/branch/own grant semantics and assignment/verified-closed ownership. Request read permission cannot disclose inaccessible task titles, identifiers, state or counts. Expiring scope is rechecked before returning the page. The canonical task workflow projection determines whether a target is open; physical TODO/COMPLETED and unknown state never substitute for verified workflow state.

Serializable request/authority/target reads protect the link, audit and request version update. Stale writes return412; duplicate links return409. The public endpoint retains HTTPS, authenticated session, global CSRF and strong If-Match. UI validates response identity and canonical target paths, clears stale/private results, and prevents duplicate submits.

## Narrow schema impact

`InternalRequestTaskLinksIsolated.sql` adds only `internal_request_task_links` with a composite request/task primary key, mandatory bit without a default, and trusted non-cascading request/task/audit FKs. It does not change generated EF entities, original status CHECKs, task creation provenance, or shared schema. Installation requires exact server/database/GUID/run marker and existing request concurrency capability; repeated installation checks compatibility.

The table is specifically operational-task metadata, not an authoritative registry of every work type. UC120 must not interpret an empty/partially visible list as completion eligibility. Legacy source links and other work types still require reconciliation. The new audit reference must be included in the archive-before-purge design; do not delete it to enable JOB06. Rollback of schema installation is safe only after proving there are no historical links, otherwise retain and review a migration. No production migration or seed was executed.

## Direct execution evidence

[Trial3](checkpoint99-trial3-runtime.json) has success=true and cleanup=true. Its nested `taskLinkBrowser` records genuine Edge → trusted HTTPS → SQL success: explicit completion choice, persisted link, reload and stale retry rejection. SQL verifies mandatory/optional values, audit provenance, unchanged task source and unchanged request lifecycle. Negative cases cover cross-branch target, cancelled/unknown workflow, missing department mapping, OWN target permission without ownership, revoked target scope, duplicate and stale links. Concurrent writes produce200/412; an injected link-insert failure rolls back relationship, audit and request version. The reused authority scenario verifies wrong department, route reauthorization, revoked role/grant, mapping/route concurrency and transaction rollback.

The outer trial `scope` string is the inherited generic department-authority label and predates this nested browser scenario. It is retained unchanged as raw evidence; the nested browser result and matching SQL assertions are the direct evidence for this batch's public path.

[Trial1](checkpoint99-trial1-runtime.json) failed during synthetic fixture setup because the existing workflow CHECK did not allow CANCELLED; cleanup passed. The fixture was corrected to leave the physically cancelled task without an authoritative workflow row, which the new path denies. No CHECK was relaxed.

Trial2 failed before HTTPS startup with `DatabaseInaccessible`, and a subsequent preflight attempt timed out while verifying disposable identity before any mutation. Read-only diagnosis returned the expected server/name and GUID, then timed out reading the run extended property. These environment failures remain unresolved; no SQL restart, timeout relaxation or identity bypass was used. Trial3 later passed unchanged identity gates.

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS0 warnings/errors after sandbox-only NuGet.Config access failure was rerun with required access; dotnet test HotelOps.slnx exits0 without discovered tests; [focused backend](checkpoint99-backend-tests.txt)54 PASS; [frontend](checkpoint99-frontend-tests.txt)21 PASS; TypeScript and production build PASS. ModelProbe retains3 pre-existing EF1002 credential-fixture warnings; Vite retains use-client and large-chunk warnings. Initial new browser tests found inaccessible select labels; explicit aria-label corrections passed all21 tests and the real browser trial.

WORKFLOWS_COMPLETED=[UC118 Link Existing Operational Task slice with explicit mandatory metadata and current department/target authority]
UI_API_SQL_E2E_VERIFIED=[select open task → choose completion requirement → save → reload persisted link; stale retry denied]
EXACT_REMAINING_GAPS=[UC118 Create New and cleaning/incident/material targets; legacy link reconciliation; UC117 remaining evidence/actor/deadline acceptance; UC119 handover and UC120 mandatory-work/escalation closure; UC102 linked creation; JOB06 finite archive policy and implementation]
NEW_BLOCKERS=[intermittent SQL metadata/startup availability persists; no new business decision required for this implemented manager path]
NEXT_BATCH=[connect UC102 Create New Operational Task to explicit request authority/link metadata atomically; implement remaining UC118 target adapters; complete UC117 acceptance and UC120 dependencies]
SPRINT_TIME_REMAINING=approximately22h at2026-11-15T19:49Z; original deadline2026-11-16T17:50Z unchanged.

No whole UC is newly COMPLETE_VERIFIED. Consecutive partial checkpoints added actual progress/results, private evidence and now an operable linked-work slice; all remain dependencies of business closure, not completed UCs. Sprint remains active.

Changed source: InternalRequestTaskLinks service/controller/DI, canonical TaskLinksPanel/HistoryPage, guarded additive SQL, owned scenario/browser and focused tests. Matrix and raw execution evidence accompany this logical implementation batch.
