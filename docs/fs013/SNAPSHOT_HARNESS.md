# Read-only snapshot harness validation

The original regression run passed 9 cases and failed 3 when read-only helper processes exceeded their unchanged 60-second deadline. The first targeted retry passed UC032 but still timed out in two lifecycle snapshot calls. Both original failure logs and receipts are retained. A read-only SQL request-wait probe observed no active waits at the instant sampled; this does not establish a complete root cause.

Only the FS013 verification harness changed: three read-only snapshot modes use asynchronous ADO.NET projections of the same existing SQL columns instead of initializing the full EF model for every child process. Scope checks still require the exact local FS013 database/owner; candidate reads retain marker/module/trigger/index prerequisites. No business write, application authorization, service, generated mapping, test assertion or 60-second helper timeout was changed.

With owned HTTP services stopped and the trial static, the original EF executable captured lifecycle, notification and execution snapshots. The new executable captured the same data. Node `util.isDeepStrictEqual` confirmed **exact parity 3/3**, including every field/value, null, GUID/date representation and array order (`SNAPSHOT_PARITY.json`). Measured complete process durations were 2371/3541/2573 ms (`SNAPSHOT_TIMINGS.json`). The EF/ADO snapshot files preserve the comparison inputs.

After rebuilding, the two remaining authenticated HTTP regression cases passed unchanged, including recipient inactivity/branch revoke/supersession and assignment authority/no-mutation checks. Requests that mutate or authorize business data continue through the real backend/EF/SQL paths. Snapshot helpers are developer evidence tools, never public API endpoints or accepted shared Notification adapters.

All 12 baseline case identities have a later PASS across the initial and targeted runs. Do not report a single clean 12/12 run: initial result was 9 PASS/3 FAIL; retest 1 was 1 PASS/2 FAIL; retest 2 was 2 PASS/0 FAIL. Combined outcomes are recorded in REGRESSION_AGGREGATE.json with the exact receipt for each case.
