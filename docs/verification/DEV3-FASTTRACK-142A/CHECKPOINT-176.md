# C176 — Completion writer preflight and blocked-room feedback

Baseline `578950e26639c3aea3cfae2fb16bff590827ef34`. New completed UCs: 0. Source covered 29/144; pending QA 2; historical COMPLETE_VERIFIED 27/144. No full business workflow newly verified.

Complete now checks actual SQL EXECUTE permission on the immutable cleaner-capture procedure and SELECT permission on its receipt table before closing a work interval or writing evidence. Execution metadata advertises Complete only with that capability; Report Blocker remains independent. This neither grants SQL privileges nor replaces actor/session/branch authorization. UI success feedback explicitly preserves the effective room block after completion/blocker reporting.

Extended the existing recovery fixture to exercise the real cleaner-capture procedure and completion outbox together with transaction rollback. This new branch is NOT verified: the run timed out in the initial CleaningStartSchema metadata probe, before fixture mutation. Follow-up read-only ODBC checks failed at prelogin on both shared-memory and the previously verified loopback TCP listener. SQL service is Running; root cause unresolved. No restart, timeout/security relaxation or shared database write. No schema change this batch.

Validation: full dotnet build HotelOps.slnx PASS, 0 warnings/errors; explicit focused regression 48 PASS; frontend typecheck/build PASS and client tests 13 PASS. Solution test command exits 0 without running the separate regression project. Vite retains warnings for ignored React Router use-client directives and a chunk above 500 kB. SQL fixture 1 FAIL (timeout), not a passing completion claim. Browser/HTTPS acceptance and restricted-SQL-identity negative test remain deferred.

Contract question pending: UC034 step 6 says Pending Inspection after Complete; GBR-044 item 5 prioritizes Pending Cleaning when the room has never Passed. Existing readiness calculation unchanged pending decision. No inferred READY or physical status introduced.

Changed: CleanerAttributionSource.cs, CleaningClosureBoundary.cs, CleaningLifecycleService.cs, CleaningExecutionPanel.tsx, cleaning-execution.test.mjs, FreshStartAdmissionSqlTests.cs, handoff and this receipt.

Next: resolve first-inspection readiness contract, continue cancellation capability/notification integration and full Complete-to-Inspection source closure. SQL outage must not block independent implementation. Full source coverage goal remains active.
