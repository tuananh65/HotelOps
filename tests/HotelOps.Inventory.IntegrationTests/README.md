# Inventory Auth/Authz regression runner

Run/build from this worktree root on feature/DEV2-inventory-authz-clean. The API and all
HTTP clients use https://localhost:5188 with the trusted .NET development HTTPS
certificate; no certificate bypass is present. Destructive execution requires
ConnectionStrings__HotelOpsDb and the exact SQL guard
KIENPC|HotelOps_Canonical_DEV2_Regression. Never use HotelOpsDB or the frozen
HotelOps_Canonical_DEV2_Test. Do not run until all external fixtures are ready.

## Dedicated Adjustment/Reversal acceptance

Run from this worktree root:

```bash
dotnet run --project tests/HotelOps.Inventory.IntegrationTests/HotelOps.Inventory.IntegrationTests.csproj -- --adjustment-reversal
```

This mode prints `RUN_MODE=adjustment-reversal` and reuses the existing regression
DB guard, API startup/shutdown, authenticated login/logout, cleanup, JSON summary
and failure handling. It runs `Masters()`, `Receipts()` and `Transfers()` only as
prerequisites, then `AdjustmentsAndReversals()`.

It skips general `AuthorizationCases()` and the later unrelated `Stocktakes()`,
`Returns()`, `Races()`, `Rollbacks()`, `DeepTests()`, `DeepRaces()`, `DeepRollback()`
and `ReadsAndRegression()` suites. No-argument mode retains the full sequence.

Positive acceptance in this dedicated mode requires the externally provisioned,
independent HTTP reversal poster via `HOTEL_OPS_INV_REVERSE_POSTER_USERNAME` and
`HOTEL_OPS_INV_REVERSE_POSTER_PASSWORD`. Missing credentials fail closed; this mode
does not use the optional service fallback for positive reversal posting. The
runner provisions no users, permissions or credentials.

## Permission contract

| Endpoint family | Permission / context |
| --- | --- |
| Shared item/category/UOM/conversion reads | INVENTORY_VIEW effective in an active branch; shared records have no branch |
| Warehouse/stock/movement/document/history/stocktake/ledger reads | INVENTORY_VIEW; DB-derived warehouse branches, both sides for transfer document reads |
| Item/UOM/conversion master mutations | ITEM_MASTER_MANAGE global ALL (global shared catalogue) |
| Warehouse create/update, thresholds | ITEM_MASTER_MANAGE on DB-verified branch; warehouse moves require both old and new branches |
| Receipt draft/edit/post/cancel | STOCK_RECEIPT_POST on document/input warehouses |
| Return draft/edit/post/cancel | STOCK_RETURN_POST on document/input warehouses |
| Transfer create/edit/cancel | STOCK_TRANSFER_POST on both source and destination |
| Transfer dispatch / receive | STOCK_TRANSFER_POST on source / destination respectively |
| Direct adjustment proposal / post | STOCK_ADJUST_POST; proposal warehouse / authoritative document warehouse; poster differs from proposer |
| Stocktake approve-adjustment | STOCK_ADJUST_POST; stocktake warehouse; existing counter/approver separation |
| Reversal proposal / post | STOCK_REVERSE; original document and all actual movement warehouse branches; poster differs from reversal proposer |
| Stocktake create/count/submit/reconcile/return/cancel | STOCKTAKE_PERFORM; stocktake warehouse |

Codes are from scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql.
STOCK_APPROVE exists but is not required for the current single adjustment
proposal/post workflow; no additional permission or approval stage is introduced. No OWN rule is
established for Inventory resources: OWN never implicitly grants branch access.
Stocktake approval retains the existing counter/performer separation, including
when an account has both perform and adjustment permissions.

List scope is applied in SQL before count/pagination. Document lists require
both warehouse sides. Direct resource routes use authoritative SQL context;
request/query branch/warehouse IDs alone never prove authorization. Shared
catalogue writes require a global grant because schema has no branch ownership.

## Independent real accounts

Supply _USERNAME and _PASSWORD environment variables for each prefix. Values
are never logged and missing credentials fail before database access. User IDs
come from real login SessionDto; cookie jars/CSRF tokens are independent.

| Prefix | Required external fixture grants |
| --- | --- |
| HOTEL_OPS_INV_SETUP | Global ITEM_MASTER_MANAGE for shared masters and warehouses in both regression branches; no stock posting grant is required by this runner |
| HOTEL_OPS_INV_STOREKEEPER | INVENTORY_VIEW, STOCK_RECEIPT_POST, STOCK_RETURN_POST, STOCK_TRANSFER_POST, STOCKTAKE_PERFORM in normal and foreign regression branches (preserves original cross-branch transfer regression); primary branch = normal |
| HOTEL_OPS_INV_OM | INVENTORY_VIEW, STOCK_ADJUST_POST, STOCK_REVERSE, STOCK_TRANSFER_POST only in normal branch; no foreign-branch grant |
| HOTEL_OPS_INV_APPROVER | INVENTORY_VIEW, STOCKTAKE_PERFORM, STOCK_ADJUST_POST in normal branch, independent of STOREKEEPER and OM |
| HOTEL_OPS_INV_REVERSE_POSTER (optional for service coverage; required for positive HTTP reversal acceptance) | Independent of all other accounts, with STOCK_REVERSE on every original movement warehouse branch; prepared externally, never granted by tests |
| HOTEL_OPS_INV_DENIED | Valid active unrestricted account without Inventory permissions |
| HOTEL_OPS_INV_FOREIGN | INVENTORY_VIEW, ITEM_MASTER_MANAGE, STOCK_RECEIPT_POST, STOCK_RETURN_POST, STOCK_TRANSFER_POST, STOCKTAKE_PERFORM only in foreign branch; no normal-branch access |

Assignments/roles/accounts/branches and branch access must be active and current.
Existing catalogue, normal/foreign departments and at least two suitable branches
must already be prepared externally. The runner grants no permissions and never
provisions passwords. Operational fixtures use production API workflows; existing
service-level concurrency/rollback and legacy direct fixtures are retained.
Other runners are changed only for compilation and need their own session/scope
fixtures before running against secured Inventory routes.

HTTP tests cover anonymous GET/write with genuine CSRF, missing grants, master
mutation separation, foreign warehouse/document/history/ledger/stocktake denial,
warehouse pagination scope, ignored body actor spoofing, actual persisted actors,
receipt/return/transfer branches, source dispatch vs destination receive,
adjustment/reversal permissions and stocktake SoD with legitimate dual grants.
Run the complete suite sequentially because fault tests compare global row counts.
Direct service fault tests use explicitly trusted in-process actors and are not
HTTP authorization tests. No runtime results are claimed by this document.

## Adjustment/reversal proposal and posting

POST /api/inventory/adjustments creates only an ADJUSTMENT DRAFT. The signed delta
is stored in StockDocumentLine.RequestedQty; ActualQty and BaseQty store its
positive magnitude, base UOM and factor 1. Creation produces no stock movements.
POST /api/inventory/adjustments/{id}/post accepts only ExpectedRowVersion (and the
existing optional command reason); line/delta fields cannot change the persisted
proposal. An independent STOCK_ADJUST_POST actor posts the signed movements
after fresh DRAFT/type/version and aggregate negative-stock checks under locks.

POST /api/inventory/documents/{originalId}/reverse creates only a REVERSAL DRAFT,
retaining its original reference and immutable line snapshot. The original stays
POSTED; stock and linked Replenishment credits remain unchanged.
POST /api/inventory/reversals/{reversalId}/post requires an independent
STOCK_REVERSE actor over the original's authoritative warehouse scopes. It
rechecks both documents, original movements, proposal snapshot and current stock,
then atomically posts inverse movements and marks the original REVERSED.
Duplicate proposals/posts and reversal-of-reversal fail closed. DRAFT reversals
are not effective reversal evidence for linked receipt reconciliation.

Stocktake approve-adjustment keeps its existing counter/approver SoD and creates
a POSTED adjustment in the same reconciliation transaction. It needs no third
actor or direct-adjustment proposal step.

Normal fixture setup Adjust() now explicitly proposes as OM and posts as
APPROVER. Six existing accounts provide independent STOCK_ADJUST_POST actors.
Positive HTTP reversal posting additionally requires externally provisioned
HOTEL_OPS_INV_REVERSE_POSTER_USERNAME/_PASSWORD. In the default full runner, if both are absent, it
prints that prerequisite and uses explicitly trusted independent service calls
for positive reversal, rollback and concurrent-post coverage. Those calls do not
prove HTTP posting authorization. Partial credentials fail closed; tests never
grant STOCK_REVERSE or change roles. HTTP denial/self-post tests still run.

Coverage includes DRAFT zero effects, signed proposal persistence, ignored actor
and delta spoofing, independent poster attribution, missing/foreign grants,
stale/double posts, aggregate/post-time shortages, movement/commit rollback,
concurrent proposals and SQL-barrier-backed reversal posting. Existing
receipt/return/transfer and stocktake workflows remain regression coverage.
Runtime has not been executed for this patch.

## Material Request SETUP prerequisites

The shared UOM/item/conversion catalogue requires global ITEM_MASTER_MANAGE;
warehouse creation also requires that permission effective on its branch.
Initial stock can use a legitimate STOCK_RECEIPT_POST account through normal
receipt APIs. Direct adjustment requires two independently authorized actors.
This task does not change Material Requests or grant its requester setup rights.
