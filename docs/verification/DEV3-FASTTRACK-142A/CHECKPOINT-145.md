# FASTTRACK-142A checkpoint145 — FE13 read scope and receiving-department integrity

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=0a0895cad9cdf8bb217ab6319a26a43cb4ee6248
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Extended the current-handler revoke counterfactual to use InternalRequestReadAccess.Resolve, the same actor-aware grant resolver as UC123. Losing the scoped read grant is positive Lost evidence when only a global IR_VIEW grant on Owner, Operations Manager or Department Head remains: those roles do not have UC123 global scope. A valid Administrator global grant remains a possible replacement but does not certify the full handler/department responsibility; result stays Unknown. Missing read-role provenance also stays Unknown. This creates no new role policy and changes no grants.

The SQL responsibility reader now requires the current receiving department and branch to be active as well as branch-coherent. Contradictory assignments in inactive scope are reported as INTERNAL_REQUEST_HANDLER:<id>_INCONSISTENT instead of being treated as a valid capability-loss candidate. No authority is inferred from User.DepartmentId or Department.ManagerUserId. Full mapped Department Head responsibility and other release prerequisites are not certified by this change.

Validation: dotnet build HotelOps.slnx PASS, zero warnings/errors; dotnet test HotelOps.slnx exits successfully without discovering the separate regression project. Focused InternalRequestRoleImpactTests 18 PASS. Explicit full backend regression 798 PASS/14 SKIP. Explicit isolated SQL reader test 1 PASS: exact server/database/GUID/run identity, synthetic fixture, SERIALIZABLE transaction, rollback and final empty users/branches/requests assertions. It covers inactive department and inactive branch plus the prior actor/permission/pre-post cases. New global-read replacement combinations have unit coverage; they are not claimed as HTTPS mutation acceptance.

No API/UI changes or new runtime deployment; no frontend rerun claim. No generated entity/schema or shared/production mutation. Revoke/deactivation release gates unchanged. SQL and build logs included as checkpoint145-sql.txt and checkpoint145-build.txt.

Changed files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleImpactTests.cs, InternalRequestRoleSqlTests.cs; this receipt, status matrix and validation logs. Newly completed whole UCs: 0; total stays 24/144. UC006/010 remain PARTIAL for actual endpoint/session rollback and remaining responsibility/writer coverage. Next highest-impact work: complete the outstanding mapped-manager responsibility contract/evidence and actual safe mutation acceptance; resume a pending FE13 lifecycle on its decision; first-pass period semantics remain awaiting the existing question. Sprint goal stays active.
