# P0 frontend scope gaps

## Implemented
- `operations-action-center.tsx`: dashboard OM groups backed by real paged requests/documents/work: transfer approvals, warehouse replenishment approval, operational pending assignment/verification/overdue. Original record links. Ordinary material requests are not presented as OM approvals. Monitoring alerts remain separately labelled.
- `transfers.tsx`: sent / received / history tabs, global search/sort then pagination, detail/history/source/children, destination OM draft and item lines, source OM approve/reject, creator submit/cancel, Storekeeper partial dispatch and actual receipt drafts/post. Uses server transfer/inventory capability flags and concurrency versions; no stock mutation on mere approval.
- `documents.tsx`, `inventory-documents.tsx`, `app.tsx`: expose transfer workspace under existing inventory navigation, including Storekeeper route handling, no new sidebar module.
- `use-paged-rows.ts`: cancellable complete authorized page collection. Requests now sort all fetched results before pagination; no misleading per-page ordering. Handler name used if provided, otherwise explicit employee ID.
- `assets.tsx`: recipient accept/reject pending handover using recorded events, current user and current asset version; asset deep links initialize detail for OM too. Backend remains authoritative.
- `insights.tsx`: Action Center follows metrics and personal attendance summary.

## Validation
- Nine edited/new TS/TSX modules transpiled and passed `node --check` twice.
- First full frontend build passed (tsc + Vite, 1678 modules, no warnings). Final rebuild started after small final asset/deep-link/route changes; parent should poll session recorded in worker final.
- No backend or database edits, no Production or server mutations, no Git operations.
- No authenticated browser runtime tests. Do not mark workflows verified from this UI implementation.

## Remaining integration needs
- Action Center lacks aggregated, authorized pending asset handovers, stocktake discrepancies and shift-handover records. It exposes links only for those three categories, not fabricated counts. Backend projection needed for exact actionable records/counts.
- Request DTO still lacks handler/actor display names; UI conditionally supports `handlerName`, otherwise identifies employee ID.
- Server-side request sorting remains preferable for large datasets. Client currently loads all authorized pages before global sorting (cancellation supported); no false partial sorting.
- Asset handover creation/recipient-selection contract and comprehensive asset acceptance semantics require additional work; current addition exposes accept/reject for an existing pending recipient offer only.
- Existing reports expose documents/assets; no dedicated cross-branch transfer/asset-ledger report was added. Transfer history workspace provides actual source documents, but is not a claimed complete report suite.
- Transfer UI needs runtime validation on TEST, particularly partial receipts, rejections, invalid quantities and remounted document routes. Existing source capabilities are used, not guessed roles as write authority.

No unresolved business question; remaining items are implementation and validation work.
