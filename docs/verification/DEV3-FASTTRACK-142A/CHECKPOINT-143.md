# FASTTRACK-142A checkpoint143 — task execution User-lock coordination

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=41d1d234b26d483550dea848d3cca9f4461efa4f
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Added actual HTTPS Start and Resume concurrency verification in the existing synthetic assignment lifecycle. The test holds the target task row, launches the authenticated CSRF/ETag-protected executor action, and identifies its blocked SQL session through sys.dm_exec_requests. A separate transaction calls the same UserEligibilityLock used by account/role mutation candidates. Its SQL wait must identify the API session as blocker. After releasing the task row, API returns 200; only then does the competing User lock acquire and observe IN_PROGRESS plus exactly one open interval. This proves actual execution retains the User lock before task writes and through commit, not merely that an authentication read waited.

The competitor never changes roles/accounts. Its transaction rolls back; whole-clone cleanup removes synthetic lifecycle writes. Harness cleanup cancels a waiting competitor and releases its transaction before draining the API, avoiding a probe-created deadlock on a failed assertion. Production source and security release fuses are unchanged. Added TaskExecution mode to the existing guarded clone runner; required capabilities install only in that clone, not the disposable source or shared database.

Validation evidence: backend build PASS0 warnings/errors; harness build PASS with three existing credential-fixture EF1002 warnings. Initial focused test-name filter matched no tests and is NOT a PASS claim. Explicit complete backend regression then ran: 783 PASS, 14 SKIP. Solution test exits successfully without discovering the separate project. Existing production frontend bundle is exercised by the assignment/decline browser scenarios; no new frontend code or frontend-build claim.

First runtime run PASS with 152 shared-run records including Start/Resume lock evidence and the existing account/role assignment races. Final runtime after cleanup cancellation hardening is recorded separately. Check executionUserLock entries for precise proof, rather than inferring it from total checks. Both clones/backups require independent DROPPED_AND_BACKUP_REMOVED receipts; runtime cleanup=false delegates to whole-clone cleanup.

Limits: this does not execute or release account deactivation/role revocation endpoints, does not prove their session invalidation/rollback, and does not certify all cross-module writers. UC006/010 remain PARTIAL. Existing assignment tests prove post-wait eligibility rejection; new tests prove the execution-first lock order. No security guard was bypassed.

Changed files: SprintTaskExecutionUserLock.cs (new), SprintTaskAssignmentScenario.cs, Invoke-Dev3LinkedTaskCreationTrial.ps1, receipt/status matrix and runtime/cleanup evidence. Newly completed UCs: 0; total remains 24/144. Next: extend responsibility coordination to remaining writers and actual gated mutation acceptance; implement first-pass only after its period-selection contract is settled; continue approved independent lifecycle work. Sprint remains active.

Final verification: checkpoint143-execution-runtime.json success=true contains both Start and Resume executionUserLock entries; checkpoint143-execution-cleanup.json and checkpoint143-first-run-cleanup.json both confirm DROPPED_AND_BACKUP_REMOVED. No live runtime work remains for this checkpoint.
