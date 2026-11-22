# HotelOps task-focused UX

User approved real 3D hotel selection. Scope: frontend only; preserve database, permissions, existing operations and sticky headers. Single agent. No Git publishing.

## Design brief
Register: Product.
Scene: Hotel staff and administrators working on desktop and smaller screens; fast, clear operational decisions.
Direction: Existing calm green/ivory HotelOps shell, compact controls and task-oriented content.
Color: Restrained; preserve --bg #f4f7f6, --ink #203b38, --accent #21665e.
Type: Existing Segoe UI/system UI, weight and scale hierarchy.
Signature: Fixed-angle architectural hotel models with name/status selection; procedural illustrations, not actual floor plans. User subsequently removed rotation controls.
Dials: variance 3, motion 2, density 6.

## Implementation checklist
- [x] Split hotel structure, system settings and operational catalogs; remove header selector and duplicate navigation; hide Audit, keep notifications by account only.
- [x] Modular Three.js building and fixed camera; cached rendered model cards, keyboard selection and no-WebGL fallback. Only reveal hotel structure after explicit hotel selection.
- [x] Real administration/operations charts, including rooms/departments per hotel and permissions per role; colored metrics. No synthetic trends or totals across incompatible stock units.
- [x] Compact account search/status filters with counts and empty states.
- [x] Vietnamese names/descriptions for all 67 actual permission codes in UI and role dialog; remove group/code columns; LED status; settings dropdown.
- [x] Sticky account bar and list headings below it.
- [x] Build, 4 workflow tests, 12 live UI scenarios, dedicated 67-permission checks, 375/768/1440 screenshots and report.

## Sequential delivery
Initial hotel/account/navigation requests verified first. Permission copy/dropdown/LED completed and tested before implementing added dashboard colors and room/department/permission charts, as requested.
The user asked whether a separate permission catalog is useful. Recommendation delivered: keep role-based permission editing and hide the standalone catalog; this was advice, not a new implementation instruction, so the current dropdown still includes the catalog.

## Evidence
See plans/reports/test-261122-1808-admin-ux.md and artifacts/admin-ux/. Final visual review corrected an empty chart grid column and tablet metric gaps.

## Runtime reconciliation
Keep user-facing Vite 5186 (node PID 41940, launcher 35444) and API 5768 (42448). Other workspace services left untouched. No disposable processes yet.

## Validation constraints
Back up primary DB before login checks, which write auth state. No business data mutation for this UI change. Test actual allowed roles and API scoping. Measure only observed browser performance, no device-wide performance claims.
