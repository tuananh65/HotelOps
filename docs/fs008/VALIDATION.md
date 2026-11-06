# Final validation commands and limits

Executed in HotelOps-DEV1 on feature/dev1-room-ops; baseline ac0780105503291386ffa39858b0fc18ae80dde6.

- dotnet build HotelOps.slnx: PASS, 0 warnings/0 errors.
- dotnet test HotelOps.slnx: exit 0, solution contains only API, so this command does not execute regression tests.
- dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --no-restore: PASS 116/116, 0 skipped. NU1900 vulnerability metadata could not reach api.nuget.org; not suppressed or treated as a vulnerability clean bill.
- dotnet build scripts/verification/FS008/FS008.Verification.csproj --no-restore: PASS, 0 warnings/0 errors.
- npm run build: PASS. Existing React Router use-client directive ignored and chunks over 500KB warnings remain.
- npm test: PASS 45/45, including independent Create and prepared assignment wire tests.
- npx playwright test --config playwright.dev1.config.ts: PASS 41/41 interception/browser tests (run before final formatting and addition of isolated negative-request case; production frontend logic unchanged afterward except removal of unused local binding).
- npx playwright test --config playwright.fs008-live.config.ts: PASS5/5, actual final results in LIVE_RESULTS.json; trusted local public certificate, real HTTPS/auth/API/SQL, no route interception or TLS bypass.
- Test-NotificationProposal.ps1: PASS six SQL component checks, 0 rows retained, runtimeAcceptance=false. Install-Lifecycle.ps1 runs install plus read-only verify against exact isolated target.
- Recipient evaluator real SQL branch revoke/rollback: PASS in RECIPIENT_SQL_RESULTS.json, no persistent grant changes.

Intermediate issues are disclosed in FRONTEND_E2E_RESULTS.md. Final SQL guard insertion initially left an embedded BOM that SQL rejected; removed BOM, reran installer/verification/component suite successfully. A final live invocation initially referenced a nonexistent trust.pem instead of the existing localhost-public.pem; TLS correctly rejected requests before business mutations. Correct public certificate path was restored and the whole live suite rerun; ignoreHTTPSErrors remains false. This is harness configuration evidence, not a hidden passing test. NO_COLOR/FORCE_COLOR and Git LF/CRLF notices remain environmental warnings. Scoped dotnet whitespace formatting reports workspace-load warnings (same package metadata limitation); successful build and actual tests recorded separately.

No successful assignment/publisher/delivery/replay, B02 acceptance, full C01 physical-commit interception or production/shared DB validation claimed. No recovery scripts/drop/reset/reseed executed. Only isolated new synthetic FS008 database modified; FS004 retained untouched. Production acceptance and module PR remain NOT_READY.

Diff whitespace check: new implementation/docs PASS. Original provided DEV2 handoff retained byte-for-byte; its seven Markdown two-space hard-break lines trigger git diff --check and are deliberately preserved. Original SHA256 remains f0613ca8e36d59f70985822b20ec55c0c91879ec0ad72f0b006d4a400557d440; checked all other staged paths separately.
