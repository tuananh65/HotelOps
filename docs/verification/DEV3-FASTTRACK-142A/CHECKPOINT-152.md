# FASTTRACK-142A checkpoint 152 — reject inconsistent cycle timing

TASK_ID=DEV3-FASTTRACK-142A
INITIAL_REMOTE_SHA=268cf9ce3440340355b9cc23266e3c66dc1021b2
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=24/144

KPI-05 now returns unavailable for overlapping work intervals within any processing cycle. Previously an overlapping intermediate interval could pass the boundary checks and yield a numeric metric. This follows the approved requirement not to guess when timing evidence is inconsistent. Adjacent intervals and gaps remain valid; elapsed cycle duration still includes Pause/Return gaps and excludes the final verification wait and Closed-to-Reopen gaps. No persistence, authorization, generated entity or schema changes.

Changed product: `src/HotelOps.Api/Services/Insights/TaskCycleDuration.cs`. Tests: `tests/HotelOps.Dev1.RegressionTests/TaskCycleDurationTests.cs`, four added cases: overlap, adjacency, resubmission gap, overlap in an earlier accepted cycle.

Direct executions in this checkpoint:
- `dotnet build HotelOps.slnx --no-restore`: PASS, zero warnings/errors.
- Explicit regression project: 816 PASS, 14 SKIP, zero failed. The skipped SQL/live tests are not counted as passed.
- `dotnet test HotelOps.slnx --no-restore`: exit 0; this solution command does not discover the separate regression project.
- `git diff --check`: PASS; Git reports expected LF-to-CRLF conversion notices.
- Guarded disposable `TaskCycles` runtime: [319 records PASS](checkpoint152-runtime.json), including real task lifecycle, three accepted processing cycles, dashboard comparison against SQL, browser display and workload regression. This reruns the existing acceptance scenario; it does not prove a new multi-task KPI denominator scenario or new Return-to-KPI runtime coverage.
- [Clone cleanup](checkpoint152-cleanup.json): completed. Independent read-only SQL master query found zero databases with the exact trial name; backup Test-Path returned false; HTTPS listeners 7142/7143 count zero. Shared/production databases untouched.

Frontend sources unchanged; npm ci/typecheck/frontend build/Auth suite not rerun. Their earlier checkpoint results are historical evidence only. No new UC completion claim. UC132 remains PARTIAL pending actor compatibility decision and remaining full acceptance, particularly multiple Closed task denominator and explicit Return/resubmit-to-KPI runtime coverage. Continue those tests and FE13 lifecycle work; sprint remains active.
