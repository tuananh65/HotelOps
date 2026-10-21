# Incident / corrective Maintenance SQL integration runner

Run from repository root using .NET 10 and the existing development database.
Supply `ConnectionStrings__HotelOpsDb` in the process environment from local
configuration. Never put credentials or a developer connection string in source.
The runner verifies the approved local server and exact `HotelOpsDB` name before
writing. It starts the built API on loopback port 5195 without a visible window.

```powershell
dotnet build HotelOps.slnx
dotnet build tests/HotelOps.IncidentMaintenance.IntegrationTests
dotnet run --project tests/HotelOps.IncidentMaintenance.IntegrationTests --no-build
```

The connection needs ordinary fixture read/write permissions and SQL Server
version-appropriate permission to inspect other sessions in `sys.dm_exec_requests`.
Run all integration suites sequentially with no unrelated writers: rollback and
preservation checks include global counts and complete legacy table snapshots.
No database schema patch, EF scaffold or migration is needed for this fix.

Owned fixtures use a unique `ICITEST_` title prefix and retained Incident IDs.
Cleanup checks ownership and deletes only their logs, works, events, audits and
incidents, in a `finally` block. It does not reset/reseed or repair legacy data.
Deliberately mismatched/terminal rows are exclusively owned fixtures. The database
CHECK rejects unknown Work statuses; a test proves that rejection leaves the row
unchanged and captures production guard SQL to prove it has no status predicate.
No constraint is disabled to manufacture an impossible unknown-status record.

Coverage includes all six non-corrective transitions, validation/normalization,
timestamps/events/audits/no-ops, the eight required corrective bypasses,
same-status no-ops with linked work, full paired lifecycle, existing business
guards, cancellation and legacy drift. Incident and Maintenance GET smoke runs
against the API. Actor/assignee/verifier IDs are temporary business identities;
tests do not prove authenticated authorization or a final RBAC policy.

Twelve races cover both lock orders for generic status versus corrective create,
start, submit-result, verify, return and cancel. A test-only command interceptor
pauses the first worker after SQL executes its real `UPDLOCK,HOLDLOCK` Incident
query while its transaction retains the lock. A second independent connection
executes the competing production method. A third connection must observe that
worker's SQL request waiting on the first worker's SPID with `LCK_M_%` before
releasing the first. This proves actual SQL blocking and order, not just task
scheduling. Assertions verify the serialized outcome and exact success history
counts, including a generic request becoming a legal no-op after waiting.

Eleven fault/cancellation cases use test-only EF SaveChanges/transaction
interceptors, actual SQL lock waits and fresh contexts. Generic status/event/audit
and paired status/event/audits are co-persisted in one SaveChanges: tests inject
after that actual SQL flush and assert rollback of all effects. Create and log
operations also exercise their separate business/history flushes. Before-commit
failures and cancellation while waiting for an Incident lock must propagate and
leave no partial state. There are no production test switches.

Complete ordered JSON snapshots of 15 tables before fixtures and after cleanup
must match: Incident/Work/log/event/audit, stock document/line/movement, Material
Request/line/fulfillment, all three FE-10 tables and Asset. Snapshots stay in
process memory and are never printed or saved. This includes the known existing
mismatch; tests do not use or repair it.
