# SignalR optimized snapshot — independent runtime verification

Date: 25 November 2026, Asia/Saigon. Scope: existing full protocol harness only. No application or harness changes. TEST database: `HotelOpsCompactWorkdayTestV22611250010`; owned API port: 5777.

## Result

- First full run: **8 PASS, 1 FAIL, 6 NOT RUN** (fail-fast). The rollback scenario observed frame count `4 !== 3`; no timeout occurred. Evidence: `artifacts/restructure-v2/realtime-optimized-full-results.json` and `realtime-optimized-first-run.log`. Do not discard this failure or describe the suite as consistently passing.
- Second full run, unchanged binary and harness: **15 PASS, 0 FAIL**, exit code 0. Evidence: `artifacts/restructure-v2/realtime-optimized-second-results.json`, `realtime-optimized-runtime.log`. Finished `2026-11-24T20:31:18.572Z` / 03:31:18 local.
- `node --check scripts/verification/v2-realtime-runtime.mjs`: PASS. PowerShell AST parse for its fixture: PASS.
- Provided build evidence verified: `realtime-optimized-build.log`: build succeeded, 0 warnings, 0 errors. The DLL copied into the test server matched the current Debug DLL SHA-256: `1CC6C4E7A18CCCD619AE434BBA6B247C287B36B11717762757BB8386B43C7BC9`.

## Second-run scenarios

| Scenario | Result | Total scenario milliseconds |
|---|---|---:|
| Anonymous negotiation rejected | PASS | 143 |
| Foreign branch low-stock event excluded | PASS | 3365 |
| Commit reaches only intended user/branch | PASS | 6008 |
| Foreign branch event excluded even when addressed to same user | PASS | 5927 |
| Unchanged event deduplicated across polling | PASS | 6503 |
| Two connections receive event once each | PASS | 8983 |
| Offline event replayed after reconnect | PASS | 6243 |
| Duplicate subscription on same connection rejected | PASS | 110 |
| Rolled-back event absent | PASS | 11338 |
| Read status updates unread snapshot | PASS | 1766 |
| Live role revocation removes event; restoration restores visibility | PASS | 9111 |
| First-login restriction rejects negotiation and closes stream | PASS | 8616 |
| Logout rejects negotiation and stops both connections | PASS | 9029 |
| Server restart recovers using frontend reconnect/onclose retry policy | PASS | 53381 |
| Session revocation disconnects and prevents subsequent delivery | PASS | 6047 |

These durations include fixture calls, explicit waits, connection startup and assertions; they are not per-event delivery latency measurements. Restart recovery remains slow (53.381 seconds for the whole scenario). No timeout in either optimized run; this does not establish a production latency SLA.

## First-run failure and diagnostics

The rollback assertion counts all frames instead of proving absence of the rolled-back notification ID. The preceding offline replay scenario creates a committed event and waits for the resumed connection, but does not wait for the original connection `a` to receive that event. The subsequent duplicate-subscription scenario completed in 108 ms. Therefore a delayed legitimate frame can increment `a.frames` during the rollback assertion. This is a plausible harness race, **not proven root cause**, because the failed run did not record each frame ID at that boundary. No dirty-read defect is established by the frame count alone. Recommended follow-up: wait for the preceding committed ID on `a` before capturing the rollback baseline, and assert rollback identity rather than only global frame count. Harness was not edited under this assignment.

First-run API log (`realtime-optimized-first-api.log`) also contains SQL error 3980 / batch aborted / operation cancelled while a subscription was stopping, plus OperationCanceledException in the hub timer. The stack passes through `DocumentService.CanReadTransfer`, `NotificationService.Visible`, and `Access.Allows`. Without timestamped connection/frame correlation, do not label it a confirmed independent product failure or hide it. Final second-run API log after restart was empty; the fixture overwrites that file on restart, so it is not evidence that the complete second run had no earlier warnings.

## Safety and cleanup

- Fresh backup was made before mutation using `Backup-Workday.ps1 -Database HotelOpsCompactWorkdayTestV22611250010`: CHECKSUM/VERIFYONLY PASS; metadata updated at 03:25:15 local.
- Preserved earlier evidence as `realtime-pre-optimized-results.json` before the first run; preserved both optimized runs separately.
- Test API processes 2872 (first run) and 6852 (initial second run) terminated; harness restart process also stopped. Port 5777 had no listener after completion. `realtime-private/process.json` and `fixture.json` absent.
- Fixture cleanup completed without errors, verifying deletion of synthetic notifications/sessions and restoration of TEST role/first-login flags. User passwords unchanged. Existing user production/develop/frontend processes were not stopped or reconfigured.
- No schema change, production mutation, external email, commit, push, or deployment.

## Limits

Real SQL Server plus ASP.NET Core SignalR LongPolling; not a mocked hub. The harness models the React frontend retry policy but does not mount React or verify browser badge/toast rendering, browser tab lifecycle, or WebSockets. Full protocol run passed once after one race-like failure; browser E2E and repeatability remain separate acceptance work.

Unresolved questions: none for this testing assignment.
