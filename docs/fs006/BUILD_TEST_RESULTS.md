# Build / test results — 06/11/2026

| Check | Result | Limits |
| --- | --- | --- |
| dotnet build HotelOps.slnx | PASS,0 warnings/0 errors | No production backend source changed |
| dotnet test HotelOps.slnx | Exit0, API-only solution, no discovered test project | Not counted as xUnit acceptance |
| Explicit HotelOps.Dev1.RegressionTests project | PASS107/107 |94 baseline +13 new contract validation cases; no SQL write |
| npm run build | PASS, TypeScript + Vite | Inherited bundle warnings retained |
| npm test | PASS39/39 |33 baseline +6 new planning/draft tests; includes prototype tests, not live acceptance |
| playwright.dev1.config.ts | PASS39/39,51.0s |28 baseline +11 new create/assignment contract tests; HTTP interception |
| playwright.fs006-live.config.ts | PASS2/2,29.2s | Real authenticated HTTPS/API/SQL GET only; LIVE_RESULTS.json |
| Scoped Prettier | PASS on changed frontend files | No full inherited lint cleanup |
| Scoped dotnet format whitespace | New contract test formatted | Existing NU1900 workspace warning, no source error |
| git diff --check / secret scan | Checked before commit, reports retained | Secret pattern scan is scoped, not universal proof |

First browser run:36/37 passed, one timeout selecting the draft staff field by exact accessible label. Fixed explicit aria-label on that select, added pagination/cancellation tests, reran whole suite39/39 passed. No failed result concealed. No failed SQL mutation occurred.

Warnings: regression project NU1900 because NuGet vulnerability metadata from api.nuget.org unavailable; formatter workspace loading warning from that same cached package metadata condition. Frontend React Router use-client directives ignored and chunks above500kB; browser NO_COLOR/FORCE_COLOR precedence; Git checkout may warn LF→CRLF conversion. No new compiler/analyzer warning. No broad lint success claimed.

API coverage: planning current route/method/DTO/permission/branch/search/pagination/errors, no EF responses, cookie transport and cancellation; future POST fresh-CSRF/If-Match/status handling remains blocked and is not called a working feature. UC032 existing save/error/conflict/CSRF/ETag regression runs with interception; real read remains40%. New Create/Assign duplicate/concurrency/audit rollback/notification delivery and refresh-after-write acceptance: NOT_RUN, BLOCKED_CONTRACT and LIVE_WRITE_BLOCKED_APPROVAL.
