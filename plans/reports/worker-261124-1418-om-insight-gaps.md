# OM insight backend integration

Scope: Compact Project 2 only. No SQL/mapping/Production or workflow mutations.

## Implemented contract

`GET /api/v2/insights/dashboard?branchId=...` retains its existing fields and adds `actionCenter` (null outside actual branch OM role).

- `updatedAt`, `activeAssets`, `retiredAssets`, `lowStockItems`, `transfersInTransit`.
- `groups[]`: `code`, full `count`, first 10 `items` from original records.
- Codes: `UNASSIGNED_TASKS`, `TASKS_TO_VERIFY`, `OVERDUE_TASKS`, `ASSET_HANDOVERS`, `WORK_HANDOVERS`, `STOCKTAKE_DIFFERENCES`, `TRANSFER_APPROVALS`, `REPLENISHMENT_APPROVALS`.
- Item: `entityKind` (`work`/`asset`/`document`/`request`), `id`, `kind`, `title`, `status`, `occurredAt`, `dueAt`, `canAct`. Frontend opens the existing detail route using this identity. `canAct` is advisory; detail API remains authoritative.
- Domain read grants checked independently. Ordinary internal/material requests are not presented as OM approvals. Replenishment approval capability checks current OM workday and current handler.
- Pending asset handovers use the latest handover event, avoiding old offers that have already been accepted/rejected. OM can act only when the designated recipient.
- Work handovers come from the existing backlog view; no invented shift-handover table/status. These are monitoring links, not new end-shift actions.
- Counted stocktake discrepancies exclude lines already resolved by an unreversed posted adjustment.
- Transfer progress uses actual posted receipts and excludes reversed transfers from the in-transit KPI.

`GET /api/v2/insights/reports/transfers?branchId=...&from=...&to=...`

- Requires REPORT_VIEW and INVENTORY_VIEW for selected branch.
- Flat document headers with nested `lines`; only transfers/requests/receipts involving that branch. Receipt reports derive source branch from their source transfer.
- Header: documentId/no/kind, recordedStatus/status, sourceBranchId/destinationBranchId/sourceDocumentId, timestamps.
- Lines: lineId/itemId/item/unit, requestedBaseQty, approvedBaseQty, actualBaseQty, receivedBaseQty. Actual transfer status uses posted receipt progress. No quantities summed across different units/items.

`GET /api/v2/insights/reports/stocktake-differences?branchId=...&from=...&to=...`

- Same REPORT_VIEW + INVENTORY_VIEW checks.
- Rows: document identity/status/date, line/item/unit, expectedBaseQty, countedBaseQty, differenceBaseQty, needsAdjustment.
- Existing maximum report period (366 days) and 10,000-row cap retained.

## Asset authorization review

Existing acceptance does not depend on removed DH: latest PENDING offer plus exact `ToUserId == actor` is required. Moves remain same branch; rowversion and operation key checked; immutable events and audit retained. Existing policy does not prohibit the initiator also being designated receiver if they are not the current custodian; no new self-review policy was invented.

Fixed branch-assigned OWNER/SYS_ADMIN satisfying receipt authority merely through an ASSET_VIEW grant: these read-only roles are now excluded from the branch recipient capability query. Explicit management capability handling remains existing behavior.

## Files

- `src/HotelOps.Api/Compact/Contracts/OperationsInsightContracts.cs`
- `src/HotelOps.Api/Compact/Services/InsightService.ActionCenter.cs`
- `src/HotelOps.Api/Compact/Services/InsightService.ResourceReports.cs`
- `src/HotelOps.Api/Compact/Services/InsightService.cs` (new response/report dispatch and injected-clock date defaults)
- `src/HotelOps.Api/Compact/Services/AssetService.cs` (read-only role receiver guard)

## Validation

Build initially stalled without output and was cancelled through its own exec session. Retry with `--disable-build-servers -m:1 -p:UseSharedCompilation=false` passed, zero warnings/errors. `dotnet test HotelOps.slnx --no-build --no-restore --disable-build-servers -m:1` passed 74 tests, skipped 3 existing SQL integration tests, failed 0. The subsequent one-line source-branch receipt visibility fix requires the final integration build; another build was started for that change. API/SQL runtime validation remains NOT_TESTED by this worker; parent owns isolated runtime fixture and frontend integration. No JavaScript changed. An attempted syntax check initially used a wrong role-contract path (MODULE_NOT_FOUND); corrected path is `scripts/database/compact-v2/p0-role-contract.mjs`.

## Remaining checks

Final repeat build also stalled without output and was cancelled through its own exec session (no server/process belonging to another task stopped). Parent must build the integrated final tree. Corrected `node --check scripts/database/compact-v2/p0-role-contract.mjs` exited 0.

- Execute dashboard/report queries against isolated SQL TEST to verify EF translation and actual data/permission scope.
- Verify pending offer disappears on receipt decision; stocktake disappears after adjustment and reappears if adjustment reverses.
- Verify both branch OMs see only related transfers/receipts and no third branch documents.
- UI must render the new group/record links and nested transfer report rows.
- Dedicated shift handover does not exist in current schema: existing work handover backlog is the supported source. Parent should not label this as a separate implemented shift-handover lifecycle.
