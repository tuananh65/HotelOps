# FS013 development defects and preserved failures

Internal developer log, not Tester acceptance or a new handover package.

| Finding | Resolution / remaining limit | Evidence |
| --- | --- | --- |
| Startup readiness and .NET SQL timeouts | Six initial attempts failed. Native identity checks succeeded; later .NET component and API connected successfully. No confirmed root cause is asserted, no global auth/SQL setting weakened. Shared-memory diagnostic was reverted. | DRIVER_PROBE.json, later commit/lifecycle receipts; startup details summarized in README |
| Script 11 dynamic DROP syntax | Assign the QUOTENAME expression to a variable before EXEC. Initial compilation failed before creating execution tables; no table reset was used. | Execution install receipt, script 11 |
| Reassign unique active-assignment index 2601 | EndedAt alone did not release the actual filtered index. Use the already-existing `REASSIGNED` status and verify it. | REASSIGNMENT_SQL_RESULTS.json; real lifecycle |
| Assignee selector accessible name | Make the select's accessible label explicit; verify retry/frozen command and server paging. | UI_INTERCEPTION_FINAL.log (failed), UI_INTERCEPTION_PAGING.log (7 PASS) |
| IN_PROGRESS participant leaves saved audit entities tracked | Clear saved tracked entities in Housekeeping caller before final SQL session gate; preserve participant/shared auth source. | LIFECYCLE_LIVE_ATTEMPT1.log; final lifecycle rollback/reassign PASS |
| Rowversion-to-text produced invalid HTTP ETag after commit | Explicit binary(8) cast before hexadecimal conversion; validate durable result identity, state, notification contract and tag before commit/response. Add 10 unit cases and stricter SQL/commit proof checks. Keep earlier malformed provisional rows, never rewrite immutable command history; invalid historical response contracts fail closed. | ATTEMPT2 failed after commit; XUNIT_CONTRACT_FIX.log; SQL/commit final and ATTEMPT4 PASS |
| Worker restart overwrote early lifecycle JSON checks | Append checks for the same run across workers. Original full failure logs remain; do not treat overwritten partial JSON as complete proof. No-Workday was executed again in final run. | ATTEMPT1/2 logs; final ATTEMPT4 JSON |
| Browser assertion ran before execution metadata response | Wait for the actual matching GET and require 200 before checking Start. No business assertion or functional timeout was relaxed. | ATTEMPT3: 1 FAIL/2 PASS; ATTEMPT4: 3 PASS |
| Native component used ExecuteScalar before later batch checks | Drain the whole command with ExecuteNonQueryAsync so a missing-recipient verifier error is observed. Reuse an existing Working fixture day rather than inserting a duplicate after live runs. | Final SQL module 6 PASS, every mutation rolled back |
| Harness rebuild overlapped owned gateway once | Stop owned services before rebuilding. Keep two MSB3026 warnings in initial log; later clean helper builds have 0 warnings/errors. | HELPER_BUILD_TRANSPORT.log; later helper build logs |

Commit/cancellation component proof uses an ASSIGNED source and gate doubles. IN_PROGRESS interval/source rollback is verified separately through real authenticated HTTP staging-failure tests. Neither substitutes an actual network disconnect or complete production least-privilege acceptance.

Open functional/integration gaps remain in ACCEPTANCE_MATRIX.md and INTEGRATION_NOTES.md, particularly Resume/blockers, shared Notification/WorkTime SQL/public entry, legacy room status/history and the unimplemented completion/rework/cancellation flows. No defect is closed merely because a build passed.
