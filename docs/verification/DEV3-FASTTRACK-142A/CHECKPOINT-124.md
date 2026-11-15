# FASTTRACK-142A checkpoint124 — public RPT07 acceptance and regression follow-up

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=0bc34ea08439cbb99874528154e6a6aede8da2d2
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate unchanged)
SHARED_DB_MUTATION=NO

Extended the existing guarded report runtime scenario with a synthetic overdue Internal Request. Real Edge opens RPT07, renders the overdue column/value and downloads CSV and XLSX. Both downloads have format-specific persisted export audits. Public HTTPS preview reports overdue=true, an owned synthetic deadline change does not alter the old CSV snapshot, and a new preview reflects overdue=false. Existing denied DEPT_MANAGER, allowed Owner/Operations Manager, CSRF, export revocation and task snapshot/browser cases also ran. Evidence: checkpoint124-report-runtime.json success=true, cleanup=true. The runner's legacy filename was artifacts/checkpoint75-trial5-runtime.json; this receipt does not relabel historical evidence. The exact disposable identity and empty baseline were checked before mutations; fixture rows are cleaned and owned host exited. No shared DB/schema mutation or new product authorization.

Broad frontend regression remains active in exec session41837, writing artifacts/checkpoint123-frontend-tests.log. It recorded one failure so far: late mutation cannot restore identity after session invalidation. Investigation found that the test waited for the pending UI, which is set before CSRF/PUT dispatch, then invalidated and completed a possibly nonexistent deferred PUT. The test now waits for the actual PUT before invalidating, matching its intended late-response scenario and preserving all security assertions. The isolated original case passed before the fix, consistent with an intermittent race; the broad-run final stack is still pending, so exact failure attribution is not yet conclusively established. The corrected focused case1 PASS. An original-file rerun14 PASS was already running before the edit and is not counted as post-edit full-file validation. Full-suite release PASS is NOT claimed.

Backend solution build PASS0 warnings/errors. Solution test exits0 with no discovery. ModelProbe build retains3 existing EF1002 warnings. This checkpoint changes verification code only; production report behavior is C123. During editing, a newline anchor mismatch and a wrong relative test-file path caused editing commands to fail; corrected before the successful trial/focused test. No test assertion or security guard was removed.

Remaining: UC135 detailed indicators/filters/PDF; pending FE13 business contracts; complete broad frontend regression and inspect its final diagnostics. Next action: resume session41837, do not restart based on incomplete output. Sprint active; no whole-UC promotion.
