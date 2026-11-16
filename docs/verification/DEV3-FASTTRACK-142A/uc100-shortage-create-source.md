# UC100 shortage selection and canonical Create UI

Date: 2026-11-16. Status: IMPLEMENTED_PENDING_QA, not COMPLETE_VERIFIED.

Compiled A004A-060/061 require HTTPS, unrestricted current SQL session, active STOREKEEPER, branch access, explicit branch, REPLENISHMENT_VIEW + REPLENISHMENT_CREATE + INVENTORY_VIEW. Optional warehouse must belong to the same active branch. Material shortages additionally require independent MR_VIEW; its scope filters the source query before aggregation/count/pagination. Malformed material provenance fails closed. Demand totals represent readable requests, not hidden demand or reservations. No Department Head admission added.

Canonical Create (056) selects low-stock or material-request sources, supports paging and multiple distinct items, editable positive base quantities, optional supplier/date, and clears selection on warehouse/branch change or refresh. Source IDs come from authorized candidates. Existing Create service rechecks source eligibility, MR_VIEW, duplicate coverage, quantity, transaction and audit. Existing authenticated transport retains CSRF and uncertain-outcome handling. Branch/warehouse filters currently use numeric IDs; descriptive selection and runtime QA remain outstanding.

Direct validation:

- dotnet build HotelOps.slnx: PASS, 0 warnings/errors. Initial sandbox restore failed to access local NuGet.Config; rerun with configuration access passed.
- Frontend typecheck and production build: PASS. Existing React Router directive and >500 kB warnings remain (934.21 kB JS).
- dotnet test HotelOps.slnx exited 0, but the solution contains only the API project; no tests executed. This is NOT a test PASS.
- Business/API/SQL/browser integration tests: NOT RUN.
- No database mutation, catalog execution, automatic grants, commit or push in this increment.

Changed source: ReplenishmentReadContract.cs, ReplenishmentService.Queries.cs, ReplenishmentCreateFields.tsx, Workspace.tsx, catalog.generated.ts, SharedAuthHost.tsx.

New COMPLETE_VERIFIED UCs: 0. UC101 coordinated stock posting remains unadmitted; existing posted-receipt linking is separate. Next: runtime QA of UC100 creation/duplicate rejection and UC101 close/link, then complete the separate STOCK_RECEIPT_POST authorization boundary without widening replenishment read access.
