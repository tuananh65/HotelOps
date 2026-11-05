# DEV1 regression framework (.NET 10 / xUnit)

Run from repository root:

```powershell
./scripts/verification/Task87/Run-FrameworkTests.ps1
```

The script creates a new results directory and fails on a nonzero exit or zero discovered tests. Raw TRX and a JSON counter summary are retained. To run directly:

```powershell
dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --logger trx
```

This project is deliberately independent of HotelOps.slnx: no shared solution or production file was modified. `dotnet test HotelOps.slnx` alone does not run these cases.

Tests call real production services/controllers with absent identity or fake permission facts. Null persistence dependencies enforce that early validation/authentication must not reach SQL. Controller calls are in-process unit tests, not HTTP server tests. Synthetic ALL/BRANCH/OWN paths verify PermissionService semantics, not installed RBAC grants. No EF InMemory, configuration secrets, database connection, schema creation or protected database access is used.

SQL-required acceptance cases are listed as NOT_RUN_SQL_GATE in the Task87 matrix; they are not placeholder framework cases or counted as skipped tests. Role revisions remain pending; tests do not assign new Reception/Department Head permissions. UC012 deactivation and UC037 cancellation are outside this suite.
