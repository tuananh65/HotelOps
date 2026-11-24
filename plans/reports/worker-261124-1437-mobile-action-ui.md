# P0 mobile and OM insight integration

Implemented responsive labeled rows for attendance summaries, Owner comparisons, stock/ledger, catalog supplies, reports/audit and operational attention tables. Weekly OM calendar becomes a labeled day list at <=680px. Desktop tables remain unchanged; no overflow clipping hides values.

OperationsActionCenter now consumes dashboard.actionCenter.groups directly (all backend counts/records/handler advisory). Removed duplicate client fetching and aggregation. Added KPI colors and authoritative detail links. Reports support transfers and stocktake-differences, flatten nested transfer lines for display/CSV without summing unlike units, Vietnamese labels, boolean labels, date filters, pagination and report deep link.

Files: frontend/src/compact/{attendance-summary,owner-overview,stock,catalog,workday,insights,operations-action-center,operations-metrics}.tsx; task-ux.css; frontend/tests/p0-insight-live.mjs.

Validation:
- npm --prefix frontend run build: PASS (latest integrated build finished, Vite 1678 modules).
- node --check frontend/tests/p0-browser-live.mjs and p0-insight-live.mjs: PASS.
- Full read-only browser suite on 5174/5774: 94 PASS, 6 FAIL, evidence artifacts/p0-workflow/browser/results.json (2026-11-24T07:54:54Z). No horizontal-scroll failures in new run. All tested mobile OM calendar, stock/catalog and attendance cases passed except one HK dashboard setup/session failure.
- Failures: DVBV maintenance requests desktop and DVCT HK dashboard mobile said Session lost; DVBV store workday mobile, DVCT admin dashboard desktop, DVCT OM dashboard/requests desktop exceeded loading timeout. No recorded API HTTP errors in those six. Several source changes HMR-loaded during run; session-loss could be transient, not proved authorization failure.
- Dedicated new-insight browser check: FAILED both branches waiting for action center region; direct API diagnostic also timed out. New report/action-center runtime remains NOT VERIFIED. No mock data, no business mutations, no backend edits.
- Browser processes/contexts closed by both scripts. Existing TEST/Production servers left running untouched.

Tool limits: normal sandbox could not read private TEST session fixture; reran read-only browser via explicit reviewed escalation under existing TEST-account authorization. Direct node_modules prettier invocation blocked by context hook; no bypass/config change. Python unavailable; edits used Node with explicit UTF-8 pipeline; corrected temporary damaged labels, final build passed.

Remaining: integration owner rerun new-insight suite after confirming API dashboard exposes nonnull actionCenter for fixture OM and backend responds; rerun six transient read-only failures when source/runtime stable. Do not claim all E2E PASS.
