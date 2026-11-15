# FASTTRACK-142A checkpoint131 - approved UC122 time window

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=88f5f0c65f71d262849128a28bb130ed399fbc5f
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Applied the approved CreatedAt-to-current-DueAt contract to the authorized internal request list. API returns the official UTC creation timestamp and server-calculated timing status; the UI displays At Risk starting at exactly 80%, including the deadline, and Overdue strictly after it. Closed/Rejected/Cancelled never qualify. Missing or nonpositive windows do not qualify. Decimal tick arithmetic preserves datetime2 precision without overflowing for long intervals. The existing server TimeProvider supplies the read snapshot; browser time is not used. Deadline changes recalculate against the original creation timestamp, never UpdatedAt. Current IR_VIEW/session/branch/direction authorization remains in place. Timing status grants no mutation capability.

Direct validation:

- dotnet build HotelOps.slnx PASS, 0 warnings/errors. Initial sandbox invocation failed reading the local NuGet.Config; rerun with authorized filesystem access passed.
- dotnet test HotelOps.slnx exits zero but discovers no tests. Explicit regression project: 760 PASS, 13 environment-gated SKIP, 0 failures. Focused timing/actor tests: 12 PASS, including 79/80/100 percent, overdue, changed deadline, terminal states, invalid windows, single-tick boundaries and maximum date range.
- Frontend history transport/browser suite: 36 PASS. New browser case displays server risk, refreshes after deadline change, and removes data on revoked access. This is mocked HTTP browser testing, not live HTTPS-to-SQL acceptance.
- TypeScript and frontend production build PASS. Existing React Router use-client and >500kB bundle warnings remain.
- ModelProbe build PASS with 3 existing EF1002 credential-fixture warnings. Exact identity-guarded disposable SQL history probe: 42 checks PASS, rollback=true; evidence in checkpoint131-ir-timing-sql.json. Synthetic persisted timestamps verify risk boundaries, deadline recalculation, invalid window exclusion and authorized-query pagination. No schema changes or shared/production mutations.
- Full frontend release retry session81908 is still running; it began before C130/C131 edits and cannot alone certify this revision.

Changed product files: InternalRequestTiming.cs, InternalRequestHistoryQueries.cs, historyApi.ts, HistoryPage.tsx. Changed validation files: InternalRequestTimingTests.cs, SprintInternalRequestHistoryProbe.cs, internal-request-history.test.mjs, internal-request-history-browser.test.mjs. Receipt and UC matrix updated.

UC122 remains PARTIAL; no new complete business workflow is claimed. At-risk filtering and the Escalate intervention transaction, one-open constraint under concurrency, audit/outbox/acknowledgment delivery, UI mutation and live HTTPS/SQL acceptance remain. The allowed-time-origin question is resolved. Next highest-impact work: implement the approved Operations Manager escalation action atomically with durable notification intent, then verify its complete UI/API/SQL workflow. Existing permission, session, ETag and CSRF protections must remain enforced.
