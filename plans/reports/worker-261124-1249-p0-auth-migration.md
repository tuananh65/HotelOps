# P0 role/auth migration

## Implemented

- `Compact/Security/Access.cs`: HasRole excludes future AssignedAt, matching effective permission checks.
- `Compact/Security/PermissionCatalog.cs`: ROOM_SUPPLY_CONFIG, STAFF_SUPPLY_VIEW, REPLENISHMENT_APPROVE, TRANSFER_REQUEST_CREATE, TRANSFER_REQUEST_APPROVE.
- `scripts/database/compact-v2/p0-role-migration.sql`: TEST-only guarded transaction, explicit eight-role allowlist, three role mappings, session revocation, historical assignment preservation, ENG/HK employment splitting, future template cloning/reuse, capacity/duplicate checks, safe unstarted maintenance reassignment or actionable rejection, immutable audit snapshot and idempotency marker.
- `p0-role-contract.mjs`: explicit source of target grants, matrix generation and static negative-contract assertions.
- `p0-role-schema-check.ps1`: read-only actual schema inspection and SQL PARSEONLY, no migration execution.
- `docs/requirements/p0-261124/permission-matrix.md`: target grants, service restrictions and backup-based recovery.

## Validation

- `node --check scripts/database/compact-v2/p0-role-contract.mjs`: Passed.
- `node scripts/database/compact-v2/p0-role-contract.mjs --write`: Passed contract assertions and generated SQL/matrix.
- Initial read-only SQL check failed in sandbox with SSPI principal error; elevated read-only retry failed with SQL post-login timeout. Actual SQL PARSEONLY has not run. Parent must rerun once connectivity is available.
- C# build, SQL apply/idempotency/rollback and runtime tests deliberately not run by worker; parent owns integration and clone mutation.
- No Production writes; no backend services, generated entities, schema gate, frontend or shared host changes.

## Parent integration notes

Runtime consumers must adopt the new role codes and restrict generic IR/maintenance grants by participant, request kind and state. MAINTENANCE_SUPERVISOR has no assignment-to-technician grant. OM has no incident mutation, ordinary IR mutation or stock posting grant. Owner remains read-only. HK staff has no incident reporting grant. The permission matrix explicitly defines scope for every code.

The script refuses active legacy-role shifts and unresolved/started maintenance reassignment. Those are intentional safety blockers. It preserves past schedule FKs. Re-running a committed migration is a no-op; later contract edits require a new reviewed migration/version, not modifying the existing audit marker.

## Unresolved

Ordinary material-request approval remains user-dependent: no MR_APPROVE_L1/L2/FALLBACK in target grants. Named replenishment approval is separate. Parent must run real migration tests and review any returned unsafe-cutover rows before applying the TEST migration.
