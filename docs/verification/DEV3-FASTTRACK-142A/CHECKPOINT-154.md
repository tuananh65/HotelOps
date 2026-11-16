# FASTTRACK-142A checkpoint 154 — Received branch consistency

TASK_ID=DEV3-FASTTRACK-142A
INITIAL_REMOTE_SHA=1fbd901f103fa5d6178300eeb5953d5104048722
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=24/144

UC123 approved Received contract requires exact current receiving-department authority within the request's branch, independently intersected with IR_VIEW. The existing filter matched authorized department IDs without comparing that department's branch to the request branch. Actual Database First mapping has an independent receiving_department_id FK, not a composite branch/department constraint. A readable legacy request with inconsistent branch/department could therefore be incorrectly classified as Received through the manager path.

InternalRequestDirection now requires receiving department branch == request branch for manager classification. Requester Sent and independent current-handler Received paths remain unchanged. No mutation authority, IR_VIEW grant, schema, generated entity or shared infrastructure changed.

Files: `InternalRequestDirection.cs`, `InternalRequestDirectionTests.cs`, `SprintRequestDirectionScenario.cs`, this receipt and runtime/cleanup evidence. The regression test grants read scope across both branches and proves that read access cannot substitute for processing responsibility. Runtime creates temporary synthetic read grants in the verified disposable clone, moves only its owned request to a mismatched branch, verifies Sent remains readable and manager Received excludes it, then restores the fixture in finally. Existing route, current-handler replacement, mapping/grant revocation, IR_VIEW intersection and browser linked-incident regressions remain.

Direct validation: solution build PASS, zero warnings/errors; explicit backend regression 817 PASS/14 SKIP; solution test command exit0 (separate regression project not discovered by that command); diff check PASS. Initial ModelProbe build caught an incorrect fixture property name, corrected to actual AccessId. Final probe build PASS with three pre-existing EF1002 warnings in SprintCredentialScenario. Frontend product files unchanged; frontend build/typecheck/npm ci/Auth suite not rerun.

Trial1 PASS97 used an explicit fixture AccessLevel value; changed it to STANDARD to match the actual mapped schema default and reran the final source. [Final trial2 PASS97](checkpoint154-runtime.json) includes HTTPS mismatch rejection and the existing browser direction/link workflow. Both clone cleanup receipts report DROPPED_AND_BACKUP_REMOVED. Independent SQL master query found zero databases with either exact trial name; both backup paths were absent; HTTPS listeners7142/7143 count zero. No shared or production mutation.

UC123 remains PARTIAL: material linked-work visibility and legacy classification contracts remain unresolved. This fixes a proven filter inconsistency, not a whole-UC completion. Continue independent FE13 lifecycle work; do not infer processing permissions from read grants or relax unresolved contracts.
