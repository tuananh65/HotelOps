# Validation results

| Check | Result | Evidence/boundary |
|---|---|---|
| dotnet build HotelOps.slnx | Passed,0 warnings/errors | BE_BUILD_FINAL.txt |
| dotnet test HotelOps.slnx | exit0,0 discovered cases | BE_SOLUTION_TEST.txt; solution contains API only |
| Explicit xUnit project | 93 passed,0 failed,0 skipped | BE_FRAMEWORK_TEST.txt;91 inherited+2 composition facts |
| DI/route probe | Passed |31 active controllers,186 action/method entries,zero normalized collisions,same scoped participant;DI_PROBE.txt |
| npm ci | Passed |199 packages; deprecations and blocked core-js/esbuild install scripts reported |
| npm run build | Passed | FE_BUILD_FINAL.txt; use-client ignored/chunks>500KB warnings |
| npm test |13 passed,0 failed | FE_TEST.txt; prototype Node tests only |
| npm run lint | Failed | FE_LINT.txt;117 inherited formatting findings; no bulk format |
| Browser Playwright | Not Run | fixed Edge/reused port config; no browser claim |
| HTTPS/SQL/API end-to-end | Not Run | trial not authorized,frontend mock-only,recovery routes inactive |

The xUnit cases include direct calls to inactive recovered controller definitions, not active HTTP route acceptance. New facts verify composed EF model and controller discovery without opening SQL. No mock/no-op publisher or synthetic UC107 success was created.

Retained harness failures: missing OpenAPI interceptor namespace; non-Development Data Protection refusal; incorrect route output cwd. Final probe corrects project/environment/output path without disabling production guards. Logs: DI_PROBE_INITIAL.txt, DI_PROBE_ENVIRONMENT_FAILURE.txt, DI_PROBE_PATH_FAILURE.txt. Reproduce final probe with node docs/fullstack-integration/verify-di.cjs.

Historical A Task80-87 SQL assertions remain historical. No UC credit or COMPLETE claim. Secret/source/worktree/diff review is separate from build/test success.

Staged git diff --check returned exit2: inherited frontend requirements trailing whitespace and two inherited xUnit EOF blanks. Originals remain unchanged to preserve provenance; see STAGED_DIFF_CHECK.txt. Git also reports LF-to-CRLF working-copy normalization warnings. These are reported, not hidden or treated as build errors.
