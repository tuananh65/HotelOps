# FAST003 focused verification

The public Reject and Request Rework paths are intentionally fail-closed. SQL primitives
are not an authoritative conclusion writer or receipt. Do not enable them by clearing
dependency arrays: accepted notification/replay, lifecycle/status projection and source
authority participants are required. Final transaction guards also prevent partial commits.

Run focused loader tests:

```powershell
dotnet run --project tests/HotelOps.Inspection.LoaderTests
```

For an explicitly authorized local synthetic trial only:

```powershell
./tests/HotelOps.Inspection.WriteTests/Prepare-Isolated.ps1 -OutputDirectory ./TestResults/fast003/run
dotnet build HotelOps.slnx
dotnet build tests/HotelOps.Inspection.WriteTests
dotnet tests/HotelOps.Inspection.WriteTests/bin/Debug/net10.0/HotelOps.Inspection.WriteTests.dll serve ./TestResults/fast003/run/environment.json ./TestResults/fast003/run
```

After READY, in another terminal:

```powershell
dotnet tests/HotelOps.Inspection.WriteTests/bin/Debug/net10.0/HotelOps.Inspection.WriteTests.dll sql ./TestResults/fast003/run/environment.json ./TestResults/fast003/run
node tests/HotelOps.Inspection.WriteTests/https.mjs ./TestResults/fast003/run
dotnet tests/HotelOps.Inspection.WriteTests/bin/Debug/net10.0/HotelOps.Inspection.WriteTests.dll sql-rollback ./TestResults/fast003/run/environment.json ./TestResults/fast003/run
```

The setup reuses existing canonical/Housekeeping/D4 installers and retargets only the
existing FE04 lifecycle installer's exact database guard in memory. It never edits that
shared SQL file. Owner GUID/instance/database checks run before setup and test mutations.
The reused property name `HotelOps.FE05INT001.Owner` is a guard namespace, not a claim
that an older trial is reused. Every new trial has its own unpredictable owner and name.

SQL core tests explicitly roll back Reject/Rework primitives, except the conditional
concurrency test which commits one **synthetic core-only FAILED fixture** on the second
inspection. That fixture has no conclusion audit, notification or authority receipt and
must never be handed to FE04 as production authority. The first inspection remains
Pending Conclusion; HTTPS tests prove valid production Reject/Rework requests cannot commit.
The final negative-cycle fixture is likewise retained test data, not lifecycle authority.

HTTPS uses real ASP.NET, SQL-backed credentials/session/cookies/CSRF and a pinned ephemeral
certificate; no interception or mock notification success. Runtime passwords, cookies,
PFX/PEM keys and local output belong only under ignored TestResults. The runner stops
its owned backend and never drops any database.

Initial SQL run stopped because its owner probe lacked transaction binding. That test
harness was corrected; 6 PASS cases were not rerun, and the remaining 9 plus one separate
rollback-failure test passed. Preserve both receipts; do not describe it as one clean run.
`resume` restarts the owned API without reseeding. Inspect partial state before repeating
any test; no automatic replay of successful mutation cases is provided.
