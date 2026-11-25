# Low-stock accuracy — bounded implementation

## Changes
- `LowStockService.cs`: alert now identifies branch/warehouse/item, shortage type, eligible available quantity, minimum, missing quantity, relevant warehouse LAUNDRY and actual incoming/outgoing in-transit quantity.
- Transit is posted shipment base quantity minus posted, unreversed receipts per source line. Unreceived discrepancy stays in transit; pending/approved requests are not counted as dispatched or reserved. Existing role/branch recipient filters, application lock and transaction boundary remain intact.
- Existing active LOW_STOCK notification is refreshed in place (title/message); identity, read/delivery/ack history remain unchanged. Closed episode history is not rewritten; existing close/reopen lifecycle remains.
- New `LowStockAlertText.cs` extracts deterministic text formatting/update behavior.
- `WorkerIntegrationTests.cs`: register LowStockService; assert same episode/IDs and preserved read timestamp after minimum changes, expiry when resolved, new deduplicated episode after shortage returns.
- New `LowStockAlertTextTests.cs`: 5 cases for READY vs total/laundry/transit, base-unit decimal precision, update history preservation, closed/unrelated notification preservation.

## Verification
- No database fixtures or HTTP writes executed; maintenance runtime owns its fixture/5779.
- Isolated solution build started: `dotnet build HotelOps.slnx --artifacts-path artifacts/restructure-v2/lowstock-build --ignore-failed-sources -p:NuGetAudit=false`.
- Initial build and subsequent incremental build failed CS1501 (0 warnings, 1 error): test compiler sees the earlier 9-argument formatter, while final source declares 10 arguments including optional outgoing. The initial build overlapped final edits, so stale incremental assembly is suspected; this is not a verified compiler defect. Failures retained in `lowstock-build.log` and `lowstock-final-build.log`.
- A forced rebuild of frozen final source, followed on success by full solution tests, is running in exec session `21193`: `dotnet build HotelOps.slnx --artifacts-path artifacts/restructure-v2/lowstock-build --no-restore -t:Rebuild`. Logs: `artifacts/restructure-v2/lowstock-rebuild.log` and `lowstock-final-tests.log`. Final result pending. NuGet audit was disabled only for the initial isolated restore invocation, not repository configuration.
- No JavaScript changed, so `node --check` is not applicable to the C# module.

## Remaining evidence required
- Final isolated build and `dotnet test HotelOps.slnx --artifacts-path artifacts/restructure-v2/lowstock-build --no-restore` (do not use results from the build that began before final transit edits).
- Run gated WorkerIntegrationTests only on an isolated `HotelOpsCompactApiTest[a-f0-9]{12}` database prepared with current lifecycle schema and a valid Storekeeper recipient; the test's strict name guard intentionally rejects the shared workday fixture.
- Runtime SQL/HTTP checks: eligible stock changes while still below minimum update same notification; no duplicate recipient/event; reusable READY10/min50/laundry70 reports missing40; incoming/outgoing partial receipts retain only unreceived remainder; draft/approved transfer request does not count as in-transit; reversed shipments/receipts are excluded; wrong-branch recipient never receives notification.
- Confirm EF translation of posted-transfer queries against SQL Server; no runtime PASS claimed yet.

Unresolved business questions: none. Approval reservation policy and operational database are unchanged.
