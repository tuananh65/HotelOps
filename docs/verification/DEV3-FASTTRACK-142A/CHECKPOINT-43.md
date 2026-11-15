# FASTTRACK-142A checkpoint43: exact reinspection assignment

New COMPLETE_VERIFIED UCs: **0**. Total remains **6 (UC035, UC036, UC044, UC046, UC109, UC110), 138 PARTIAL**. This is an implementation checkpoint, not sprint completion or release acceptance. Original deadline: 2026-11-16 17:50 UTC.

## Implemented and verified

The new conditional POST `/api/branches/{branchId}/reworks/{id}/reinspection` names the exact submitted rework. The service preserves session/branch permission, active supervisor actor and assignee, cleaner separation, current cycle, source ETag, future UTC deadline and a protected UC036 submission receipt. It creates the inspection, immutable source binding, selected-defect result slots, operation receipt, audit and assignment notification intent in one serializable transaction. Direct binding/ledger/source-proof DML must be unavailable to the runtime principal. The generic task assignment now refuses to infer a pending rework.

`checkpoint43-assignment-hex-fixed.json`: both normal and effectively blocked fixtures PASS, including the existing browser Reject/Rework/Receive/Submit chain and the newly nested SQL assignment checks. New assignment checks use a real SQL session and permission service under a restricted database principal: stale ETag rejection, branch revocation rejection, audit-failure rollback, concurrent identical operation yielding one binding, changed-body replay rejection and duplicate assignment rejection. Installation/cleanup is rehearsed under rollback; committed trial schema and synthetic data are then cleaned. **The new assignment endpoint itself has not yet been exercised through HTTPS/browser.** Existing browser acceptance is not relabeled as new UC041/047 browser acceptance.

Backend/ModelProbe build PASS, zero compiler warnings. `dotnet test HotelOps.slnx` exits successfully but has no test project. `checkpoint43-regression.txt`: **528 PASS, 11 SKIP, 0 FAIL**, including ten new malformed/foreign assignment receipt cases. Unchanged frontend full regression from checkpoint42: **3262 PASS, 1 SKIP, 0 FAIL**, committed/pushed as `111eddbe19eb71e99935fc7d4f7effe48c9787f0`. No dependency changes or repeat npm ci.

## Retained failure and correction

The initial assignment trial and two diagnostic receipts failed before commit because SQL converted a timestamp column directly to varchar as eight raw bytes. A table-variable reproduction returned direct_length=8. Explicit binary(8) conversion before style-2 hex conversion produces the required 16 uppercase hex digits inside a strong ETag. The writer was corrected; the validator was not loosened. Failed receipts are retained. Temporary diagnostic values were removed from application source after confirmation.

Independent cleanup after the successful trial: zero users, reworks and attachments; reinspection binding table and DEV3_Reinspector principal absent. The exact target remains server meita, database HotelOps_DEV3_142A_20261114_cc78c13f, GUID 1D5A9B53-7D19-435A-96AB-9FDFF447B6E0 and run cc78c13ffc124efcad67b3d804baa84c. No shared HotelOpsDB mutation, generated entity changes, migration or deployment.

## Remaining lifecycle and continuation

UC041 remains PARTIAL: add assignment options and canonical supervisor UI, verify HTTP CSRF/preconditions, deliver the persisted assignment intent and enforce source read authorization. Current exact-defect lane requires the original checklist version still Published; retirement/reassignment and initial-cleaning coverage remain unverified.

UC047 remains PARTIAL: compose assigned-inspector detail/evidence, exact per-defect Fixed/Not Fixed validation, Passed/Failed conclusion transaction, effective readiness/history, executor notification and next-round Request Rework. The conclusion DTO and guarded disposable writer are preparatory source only, not acceptance evidence. Preserve the existing unaccepted candidate flags. Do not mark UC047 complete from assignment tests.

The new disposable schema uses binding/results/evidence links; its Failed-rework mapping extends only the workflow CHECK and preserves/restores its original alternatives. This proposed conclusion mapping still needs execution tests. No lasting schema deployment has occurred. See implementation decision43 in the approved decision log.

Next highest-impact work: complete UC041 source/read/UI and UC047 conclusion on this verified binding, then FE11 remaining lifecycle, then FE13/UC006/010. Continue the active sprint without another broad audit.

Known environment/runtime limitations: Windows sandbox process startup error1909 required authorized escalated shell execution. Existing EF unmapped-index and unordered-Take warnings remain. Notification tests explicitly invoke delivery; no new hosted-worker scheduling claim is made. Final committed-source trial and Git identities are recorded in the subsequent receipt/Git history.
