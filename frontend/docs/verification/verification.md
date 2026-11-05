# Frontend verification

Verified on 5 October 2026.

- 80 distinct screen routes render, including seeded detail records, without browser JavaScript errors.
- All 140 UC IDs map to the 80-screen inventory; all 15 FE identifiers are represented.
- Exactly 80 `.tsx` files exist under `src/screens/`; the validator checks their exports, explicit route imports and registry order against the requirement manifest. `/dev/screens` lives outside that directory and is not included in the count.
- 14 service tests pass: authentication lockout, scope, workday requirements, cleaning completion, row versions, audit redaction, transaction rollback, inspection approval, room blocking, notification isolation, receipt idempotency, material partial fulfillment, scheduled maintenance generation, rich seed volumes/references, deterministic IDs/persistence and pre-refactor URL aliases.
- 7 browser tests pass: all 80 routes, desktop/mobile layout, room create/update and RBAC denial, inspection recording followed by result approval, development index/direct detail refresh, manager employee workload/time logs, and CSV/XLSX/PDF downloads sharing a snapshot.
- Formatting check (`npm run lint`), TypeScript check and production build pass.
- Screenshots: `dashboard-desktop.png`, `readiness-mobile.png`, `refactor-users-desktop.png`, `refactor-rooms-tablet.png`, `refactor-workload-desktop.png`. Detailed browser report: `playwright-report/index.html`.
- Seed dataset counts: `mock-counts.json`. Required filesystem inventory: `screen-files.json`. Reproduce with `node scripts/verify-screens.mjs`.

The export libraries are loaded on demand. Vite reports size warnings for the application and ExcelJS chunks; they do not block the build. These checks verify representative frontend workflows and route coverage, rather than exhaustive acceptance of every alternative and exception flow in all 140 UCs. Backend authentication, private storage, durable scheduling, distributed concurrency and server transactions are not present in this browser prototype.
