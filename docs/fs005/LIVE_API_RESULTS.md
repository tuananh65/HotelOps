# Live API / SQL results

Target: existing `HotelOps_Canonical_20261105_180000_f040001_Fresh` on local SQLEXPRESS; owner marker DEV1_FS004. Read-only metadata preflight: correct local instance, database exists/owned, rowversion8 bytes, three optional markers, six progress tables. Existing trusted HTTPS certificate; no private-key export, trust changes or TLS ignore. Production startup schema validator and service DI succeeded.

[Receipt](LIVE_RESULTS.json) records real browser → HTTPS gateway → Vite proxy → HTTPS API → SQL. No interception. Actual API-assisted cookie login using existing DPAPI fixture IPC, CSRF bootstrap and normal session validation; not frontend sign-in UX acceptance. Only cleaning GETs; auth login/session touch writes occur normally. No fixture reset/reseed, DDL, grants or cleaning mutations.

Verified: anonymous401; owner with OWN execution can read UC032 but cannot monitor403; authorized supervisor list/detail200; foreign branch list/detail403; missing detail404; malformed pagination400; assignee/keyword filters, empty result, paging and React rendering; Readiness → filtered list → detail, selecting retained task dates through UI. Initial successful run: 1/1,24.4s. Final expanded repeat PASS1/1,22.1s, including floor/status/kind/overdue filters and distinct second-page rows; same retained trial without reset/reseed.

Independent existing helper SQL observation before/after equals: task IN_PROGRESS,40%, progress logs4, matching task audits4, evidence2, owner/branch match and private evidence files exist. Monitor DTO agrees on status/percent/log/audit counts. This proves retained task observation, not a whole-database checksum or new mutation correctness.

Interception supplies deterministic503/404/error/empty/cancellation coverage separately. No live service-failure injection, new rework fixture, large-volume scale test, or UC032 live PUT in FS005. Existing UC032 write regression is tested by Node/browser interception; FS004 historical live mutation result is not counted as a new FS005 test.

Reuse: build backend/helper, invoke FS004 Start-LiveEnvironment.ps1 -StartApprovedTrial, then from frontend set FS005_LIVE_ENABLED=APPROVED_FS005_READ_ONLY and NODE_EXTRA_CA_CERTS to existing private public PEM, run Playwright with playwright.fs005-live.config.ts. Stop using FS004 Stop-LiveEnvironment.ps1 which validates PID/start-time. Never call credentials-ipc into terminal. Live tests opt in to FS004 helper only inside captured child-process memory.

First harness attempt stopped before tests: missing FS004 helper opt-in, private output redacted. Fixed child environment without changing helper/auth code; later real suite passed. No failed acceptance result is hidden.
