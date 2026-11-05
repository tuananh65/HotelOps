# Regression results — 06/11/2026

| Check | Result / evidence |
| --- | --- |
| dotnet build HotelOps.slnx | PASS; zero warnings/errors after controller activation |
| dotnet test HotelOps.slnx | Exit0; solution contains API only, no discovered tests, not xUnit acceptance |
| Explicit tests/HotelOps.Dev1.RegressionTests project | PASS94/94; includes new exact-route/cookie/no-anonymous/collision test and recovery isolation; NU1900 warning retained |
| npm run build | PASS after final detail change; TypeScript + Vite |
| npm test | PASS33/33; five new adapter tests plus existing28, including prototype tests not counted as live acceptance |
| playwright.dev1.config.ts interception | PASS28/28,1.1m after final UI change;11 monitor/detail +17 existing room/UC032 |
| playwright.fs005-live.config.ts | Initial PASS1/1,24.4s; final expanded repeat PASS1/1,22.1s, recorded in LIVE_RESULTS.json |
| Scoped Prettier | All changed TypeScript/config/tests checked; no inherited full lint claim |
| git diff --check | PASS before final documentation; final staged check repeated before commit |
| Secret scan | Final changed-file scan performed before commit; no credential fixture, traces or private logs staged |

New xUnit2031 warning in first new route-test run was fixed; rerun94/94 has no new analyzer warning. NU1900 remains: existing cached restore could not fetch NuGet vulnerability metadata from api.nuget.org. Frontend inherited React Router use-client directives ignored, chunks above500kB; browser NO_COLOR/FORCE_COLOR precedence warning. Git may report LF→CRLF conversion according to checkout configuration. No unrelated lint cleanup.

Live is distinct from interception: real login/session/cookie/HTTPS/SQL; no bypass or mocked API. UC032 live read40% verified, live PUT intentionally not rerun because FS005 authorizes only read business operations. Existing Node/interception tests exercise UC032 save, ETag/CSRF/conflicts, preserving its accepted behavior.

Scoped dotnet format whitespace on the new route test: PASS; diagnostic workspace warning is the same NuGet vulnerability metadata fetch issue (NU1900), not a source compilation failure.
