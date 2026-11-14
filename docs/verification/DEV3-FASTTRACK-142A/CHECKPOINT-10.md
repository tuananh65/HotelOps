# FASTTRACK-142A — checkpoint 10: positive inventory dashboard SQL verification

Baseline `97f858c6795a430205221ac9e7a55bf671faf2f2`, canonical `integration/dev1-dev2-dev3-unblock-20261114`. Previous checkpoint was verified progress. Sprint remains ACTIVE; no complete-UC claim.

Implemented a reproducible positive inventory fixture in `scripts/verification/CanonicalDatabase/ModelProbe/SprintDashboardInventory.cs`, composed into existing `SprintDashboardScenario.cs`. No application business rule, permission, generated entity or schema was changed merely to satisfy tests. Reviewed StockMovement/StockDocument/StockDocumentLine/InventoryItem/Warehouse/StockThreshold entities, DbContext column precision and existing inventory integration fixture conventions before implementation.

The real HTTPS dashboard now has direct nonempty SQL evidence for stock movements and low-stock thresholds. Two items use distinct base units, three warehouses cover permitted department, permitted branch without department, and foreign branch. Synthetic POSTED RECEIPT/ISSUE records retain source document lines and signed decimal movements; direct seeding is test preparation, not inventory posting-service acceptance.

At 2026-11-14 UTC, permitted warehouse/item A receives 10.125 and issues 2.005: expected net 8.120. A prior-day 5 and next-day 100 are excluded from that period but included in current on-hand balance 113.120, below minimum114. Item B is5.5 with minimum5.5 and second warehouse itemA is2 with minimum2: equality must not count as low stock. Foreign warehouse has999/minimum1000 and must not enter totals. Material amounts remain separate by item/unit/warehouse and serialize as strings. Department filter removes the departmentless warehouse; empty later period returns no quantities.

Final command `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-dashboard docs/verification/DEV3-FASTTRACK-142A/dashboard-inventory-verified.json`: **29 checks PASS, cleanup=true**. These include existing auth/scope/session checks plus three positive inventory HTTP reads and inventory assertions; do not describe them as29 separate business UCs. First trial also passed (`dashboard-inventory-first.json`). Final run additionally validates hardened cleanup: capture generated IDs before commit so uncertain commit outcomes remain cleanable; explicit noncancelled cleanup token; verify no owned documents/units/category/items/warehouses/movements remain.

`dotnet build HotelOps.slnx`: PASS0warnings/errors (`checkpoint10-build.log.txt`). Harness build: PASS0warnings/errors. `dotnet test HotelOps.slnx`: exit0 with restore only, no tests discovered (`checkpoint10-solution-test.log.txt`). Frontend code unchanged; npm ci/typecheck/build/browser regression not rerun. Prior checkpoint09 140browser tests and frontend build remain historical evidence, not new runs.

Runtime emitted existing RoomTypeBranchV1 unmapped-index warnings and Skip/Take-without-OrderBy warning; retained as known issues. API process was stopped after verification.

Safety: outer SprintAuthProbe verifies exact server `meita`, disposable database `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0` and run marker `cc78c13ffc124efcad67b3d804baa84c` before any mutation. Seed is transactional, uses random unique prefix and actor; HTTP fixture is committed for a separate API connection to read, then exact-ID/prefix cleanup runs in the outer cleanup transaction. The outer harness also cleans personnel/session/audit data. No shared HotelOpsDB/ImportReview mutation; no credentials committed; no TLS bypass for HTTPS. SQL uses existing explicitly trusted development certificate configuration and is not production certificate acceptance.

Remaining: material request/replenishment positive aggregates, preventive-maintenance projection, UI drill-down, missing KPI sources and browser-to-SQL end-to-end acceptance. UC134 remains PARTIAL. These tests do not verify inventory writer lifecycle, concurrent posting or global warehouse permission policy beyond the existing DASHBOARD_VIEW branch scope.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=10_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
SOURCE_IMPLEMENTED=REAL_INVENTORY_DASHBOARD_SQL_FIXTURE_AND_CLEANUP_VERIFIER
CHANGED_PATHS=scripts/verification/CanonicalDatabase/ModelProbe/{SprintDashboardInventory.cs,SprintDashboardScenario.cs}; docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-10.md,UC_STATUS_MATRIX.md,checkpoint10*,dashboard-inventory*.json}
TESTS_EXECUTED=29_HTTPS_SQL_CHECKS_PASS
BUILD_STATUS=BACKEND_AND_PROBE_PASS_0_WARNINGS;FRONTEND_UNCHANGED
FE05_STATUS=PARTIAL_UNCHANGED
UC006_UC010_STATUS=PARTIAL_UNCHANGED
FE11_STATUS=PARTIAL
FE13_STATUS=PARTIAL
FE14_STATUS=POSITIVE_INVENTORY_DASHBOARD_AGGREGATES_VERIFIED;OTHER_GAPS_REMAIN
FE15_STATUS=AUDIT_READ_PARTIAL;REPORTS_PENDING
ISOLATED_SQL_STATUS=SYNTHETIC_SEED_AND_EXACT_CLEANUP_PASS
HTTPS_API_STATUS=29_CHECKS_PASS_TRUSTED_LOCALHOST
E2E_VERIFIED_WORKFLOWS=HTTP_TO_SQL_INVENTORY_DASHBOARD_READS;NO_BROWSER_SQL_E2E
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_PUBLIC_LIFECYCLE_COMPOSITION;UC006_010_WRITERS;FE11_13_WORKFLOWS;MISSING_KPI_SOURCE_CONTRACTS;BROWSER_SQL_E2E
NEXT_IMPLEMENTATION=Browser-to-SQL acceptance; positive material-request/replenishment/PM dashboard fixtures; confirmed FE11/FE13 workflows

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS are delivered after commit to avoid self-reference.
