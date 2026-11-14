# DEV3-NEXT-004 — UC139 CSRF preflight status preservation

Status: IMPLEMENTED_SOURCE_ONLY. Review pending.
Branch: feature/dev3-uc139-csrf-preflight-status.
Base: integration/fullstack-base at 1916bca64017918ec1d34404d53ccd7dd28b543f.

## Implementation

The scheduled Start/End client collapsed all failed CSRF bootstraps into NOT_DISPATCHED/CSRF and discarded HTTP status. Callers therefore could not distinguish session-invalid 401 from 403 denial or 503 unavailable. Preserve only the numeric status for an existing HTTP failure, using the existing optional ScheduledResult.error.status field. Do not return ProblemDetails, title, detail, traceId, token or exception text. Network, cancellation and malformed bootstrap responses have no fabricated HTTP status.

All failed bootstraps still return NOT_DISPATCHED and send zero mutation requests. No change to mutation response mapping, POST payload, If-Match, cookie transport, session/branch current checks, abort signals, dispatch default or retry behavior.

Nine new API tests cover Start and End for each of 401/403/503, plus network/malformed/cancelled bootstrap. They assert a single GET to /api/auth/csrf, credentials include, no-store, sanitized exact result and no POST. Six HTTP-status regression cases failed before the fix (exit 1).

## Executed receipts

- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs: 107/107 PASS, exit 0 (52 scheduled API + 20 browser + 35 read).
- npm.cmd run typecheck: PASS, exit 0.
- npm.cmd run build: PASS, exit 0; two inherited React Router use-client warnings.
- Scoped Prettier on scheduledWorkdayApi.ts and scheduled-workday-api.test.mjs: PASS, exit 0.
- Staged allowlist, whitespace and secret-pattern scan checked before commit.

## Integration and safety

PR #33 remains a separate unmerged source branch. This branch does not copy or merge it and its 107 tests do not include PR33 recovery tests. Existing scheduledMessage consumes the optional status; PR33 adds the panel invalidation path for 401. Combined preflight-to-recovery browser acceptance after integrating PR33 remains pending; no such acceptance is claimed here.

No backend, Auth, public route, shared policy, schema, SQL, DEV1/DEV2-owned source or production feature flag changed. WorkdaysController remains NonController, candidate stays unmounted, dispatch defaults OFF. No shared SQL, live HTTPS, deployment or PR merge. UC139 remains PARTIAL; transaction/race/rollback/audit acceptance requires an approved disposable trial. UC008 Update remains blocked by version contract #28.

## SHA-256
frontend/src/workday/scheduledWorkdayApi.ts: 594E4015BD174D2FC047B1CF18CDF8C9B18F229472A8F0BD539E2CB0E3205DB4
frontend/tests/scheduled-workday-api.test.mjs: 7AAD1E8ABF46F885CCF5EBA66ED171B10F2CED9163E535BC34CCEED46728BE24

## DEV3-NEXT-005 combined acceptance

Merged integration 78f57f1f22bbf401434d058b47b83033a2b43898 without conflict. Allowlist expanded to four files by adding scheduled-workday-browser.test.mjs. Eleven new browser cases cover CSRF 401 on Start/End with successful/failed revalidation, branch bypass prevention, independent 403 denial, 503 unavailable, network/cancelled/malformed preflight and zero POST. Console and rendered-content assertions reject sensitive fixture text. Existing API tests verify no fabricated status and sanitized NOT_DISPATCHED results.

Final combined regression 123/123 PASS (52 API + 36 browser + 35 read), exit 0; Auth HTTP/state 94/94 PASS, exit 0; typecheck/build/scoped Prettier exit 0. Initial combined run 122 PASS / 1 FAIL: assertion incorrectly waited for transient provider loading after 401. Corrected to assert final Confirmed unrestricted session required state; no production behavior changed. Two inherited React Router build warnings. Calendar and previous session tests preserved. Shared SQL/live, concurrency/rollback/audit acceptance remain NOT_RUN; all activation gates unchanged.
