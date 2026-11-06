# Test environment

Verified toolchain: .NET SDK 10.0.402 / net10.0, Node 24.21.0, repository-locked npm dependencies (ExcelJS 4.4, Playwright installed), local SQL Server SQLEXPRESS, Windows DPAPI and Microsoft Edge. Use existing trusted ASP.NET localhost development certificate in CurrentUser store; gateway exports PUBLIC PEM only. No ignoreHTTPSErrors, private-key export, trust-store edit or production data import.

Only SQL target: HotelOps_Canonical_20261106_f100001_Fresh, owner marker HotelOps.FS010.Owner=DEV1_FS010, verified local machine+SQLEXPRESS. Do not point this harness at shared/production. FS004/FS008/FS009 retained unchanged. No reset/reseed/drop of any retained trial. Forward scripts refuse wrong owner/partial packages; recovery refuses populated evidence. Existing exact target creation refuses overwrite. Owner must provision a new separately interlocked target if re-creation is needed; do not edit guards to bypass isolation.

Fresh setup on the designated development machine only (skip create/seed when retained target exists):

    powershell -NoProfile -File scripts/verification/FS010/New-ApprovedTrial.ps1 -ExecuteApprovedCreate -AllowLocalSqlCertificateException
    powershell -NoProfile -File scripts/verification/FS010/Install-Lifecycle.ps1
    dotnet build scripts/verification/FS010/FS010.Verification.csproj
    dotnet scripts/verification/FS010/bin/Debug/net10.0/FS010.Verification.dll seed --approved-seed --local-sql-certificate-exception
    powershell -NoProfile -File scripts/verification/FS010/Install-Candidate.ps1

No commands above write FS009. Local SQL certificate exception is scoped to the verified local fixture and recorded in private metadata, not appsettings. API connection is supplied only to owned child process environment by Start-LiveEnvironment. Normal development connection remains ConnectionStrings:HotelOpsDb in local User Secrets; never copy secrets into these docs.

Synthetic accounts: fs010.admin (existing SYS_ADMIN and branch HK_SUPERVISOR test grants), fs010.owner and fs010.other (HK_STAFF/B1), fs010.denied (negative fixture). B1/B2 numeric IDs, shift windows, rooms/items/type/priority come from .private/fixture.json and authoritative options; no hard-coded role/branch IDs. Passwords are random DPAPI CurrentUser ciphertext, never included in handover. Tester requires owner-assisted credential access or locally provisioned equivalent accounts; credentials-ipc is private in-memory test harness IPC and must NEVER be invoked into terminal/log output. Seed shift is time-limited; revalidate eligibility if running on a later day; do not silently change fixtures to make a failed test pass.

Start candidate environment:

    powershell -NoProfile -File scripts/verification/FS010/Start-LiveEnvironment.ps1 -StartApprovedTrial

UI https://localhost:5173; Vite loopback http://127.0.0.1:5189; API https://localhost:7443. Start script refuses occupied ports and owns PID/start-time receipt. Candidate flag FS010_PROVISIONAL only in Development and exact owner/database marker; omitted/default OFF fails closed. For default-off run, stop owned processes then start with -DisableCandidate. Never enable candidate on shared/production.

Stop before building helper/backend because running API/gateway holds DLLs:

    powershell -NoProfile -File scripts/verification/FS010/Stop-LiveEnvironment.ps1

Build first, start services second, run tests third; no parallel builds against same backend artifacts. Browser runner must inherit NODE_EXTRA_CA_CERTS pointing at FS010/.private/localhost-public.pem; Run-LiveSuite.ps1 does this without disabling TLS. No reset/reseed between full suite repeat runs; if room capacity is insufficient, candidate-expand-rooms appends new synthetic rooms with unique names, preserving all prior rows. Fault fixtures are always disabled in finally; inspect failures instead of resetting evidence.

Commands:

    dotnet build HotelOps.slnx
    dotnet test HotelOps.slnx
    dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj
    npm.cmd --prefix frontend run build
    npm.cmd --prefix frontend test
    powershell -NoProfile -File scripts/verification/FS010/Run-LiveSuite.ps1 -RunId tester_run01

Interception: from frontend run npx.cmd playwright test --config playwright.dev1.config.ts; when using owned Vite set DEV1_TEST_EXTERNAL_SERVER=1. Commit/guard component: dotnet helper candidate-commit-proof / candidate-physical-proof --local-sql-certificate-exception. These modes validate local SQL identity before writes. Gate doubles in commit component are not authentication evidence.
