# FASTTRACK-142A checkpoint125 — canonical task lifecycle in RPT06

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=3723b43e59fb9e3d587c94673205f4ee75f39a93
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate unchanged)
SHARED_DB_MUTATION=NO

RPT06 now includes workflowState from the existing authoritative TaskWorkflowRead projection and nullable overdue at the frozen report snapshot under GBR135. Existing physical status remains a separately named source field. A missing workflow row, inconsistent physical/canonical pair, or unavailable capability yields null lifecycle/overdue, never an invented lifecycle or false assurance. Existing authorized report scope, dates, row limit, snapshot and export admission remain unchanged. This is row-level reporting, not all task-effort/shift metrics or full UC135 acceptance.

Direct final evidence checkpoint125-reports-sql-verified.json:50 SQL/CSV component checks PASS, rollback=true. Added coverage exercises missing workflow rows, IN_PROGRESS and PAUSED overdue rows, inconsistent physical/workflow state, and missing capability. Missing-capability verification temporarily removes only the capability marker inside a transaction savepoint, restores it by rolling back the savepoint, then rolls back the entire synthetic fixture. No FK/trigger disabling or shared database mutation. Existing source scope, UTC, frozen CSV, RPT07 deadline/terminal boundaries, empty scope and row-limit tests also run.

Failures retained: first SQL probe failed because EF cannot compare an entire keyless TaskWorkflowRow with null; changed to property comparison. A49-check retry passed. An added missing-capability test then failed because the empty fallback projection did not imply null values; explicit capability conditions now force null. Final50 checks pass. All attempts rolled back. No new HTTPS/browser claim for the RPT06 fields; RPT07 public evidence remains C124.

Backend build PASS0 warnings/errors. Focused report regression39 PASS,1 environment-gated SQL/HTTPS test SKIP. Solution test exits0 without discovery. ModelProbe retains3 existing EF1002 warnings on credential fixtures. Frontend product unchanged. Corrected change-department browser file rerun14 PASS after C124; broad frontend session41837 remains running with one recorded pre-fix failure and no terminal result. Do not restart it or claim full release PASS.

Remaining: UC135 effort/shift aggregation, other report indicators, specialized filters/PDF; FE13 pending lifecycle contracts. Next: finish broad frontend result collection and extend public task-report acceptance where needed. Sprint active, no whole-UC upgrade.
