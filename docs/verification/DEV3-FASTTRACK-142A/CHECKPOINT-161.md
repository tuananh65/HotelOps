# FASTTRACK-142A checkpoint161 - mapped manager current-handler capability

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=eb0af822372d22520260ca1964023162d619c5e5
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

The UC010 current-handler responsibility reader now recognizes both existing FE13 progress-authority routes: proven active membership in the current receiving department, or explicit user/branch/current-department management mapping plus an eligible DEPT_MANAGER IR_PROCESS BRANCH path. It reads only the approved installed mapping capability and retains HOLDLOCK on the mapping query in the caller's transaction. Mapping proof is restricted to active employees/accounts, coherent active receiving scope and a unique current assignment. No authority is inferred from User.DepartmentId for management or Department.ManagerUserId.

The counterfactual evaluator reuses InternalRequestProcessAuthority.Eligible. A surviving staff/OWN process grant cannot replace a mapped manager's authority when home-department membership is absent. IR_VIEW, actor restrictions and all previous scope/lifecycle guards remain. Missing/revoked/future mapping cannot establish positive preservation. This is only assigned-handler responsibility coverage; it does not invent a last-capable-manager rule or certify all managerial responsibilities.

Direct evidence:
- Solution build PASS, zero warnings/errors; solution test exit0.
- Separate backend regression: 829 PASS / 14 runtime SKIP / 0 FAIL.
- Focused handler capability tests after final SQL-case additions: 21 PASS.
- Disposable SQL final run: 1 PASS. Exact server/database/GUID/run identity and empty-baseline guards execute before synthetic writes. A real handler with no home department obtains authority through explicit mapping. Revoking the unrelated staff role preserves handler capability before/after mutation and real progress authority; removing the manager role denies progress despite the staff grant. Revoked/future mappings and a changed receiving department deny progress and remain unresolved in the responsibility reader. Existing role/scope/state tests and checkpoint160 grant-drift checks still pass.
- SQL uses Serializable and finally rollback; users/branches/requests are asserted empty after rollback. No new schema installation or shared/production mutation. See checkpoint161-sql.txt.
- git diff --check PASS, with existing LF-to-CRLF normalization notices.

No HTTPS/browser revoke/deactivation acceptance, concurrent mapping-race proof or new UC completion is claimed. The release fuses remain unchanged. UC006/010 remain PARTIAL; mapped non-handler responsibilities, remaining writer coordination, actual candidate-writer/session/audit/rollback/race acceptance remain outstanding. No frontend/generated entity changes.

Changed source/test files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleImpactTests.cs, InternalRequestRoleSqlTests.cs. Documentation: this receipt, SQL output, UC matrix. Next: resolve remaining designated responsibility coverage, then writer coordination and integrated revoke/deactivation runtime acceptance. Sprint remains active.