# D08-A1 — exact proposed contract, NOT accepted

All three descriptors are implemented in InspectionRecipientPermissionContract.cs. AcceptedConcreteBinding=null, private evidence=false; matching an existing grant/relationship does not grant production read authority. No new permission code, role, catalog record, endpoint or frontend authority is introduced.

| Candidate ID | Existing grant | Independently required persisted relationship | Permitted proposed source summary fields |
|---|---|---|---|
| FE05.RESPONSIBLE_SUPERVISOR.SUMMARY.V1.CANDIDATE | INSPECTION_PERFORM | Exact persisted responsible supervisor for Inspection/task/room/branch/current cycle, retained binding revision | inspectionId, branchId, roomId, cleaningTaskId, cycleRevision, inspectionRevision, workflowState |
| FE05.ORIGINAL_CLEANER.SUMMARY.V1.CANDIDATE | CLEANING_VIEW | Exact R05 AssignmentId/CleanerUserId/CompletionProgressId/completed time/CompletionRevision/WriterTransactionId for task/room/branch/current cycle | inspectionId, branchId, roomId, cleaningTaskId, assignmentId, completionProgressId, completionRevision, workflowState |
| FE05.REWORK_ASSIGNEE.SUMMARY.V1.CANDIDATE | CLEANING_VIEW | Exact persisted Rework assignee, PK/FKs, Rework and Inspection revisions/current cycle; original DECISION08 checks | reworkId, inspectionId, branchId, roomId, cleaningTaskId, reworkRevision, workflowState |

Shared conditions: existing eligible grant path through PermissionGrantRules/EfPermissionDataSource, its assignment/role/time/branch access; active recipient account/employment and active branch/current branch access; exact FAILED source and revision/current cycle; retained actual Users set/physical transaction. OWN is narrowed to the independently proven relationship. Existing BRANCH/GLOBAL grants do not skip relationship checks. Acting user additionally has a real validated SQL session, assigned Inspection ownership and existing INSPECTION_PERFORM branch authorization. No requirement that an offline notification recipient have an active login session is invented.

No broad Inspection history collection, unrelated defects, description/note content, download URL or private file bytes are included. A cleaner may receive only the minimal summary for their own task/cycle; richer failure/criterion details require a separately approved field/read contract, not automatic INSPECTION_VIEW.

## Minimal Project Leader decisions

1. Approve/reject these exact three resource bindings, existing-code mappings and summary field allowlists; explicitly accept the OWN-resource meaning above. This is approval of narrow business/security source reads, not adding broad grants.
2. For a freshly persisted uncommitted Rework, approve/reject same-transaction staging metadata preflight plus mandatory trusted receipt/proof/projection verification before commit. Keep original FE04 different-transaction consumer proof unchanged. A notification recipient relationship alone is insufficient.
3. Only after accepted bindings exist may the closed acceptance checks be replaced by their accepted concrete evaluators. Public mutation gates must remain closed until ALL N01/schema/proof/storage/runtime conditions also pass. No configuration flag can turn this proposal into authority.

Unit tests cover positive BRANCH/GLOBAL/OWN paths and wrong code/branch, revoked/expired assignment, inactive role and legacy DEPARTMENT denial for all roles. New actual-SQL component cases match responsible supervisor, real R05 original cleaner and exact Rework assignee using their own existing grant; revoked grants and wrong relationship deny. These are candidate evaluations, not production source-read acceptance. Completed/replay owner assertions now explicitly reject unaccepted recipient bindings so a stored receipt cannot bypass this gate.
