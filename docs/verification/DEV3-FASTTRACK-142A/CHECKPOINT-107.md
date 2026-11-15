# FASTTRACK-142A checkpoint107 — incident links and Sent/Received

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=bb348f3e164ea1c35c6fd1ffdccfa3735d90ea2d
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

## Business behavior delivered

UC118 Link Existing Incident now operates from canonical Internal Request history. Candidates intersect current IR_VIEW, exact receiving-department DEPT_MANAGER/IR_PROCESS authority and independent IncidentReadAccess (GBR165). No target permission is borrowed from IR_PROCESS. Open/nonterminal incidents can be linked with an explicit Required/Optional choice; Closed/Cancelled are excluded. The request remains In Progress. The request's existing row_version, session/CSRF/If-Match guards and Serializable transaction protect link + audit + request version atomically. The saved link opens the independently authorized canonical incident detail. Target role revocation removes link metadata from subsequent reads.

The additive four-column incident-link table preserves existing related_incident_id provenance and generated entities. Its primary key prevents duplicate links; restrictive FKs protect request, incident and audit references. The idempotent SQL script runs only against a verified disposable clone, with exact instance/database/GUID/run guards. It is not deployed to shared SQL. JOB06 must retain referenced audit until approved archive/retention migration; no cascade, disabled FK or purge activation is introduced. This explicit link list is not yet a UC120 closure predicate, and legacy links still require reconciliation.

The approved UC123 direction contract is implemented in API and canonical UI. Sent selects RequesterId. Received selects the unique active current handler OR a currently eligible DEPT_MANAGER with IR_PROCESS and explicit mapping to the current receiving department. Both operate over the existing IR_VIEW-scoped query before count/pagination. No department inference, duplicate union rows or read grant from processing authority. Receiving-department, mapping, assignment and grant changes affect the next read. Serializable reads plus final authority comparison protect disclosure; unavailable mapping capability for an eligible manager returns503 rather than guessing.

## Direct evidence

[Final trial3](checkpoint107-trial3-runtime.json) success=true includes real Edge -> trusted HTTPS -> SQL Sent/Received selection and Link Existing Incident -> Required saved -> target detail. Direction checks cover cross-branch exclusion, overlap once in each list, routing, current/former handler, revoked mapping, revoked IR_PROCESS, missing IR_VIEW and restored access. Link checks cover explicit mandatory input, CSRF, If-Match, unrelated/cross-branch target, revoked mapping, closed target, injected insert failure with audit/version rollback, stale412, duplicate409, persisted audit and unchanged request status. Concurrent same-version POSTs return one200/one412 and persist exactly one optional link.

[Trial3 cleanup](checkpoint107-trial3-cleanup.json) proves clone and backup removed. [Trial2](checkpoint107-trial2-runtime.json) passed incident-link workflow before direction implementation. [Trial1](checkpoint107-trial1-runtime.json) failed the browser's exact-text selector on the Required label after a successful POST; the selector was corrected, without changing authorization or bypassing a business assertion. All3 cleanup receipts confirm removal. Runtime cleanup=false delegates whole-clone cleanup to the separate receipt. The inherited top-level scope string is generic department-authority text; requestDirectionCheck/incidentLinkCheck/incidentLinkBrowser entries are the actual evidence.

REGRESSION_STATUS=backend build PASS0 warnings/errors; solution test exits0 without discovering a test project; actual FE13/Incident backend64 PASS; frontend production build/typecheck PASS. ModelProbe retains3 existing EF1002 credential-fixture warnings; Vite use-client/large-chunk warnings remain. Frontend history suite28 PASS, including explicit mandatory selection and stale incident-link no-replay; focused new browser case also1 PASS. An initial append used an incorrect relative path; the resulting zero-match test process was stopped, not counted as PASS. Dependencies unchanged; npm ci not repeated.

WORKFLOWS_COMPLETED=[mapped manager links visible existing incident with Required/Optional and audit; canonical Sent/Received with current responsibility and independent read scope]
UI_API_SQL_E2E_VERIFIED=[both workflow paths in trial3; fault rollback and concurrent200/412]
EXACT_REMAINING_GAPS=[UC118 Create New Incident/material; legacy linked-work reconciliation; UC123 material visibility; UC114 creation, UC119 handover and UC120 complete-request lifecycle; JOB06 archive/retention]
NEW_BLOCKERS=[]
NEXT_BATCH=[material target authorization/read adapter; highest-readiness FE13 lifecycle closure; reconcile exact UC117/118/123 acceptance]
SPRINT_TIME_REMAINING=approximately20h46 at2026-11-15T21:04Z; deadline2026-11-16T17:50Z unchanged.

No whole-UC upgrade: this batch closes two operable missing workflow paths, with direct persistence/security evidence. Existing23 COMPLETE_VERIFIED UCs are historical acceptance, not re-executed here. No production-readiness claim.
