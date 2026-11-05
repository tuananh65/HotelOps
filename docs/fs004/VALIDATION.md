# Validation — 2026-11-05

| Check | Result |
| --- | --- |
| dotnet build HotelOps.slnx | PASS, zero warnings/errors |
| dotnet test HotelOps.slnx | Exit0; solution contains API only, no discovered test project |
| Explicit HotelOps.Dev1.RegressionTests project | 93/93 PASS |
| FS004.Verification helper build | PASS; NU1900 warning |
| Frontend npm run build | PASS |
| Frontend npm test | 28/28 PASS; includes prototype tests, not live acceptance |
| Existing playwright.dev1.config.ts interception suite | 17/17 PASS, 36.4s; separate temporary Vite, no live SQL credit |
| playwright.live.config.ts real browser/API/SQL | 2/2 PASS twice: 56.2s and 19.7s; no reset/reseed |
| New TypeScript/config Prettier check | PASS |
| Four PowerShell scripts AST parse | PASS |
| Setup/helper default plan commands | PASS; writes=false, all18 source hashes matched |
| Final read-only preflight | Local SQL Running; valid/trusted dev cert; target exists/owned; rowversion and optional markers present |
| Actual schema validators | Startup Compatible; BranchV1; Checklists1; Housekeeping/Progress true |
| Final SQL persistence | IN_PROGRESS/40; logs4; audits4; evidence2; private files and actor/branch match |
| Service cleanup | Three owned processes stopped; database/private fixtures retained |

NU1900: package vulnerability metadata could not be fetched from api.nuget.org; build/test execution succeeded. Frontend inherited warnings: react-router use-client directives ignored during bundling and chunks above500kB. Browser runner also reports NO_COLOR/FORCE_COLOR environment precedence. No inherited lint cleanup or full lint success is claimed.

Live evidence: LIVE_RESULTS_FIRST.json and LIVE_RESULTS.json. No screenshot/HAR/trace/token artifacts are committed. Git staged diff/whitespace and scoped secret scans are required before commit. These tests certify the scoped local slices, not frontend login UI, whole Room Lifecycle or production.
