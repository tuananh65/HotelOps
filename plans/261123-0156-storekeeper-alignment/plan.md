# Storekeeper alignment — Project 2

Scope: CompactDB only, existing 16 grants, no schema/grant/primary writes. Single agent. Preserve compact controls, branch under logo, navbar titles, closed filters, drawers/modals and pagination.

- [x] Read source, mappings, SRS/change notes; read-only DB verification: role 6, 16 expected grants, one Storekeeper in each DVBV/DVCT.
- [x] Backend: stock dashboard/ledger and document context; guarded linked drafts, date/sort filters; harden actual workflow gaps without new lifecycle.
- [x] Frontend: eight permission-guarded groups, dashboard, catalog/balance, source-linked document actions, independent approvals, personal Workday.
- [x] Isolated SQL/API tests: 38 Storekeeper checks, 128 API regression checks, live browser receipt/fulfillment verified by SQL; no primary writes.
- [x] Typecheck/build, .NET 28 passed/2 environment-dependent skips, 27 Storekeeper browser views, 72 other-role views, all five shared UX suites.
- [x] Review, audit table/report, journal. Fixture APIs stopped by finally; primary API restarted port 5768 PID 27836; existing Vite 5186 retained.

Primary operational blocker (read-only verification): exactly one active account per branch has each return/adjust/reverse permission. Independent approval cannot complete on primary until staffing/authorization owner resolves that configuration. Do not alter grants/accounts here. Strict branch-owned item master and cross-branch transfer remain outside the existing approved schema/workflow. See report implementation-261123-0156-storekeeper-alignment.md.

Evidence: existing items are a shared catalog with an approved V10 branch-write exception in CatalogService. No new global grants will be added. Cross-branch transfers are currently unsupported by the same-branch warehouse contract; retain rejection. Inventory has no reserved-quantity model; display physical balance only.

## Audit
| Feature | Current UI/API | Permission/workflow | Gap/action |
|---|---|---|---|
| Dashboard | Generic personal view | INVENTORY_VIEW; no DASHBOARD_VIEW grant | Add inventory insight using existing domain grants |
| Items | Catalog CRUD | Existing shared V10 catalog exception | Reuse; protect units with history; disclose shared scope |
| Balance | Flat on-hand table | Branch ledger | Add units/policy/last movement and scoped ledger drawer |
| Receipts/issues/transfers | Generic documents | Transactional immutable ledger + SQL constraints | Source selectors, linked drafts, previews, permissions |
| MR fulfillment | Generic MR detail | Approved MR + independent Storekeeper | Linked ISSUE draft with remaining quantities; no approval button |
| Stocktake/adjust/reverse | Generic numeric source IDs | Independent approval + original movement | Context/linked drafts, safe controls and verification |
| Replenishment | Generic documents | Receive permission + receipt | Linked RECEIPT; no double addition |
| History | Audit report requires unrelated grant | Inventory business events | Branch document history and ledger; no AUDIT_VIEW |
| Workday | Shared component | OWN attendance | Reuse via secondary action |
