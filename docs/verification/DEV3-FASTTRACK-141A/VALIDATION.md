# Direct validation — 2026-11-15

Environment actually used: Windows PowerShell, .NET SDK 10.0.403, Node v26.13.0, npm 11.20.0. Browser tests used the installed local Edge with synthetic responses. No dependency downgrade or security relaxation was made.

Committed `.log.txt` copies are normalized to UTF-8/LF with trailing whitespace removed; messages, warnings and outcomes are retained. Original PowerShell `.log` captures remain locally ignored. EVIDENCE_HASHES.json records both raw-local and normalized-copy hashes; normalize checkout line endings before comparing copy hashes on Windows.

| Command / scope | Direct result | Evidence / qualification |
|---|---|---|
| `dotnet restore HotelOps.slnx` | PASS, exit 0 | backend-restore.log.txt; rerun after sandbox NuGet access denial |
| `dotnet build HotelOps.slnx` | PASS, exit 0; 0 warnings/errors | backend-build-final.log.txt |
| `dotnet test HotelOps.slnx` | Exit 0, **no test discovery** | solution-test.log.txt; API is the sole solution project |
| `dotnet test tests/HotelOps.Dev1.RegressionTests` | 450 PASS, 11 SKIP, 0 FAIL | dev1-regression.log.txt; live SQL/HTTPS cases skipped |
| `dotnet test tests/HotelOps.Checklist.CollectionTests` | 28 PASS, 0 FAIL | checklist-collection-test.log.txt |
| `dotnet run --project tests/HotelOps.Inspection.DomainTests` | 45 PASS, 0 FAIL | inspection-domain.log.txt; in-memory domain only |
| `dotnet run --project tests/HotelOps.Inspection.LoaderTests` | 36 PASS, 0 FAIL | inspection-loader.log.txt; read orchestration doubles |
| `dotnet run --project tests/HotelOps.Dev2Reconciliation.Offline` | 25 assertions PASS | dev2-offline.log.txt; 0 SQL connections |
| `dotnet run --project scripts/verification/Dev2AllActions -- offline <output>` | 1156 assertions PASS; 61 action components | dev2-actions.log.txt; 26 read/35 mutation bindings; 0 fully accepted business actions; no SQL |
| `dotnet run --project tests/HotelOps.Dev3Unblock.NoSql` (baseline) | FAIL: Dev2AccessDenied not expected by legacy harness | dev3-nosql-initial.log.txt |
| Same NoSQL command after repair | 101 PASS | dev3-nosql-final.log.txt; nine middleware cases + eight first-User-command probes, no physical SQL |
| `npm --prefix frontend ci` | PASS, exit 0; 0 vulnerabilities reported | npm-ci.log.txt; esbuild allowScripts warning retained |
| `npm --prefix frontend run typecheck` | PASS, exit 0 | typecheck-final.log.txt |
| `npm --prefix frontend run build` | PASS, exit 0 | frontend-build-final.log.txt; use-client/chunk warnings retained |
| Targeted Auth/Router with concurrency 1 | 245 PASS, 0 FAIL | auth-router-verified.log.txt; mocked transport, real browser/store/router |
| Canonical same-account authority replacement | 1 PASS | canonical-session-replacement.log.txt; strict DTO, old data removed |
| Initial full `npm test` | 3154 PASS, 1 FAIL, 1 cancelled, 1 SKIP | frontend-tests-initial.log.txt; browser overload timeouts; affected tests passed sequentially |
| First bounded full `npm test` | 3170 PASS, 1 FAIL, 1 SKIP | frontend-tests-bounded.log.txt; old gate-OFF fixture treated canonical wrapper as configurable; fixed by retaining explicit reusable-host opt-out |
| Final bounded full `npm test` | 3171 PASS, 0 FAIL, 0 cancelled, 1 SKIP; exit 0 | frontend-tests-verified.log.txt; all 3172 cases accounted for |
| Prettier on changed TSX/test files + `git diff --check` | PASS | Existing LF-to-CRLF Git notices are not suppressed |
| Real SQL / HTTPS / deployed API | NOT_RUN | SQL_HTTPS_TRIAL_PLAN.md; no mutations or API host startup |

One pre-existing frontend skip is `NOT_RUN_AUTHORIZATION_REQUIRED: target provider positive admission needs mounted browser effects` in a historical test. Its old “browser startup is not authorized” reason is an inherited test skip, not a current DEV1/DEV2 approval requirement. Canonical browser coverage in this task runs explicitly; the skipped scenario is not claimed as passed.

During development, test-only fixture mistakes (late fetch replacement, null synthetic email, old attribute assertions, handover discovery ordering) were corrected against the actual APIs/DTOs/middleware. An attempted broad run with `--test-concurrency` appended after the glob did not limit workers and was stopped; it is not counted as a completed test run. Final package.json places the option before the test paths.

Files under `dev2-actions-offline/` include intentionally generated FAIL-shaped diagnostic fixtures (for example FIX05_TEST_missing.json). These are inputs/results exercised by the successful diagnostic verification harness, not failed live API executions. OFFLINE_ACTION_RESULTS.json is the aggregate assertion receipt. No historical handoff PASS is used as direct validation evidence here.
