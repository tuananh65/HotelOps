# SignalR recovery verification — 25 November 2026

Scope: independent protocol/runtime tester; only `v2-realtime-runtime.mjs`, `v2-realtime-fixture.ps1`, and this report edited. No application source changes. Database fixed to `HotelOpsCompactWorkdayTestV22611250010`; test server owns port 5777. Production/develop/frontend processes preserved.

## Diagnosis and harness changes

- Historical startup failure (`realtime-runtime-results.json` before this work): `SchemaGate.Validate` rejected a previous binary/schema combination before listening. Current compiled binary starts successfully against the current 57-table TEST schema. No schema bypass or database mutation was used to resolve startup.
- Transport-only reconnect failure reproduced: client transitioned `reconnecting` → `closed`, ending `Disconnected` after retry exhaustion. Evidence preserved in `artifacts/restructure-v2/realtime-recovery-transport-only.json`. The original harness omitted the frontend's `onclose` retry and stream-error handling in `frontend/src/compact/notification-realtime.tsx`. Harness now models that existing policy: automatic reconnect, stop on stream error, retry start every 15 seconds after close, resubscribe after recovery.
- Foreign-branch zero-count assertion was invalid after cross-branch transfer notifications were implemented: authorized transfer notifications can appear for both branches. Test now inserts a foreign-branch LOW_STOCK event and verifies that its ID remains excluded.
- Revocation frame-count assertion was too broad around an in-flight snapshot. Test now requires negotiation HTTP 401, transport leaving Connected, no event created after revocation ever appearing, and no further frames after disconnection. It does not prohibit a terminal empty snapshot that clears the badge.
- A later run hit the previous 15-second initial-snapshot timeout while adding another concurrent connection; the same scenario had passed in the prior run. Evidence retained in `realtime-recovery-snapshot-timeout.json`. Initial snapshot integration budget is now bounded at 45 seconds and failures include connection state and stream errors. This is not evidence of acceptable production latency; investigate/query-profile the existing per-notification authorization work if latency remains high.
- Fixture starts from a private copy of the compiled output, retains that copy across restart, checks port ownership, checks PID creation time before stopping, and uses hidden processes. Setup persists session metadata after each session so partial setup can be cleaned. Cleanup validates zero fixture notifications/sessions and restored role/password-change flags in SQL before removing metadata.

## Final execution

Full pre-final run: **14 PASS / 1 FAIL**, with restart recovery, offline replay, multiple connections and API logout all passing. The remaining failure was the old revocation frame-count assertion described above. Evidence: `artifacts/restructure-v2/realtime-recovery-pre-final.json`.

Two subsequent full-suite runs timed out under concurrency (initial snapshot, then committed-event delivery). Those failures are retained in `realtime-recovery-snapshot-timeout.json` and `realtime-recovery-delivery-timeout.json`; they are not reported as PASS.

Final focused run: **5 PASS / 0 FAIL**, exit code 0. Evidence: `artifacts/restructure-v2/realtime-runtime-results.json` (`mode: recovery-only`) and `realtime-recovery-focused.log`.

1. Two connections receive the same persisted event once per connection.
2. Offline persisted notification replays in the first resumed snapshot.
3. HTTP logout revokes the session (subsequent negotiate 401) and stops both active connections without delivering later events.
4. Actual owned API stop/restart recovers using the frontend reconnect/onclose policy, resubscribes, replays the event committed during downtime, and receives a new event afterward. Total scenario 72,993 ms (includes fixture stop/start and SQL startup); no production recovery-time SLA asserted.
5. Explicit session revocation rejects negotiation with 401, disconnects the stream, and never delivers the event created after revocation (6,467 ms total scenario).

Cleanup completed without error. SQL cleanup checked fixture notification/session deletion and restored role/first-login flags. Fixture/process metadata are absent. Port 5777 and owned realtime process checks are recorded in the completion report; existing user server processes were preserved.

Conclusion: recovery/security protocol scenarios passed. **Do not mark the entire realtime suite or UI E2E acceptance as passed**: full-suite timing instability still needs investigation in a quieter environment or query profiling. No proven application correctness defect was changed by this tester.

Command: `node scripts/verification/v2-realtime-runtime.mjs` (elevated Windows-integrated SQL access).

Focused mode: set `HOTELOPS_REALTIME_RECOVERY_ONLY=1` before that command. It selects only five recovery/security scenarios and removes unneeded background connections; JSON records `mode: recovery-only`. This mode is not a replacement for full-suite acceptance.

Syntax: `node --check scripts/verification/v2-realtime-runtime.mjs` passed; PowerShell AST parse passed.

## Limits

- Real ASP.NET Core SignalR LongPolling + SQL Server, not a mocked hub. No WebSocket transport test; the current frontend explicitly selects LongPolling.
- The JavaScript harness models the frontend connection/retry policy; it does not mount React or prove visual badge/toast rendering or browser-tab lifecycle. Two actual hub connections verify protocol behavior, not two browser tabs.
- Fixture uses isolated test sessions for existing TEST storekeepers. It temporarily changes only their TEST role revocation / first-login flag and restores them; passwords are never changed. No real email, production changes, commits, pushes, or deployment.
- Persistent snapshots replay unread IDs, not a guarantee of one toast per user across multiple browser tabs. Per-connection unchanged-event deduplication is tested.

Unresolved questions: none for this harness scope.
