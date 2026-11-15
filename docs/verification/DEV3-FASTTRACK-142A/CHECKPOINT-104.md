# FASTTRACK-142A checkpoint104 — create cleaning work from a request

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=a8d759b46dddd6e3b59180eb2d0915dbc75ef0fa
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

UC118 now opens the existing UC028 form with authorized room, priority, instruction and deadline prefill. The user explicitly selects Required or Optional and uses Save or Save and Assign. Source room may be absent: the form asks for a room in the request branch. Source context is loaded from the server; URL parameters cannot supply authority or a trusted version.

The existing cleaning creation transaction is extended with source request version validation, link and audit. CLEANING_ASSIGN is checked independently from current IR_VIEW, IR_PROCESS and exact receiving-department mapping at both BatchAsync gates. All existing active-room/reference/checklist/current-cycle/open-task guards remain. Task, cycle pointer, readiness event, task audit, request audit/link and request row_version commit together. Creation never closes the request. No new schema beyond C103 link capability, generated entity change or alternate cleaning workflow.

The optional source DTO requires both request and canonical8-byte version plus explicitly supplied mandatory boolean. Linked POST requires matching If-Match. Stale retry returns412 rather than creating a second cycle; there is no automatic retry. Existing standalone DTO transport remains unchanged. Canonical form binds loaded context to current auth generation, aborts stale work, prevents duplicate submission, clears uncertain linked context and offers reload. Missing target/source authority produces MSG02 with the Link Existing/responsible-role alternative.

## Direct execution

[Trial1](checkpoint104-trial1-runtime.json) success=true. Real Edge → trusted HTTPS → disposable SQL follows Request → Create New cleaning → prefilled UC028 → explicit Required → Save and Assign → reload source link. SQL verifies new cleaning task/current cycle/mandatory link and audit. Tests reject missing CSRF/If-Match, target permission loss, missing source mapping and stale source. An injected link-insert failure rolls back task/cycle/readiness/link/audit/source version. Replaying the old source version after success cannot create a second task. The request stays IN_PROGRESS.

[Cleanup](checkpoint104-trial1-cleanup.json) confirms DROPPED_AND_BACKUP_REMOVED. Runtime cleanup=false delegates whole-clone disposal; the separate receipt proves disposal. The inherited generic outer scope label does not describe the nested browser evidence. Synthetic catalog/grant/room/item changes were confined to the independently guarded clone. Shared/production SQL was not changed.

After this trial, final frontend-only refinements made prefill403 show MSG02 and explained requests with no room; the successful creation path was unchanged. Final frontend typecheck/build and focused tests were rerun. No second SQL trial is claimed for those message changes.

REGRESSION_STATUS=solution build PASS0 warnings/errors; solution test exits0 without discovered test project; focused actual cleaning/request backend65 PASS including omitted completion metadata and invalid source pairing; frontend26 PASS including linked source If-Match/no-retry and standalone compatibility; typecheck/production build PASS. Existing ModelProbe3 EF1002 fixture warnings and Vite use-client/large-chunk warnings remain. Dependencies unchanged; npm ci not rerun.

WORKFLOWS_COMPLETED=[UC118 Create New cleaning through existing UC028 with atomic mandatory link and assignment handoff]
UI_API_SQL_E2E_VERIFIED=[source prefill → create cleaning → Save and Assign navigation → persisted source link; transactional fault rollback]
EXACT_REMAINING_GAPS=[UC118 incident/material targets and legacy reconciliation; UC117 incident/material visibility; UC028 full standalone HK_SUPERVISOR, blocked-room/status-history and message acceptance still needs dedicated verification; UC119 handover and UC120 closure; linked recurring policy and JOB06 archive]
NEW_BLOCKERS=[]
NEXT_BATCH=[implement linked incident/material targets using their actual contracts; finish UC117/118 acceptance; connect UC120 authoritative completion dependencies]
SPRINT_TIME_REMAINING=approximately21h23 at2026-11-15T20:27Z; deadline2026-11-16T17:50Z unchanged.

Changed source: cleaning creation DTO/service/controller and new source helper, canonical UC028 form/transport/link entry, isolated browser/scenario, focused tests, matrix and receipt. UC028 and UC118 remain PARTIAL; no whole UC newly COMPLETE_VERIFIED. Sprint remains active.
