# FE05 execution/history live verification

This harness is scoped to UC042/043/048. It reuses the isolation guards and fixture
patterns from ChecklistLive/Task73; it is not an authentication or lifecycle writer.

Only the reviewed local SQL Express instance is permitted. A new uniquely named
canonical trial is created, marked with an unpredictable owner UUID, and checked
before setup mutations and before every unsafe proxied HTTP request. The retained
property name `HotelOps.FE05INT001.Owner` is reused as a guard; the UUID and database
name identify this independent FAST001 trial. Existing trials are never reset or dropped.

From repository root, in an environment authorized for local disposable SQL:

```powershell
./scripts/verification/InspectionLive/Prepare-Isolated.ps1 -OutputDirectory ./TestResults/fast001/new-run
dotnet build HotelOps.slnx
dotnet build scripts/verification/InspectionLive/InspectionLive.csproj
dotnet scripts/verification/InspectionLive/bin/Debug/net10.0/InspectionLive.dll serve ./TestResults/fast001/new-run/environment.json ./TestResults/fast001/new-run
```

In another terminal, after backend prints READY:

```powershell
cd frontend
npm ci
npm run build
node tests/inspection.live.mjs ../TestResults/fast001/new-run
```

Uses ports 7447/7448 and Edge. The HTTPS proxy pins the ephemeral backend certificate;
the test browser accepts this local test certificate. It serves the actual production
frontend build and forwards actual HTTP, without request interception or mock session.
Real SignInScreen logs in synthetic HK Supervisor and denied accounts. Existing catalog
INSPECTION_PERFORM/INSPECTION_VIEW grants must already exist; no permission is granted.

Synthetic fixture prerequisites are two assigned inspections with explicit cleaning-task
bindings and an assigned checklist version. They are fixture state, not evidence of
UC041 or any cleaning-completion/rejection writer. No D4 patch is installed or tested.

`runtime.private.*`, private evidence and credentials exist only under ignored TestResults.
Never copy them into receipts or commits. The runner creates `stop` to terminate its owned
backend; it leaves the trial database intact. No auto-retry of mutation is implemented.
`resume` only restarts an owned backend with existing fixtures. Do not rerun the mutation
suite against partially completed data as if it were a clean test. Inspect receipts first.

Focused checks (HTTP interception is explicitly not live):

```powershell
cd frontend
node --test tests/inspection-api.test.mjs
npx playwright test --config playwright.inspection.config.ts
```
