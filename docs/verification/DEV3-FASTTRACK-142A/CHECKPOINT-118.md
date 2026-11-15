# FASTTRACK-142A checkpoint118 — current Internal Request handler revoke impact

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=46caf4865d722530e886492a837f20af6d17b48f
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

UC010 responsibility review now includes active Internal Request assignments. Ended/future assignments, multiple active owners, invalid receiving-department branch or non-In-Progress state remain unresolved. For a consistent current handler, losing either IR_PROCESS or independent IR_VIEW is positive blocking evidence. Surviving grants remain Unknown because this grant projection cannot certify actor role and department eligibility. No deactivation/revoke release fuse or endpoint changed; no permission grants added.

Backend solution build PASS0 warnings/errors. Solution test exits0 without discovery. Relevant role-impact/revocation/deactivation regression18 PASS,1 SQL test SKIP. New tests cover loss of each permission, surviving grants, contradictory closed state and foreign branch. The new EF query has compiled but has NOT been executed against SQL in this batch. No API/browser/mutation acceptance claim. Frontend unchanged.

Files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleImpactTests.cs and this receipt. UC006/010 remain PARTIAL: actual writer/session invalidation, complete role-dependent responsibility coverage and retained-lock races still require acceptance. Next: disposable SQL verification of current-request responsibility before considering any release change. Sprint remains active.
