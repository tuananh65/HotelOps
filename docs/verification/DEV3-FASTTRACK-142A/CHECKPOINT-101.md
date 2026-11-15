# FASTTRACK-142A checkpoint101 — historical evidence and deadline acceptance

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=bbdf7f84a2ff069f81bfc2a148ed5cbbb12e403a
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

UC117 no longer hides evidence older than the first100 attachments. The existing progress GET accepts a positive evidenceBefore cursor, with descending unique attachment ID and a bounded100-row page. Every page retains existing session/current IR_VIEW, exact progress-event/request association, Serializable snapshot and final authorization checks. No schema or generated entity change.

Canonical UI loads older files without resetting unsaved progress/note/result/deadline inputs. It checks source identity, unchanged request version/count, descending IDs and exact download paths; malformed, changed or denied responses remove the private snapshot and require reload. The same busy guard prevents overlapping saves/page requests. Downloads continue to enforce their existing independent authorization; no storage keys are exposed.

Direct evidence: [trial2](checkpoint101-trial2-runtime.json) success=true. Real Edge → trusted HTTPS → disposable SQL displays101 historical attachment records, preserves unsaved note, then performs mapped Department Head deadline change with reason, saves60% and reloads the persisted deadline. SQL verifies due_at and audit reason, and the request remains IN_PROGRESS. Public API checks100+1 pagination without duplicate/version mutation and rejects cursor0. Historical attachments in this trial are synthetic metadata; this trial does not claim download bytes. Actual upload/download bytes remain C98 historical evidence.

[Cleanup](checkpoint101-trial2-cleanup.json) confirms DROPPED_AND_BACKUP_REMOVED. Trial1 also passed the API pagination checks; initial cleanup was blocked by the other-session guard. Read-only process/SQL inspection later found no remaining trial host or SQL session, and the unchanged identity-guarded cleanup succeeded. No connection was forcibly killed, no timeout or guard relaxed. Runtime cleanup=false indicates delegated whole-clone disposal; separate cleanup receipts prove disposal. The inherited outer scope string is generic; nested browser/public SQL checks are the evidence for this batch.

REGRESSION_STATUS=required solution build PASS0 warnings/errors; solution dotnet test exits0 without discovering a test project; actual FE13 regression project54 PASS; canonical frontend browser suite20 PASS including draft preservation, revoked access and changed-version paging; typecheck and production build PASS. ModelProbe retains3 existing EF1002 fixture warnings; Vite retains use-client and large-chunk warnings. Dependencies unchanged, npm ci not rerun.

WORKFLOWS_COMPLETED=[UC117 historical evidence navigation and public manager deadline-change acceptance]
UI_API_SQL_E2E_VERIFIED=[101 attachments across two pages; preserve draft; change deadline with reason; save/reload progress; SQL audit and unchanged lifecycle]
EXACT_REMAINING_GAPS=[UC117 non-operational linked-work projections and full handler public acceptance; UC118 cleaning/incident/material adapters and legacy links; UC119 handover and UC120 closure; linked recurring JOB05/closure decision; JOB06 finite archive policy]
NEW_BLOCKERS=[]
NEXT_BATCH=[finish UC117 handler public acceptance and remaining linked work; UC118 target adapters; UC120 authoritative completion dependencies]
SPRINT_TIME_REMAINING=approximately21h38 at2026-11-15T20:12Z; deadline2026-11-16T17:50Z unchanged.

Changed files: ProgressService/Controller/Panel, history browser tests, existing isolated probe/browser, matrix and receipt/evidence. No new COMPLETE_VERIFIED claim; sprint remains active.
