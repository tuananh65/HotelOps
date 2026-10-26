# Canonical Database — Task 15

Baseline: `feature/BE1-database-integration`, commit `5299b5e`. Origin develop was queried directly on 2026-10-26 and had the same SHA. No merge was performed.

This package composes the existing releases and four DEV2 patches without modifying them. It creates isolated databases only on the reviewed SQL instance. It never writes to `HotelOpsDB`, changes permissions, restores backups, drops databases, or starts a business API.

Read [installation plan](INSTALLATION_PLAN.md), [seed manifest](CANONICAL_SEED_MANIFEST.md), [schema comparison](SCHEMA_COMPARISON.md), [trial results](DATABASE_TRIAL_RESULTS.md), and [outstanding issues](OUTSTANDING_ISSUES.md).

From the repository root, in PowerShell with the approved SQL identity:

```powershell
& ./scripts/database/release/canonical/Run-Canonical.ps1 -Stage Preflight
$canonicalRun = 'HotelOps_Canonical_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./scripts/database/release/canonical/Run-Canonical.ps1 -Stage Trial -RunId $canonicalRun
& ./scripts/verification/CanonicalDatabase/Test-Guards.ps1 -RunId $canonicalRun
& ./scripts/verification/CanonicalDatabase/Test-Catalog.ps1 -RunId $canonicalRun
dotnet build HotelOps.slnx
dotnet build scripts/verification/CanonicalDatabase/ModelProbe/ModelProbe.csproj
dotnet scripts/verification/CanonicalDatabase/ModelProbe/bin/Debug/net10.0/ModelProbe.dll ($canonicalRun + '_Fresh') ('scripts/verification/CanonicalDatabase/evidence/' + $canonicalRun + '-ef-fresh.json') --persistence
```

Use the probe against `_Upgrade` and `_AuthUpgrade` with separate evidence filenames too. Without `--persistence`, the probe only reads; with it, a UOM fixture tests insert/update/rowversion and stale-write rejection, then rolls back. This is a verification console project, outside the application solution; it introduces no backend architectural layer.

`Trial` refuses any existing target and retains databases after success or failure for investigation. Do not reuse its RunId or retry schema patches on a partial install. `Verify` rechecks existing trial targets without applying setup/seed and writes a separate evidence report. No automatic cleanup is included.

The final canonical contract uses PRIORITY `LOW/NORMAL/HIGH/CRITICAL`, per SRS GBR-085 and the explicit Task 15 decision. Earlier evidence with RunId `HotelOps_Canonical_20261026_123559_826e9eff` predates that decision and is retained as superseded evidence; it is not the final canonical release result.
