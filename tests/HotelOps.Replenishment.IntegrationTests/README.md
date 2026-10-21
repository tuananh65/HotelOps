# FE-10 real-SQL integration runner

Run from repository root with .NET 10 and the reviewed FE-08/09/10 patches already
applied. Supply `ConnectionStrings__HotelOpsDb` through local process configuration
or local User Secrets; never add credentials or connection strings to source.
This implementation runner explicitly verifies `KIENPC / HotelOpsDB` before writes.
The reusable SQL patch itself requires only the exact database name `HotelOpsDB`.

```powershell
dotnet build HotelOps.slnx
dotnet build tests/HotelOps.Replenishment.IntegrationTests
dotnet run --project tests/HotelOps.Replenishment.IntegrationTests --no-build
```

The runner starts the built API on loopback port 5193 without a visible window.
Run it sequentially with Inventory/Material Request runners and with no unrelated
writers: fault assertions compare database-wide row counts. The connection needs
fixture read/write permission and SQL Server version-appropriate permission to
inspect `sys.dm_tran_locks`.

Each run creates a unique `RPLTEST_` prefix, retains owned item/warehouse IDs and
cleans only owned fixtures in `finally`. It does not reset/reseed or repair legacy
rows. Legacy adoption tests remove detail/movement evidence only from their own
receipts. Cache/numbering tests alter only owned replenishments. Eight existing
business tables are compared as complete ordered JSON before/after cleanup,
including persisted rowversions.

Fourteen race families use independent DbContexts/connections. A third connection
holds the Catalog application lock; the runner requires SQL's lock manager to
show both competing transactions in WAIT before releasing the blocker. Merely
starting tasks together is not accepted as overlap proof.

Test-only EF SaveChanges/transaction interceptors inject failures after real SQL
writes and before commit. Some stages intentionally share one atomic SaveChanges
boundary (receipt state/detail/movements, or cache/status/coverage/audit); labels
identify the semantic effects whose rollback is asserted, not separate production
flushes. Fresh contexts verify complete business, audit and stock rollback.
Cancellation is tested while waiting for Catalog, FE-10 owner and shortage locks.
There are no production fault switches.

Coverage includes the ten HTTP routes, source eligibility/physical shortage,
cross-source uniqueness, exact quantities, numbering/rollback, receipt posting,
safe adoption, reversal/collision, audit JSON, cache reconciliation, master
dependencies, SQL catalog versus EF metadata and read smoke of Incident,
Maintenance and Asset. Actor IDs exercise temporary business identity only;
the suite does not prove authenticated authorization.
