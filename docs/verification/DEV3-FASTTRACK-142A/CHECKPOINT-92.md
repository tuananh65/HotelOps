# FASTTRACK-142A checkpoint92: canonical UC116 Accept/Reject API and UI

TASK_ID=DEV3-FASTTRACK-142A
CURRENT_HEAD=commit containing this receipt; starting local/remote ce54de8242476ebc478c5010561097d4a643fc4f.
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=21/144
PARTIAL=123/144
SHARED_DB_MUTATION=NO

Implemented GET/POST `/api/internal-requests/{id}/decision` around the SQL-verified decision service. GET requires current IR_VIEW scope, returns DTO-only subject/description, submission time, age, deadline, version and advisory processing capability. It locks the request before reading the snapshot and revalidates session/read scope. POST requires HTTPS, authenticated cookie, global API CSRF and one canonical strong If-Match containing the eight-byte version; the service rechecks DEPT_MANAGER, IR_PROCESS and the exact current-department mapping. Missing version is428, stale version412, invalid decision input400. Nothing relies on the UI capability flag to authorize a write.

The canonical history timeline mounts a decision form with Accept, Reject, reason and optional adjusted UTC deadline. It displays MSG08/MSG19 only after a validated successful response. A synchronous submit guard prevents double-click writes; failures remove the editable snapshot and require explicit reload rather than replay. Session-generation changes and cancellation remove private details. Accepted state displays the committed adjusted deadline immediately. Request/response identity and version are validated in the client.

## Direct acceptance evidence

[Final trial2](checkpoint92-trial2-runtime.json) PASS, cleanup=true. Two real Edge sessions traverse canonical login -> request deep link -> Accept with changed deadline/reason or Reject with mandatory reason -> reload committed state -> Logout. TLS uses the trusted certificate with normal validation. SQL verifies request status, actor, event and audit after each UI decision; each produces a deliverable requester notification intent. Public API rejects missing CSRF400, missing If-Match428, blank rejection400 and stale retry412. The existing real SQL transaction/audit/outbox rollback and concurrent-decision scenarios also execute in this trial.

Requester delivery is invoked explicitly by the isolated probe after each browser decision. **The scheduled worker loop is not yet an E2E acceptance claim.** No production deployment or additional schema change occurred in this batch; checkpoint91's exact disposable migration is reused idempotently. Synthetic credentials are passed only via child stdin and never published.

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS, zero warnings/errors; required solution test exits0 without discovering tests; actual focused FE13 regression18 PASS. [Frontend tests](checkpoint92-frontend-tests-final.txt):13 PASS, covering stale decision reload, double-click exclusion, loss of session during a pending write, read-only capability, history privacy, notification/return URL validation. [TypeScript/production build](checkpoint92-frontend-build-final.txt) PASS with existing use-client/chunk warnings. ModelProbe PASS with the same3 EF1002 credential fault-probe warnings. Initial TypeScript narrowing and ExceptionContext compile errors were fixed before final validation; neither is hidden as PASS. No dependency reinstall or broad unrelated suite rerun.

WORKFLOWS_COMPLETED=[public Accept with reasoned deadline adjustment, public Reject with required reason, persisted decision/audit/requester intent]
UI_API_SQL_E2E_VERIFIED=[canonical Accept and Reject with CSRF/ETag and committed SQL evidence]
EXACT_REMAINING_GAPS=[UC116 offer/execute Assign handoff to UC115; UC115 handler assignment/reassignment and pending routing; scheduled notification worker acceptance; UC114 real creation and legacy SUBMITTED provenance; physical outbox/notification retention]
NEW_BLOCKERS=[]
NEXT_BATCH=[UC115 handler selection/assignment and Accepted-to-In-Progress transition, wire its offer after UC116 Accept; route/reassign with current-department authority and durable notifications; verify complete related UCs together]
SPRINT_TIME_REMAINING=approximately23h24m at2026-11-15T18:26Z; deadline2026-11-16T17:50Z unchanged.

UC116 remains PARTIAL because the SRS explicitly offers assignment after acceptance; that handoff is not implemented yet. Browser mutation PASS is concrete progress, not a substitute for the missing acceptance criterion. Sprint remains active.
