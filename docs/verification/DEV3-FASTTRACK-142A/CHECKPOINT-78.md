# FASTTRACK-142A-ACCELERATOR — Checkpoint 78

NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.

Added real HTTPS assignment races against account/role changes under the same UserEligibilityLock used by the mutation candidates. The existing identity-verified disposable FE11 fixture owns both synthetic actors and tasks. Each race holds the assignee lock, changes account or role eligibility, observes the API request blocked on that SQL session in sys.dm_exec_requests, commits the competing change, then verifies HTTP400 and an unchanged task/assignment/interval/progress/audit/version fingerprint. Fixture authority is restored only after draining the request. This proves the actual assignment writer reads current eligibility after waiting; it does not exercise the unreleased deactivation/revoke endpoint.

[Final runtime](checkpoint78-trial2-runtime.json): PASS,148 shared-run records including both new races and the existing Accept/Decline/Assign/Reassign browser workflows, rollback, notification and ownership assertions. No production source or security gate changed. The fixture uses the ordinary authenticated HTTPS endpoint, CSRF and ETag.

Build and ModelProbe build: PASS,0 warnings/errors. Backend regression:634 PASS,12 SKIP. Solution test exits0 but discovers no test project. Frontend unit/typecheck/build not rerun because unchanged; the existing built canonical frontend was exercised in the browser trial. Existing runtime EF query warnings remain.

[Trial1](checkpoint78-trial1-runtime.json) failed because @@SPID is smallint while the harness expected int. Fixed with an explicit SQL conversion; the failed run is retained. Cleanup initially refused while the terminated host's SQL session still existed. A subsequent read confirmed zero sessions; cleanup then succeeded without killing or bypassing session guards.

Both owned clones and backups were removed. [Cleanup](checkpoint78-cleanup.json) records zero private evidence files and no7142 listener; [source SQL baseline](checkpoint78-source-cleanup.txt) is empty with original guards restored. No shared/production DB mutation.

UC006/010 remain PARTIAL: actual mutation/session rollback and cross-module writer coordination are still unverified, including task execution, recurring generation and other responsibilities. These two assignment races do not lift global release fuses or certify all writers.

NEXT_BATCH=Extend concrete writer evidence to FE11 execution and recurring responsibility, then integrate the remaining deactivation/revoke acceptance; continue independent approved workflows where contracts are sufficient.
