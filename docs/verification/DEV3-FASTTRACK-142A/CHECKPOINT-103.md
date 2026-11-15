# FASTTRACK-142A checkpoint103 — link existing cleaning work

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=1eb1821e4206e50a89b79cbf7223a54840737429
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

UC117/118 now display explicitly linked cleaning work and offer Link Existing in the canonical request screen. The user selects a currently visible open cleaning task in the request branch and explicitly marks Required or Optional. Link, audit and existing request row_version update commit in one Serializable transaction; no cleaning lifecycle or request closure is changed.

Request access requires current session/IR_VIEW. Writing additionally requires active DEPT_MANAGER, IR_PROCESS and the exact current receiving-department mapping. CLEANING_VIEW is independently checked with the existing cleaning-execution global/branch/current-own-assignment semantics; room and branch must be active. No IR_VIEW/IR_PROCESS grant substitutes for target access. Candidates use only known open cleaning workflow_status values from the existing trusted CHECK: PENDING_ASSIGNMENT, ASSIGNED, IN_PROGRESS, ON_HOLD, PENDING_INSPECTION and REWORK_REQUIRED. Unknown/terminal states are not admitted. Final authorization and visible counts are rechecked before returning results. HTTPS, CSRF and canonical If-Match guards remain enforced.

## Narrow isolated schema

InternalRequestCleaningLinksIsolated.sql adds only internal_request_cleaning_links with composite request/cleaning-task PK, explicit mandatory bit and trusted non-cascading request/task/audit FKs. Exact server/database/GUID/run identity and request concurrency capability are prerequisites. The trial installs it twice to verify idempotency. Generated entities, original status CHECKs and shared/production schema are unchanged. Missing capability returns503.

The new audit FK participates in JOB06 archive-before-purge requirements; it must not be removed or cascaded to force purge. This table is not a complete UC120 closure registry. Legacy sources, incident/material links and other dependencies still need reconciliation. Any future rollback must prove no historical links exist or retain/review the table; no receipt deletion is authorized.

## Executed evidence

[Trial2](checkpoint103-trial2-runtime.json) success=true. Its nested cleaningLinkBrowser shows real Edge → trusted HTTPS → disposable SQL: select open cleaning, choose Optional, save, reload durable link with canonical target path. SQL verifies mandatory=false and actor audit; the request stays IN_PROGRESS. API/SQL cases cover foreign branch, Completed target, OWN permission without assignment, revoked current management mapping, duplicate link, concurrent200/412, and injected insertion failure rolling back audit/request version. Existing task-creation/progress/handler flows also passed as regression.

[Cleanup](checkpoint103-trial2-cleanup.json) proves DROPPED_AND_BACKUP_REMOVED. Trial1 failed because no active CLEANING_TYPE fixture existed; the scenario now adds a unique synthetic reference only when no active type exists. Its clone/backup were also removed. No existing reference was overwritten and no security guard relaxed. Runtime cleanup=false delegates whole-clone disposal; the separate receipt proves cleanup. The outer scope string remains the inherited generic label; nested browser/API/SQL checks are the public-path evidence.

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS0 warnings/errors; solution test exits0 without discovering a test project; focused actual FE13 tests54 PASS; frontend canonical regression21 PASS; typecheck/production build PASS. ModelProbe retains3 existing EF1002 fixture warnings, Vite use-client/large-chunk warnings. No dependency/lockfile changes; npm ci not rerun.

WORKFLOWS_COMPLETED=[UC118 Link Existing Cleaning Task with explicit completion requirement and scoped traceability]
UI_API_SQL_E2E_VERIFIED=[select open cleaning task → choose Optional → save → reload persisted link; rollback and race checks]
EXACT_REMAINING_GAPS=[UC118 Create New cleaning using UC028, incident/material links and legacy reconciliation; UC117 incident/material linked-work visibility; UC119 handover and UC120 authoritative mandatory-work closure; linked recurring decision; JOB06 archive]
NEW_BLOCKERS=[]
NEXT_BATCH=[connect Create New cleaning through UC028 with atomic link metadata; implement incident/material linked-work adapters; complete closure dependencies]
SPRINT_TIME_REMAINING=approximately21h29 at2026-11-15T20:21Z; original deadline2026-11-16T17:50Z unchanged.

Changed source: cleaning-link service/controller/DI, canonical CleaningLinksPanel/HistoryPage, guarded additive SQL, disposable scenario/browser/runner, matrix and receipt. No whole UC newly COMPLETE_VERIFIED. Sprint remains active.
