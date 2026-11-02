# Public prototype verification — 2026-10-05

- Exactly **80 required screen-level TSX files**, 80 route imports, 15 FE groups and 140 mapped UC identifiers verified by `scripts/verify-screens.mjs`.
- **13/13 service and data tests passed.** Includes no-session access, deterministic sample IDs and persistence, cleaning/inspection, room blocking, inventory posting, material approval/issue and maintenance generation.
- **5/5 browser scenarios passed.** All 80 English routes opened without login or permission fixtures; all 80 Vietnamese titles and default tables rendered; language persisted; reports and the design-system page displayed; mobile stayed within 390px; mock user creation and editing worked.
- **80/80 production-build routes passed in a second browser context.** Root opens the directory; Copy Link uses the current origin; direct detail refresh and design-system refresh work; zero page errors. See `production-preview.json`.
- `npm run lint`, TypeScript compilation and `npm run build` passed. Static files are in `dist/`.
- Build includes non-fatal third-party directive and bundle-size warnings. Spreadsheet and PDF exporters load on demand.

## Default data

40 users, 64 rooms, 40 cleaning tasks, 31 inspections, 32 incidents, 24 scheduled maintenance occurrences, 50 assets, 48 items, 49 stock transactions, 24 material requests, 40 operational tasks, 17 shifts, 11 handovers, 17 internal requests, 55 notifications and 597 audit examples. Maintenance task views combine incidents and scheduled occurrences rather than storing duplicate tasks in the legacy empty `maintenance` array. Room examples include pending cleaning, in-progress cleaning, inspection, rework, ready and blocked states.

Exact collection counts: `mock-counts.json`. Required file list: `screen-files.json`. Working route examples and traceability: `../screen-routes.md` and `../screen-matrix.md`.

## Screenshots

- `public-directory.png`
- `public-dashboard.png`
- `public-design-system.png`
- `public-rooms-mobile.png`

## Hosting status

Vercel and Netlify configurations and a portable SPA `_redirects` file are included. README explains static deployment and post-deployment checks. **No public URL has been published**; the production preview is local only. This is a frontend prototype with fictional browser-local state, not a production security or backend implementation. Traceability covers all 140 UCs; these tests do not independently accept every alternate UC path.
