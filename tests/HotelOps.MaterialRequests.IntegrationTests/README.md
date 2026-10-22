# FE-09 SQL/API integration runner

Run from repository root with .NET 10 and SQL Server. Supply
`ConnectionStrings__HotelOpsDb` in the process environment using local configuration;
never add credentials to tracked files. The runner refuses any target other than
the explicitly approved local implementation database. It launches the built API
on loopback port 5191 without a visible window.

```powershell
dotnet build HotelOps.slnx
dotnet build tests/HotelOps.MaterialRequests.IntegrationTests
dotnet run --project tests/HotelOps.MaterialRequests.IntegrationTests --no-build
```

Apply the reviewed FE-09 schema patch and synchronize Database First before running.
Use a backed-up development database, never production. The runner creates unique
fixtures, retains their IDs, and deletes only those fixtures in `finally`. It does
not reset/reseed the database. Run suites sequentially: rollback assertions compare
database-wide row counts and expect no unrelated writer during the run.

The connection needs fixture read/write permission and permission to inspect
`sys.dm_tran_locks` (SQL Server version-appropriate server-state permission).
Concurrency tests hold the Catalog application lock on a third connection and
wait until SQL's lock manager shows both competing transactions waiting before
releasing it. Each worker uses its own DbContext/connection.

Fault injection uses test-only EF interceptors and an `IInventoryIssuePosting`
wrapper. There are no production fault switches. New connections verify rollback
of fulfillment headers/lines, ISSUE headers/lines, movements, request state,
audits and stock. Tests also check cancellation and discarded tracked state.

Coverage includes revisions/snapshots, retired lines, no-op/stale edits, SoD,
partial/all-zero approvals, Incident subset, typed receiving locations, historical
NeededAt, multi-batch ISSUE, duplicate items/UOMs, conversion changes, shortage,
remainder closure, RETURN lineage, numbering and catalog-versus-EF metadata.
The existing Inventory runner remains the regression suite for its stock workflows
and existing DEV2 API read endpoints.

Actor/approver IDs exercise temporary business identity checks only; this runner
does not prove authenticated authorization. UC-092 coverage is Incident-only.
