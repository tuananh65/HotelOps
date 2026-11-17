# User management: four layouts and nine palettes

Run node docs/frontend-redesign/preview/server.mjs and open http://127.0.0.1:60335/?variant=c&palette=8. Loopback-only preview; no deployment.

36 independent layout/palette combinations. A: single sidebar and adjustable split inspector. B: horizontal navigation and full-width table. C: two-tier navigation and quick-view inspector. D: overview counts and separate account views.

Palettes: 1 navy/blue, 2 forest/teal, 3 plum/stone, 4 graphite/ice, 5 deep teal, 6 electric fuchsia, 7 fresh purple, 8 cocoa/wax-paper cream, 9 cloud white/ink. Existing links remain compatible. Query parameters preserve the selected layout and palette.

New colours take inspiration from 2026 forecasting, with custom HEX values adjusted for UI readability rather than official colour specifications:
- WGSN autumn/winter: https://mlp.wgsn.com/webinar-recoreded-key-colours-aw-26-27.html (Transformative Teal, Fresh Purple, Cocoa Powder, Wax Paper).
- WGSN spring/summer: https://www.wgsn.com/cs/node/1955 (Electric Fuchsia).
- Pantone Cloud Dancer: https://www.pantone.com/color-of-the-year/2026.

Scope: account directory, name/email search, status and branch filters, profile, branch grouping and local-only create/edit drafts. Account state/access buttons only explain their preview behavior. All accounts and role assignments are illustrative, not an approved permission matrix. No production frontend/backend, API, authorization or database changes.

This colour update modifies index.html, preview.js, preview.css and README.md. The local verification helper under .impeccable/review was updated for the five new palettes.

Validation: Passed 40 new layout/palette/viewport cases in Playwright/Microsoft Edge at 1440px and 390px. Checked search/email, empty results, status/branch filters, profile navigation, nonmutating account action, draft create/edit and reload reset. No horizontal overflow or browser JS errors. Visually inspected desktop fuchsia and mobile cocoa captures. Original four palettes previously passed 32 cases. JS syntax check: Passed. git diff --check: Passed (preview files are currently untracked). Build: Not Run (standalone HTML/CSS/JS). Real API/database verification: Not Run; no connections are made. These checks cover this preview, not all 80 production screens.
