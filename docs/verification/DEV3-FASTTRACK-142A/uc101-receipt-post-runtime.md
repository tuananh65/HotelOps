# UC101 receipt Post, rollback and partial closure

2026-11-16. TASK_ID=DEV3-FASTTRACK-142A. No new COMPLETE_VERIFIED UC.

The real HTTPS trial exposed missing `dbo.stock_receipt_line_details` on the disposable recovery database. Added `scripts/database/development/InventoryReceiptDetailsRecoveryV1.sql`, extracting only that table's existing DDL from `database/patches/20261019_inventory_contract.sql` (SHA256 55437D8B08767C9722D323B808231552837C561663E6EDC192CE4670979A0D8E). No generated entities, shared mappings or production configuration changed.

Installer requires exact server/database/GUID, recent checksum backup set 4 and the already verified separate restore, standalone transaction and checked inventory schema application lock. It checks all four columns, PK and enabled/trusted non-cascading FK, records version 1 and rolls back by default. Direct execution: rollback trial returned Applied=false and table absence was verified; Apply=true and idempotent reapply=true. Executed installer SHA256: 15D1936FDC496F24621934D9769059808AEF60F079AAEF2D8A0C662EC8AC1BDF. Only `HotelOps_DEV3_142A_Recovery_2b221f40` was mutated; no shared/production deployment.

## Direct evidence

`recovery-replenishment-post-schema-20261116.json` records PASS and cleanup=true using the real HTTPS API, trusted local TLS, persisted SQL session and synthetic branch/catalog/grants:

- Create, duplicate coverage rejection, scoped list/detail and UC100 notification checks still pass.
- Post without RECEIVE/STOCK_RECEIPT_POST grants: HTTP 403.
- After adding only synthetic role grants, authorized Draft receipt discovery includes the fixture.
- Post 6 against replenishment demand 5: HTTP 400; SQL confirms receipt remains Draft with no posting timestamp, stock movement, receipt detail or replenishment credit.
- Post 2: HTTP 200, Partially Received, net received 2 and remaining 3; exactly one stock movement and one matching credit are persisted.
- Close the remaining 3 with the refreshed replenishment version: HTTP 200; terminal state, released coverage and audit verified.
- Role revocation, CSRF, session revocation and synthetic cleanup pass.

The receipt Draft is explicitly a synthetic fixture, not evidence that the UC079 receipt-creation UI was exercised. No background workers were enabled.

## Initial failure and recovery

`recovery-replenishment-post-20261116.json` is retained unchanged: the schema preflight failed, then cleanup attempted the absent receipt-detail table and rolled back. Its cleanup=false is real. User 37, root 5 and branch 9 were subsequently identified by exact synthetic username, email and branch/item/warehouse codes. A guarded transaction removed only that fixture and verified user/branch absence (`EXACT_FIXTURE_CLEANUP=1`). No receipt document had been created. Cleanup now checks receipt-table presence before querying it so a missing schema does not mask preflight failure or retain unrelated fixture rows.

## Validation and remaining acceptance

Solution build PASS, zero warnings/errors. `dotnet test HotelOps.slnx --no-restore` exits zero but executes no tests. ModelProbe build PASS with the three pre-existing EF1002 warnings in SprintCredentialScenario (not executed). Runtime retains existing RoomTypeBranchV1 index, unordered limiting and multiple-Include warnings. Frontend unchanged this increment; no frontend test rerun claimed. Temporary HTTPS host stopped.

UC101 remains PARTIAL_IMPLEMENTATION: Fully Received, independent Posted receipt link, reversal, concurrency and browser acceptance remain unverified here. Next high-impact work is to finish those lifecycle paths and canonical browser receipt verification rather than infer completion from this partial-flow PASS.

Changed files: RecoveryReplenishmentPost.cs, RecoveryReplenishmentScenario.cs, InventoryReceiptDetailsRecoveryV1.sql, the two runtime JSON artifacts, UC_STATUS_MATRIX.md and this receipt.
