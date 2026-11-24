# P0 Supply alerts integration handoff

Implemented (Project 2 only):
- `LowStockService.cs`: transactional reconciliation, strict stock < minimum; one episode key per policy; recipient/event dedup; active Storekeeper + permission + branch membership only. Existing `ExpiresAt` resolves notifications at stock >= minimum; later shortage creates a new episode. Vietnamese item/unit/available/minimum/shortage text. No schema changes.
- `OperationsWorker.cs`: scheduler calls reconciliation instead of generic INVENTORY_VIEW broadcast.
- `SupplyConfigurationService.cs`: inject LowStockService; reconcile after configuration save; disable prior threshold when configuration disabled or warehouse changed. Preserved concurrent one-warehouse validation edits.
- `DocumentService.Supplies.cs`: supply postings notify Storekeeper once per document and reconcile stock in the same transaction.
- `scripts/verification/p0-low-stock-contract.mjs`: source contract checks only.

Parent integration required (not owned by this worker):
1. Register `LowStockService` scoped in CompactHost. SupplyConfigurationService now requires it.
2. DocumentService ordinary stock posting/reversal/transfer/receipt: invoke `await new LowStockService(db,notifications,clock).Reconcile(ct,affectedBranchId)` after stock movements have been saved and before transaction commit. Both affected branches for cross-branch postings as appropriate. Supply-specific path already hooked.
3. StaffSupplyService Start shortage branch: after SHORT allocation/snapshot saved, invoke `await new LowStockService(db,notifications,clock).InitialShortage(allocation,ct)`. This sends one event per allocation to staff, HK Supervisor (STAFF_SUPPLY_VIEW), Storekeeper (INVENTORY_VIEW). Existing Start retry returns before this hook. Inject notifications/service as preferred. InitialShortage supports zero-config and insufficient stock without issuing anything.
4. NotificationService.Visible: `LOW_STOCK` should require current Storekeeper role + INVENTORY_VIEW. `SUPPLY_INITIAL_SHORT` needs recipient's active branch access and STAFF_SUPPLY_VIEW (own allowed) or Storekeeper INVENTORY_VIEW. `SUPPLY_POSTED` is document-linked Storekeeper INVENTORY_VIEW; existing fallback already handles it. Never authorize by arbitrary notification type alone without role/branch checks.
5. Inventory policy CRUD outside supply config needs immediate Reconcile too.

Concurrency: reconciliation takes transaction-owned SQL application lock; policy rowversion and notification unique key remain enabled. Stock writers retain existing Serializable transactions. Scheduler vs a posting transaction may produce a SQL deadlock; existing Atomic maps 1205 to TRANSACTION_RETRY_REQUIRED and entire transaction rolls back. No success on failed transaction. Integration concurrency tests should exercise retry and prove no duplicate stock/event.

Validation: `node --check scripts/verification/p0-low-stock-contract.mjs` passed. Source contract runner executed separately; see tool output. C# build, SQL/API and concurrency NOT RUN per delegated scope; parent must verify. No server/process started, no database mutation, no production changes.

Required runtime scenarios: minimum 50 / stock 50 no alert; stock 49 single episode; repeated reconciliation no duplicate; receipt to 50 expires old alert and clears policy; later 49 creates new episode; inactive role/account/membership excluded; config threshold change immediately reflected; supply transaction rollback removes stock and notification together; repeated Start/End returns no duplicate notification.

Unresolved business questions: none.
