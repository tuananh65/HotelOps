# Inventory real SQL/API integration suite

Run from the repository root after building the API:

```powershell
dotnet build HotelOps.slnx
dotnet run --project tests/HotelOps.Inventory.IntegrationTests
```

Supply `ConnectionStrings__HotelOpsDb` in the test process environment using
your local configuration. Do not commit a connection string. The suite refuses
all writes unless SQL reports the exact task target `KIENPC|HotelOpsDB`.
Port 5188 must be free. The suite launches its own API process without a visible
window and stops only that child process.

This is an executable assertion suite, with no additional test-framework package.
It is run with `dotnet run`; `dotnet test HotelOps.slnx` does not discover these
console assertions. A failure produces a nonzero exit code and a final JSON
summary reports request counts, HTTP status distribution and assertion counts.

Tests use the real SQL provider, transaction-owned application locks and HTTP
requests. They cover catalog validation, rowversions, stock ledger semantics,
receipt/transfer/return/adjustment/stocktake/reversal workflows, all required
concurrency families, SQL catalog metadata and API read regression for the
existing DEV2 modules. Internal issue transaction participation is tested without
implementing FE-09 workflow or authorization.

Fault interceptors exist only in this test project. They inject failures after
executed business/audit SQL and before transaction commit; production has no
fault-injection switch or dependency on the test project.

Fixtures use a fresh `INVTEST_` prefix and runtime-discovered reference IDs.
Cleanup deletes only owned fixture IDs in FK order, including their audits.
Legacy shapes and malformed numbers are simulated only on fixture rows.
No sample/business rows, role grants or schema constraints are modified.
Cleanup runs on both success and failure after target verification. A process
crash can bypass cleanup; retain the printed fixture prefix to investigate.

The suite assumes exclusive use of its API port and no simultaneous test run.
Rollback checks compare table counts, so unrelated concurrent database writes
can make those assertions fail. It does not authenticate the caller or test
role permissions; `ActorUserId` remains temporary integration input.
