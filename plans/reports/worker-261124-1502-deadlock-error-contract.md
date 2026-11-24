# Nested SQL failure contract and reservation concurrency

Implemented:

- `src/HotelOps.Api/Compact/Infrastructure/ApiSupport.cs`: walk the exception inner chain to classify genuine SQL failures. SQL 1205 wrapped by EF `InvalidOperationException` now follows the existing HTTP 409 `TRANSACTION_RETRY_REQUIRED` contract. No transaction retry or concurrency relaxation. Business API exceptions retain priority; unknown failures remain 500.
- `tests/HotelOps.Compact.Tests/ErrorMiddlewareTests.cs`: seven focused middleware cases exercise real synthetic SqlClient exceptions through EF wrappers, known SQL mappings, unknown errors, ordinary invalid operations and business errors. Assert one downstream invocation and no leaked exception detail. No database required.
- `scripts/verification/p0-runtime-concurrency-scenarios.mjs`, E2E-07 only: accept either real insufficient-supply conflict or a retry-required deadlock loser; then explicitly retry that loser after the winner commits and require `STAFF_SUPPLIES_INSUFFICIENT`. Check balances and full allocation ledger stay unchanged on retry. Finally cancel successful fixture assignments through the normal API, preserving history.

Interrupted-run recovery is opt-in using `P0_RECOVER_RESERVATION_WORK_ID`. Integration owner must identify the winner first from the existing TEST task/API evidence, set that exact ID for the next E2E-07 run, then clear it. Recovery verifies synthetic branch, exact race title, allowed room 102/103, fixture creator, active fixture HK assignment and ASSIGNED status. It refuses unrelated or started work. No SQL mutation, stock fabrication or deletion.

Verification: `node --check scripts/verification/p0-runtime-concurrency-scenarios.mjs` passed. Build/C# tests deliberately not run because integration owner controls the single build. Runtime not run; no API calls or process changes performed by this worker. Parent must compile and run `ErrorMiddlewareTests`, restart TEST API, resolve the previously assigned winner, then execute E2E-07.

Remaining questions: none. Previous winner ID is intentionally not guessed; runner traces record POST status but not returned work ID.
