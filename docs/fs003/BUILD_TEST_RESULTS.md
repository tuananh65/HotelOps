# FS-003 — Build/test results

Kết quả chạy mới trên branch feature/dev1-room-ops; không cộng historical SQL checks vào totals. No C# source changes.

## Preserve FS-002 before commit

| Command/check | Result |
|---|---|
| Review tracked/untracked files + exact allowlist | 14 files; JSON parses; no real credential/private-key patterns found; manual source/metadata review |
| `dotnet build HotelOps.slnx` | Passed,0 warnings/errors |
| `dotnet test HotelOps.slnx` | Exit0;0 cases (solution contains API only) |
| `dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --no-restore` | 93 passed,0 failed,0 skipped; NU1900 vulnerability-feed unavailable |
| Scoped Prettier | Passed |
| `npm run build` | Passed; React Router use-client ignored, chunks>500kB |
| `npm test` |20 passed |
| DEV1 Playwright, task-owned Vite5189 + external-server flag |5 passed, exit0; interception only |
| `git diff --cached --check` |Passed; exact14 files staged |
| Commit/push/remote |6426d7a773743c789e2395aa087ea703dda083e3; remote feature/dev1-room-ops same SHA |

## Final FS-003 code

| Command/check | Result / scope |
|---|---|
| `npm run typecheck` |Passed after initial implementation; final build also runs TypeScript |
| `npm run build` |Passed after final app-source changes,1953 modules; same inherited directive/chunk warnings |
| `npm test` |28 passed,0 failed:13 prototype+7 FS002+8 UC032 client tests |
| `DEV1_TEST_EXTERNAL_SERVER=1` then `node node_modules/@playwright/test/cli.js test --config playwright.dev1.config.ts` |17 passed, exit0:5 existing board +12 new progress/board cases. HTTP interception, not SQL |
| Scoped Prettier |Passed for DEV1 API TS, progress panel/detail, new tests and DEV1 Playwright config |
| Backend build/xUnit |Same run above remains applicable; backend tree unchanged, no reason to rerun SQL harness |
| Full inherited lint |Not rerun in FS003; last FS002 failed116 files. One scoped detail file now formatted; no broad formatting or claim full lint passes |
| Git diff/secrets/staged allowlist |Checked before publication; no cert/secrets/cookie/session token or generated outputs staged |
| Live HTTPS/API/SQL |BLOCKED_EXTERNAL/Not Run; no live200/401/403/write assertion |

Browser reproduction (Edge installed, isolated port5189 must be free): start `node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5189` in frontend; set DEV1_TEST_EXTERNAL_SERVER=1 in test process and run command above; stop task-owned server after tests. Do not reuse or kill another worktree server. HTTP fixture server does not verify Secure-cookie behavior. Automatic webServer option remains available, but this host's reliable validation uses separately owned Vite to avoid teardown issues documented in FS002.

No new test failures remained. Environment audit metadata connection did not succeed; raw provider messages were redacted. No TLS bypass, SQL DDL/DML/test-user provisioning or actual authenticated backend listener was used. Test fixtures are only in tests; fake token strings cannot authenticate against backend. Build outputs and test reports remain ignored.
