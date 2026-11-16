# UC101 coordinated receipt posting authority

Date: 2026-11-16. Status: PARTIAL_IMPLEMENTATION; runtime NOT_TESTED.

A004A-059 now has a compiled production authorization contract. Current Storekeeper, unrestricted SQL session, active branch access, REPLENISHMENT_RECEIVE, REPLENISHMENT_VIEW, INVENTORY_VIEW and separate STOCK_RECEIPT_POST are required in the actual receiving warehouse branch. Linked Material Requests retain independent MR_VIEW/provenance checks. No grants are created.

The exact input/root/principal is bound through the action transaction. Admission and BeforeStockPosting require a Draft receipt in the same warehouse, no source warehouse, reversal, cancellation, existing ledger movement or replenishment credit. All supplied receipt lines must belong to that document, and every credit mapping must match the target replenishment/warehouse/item. BeforePersistence and BeforeDisclosure require Posted state, posting actor/time and accepted quantities matching the input. The existing inventory primitive preserves document rowversion, quantities, conversion, discrepancy reason, ledger and audit checks; outer Serializable action owns commit/rollback and rechecks grants. Replenishment version, net received totals and duplicate coverage remain in the service.

Direct evidence: dotnet build HotelOps.slnx PASS, zero warnings/errors. dotnet test HotelOps.slnx exits zero but the solution contains only the API project: no tests executed, not a test PASS. SQL/HTTPS/browser tests NOT RUN; no database mutation. No claim of COMPLETE_VERIFIED.

Remaining: canonical Draft receipt discovery/verification UI; requester waiting-for-stock notifications per UC080; targeted SQL/HTTPS rollback, concurrency, denied/revoked grant and foreign warehouse tests. Do not enable a raw editable-ID posting form as a substitute for the receipt verification workflow.
