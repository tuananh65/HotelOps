# SQL/live acceptance

Target HotelOps_Canonical_20261106_f100001_Fresh, local SQLEXPRESS, verified DEV1_FS010 ownership. FS004/FS008/FS009 no writes/reset/reseed. Exact baseline schema files hash-verified by New-ApprovedTrial; synthetic accounts/shift/rooms only. No shared/production connection secrets or system data copied.

- run03_LIVE_RESULTS.json: one uninterrupted full run12/12 PASS.
- run04_LIVE_RESULTS.json: independent repeat12/12 PASS without reset/reseed.
- ACTUAL_RESTART_RESULTS.json: actual owned API/gateway/Vite restart1/1 PASS; original key/body/If-Match replay returns identical durable result; source/assignment/audit/notification snapshots unchanged.
- COMMIT_FAULT_RESULTS.json: real SQL producer component4/4, before/after commit errors/cancellation, actual five-table deltas and fresh-context retry. Auth doubles explicitly labeled.
- PHYSICAL_GUARD_RESULTS.json: 9 cases including sync rejection, missing intent/audit, ExecuteUpdate source revision, mutation-after-validation, savepoint/cancellation and confirmed provider bypass. 8 protected behavior checks PASS plus one expected unsupported bypass, NOT complete C01.
- NOTIFICATION_SQL_RESULTS.json: six real SQL FK/unique/check cases, rollback-only and no retained-row delta.
- SCHEMA_SAFETY_RESULTS.json: two idempotent proposal executions, partial refusal51891 + rename rollback, populated recovery refusal51917, rowDelta0; no destructive recovery.
- Default-off receipt is separate; do not aggregate it into 12-case full runs.

Real browser cases run React against real HTTPS/API/SQL (create navigation, assigned source header, assignment, blocked form); other live cases are API/SQL using Playwright request. Interception47/47 is separate. Current full-suite reports include source/operation/publication/intent/inbox facts; no result decided solely by HTTP response. Missing accepted shared A01/A02 still blocks accepted production UC029, regardless of local candidate PASS.

No actual SQL service/network partition crash proof, external-client guard or final module SRS/Tester acceptance claimed.

DEFAULT_OFF_RESULTS.json: real HTTPS/API/SQL/React1/1 PASS, assignment503/no mutation/Save disabled. Owned services stopped after validation; retained SQL/private fixtures preserved.
