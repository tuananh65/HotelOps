# UC101 coordinated receipt posting authority

Date: 2026-11-16. Status: PARTIAL_IMPLEMENTATION; runtime NOT_TESTED.

A004A-059 now has a compiled production authorization contract. Current Storekeeper, unrestricted SQL session, active branch access, REPLENISHMENT_RECEIVE, REPLENISHMENT_VIEW, INVENTORY_VIEW and separate STOCK_RECEIPT_POST are required in the actual receiving warehouse branch. Linked Material Requests retain independent MR_VIEW/provenance checks. No grants are created.

The exact input/root/principal is bound through the action transaction. Admission and BeforeStockPosting require a Draft receipt in the same warehouse, no source warehouse, reversal, cancellation, existing ledger movement or replenishment credit. All supplied receipt lines must belong to that document, and every credit mapping must match the target replenishment/warehouse/item. BeforePersistence and BeforeDisclosure require Posted state, posting actor/time and accepted quantities matching the input. The existing inventory primitive preserves document rowversion, quantities, conversion, discrepancy reason, ledger and audit checks; outer Serializable action owns commit/rollback and rechecks grants. Replenishment version, net received totals and duplicate coverage remain in the service.

Direct evidence: dotnet build HotelOps.slnx PASS, zero warnings/errors. dotnet test HotelOps.slnx exits zero but the solution contains only the API project: no tests executed, not a test PASS. SQL/HTTPS/browser tests NOT RUN; no database mutation. No claim of COMPLETE_VERIFIED.

Remaining: canonical Draft receipt discovery/verification UI; requester waiting-for-stock notifications per UC080; targeted SQL/HTTPS rollback, concurrency, denied/revoked grant and foreign warehouse tests. Do not enable a raw editable-ID posting form as a substitute for the receipt verification workflow.

## Draft receipt discovery and verification UI increment

Added GET /api/replenishments/{id}/draft-receipts with exact registry/compiled read contract. Requires current Storekeeper plus REPLENISHMENT_VIEW, REPLENISHMENT_RECEIVE, INVENTORY_VIEW and STOCK_RECEIPT_POST, current branch/session and independent linked MR_VIEW. Serializable disclosure returns only same-warehouse Draft receipts with at least one matching open item, active item/unit catalog, no existing stock movements or replenishment credit. Twenty-document cursor pages return every receipt line and both document/replenishment versions, without raw audit or EF entities.

Canonical UI now enables A004A-059 with a receipt selector, explicit accepted/rejected/damaged quantities per displayed receipt unit, discrepancy reasons matching the existing minimum of three trimmed characters, and confirmation of all-line stock posting. Unmatched lines are clearly marked stock-only. Source IDs, mappings and versions are taken from the loaded response; no editable root/version/mapping fields. Refresh/page changes clear selection; async responses are bound to session generation and replenishment version. Existing CSRF transport, uncertain-outcome handling and server atomic checks remain in use.

Direct verification: backend full build PASS, 0 warnings/errors; frontend typecheck/build PASS, existing React Router directive and bundle-size warnings (940.03 kB JS). Solution test command exited zero but executed no tests. API/SQL/browser behavior NOT RUN; no database mutation. These are source/build results, not COMPLETE_VERIFIED evidence. Outstanding: waiting-requester notification and targeted end-to-end acceptance.

Files: ReplenishmentsController.cs; Dev2ActionAdmissionRegistry.cs; ReplenishmentReadContract.cs; ReplenishmentService.DraftReceipts.cs; ReplenishmentPostFields.tsx; Workspace.tsx; catalog.generated.ts; SharedAuthHost.tsx; this receipt.
