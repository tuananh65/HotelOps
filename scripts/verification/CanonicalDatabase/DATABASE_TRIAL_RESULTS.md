# Database trial results — 2026-10-26

## Final run

RunId: `HotelOps_Canonical_20261026_124702_3bcc1bfa`.

Server identity: reviewed SQLEXPRESS instance; SQL Server version `16.0.1000.6`. Execution identity was sysadmin. Trial creation completed at **19:48:44 Asia/Saigon** (12:48:44 UTC). No backup/restore, permissions change, or main-database write was executed.

| Check | Fresh | RBAC upgrade fixture | Auth upgrade fixture |
|---|---|---|---|
| Combined setup | PASS | PASS | PASS |
| Tables / rowversions | 82 / 9 | 82 / 9 | 82 / 9 |
| FKs / CHECKs / defaults / indexes | 238 / 54 / 80 / 201 | Same | Same |
| Roles / active / permissions / grants | 12 / 10 / 62 / 140 | Same | Same |
| Branches / departments / references | 2 / 8 / 15 | Same | Same |
| Users / assignments / sessions | 0 / 0 / 0 | Same | Same |
| CHECKDB / CHECKCONSTRAINTS | PASS | PASS | PASS |
| Canonical seed replay, complete rows unchanged | PASS | PASS | PASS |
| Schema comparison against Fresh | Baseline | PASS | PASS |
| EF model and all 82 entity reads | PASS | PASS | PASS |
| Mapped columns, keys and 238 FKs | PASS | PASS | PASS |
| Nine rowversion mappings | PASS | PASS | PASS |

EF evaluated 279 index mappings per model; 165 missing nonunique convention indexes are advisories. Explicit mappings and unique keys passed. The Fresh `--persistence` run passed EF insert/update generated-rowversion checks, rejected a stale rowversion with `DbUpdateConcurrencyException`, and rolled back its UOM fixture.

Final guard regression passed: main database name rejected, unreviewed instance rejected, existing target rejected without overwrite, exact seed replay, FK/CHECK enforcement, PRIORITY URGENT rejected, all four canonical priorities present, SEVERITY CRITICAL preserved, canonical data unchanged after rollback.

`Test-Catalog.ps1` also passed on all three targets: bidirectional set comparison against release 03 confirms exact role codes/activity/system flags, permission codes/module/resource/action, and role-permission-scope grants. This includes the catalog rebuilt by upgrade 02, not merely aggregate counts. Table-variable seed extraction performs no live catalog writes.

Evidence: the final RunId JSON and three `*-schema.json` files; `final-ef-fresh.json`, `final-ef-upgrade.json`, `final-ef-auth-upgrade.json`; the final RunId `-guards.json` and `-catalog.json`. Source SHA256 values are in the trial report. The original eight SQL sources are unchanged.

## Build and test execution

- `dotnet build HotelOps.slnx`: PASS, zero warnings/errors.
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe/ModelProbe.csproj`: PASS, zero warnings/errors after final changes.
- `dotnet test HotelOps.slnx`: exited 0; the solution only includes the API project and discovers no automated test cases. This is not a claim that application tests passed.
- Actual assertions were executed through the independent SQL trial, guard runner and ModelProbe console project. Existing DEV2 integration runners were not run because their machine/main-database guards are incompatible with this isolated fixture setup; no guard was disabled.
- API endpoint persistence and positive login/authorization: NOT RUN; approved personnel/master fixtures are still absent. Auth schema/permission projections were verified, but session lifecycle and positive branch/SoD scenarios were not.

## Retained intermediate evidence

The earlier RunId `HotelOps_Canonical_20261026_123559_826e9eff` created three isolated databases with the observed URGENT reference value before the explicit GBR-085 decision. They are superseded and retained, not silently converted or deleted. Do not use them as final canonical databases.

`ef-fresh-relational.json` records an intermediate failing index assessment which treated all EF convention indexes as required physical schema. The final probe explicitly distinguishes configuration sources and retains those differences as 165 advisories; no index discrepancy was repaired or concealed. Initial probe project-reference/import compilation errors and transient NuGet NU1900 were corrected; forced restore and final builds succeeded without warnings. No production C# file was changed to resolve them.

## Workspace safety

Main checkout remains on the requested feature branch at `5299b5e`, with no tracked/index diff. New task files remain untracked. Existing untracked files, WAV and both rehearsal worktrees were retained. The Task11 rehearsal worktree has pre-existing staged changes; this task did not stage, reset or modify them. No commit, push, merge or worktree deletion occurred.

These trials demonstrate reproducible setup and synthetic upgrades. They do not certify an arbitrary restored database with historical personnel/transactions, or least-privilege application runtime access.
