# Supply frontend integration handoff

Implemented four additive files only, no shared routes/navigation/backend/SQL/production edits:

- `frontend/src/compact/supply-api.ts`: typed DTOs matching SupplyContracts, save API, Vietnamese quantity/group/error helpers.
- `frontend/src/compact/supply-configuration.tsx`: default `SupplyConfigurationPage`; current branch A/B/C filter/search, revisions, updated timestamps/actor, units, warehouse and minimum/initial/per-guest quantities; compact green/red status switches; create/edit dialogs. Writes require both OM role and ROOM_SUPPLY_CONFIG branch permission. Reads honor STAFF_SUPPLY_VIEW own/branch. New config requires explicit quantities, subject and warehouse; no fabricated catalog defaults. Asset subjects constrained to C; non-A never sends initial/per-guest consumption. RowVersion included. Branch change remounts all forms/data.
- `frontend/src/compact/staff-supplies.tsx`: default `StaffSupplies`; own/branch allocations, status/user filters, actual initial/additional/reserved/used/returned/remaining/available values, shortage explanation, real ledger drawer. Mobile stacks table rows without horizontal scroll. No quantity totals across unlike units. No invented issue/return endpoints: existing workday transition handles those operations.
- `frontend/src/compact/supplies.css`: scoped responsive styles, retained shared dialog/page title/toolbar/status switch/popup components.

## Route owner integration

- Route `supply-config` -> `<SupplyConfigurationPage />`, guard ROOM_SUPPLY_CONFIG or STAFF_SUPPLY_VIEW (including own).
- Route `staff-supplies` -> `<StaffSupplies />`, guard STAFF_SUPPLY_VIEW (including own). Own scope is enforced again by backend even with manually edited URLs.
- Optional `StaffSupplies` props: `onRequestRefill(allocation)` callback navigates to the **real** unified CONSUMABLE_REFILL_REQUEST creation flow. Button is only offered on the current user's nonclosed allocation when callback exists. No guessed hash/API was wired. Request service must revalidate live shift.
- Optional maps `staffNames: Record<number,string>` and `unitNames: Record<number,string>` use already-authorized response data. Current allocation DTO has only userId and unitId, no display names or planned shift date. Fallbacks visibly identify staff/unit by ID; parent should add display names in allocation DTO or supply authorized maps. Do not call privileged `/users` just to fill names. Config references resolve base units via existing workspace options.
- Refills/returns/consumption cannot mutate stock directly from this UI.
- `supplies/documents/{id}` detail page not added: allocation ledger supplies work links; parent may wire document notifications to its supply document detail component.

## Validation

- Three TS/TSX modules transpiled with TypeScript then checked using `node --check --input-type=module`: passed.
- Full `npx tsc --noEmit` session 9182 and `npx tsc -b --pretty false` session 56639 started, still running at handoff preparation; no diagnostics yet. Parent should collect final results, not claim full build success from syntax checks.
- Targeted TypeScript program validation covering all three new TS/TSX modules and their imports: passed (exit 0).
- Formatter direct access blocked by project PreToolUse node_modules rule; no rule/ignore file changed. Formatting is not a functional test failure.
- Browser/API verification not executed: parent must wire routes and test against isolated P0 clone. No production data modified.

## Outstanding integration

Real unified refill callback, authoritative display-name DTO/maps, route registration, browser verification. Backend current configuration endpoint returns only current revisions; this page displays current revision metadata, does not fabricate historical revision browsing.
