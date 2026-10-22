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
Apply the reviewed `20261022_maintenance_material_usage_contract.sql` patch and
synchronize Database First before running the material-usage tests. No EF migration.

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

Complete ordered JSON snapshots of 16 tables before fixtures and after cleanup
must match: Incident/Work/log/event/audit, stock document/line/movement, Material
Request/line/fulfillment, all three FE-10 tables, Asset and material usages. Snapshots stay in
process memory; failure diagnostics print only table positions and changed row keys.
This includes the known existing
mismatch; tests do not use or repair it.

Material Usage V1 adds real FE-09 approval/fulfillment/ISSUE fixtures. Coverage
includes historical source identity, inactive item/UOM/category/warehouse,
base/original UOM snapshots, conversion-master changes, exact decimal arithmetic,
immutable incremental logs, availability/detail/audit projection, duplicate POST
semantics, SQL unique/composite/CHECK enforcement, shared consumed/returned cap,
usable/damaged/reversed RETURN, legacy-valid RETURN and terminal Work history.
Usage itself must leave documents, movements and OnHand unchanged. The old
MaterialIssueId header remains null; omitted/null/empty input stays a plain log.

Sixteen additional races cover usage/usage, usage/RETURN in both orders,
different lines of one ISSUE, inverted multi-document payloads, verify/cancel/
plain-log/return-for-rework in both orders, and Inventory/MR/Replenishment Catalog
writers. Source-document races pause a real locked SQL document read and prove
the second SPID waits on the first; plain commands retain Incident-first locks.
Eleven additional fault/cancellation cases cover three material persistence
flushes, commit, two RETURN flushes/commit and waits on Catalog/document/Incident/
Work. Fresh contexts compare logs/usages/audits/RETURN rowversions/stock effects.
Direct service tests construct InventoryService and MaintenanceWorkService with
the same DbContext; the capacity primitive refuses a missing transaction/Catalog
and must neither commit nor write. Production has no test-only fault switches.

All owned material masters, MR evidence, stock effects, replenishments and audits
(including conversion/threshold audits with ID-only snapshots) are removed in
finally. No shared category is toggled: inactive-category tests own their category.
Existing console suites must run sequentially: IncidentMaintenance, Inventory,
MaterialRequests, Replenishment. `dotnet test HotelOps.slnx` does not discover them.
