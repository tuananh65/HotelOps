# Live regression

| Independent full run | Outcome |
| --- | --- |
| full01 | 11/12; Create Monitor link not visible within5000ms |
| full02 | 12/12 uninterrupted PASS |
| full03 | 12/12 uninterrupted repeat PASS |

Each full*_LIVE_RESULTS.json has its own runId. No reset/reseed between runs; append25 synthetic rooms before full02 only to supply unused capacity, no per-case data repair. Full02/full03 retained the same data and no intervening room append. Publication-specific delivery isolates cases; unique GUID commands and unused rooms isolate new writes. No focused rerun aggregated as full PASS.

Full01 failed after valid Create/navigation and duplicate checks while waiting for Monitor link before its async data response was explicitly awaited. Test now waits for exact GET Monitor response, asserts200 + correct task in items, then asserts visible link; no timeout increased. New response-ready check passes in both full02/full03. SQL helper timings retained; no claim the original FS009 timeout root cause is solved.

Early interception run shared Playwright default output root and cleared a failed raw error-context artifact; terminal assertion detail and full-run JSON are retained. Subsequent SQL runs are sequential, and handover guide forbids parallel live/other browser suites; any future interception invocation should use a separate output directory. No repeated mock tests are labeled live.

Backend build PASS0warnings/0errors; explicit xUnit135/135 (NU1900 vulnerability metadata warning retained), solution has no test project. Frontend build PASS with existing React Router/chunk warnings; Node46/46, interception47/47. UC028 SQL/preview/navigation/races/rollback/security, UC029 same-key/conflict/concurrency/staging/delivery/recipient policy, UC032 actual progress100 remainsIN_PROGRESS/stale412/owner403 and Monitor/Detail are covered.

Separate Tester functional smoke1/1, helper fault gate denied before SQL, launcher private-memory auth smoke passed. Separate actual restart/default-off receipts are not counted inside12-case full runs. No actual Tester acceptance, production/shared notification acceptance or network-partition proof.

Final separate actual process restart1/1 PASS; default-off real browser/API/SQL1/1 PASS with503/no mutation/Save disabled. All owned developer/Tester services stopped after validation; data/private fixtures retained.
