# FASTTRACK-142A checkpoint94 — Route and department authority handover

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=4183ae4670dc768e742fe5033ef66d6a1003da02
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=22/144
PARTIAL=122
SHARED_DB_MUTATION=NO

## Implemented behavior

UC115 Route now has canonical UI and authenticated HTTPS GET/POST `/api/internal-requests/{id}/route`. It permits only Pending Acceptance, a different active department in the same branch, and a trimmed GBR012 reason of10–4000 characters. Current SQL session, origin DEPT_MANAGER/IR_PROCESS/exact mapping, strong If-Match and global CSRF remain mandatory. Existing active assignments cause fail-closed rejection. Request remains Pending Acceptance and its receiving department/version change atomically with event, audit and durable recipient intents.

GBR147 intentionally transfers responsibility: the origin manager need not manage the destination. The transaction rechecks origin authority before commit, while all subsequent request mutations resolve the new current department. The helper extracted from ProcessAuthority retains active account/employment, current role/grant/branch access and exact mapping checks. Route snapshots candidate recipient IDs, takes ordered user locks, then rechecks the candidate set under Serializable before taking responsibility. A changed candidate set rejects the operation instead of silently missing a newly mapped recipient.

The additive `internal_request_route_outbox` stores origin/destination provenance and deduplicated requester/eligible target-manager intents. Exact disposable server/database/GUID/run guards and trusted composite department/branch FKs preserve the baseline. No generated EF entity, status CHECK or shared database change. Delivery and source opening validate current IR_VIEW; destination-manager access additionally requires current explicit authority and matching current receiving department. The requester remains a related-record reader. Worker integration is present, but this batch does not claim scheduled Route delivery E2E.

RoutePanel loads scoped target departments, requires a reason and sends fresh CSRF plus loaded version. Successful routing removes the old decision snapshot and requires reloading current access. Stale/error responses remove editable route state and never automatically replay. Synchronous submission guard excludes double clicks.

## Executed evidence

[Final trial4](checkpoint94-trial4-runtime.json) success=true, cleanup=true. Real Edge browser logs in, selects target department/reason, routes via trusted HTTPS, reloads Pending Acceptance, and verifies that the old manager no longer has Accept UI or API processing access. API denies missing CSRF400, missing version428 and short reason400. SQL verifies department/event/audit and deliverable requester/target-manager notifications.

The same trial exercises invalid lifecycle/same department/cross-branch/inactive destination, stale version, outbox-trigger rollback, concurrent Route with one commit, and exact old-manager denial/new-manager authorization after handover. Delivery is idempotent; a valid target-manager session can open the source, then mapping revocation denies both delivery and source despite IR_VIEW. Accept/Assign/Reject and prior authority regression also execute successfully.

REGRESSION_STATUS=solution build PASS zero warnings/errors; required solution test exits0 without discovered tests; focused FE13 backend29 PASS; frontend15 PASS; TypeScript/production build PASS. Existing Vite use-client/chunk warnings and three credential-probe EF1002 warnings remain. No dependency reinstall or unrelated broad suite rerun.

Trial2's Route checks passed but its later assignment regression timed out during navigation to Logout, with an in-flight assignment read; it is a failed overall trial. The unchanged retry trial3 and final trial4 passed. No security guard or timeout was relaxed. A one-off navigation/lock timing risk remains for observation; a specific root cause was not established and is not claimed fixed.

WORKFLOWS_COMPLETED=[Pending Acceptance Route with current authority transfer; persisted audit and requester/target-manager notification intents; delivery/source revocation guards]
UI_API_SQL_E2E_VERIFIED=[canonical Route and old-manager UI/API denial; regression Accept/Assign/Reject]
EXACT_REMAINING_GAPS=[scheduled Route notification E2E; destination-manager public Accept/Assign after Route; full public reassignment acceptance; UC117/120 lifecycle; physical outbox retention/JOB06 archive]
NEW_BLOCKERS=[]
NEXT_BATCH=[verify Route worker plus destination-manager intake and public reassignment to close UC115; implement UC117 progress and UC120 completion; UC118 linked work]
SPRINT_TIME_REMAINING=approximately22h58m at2026-11-15T18:52Z; deadline2026-11-16T17:50Z unchanged.

CHANGED_FILES=RouteController/Service/Delivery; RoutePanel and DecisionPanel integration; ProcessAuthority helper; worker/source resolver/DI/error mapping; guarded route outbox SQL; route SQL/browser probes and decision-fixture integration; route unit/frontend tests; matrix and receipts. Sprint remains active; no additional whole UC is asserted complete.
