# FASTTRACK-142A checkpoint95 — completed Route/Assign/Reassign workflow

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=3ad281494fc0b7e6a9e9597224273749443c66b7
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[UC115]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121
SHARED_DB_MUTATION=NO

## Business result

UC115 now meets its scoped acceptance criteria; see [criterion map](UC115_ACCEPTANCE.md). The canonical multi-actor workflow is source-manager Route → destination-manager Accept → Assign → Reassign → new-handler notification/source. UI history displays the four events and ended/current assignment provenance. SQL preserves actual actors, four audits and exactly one current handler. Actual hosted worker delivers exactly seven expected recipient/event receipts without duplicates after another scan. Notification source authorization and processing authorization remain distinct.

Fixed a real UI defect: successful first assignment previously left the decision header displaying ACCEPTED until manual reload. AssignmentPanel now supplies the validated committed version to DecisionPanel, which updates the matching current-authority request to IN_PROGRESS. No optimistic update before server success and no authentication/CSRF/ETag relaxation.

## Executed validation

Final [trial3](checkpoint95-trial3-runtime.json): success=true, cleanup=true, trusted Edge → HTTPS → exact disposable SQL. Trial2 also passed. Trial1 failed while loading the source manager request before Route and cleanup passed; intermittent navigation/read timing remains unresolved. A read-only DMV observation during trial2 saw short update-lock waits of40/287ms, insufficient to establish the failed trial's cause.

REGRESSION_STATUS=solution build PASS (0 warnings/errors); solution test exits0 without discovered tests; focused FE13 backend29 PASS; frontend15 PASS; TypeScript and Vite production build PASS. ModelProbe builds with three existing EF1002 credential-probe warnings. Existing Vite use-client/chunk warnings remain. No dependency/security downgrade.

WORKFLOWS_COMPLETED=[UC115 Route/Assign/Reassign with exact department authority transfer, history, audit and automatic notifications]
UI_API_SQL_E2E_VERIFIED=[source manager → destination manager → current handler; pending assignment409; stale reassignment412; accepted Route409; unmapped handler processing403]
EXACT_REMAINING_GAPS=[UC114 creation; UC117 progress/results; UC120 completion; UC118 linked operational work; UC123 remaining filters/withdrawal; JOB06 archive and physical outbox retention]
NEW_BLOCKERS=[]
NEXT_BATCH=[UC117 progress/results storage and UI/API; UC120 completion guards and E2E; UC118 linked-work authority and completion metadata]
SPRINT_TIME_REMAINING=approximately22h49m at2026-11-15T19:01Z; deadline2026-11-16T17:50Z unchanged.

CHANGED_FILES=AssignmentPanel.tsx; DecisionPanel.tsx; internal-request-history-browser.test.mjs; sprintInternalRequestLifecycleBrowser.mjs; SprintInternalRequestWorkerScenario.cs; SprintInternalRequestLifecycleScenario.cs; acceptance map, matrix and execution receipts. No generated entities, shared schema, dependencies or production configuration changed. Sprint remains active.
