# FAST018 scoped development host

Verification executable only. Program.cs is linked from the unchanged API startup. It reuses real cookie/session, authorization, CSRF and DI. ScopedStartup adds only the NonController assignment candidate to this executable and checks local SQL connection/server/database/owner on every request. It does not activate production dispatch. Never launch against shared SQL.

The existing preparer, installer and component checks ran in this NEW worktree using TestResults/fast017/sql because the unchanged installer intentionally pins that local relative path. No FAST017 worktree or database was reused. Fresh identity and synthetic-only receipt must pass before any setup. Do not copy runtime.private.json, certificate/key/password or SQL files into delivery.

Actual verification commands (from repository root unless noted):

    dotnet build HotelOps.slnx
    dotnet build scripts/verification/AssignmentIntegrationHost
    dotnet build tests/HotelOps.Inspection.WriteTests
    dotnet test tests/HotelOps.Checklist.CollectionTests
    dotnet test HotelOps.slnx

Owned fresh trial was prepared with tests/HotelOps.Inspection.WriteTests/Prepare-Isolated.ps1 and unchanged Install-AssignmentTrial.ps1, followed by seed and fast017-assignment-sql modes. Snapshot-Owned.ps1 checks exact ownership. Its explicit FE05_PREPARE_HTTP_FIXTURE=YES option cancels only synthetic FAST_TASK_2/FAST2 assigned fixture rows to restore a pending queue after the domain suite; this is not a business cancellation writer or acceptance test. Leave the option unset for read-only snapshots.

    dotnet tests/HotelOps.Inspection.WriteTests/bin/Debug/net10.0/HotelOps.Inspection.WriteTests.dll fast018-serve TestResults/fast017/sql/environment.json TestResults/fast017/sql

From frontend:

    npm ci
    npm run build
    npx vite build --config vite.fe05.config.ts
    node --test tests/inspection-api.test.mjs tests/inspection-reads.test.mjs tests/inspection-assignment.test.mjs tests/fe05-checklists.test.mjs tests/fe05-collection.test.mjs
    node tests/fe05-consolidation.live.mjs ../TestResults/fast017/sql
    npx playwright test --config playwright.fe05-fast017.config.ts

The live runner uses built dist/fe05 and an HTTPS proxy with the exact owned certificate, real API login, secure cookies, CSRF and no HTTP interception. It does not test a shared SignInScreen. Initial fixture/permission-expectation failures remain in the handoff; the final suite is 10/10. CHECKLIST_MANAGE is not granted to int.hk; its collection 403 is expected. Intercepted browser regression is reported separately (5/5).

Stop this owned host by creating TestResults/fast017/sql/stop; it also has a 15-minute guard. Do not drop/reset retained databases. Use docs/verification/fe05-fast018 for source and sanitized receipts. Required authoritative notification acceptance remains absent; 503 is the expected mutation outcome.
