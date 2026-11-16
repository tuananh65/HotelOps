# FASTTRACK-142A checkpoint159 — proven surviving FE13 handler capability

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=4390c90891647cf95f34ee4693224e79801d5835
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

The UC010 counterfactual previously returned Unknown even when current handler capability survived on independently eligible role/grant paths. The SQL reader now supplies positive membership proof only for the unique current assignment in an active, coherent receiving branch/department and an active, non-deleted handler whose current department matches. The grant evaluator returns Preserved only with proven membership, eligible handler actor paths before/after, actor-aware IR_VIEW before/after and preserved IR_PROCESS/IR_VIEW scopes. Missing membership/provenance and contradictory lifecycle remain Unknown; losing a required capability remains Lost.

This uses User.DepartmentId only for the existing GBR149 handler-membership contract, never for Department Head management authority. Explicit managed-department mapping policy remains separate. The generic unresolved responsibility gates and all deactivation/revoke release fuses are unchanged; a Preserved handler result does not authorize the candidate writer or establish safe revocation overall.

Validation: solution build PASS0 warnings/errors; solution test exits0 without discovering the separate project; explicit backend regression826 PASS/14 SKIP. Two new unit cases exercise verified versus absent membership and required process-grant loss. Real isolated SQL test1 PASS, rerun after adding direct comparison with InternalRequestProgressAuthority: valid survivor allows progress before and after selected-role deactivation; removed home membership denies progress and remains Unknown in revoke review. Existing ineligible-role, inactive branch/department, expired grant, future assignment and terminal contradictions remain covered. Before/after counterfactual executes within the same Serializable transaction.

[SQL execution evidence](checkpoint159-sql.txt). The existing test verifies exact disposable server/database/GUID/run identity and empty users/branches before mutation. Finally rolls back all synthetic writes and verifies users, branches and requests empty. No shared/production mutation, schema change, generated entity edit, endpoint/UI change or frontend test claim. No HTTPS candidate revoke/deactivation run in this checkpoint.

Changed files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleImpactTests.cs and InternalRequestRoleSqlTests.cs, plus this receipt, SQL evidence and UC matrix. Remaining highest-impact work: mapped-manager and other designated responsibility contracts, writer coordination across modules, then real revoke/deactivation session/audit/race acceptance. FE13 material/handover decisions remain pending; no protection bypass. Sprint remains active.
