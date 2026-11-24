# P0 supplies domain — integration handoff

Implemented additive schema, mapped entities, branch configuration versions, shift allocation snapshots, balances/append-only ledger, cleaning reservations and use, internal stock issue/return writer, scoped query/configuration APIs. No SQL applied, runtime started, build run or production changed by this worker.

## Parent integration required

1. Call `MapSupplies(modelBuilder)` from the existing Runtime mapping partial; register `SupplyConfigurationService` and `StaffSupplyService` as scoped. Add schema gate expectations for six new tables.
2. Apply `scripts/database/compact-v2/p0-supplies.sql` only on the backed-up/restored clone `HotelOpsCompactWorkdayTestP0261124`. It rejects production database names. No seed data/warehouse guessing. Stock document kinds `SUPPLY_ISSUE` and `SUPPLY_RETURN` are deliberately inaccessible through generic stock mutation APIs.
3. Add permissions `ROOM_SUPPLY_CONFIG` (OM branch only) and `STAFF_SUPPLY_VIEW` (own HK; branch HK supervisor/storekeeper/OM; owner according to approved matrix). No grants applied by worker.
4. Workday start: after saving WORKING, invoke internal `Start(assignmentId, ct)` inside the existing Atomic transaction **only for HK_STAFF**. Starts without config or sufficient stock succeed as SHORT with actual zero issue; retries return the same allocation. No partial initial issue.
5. Cleaning assignment/reassignment: invoke `Reserve(workId, newUserId, operationKey, ct)` inside the same transaction as assignment. Existing work eligibility remains the caller's responsibility. Uses room type max capacity and shift snapshot; no REWORK deduction. Invoke `Release(workId, key, ct)` on cancellation/unassignment. The hook has no public mutation endpoint and cancellation authorization stays with WorkService.
6. Cleaning completion: invoke `Consume(workId, key, ct)` inside the same transaction. Central stock is unchanged here. Consumption is once per reservation/task/item. For migration, legacy open cleaning assignments have no reservations: establish validated reservations or explicitly quarantine them before enabling the complete hook; do not invent stock.
7. Approved refill: validate the structured request is `CONSUMABLE_REFILL_REQUEST`, PENDING_STOREKEEPER, HK-approved exact quantities, then invoke `Refill(allocationId, requestWorkId, approvedLines, key, ct)` in the same transaction. It independently checks STOREKEEPER/MR_FULFILL, same branch/creator, live HK shift, snapshot items, stock and request idempotency. Parent owns route/status/notification rules.
8. Manual shift end: first resolve pending refill requests according to agreed policy, then `End(assignmentId, key, ct)` **before** saving ENDED in the same transaction. Returns all unreserved remaining supplies; blocks active cleaning/rework and reservations. Auto-expiry policy remains with parent; do not call this own-actor manual hook under an unrelated scheduler actor.
9. Notifications: parent adds initial shortage/issue, refill/return notifications for staff/supervisor/storekeeper and invokes existing low-stock episode checks after automatic documents/config updates. Configuration updates synchronize existing InventoryPolicy.MinimumQty (preserve target unless minimum increases). No alert scheduler changes were made.

## HTTP contracts

- GET `/api/v2/supplies/configuration?branchId=...`
- PUT `/api/v2/supplies/configuration`: SaveSupplyConfigurationRequest, revisions protected by current RowVersion; item or asset exactly one; A requires explicit warehouse/base unit and positive initial/per-guest values; B/C never auto-fill.
- GET `/api/v2/supplies/allocations?branchId=...&userId=...`: own HK or branch scope; latest 100 allocations, explicit DTO balances.
- GET `/api/v2/supplies/allocations/{id}/history`: latest 1,000 ledger entries, scoped DTOs.
- GET `/api/v2/supplies/documents/{id}`: scoped automation document/lines, shift and staff provenance. Use this detail route for storekeeper notification links; generic document detail currently rejects the new kinds.

## Files

- `scripts/database/compact-v2/p0-supplies.sql`
- `src/HotelOps.Api/Compact/Entities/Supply.cs`
- `src/HotelOps.Api/Compact/Data/HotelOpsCompactDbContext.Supplies.cs`
- `src/HotelOps.Api/Compact/Contracts/SupplyContracts.cs`
- `src/HotelOps.Api/Compact/Controllers/SuppliesController.cs`
- `src/HotelOps.Api/Compact/Services/SupplyConfigurationService.cs`
- `src/HotelOps.Api/Compact/Services/StaffSupplyService.cs`
- `src/HotelOps.Api/Compact/Services/StaffSupplyService.Reservations.cs`
- `src/HotelOps.Api/Compact/Services/DocumentService.Supplies.cs`
- `scripts/verification/p0-supply-contract.mjs`

## Validation and limits

The JavaScript integration contract exports lifecycle, shortage and scope assertions for the parent's actual clone fixture adapter. It is not a fake database or inventory seeder. Syntax checking is performed separately; API/SQL/build/concurrency execution must be run by parent after the mapping and host hooks are integrated. Required scenarios include 2+3+4 reservations from 10, rejecting additional 2, retries, old/new staff atomic reassign, independent concurrent starts/assignments/refill, config revisions mid-shift, rowversion conflict, no stock on short allocation, end returns only unused stock, no generic stock reversal of automation documents.

Outstanding: approved explicit warehouse/catalog IDs and auto-expiry resolution policy. No seeded Group A/B/C classifications are invented from Vietnamese names.
