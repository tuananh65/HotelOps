# DEV3-NEXT-005 — UC139 client mutation exclusion

Status: IMPLEMENTED_SOURCE_ONLY; independent review pending.
Branch: feature/dev3-uc139-client-mutation-exclusion.
Base: integration/fullstack-base at 78f57f1f22bbf401434d058b47b83033a2b43898.

## Implementation

The panel already guards duplicate clicks but direct concurrent calls to the same scheduled API client could dispatch two POSTs. Added a synchronous per-instance in-memory guard shared by Start and End, covering CSRF bootstrap, POST and response validation. An overlapping call returns NOT_DISPATCHED with the additive BUSY reason; it is not queued or retried. Finally releases the guard when the first invocation settles. A safe BUSY message is available through scheduledMessage. No backend policy or HTTP contract changed.

Eight controlled-concurrency regressions cover Start/Start, Start/End, End/Start and End/End while bootstrap or POST is deferred. All eight failed before the fix (exit 1), because both operations were confirmed by the synthetic fetcher. Three more checks verify release after confirmed/rejected/preflight-failed completion and no queued follow-up. Every overlap case now asserts one CSRF GET and one POST total.

## Executed receipts

- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs: 114/114 PASS (54 API + 25 browser + 35 read), exit 0.
- npm.cmd run typecheck: PASS, exit 0.
- npm.cmd run build: PASS, exit 0; two inherited React Router use-client warnings.
- Scoped Prettier on scheduledWorkdayApi.ts, scheduledContracts.ts and scheduled-workday-api.test.mjs: PASS, exit 0.
- Exact allowlist, whitespace and secret-pattern scan checked before publication.

## Boundaries

This branch does not include unmerged PR34 and does not claim its CSRF combined acceptance. No controller, service, Auth, public route, DEV2 source, shared policy, schema or SQL change. WorkdaysController remains NonController; panel is not mounted in production; dispatch defaults OFF. No live HTTPS/SQL, migration, activation, deployment or merge. UC139 remains PARTIAL.

The guard is per API instance, not server idempotency, cross-tab exclusion or a permanent unknown-outcome lock. Existing panel logic still owns uncertain-outcome recovery; release of the client guard does not authorize replay. Synthetic sequential successes only verify guard lifecycle, not that repeated real Start requests are business-valid. SQL duplicate races, rollback and audit atomicity require approved isolated acceptance and remain NOT_RUN. UC008 Update remains version-blocked.

## Source hashes
frontend/src/workday/scheduledWorkdayApi.ts: EF263F068C1B5C21B27A68A0F728B6A8B77FC2228718FB343D9B3573EA3C259D
frontend/src/workday/scheduledContracts.ts: 829C68C39596E56046F56D18BF1383679FD6BB69043A179BAADD2A283A57488C
frontend/tests/scheduled-workday-api.test.mjs: C44551F069E3162CBFEF58722872F523A2B1E6F84546C0A9926AD981FE7EFEE4
