# UC100 alternate-unit source completion

2026-11-16: IMPLEMENTED_PENDING_QA. No new COMPLETE_VERIFIED.

Canonical shortage responses now include active item-specific direct UOM conversions to base, plus the base unit. Existing branch, warehouse, INVENTORY_VIEW, REPLENISHMENT_VIEW/CREATE and independent MR_VIEW checks still control disclosure. UI selects item from eligible shortages, selects unit and adjusts quantity. Changing unit clears quantity for explicit confirmation.

Create accepts optional paired InputUomId/InputQty for compatibility with existing base-unit callers. Inside the catalog-locked action transaction, existing Inventory quantity conversion validates active item/unit and direct conversion; converted quantity must exactly equal RequestedBaseQty. Mismatch, incomplete pair, invalid conversion or precision fails before save. Existing source-demand, duplicate coverage, role/grant/session, transaction and audit guards remain. Base-unit persistence is unchanged; creation audit Extra records the entered unit/quantity and accepted base quantity. No generated entity or schema edits.

Validation executed: backend dotnet build HotelOps.slnx --no-restore PASS, 0 warnings; frontend production build and explicit typecheck PASS. Ten direct assertions against transpiled replenishmentQuantity.ts passed: base/alternate units, fractional exact conversion, excess precision, zero/negative amounts, zero factor and unsafe numeric input. Decimal conversion rejects rounding and lossy JSON numbers. Existing React Router directive/bundle-size warnings remain.

Deferred Human QA: UI -> HTTPS -> SQL alternate-unit create, changed/inactive conversion, invalid pairs, stale shortage, duplicate concurrent create, transaction rollback, audit input evidence, manager notification. Prior creation notification evidence remains historical. Notification delivery remains opt-in and requires schema/capability/current grants. No DB writes in this batch.
