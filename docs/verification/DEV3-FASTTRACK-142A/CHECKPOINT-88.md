# FASTTRACK-142A checkpoint88: usable UC123 read workflow

CURRENT_HEAD=see commit containing this receipt; baseline a6de41a3f0eb1c3a6d2afa6c3737ece604d419e7, remote reconciled without divergence before work.
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=19/144
PARTIAL=125/144
WORKFLOWS_COMPLETED=[UC123 authorized list -> search/overdue filter -> timeline -> return to list, read portion only]
UI_API_SQL_E2E_VERIFIED=[requester/current handler, Department Head branch list and timeline, revoked-role denial]
SHARED_DB_MUTATION=NO

Canonical /internal-requests now connects to the scoped IR_VIEW API. Dashboard and fallback navigation admit the link through the API; the protected login return is registered. List shows status, branch, receiving department, current handler, UTC deadline and overdue flag. Timeline displays source events, assignments and feedback, with independent event/assignment pagination. Session changes, pending revalidation, changed queries and denied refreshes remove prior private results. Text renders as React text, never HTML. No lifecycle mutation is implied by a read grant.

The original checkpoint88 trial exposed a missing Dashboard link; it failed and cleaned its fixture. The link was fixed. Retain failed trial1 as evidence of the corrected integration defect, not a PASS. Successful trial2 exercises real canonical frontend, trusted HTTPS and owned disposable SQL with27 synthetic requests (26 in scope), list pagination25+1, exact related visibility, assignment timeline, empty search, wrong-branch source404, revoked-role navigation denial and logout. It also reruns existing scoped read and report export checks. All temporary rows/catalog grants are cleaned; owned host stops.

[Final trial3](checkpoint88-trial3-runtime.json) repeats those workflows after adding the receiving-department display and strict response validation. Final focused client/UI tests6 PASS. No product backend change in this batch.

REGRESSION_STATUS=backend/ModelProbe builds PASS, zero warnings/errors; focused backend authorization9 PASS; solution test exits0 with no test discovery (not counted as regression). Frontend typecheck/production build PASS with existing React Router use-client and Vite chunk-size warnings. Focused auth/router/client179 PASS; two UI privacy regressions PASS. The first new UI-test bundling attempt failed for missing output path and was corrected before rerun. No lockfile changes or npm ci in this batch. Broad670/12 backend evidence is inherited from checkpoint87, not a new broad execution.

EXACT_REMAINING_GAPS=[UC123 sent/received direction and status/type/priority/department filters, authorized linked-work history, withdrawal reason/atomic audit/receiving-DH notification; UC114 material-vs-nonmaterial classification and physical initial status; UC115/116 trustworthy receiving-department authority; UC117 percentage/result storage absent from generated InternalRequest and authoritative event/version contract still needed]
NEW_BLOCKERS=[] (these schema/authority gaps remain unresolved; no unsafe mutation endpoint opened)

Zero new full UCs is justified by connecting the previously API-only UC123 path to an operable browser workflow. It does not justify further peripheral test expansion. The approved acceleration directive is now applied: continue lifecycle implementation in workflow batches, reuse accepted infrastructure/regression, and move to independent functionality when an exact business contract is missing.

NEXT_BATCH=[FE13 UC114/117/118: inspect approved lifecycle fields and existing event/link storage, implement safe current-handler progress and linked-work paths where contracts suffice; keep UC115/116 department mutations fail-closed; then UC006/010 public workflow closure]
SPRINT_TIME_REMAINING=approximately24h06m at2026-11-15T17:44Z; fixed deadline2026-11-16T17:50Z unchanged.
