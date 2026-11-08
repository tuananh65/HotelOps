# FAST002 isolated read/selector verification

Reuse the existing disposable setup and ownership guards from `../InspectionLive`.
Only the reviewed local SQL Express instance and newly created synthetic canonical
trials are permitted. Do not target shared SQL or reset retained trials.

From repository root:

```powershell
./scripts/verification/InspectionLive/Prepare-Isolated.ps1 -OutputDirectory ./TestResults/fast002/new-run
dotnet build HotelOps.slnx
dotnet build scripts/verification/InspectionReadLive/InspectionReadLive.csproj
dotnet scripts/verification/InspectionReadLive/bin/Debug/net10.0/InspectionReadLive.dll serve ./TestResults/fast002/new-run/environment.json ./TestResults/fast002/new-run
```

In another terminal, after READY:

```powershell
cd frontend
npm run build
node tests/inspection-reads.live.mjs ../TestResults/fast002/new-run
```

Real Sign-In/cookie/CSRF/HTTPS/SQL, no interception. The runner checks ownership before
unsafe requests. Fixtures: two assigned inspections, three cleaning tasks (one waiting
without an open inspection), existing HK Supervisor permission grants, inactive MAJOR
and active MINOR in the synthetic catalog. No new permission is granted. Fixture state
is not evidence of a production assignment writer. The suite never calls UC041 writer.

An optional third runner argument is an earlier live-results receipt. Already-PASS
cases are marked NOT_RERUN (login bootstrap is repeated), and the new run executes failed
or unrun cases only. Inspect partial mutation state before continuing. The successful
recorded continuation followed a read failure before any Inspection mutation.

`resume` restarts the owned backend without reseeding. All runtime credentials, keys,
certificates and private artifacts remain under ignored TestResults. The runner stops
its owned backend, leaves the trial intact, and does not reset any historical database.
See FAST002 receipts for the initial collection 500, fix and continuation; do not
present those two runs as a single clean run.
