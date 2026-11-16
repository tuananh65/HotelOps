# FASTTRACK-142A checkpoint144 — FE13 handler actor loss during role revocation

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=62c381b72c0b342f5b4dff732d78c4ab722329d9
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

The UC010 counterfactual reader previously saw surviving IR_PROCESS/IR_VIEW grants without role provenance. An Operations Manager grant could therefore leave the handler capability classified Unknown even when the last eligible handler actor role was removed. The reader now carries actual RoleCode and reuses InternalRequestProgressAuthority.EligibleHandler. Loss of the last eligible scoped handler role is positive Lost evidence, even if an Operations Manager, Owner or Administrator retains the same permission codes. No hard-coded role IDs or catalog changes in product code.

Surviving eligible grants still return Unknown: this change does not certify receiving-department eligibility, mapping, lifecycle or writer coordination. Legacy callers without role provenance retain Unknown. Missing either required permission remains Lost as before. All deactivation/revoke release gates remain intact; no endpoint is enabled.

Direct validation:
- dotnet build HotelOps.slnx: PASS, zero warnings/errors.
- dotnet test HotelOps.slnx: successful exit but no separate regression-project discovery.
- Focused InternalRequestRoleImpactTests: 13 PASS, including three ineligible actors, six eligible actor alternatives and expired/foreign branch alternatives.
- Full regression project: 793 PASS, 14 SKIP, zero failures. SQL opt-in tests are not counted as ordinary suite passes.
- Explicit InternalRequestRoleSqlTests: 1 PASS on exact server/database/GUID/run disposable identity with synthetic fixture inside SERIALIZABLE transaction and rollback. Confirms real EF projection carries role identity; OM with both permissions cannot replace DEPT_MANAGER handler capability; before/after in-transaction selected role removal agrees; an eligible HK_STAFF alternative remains Unknown; expired grants, inconsistent assignment, terminal history and IR_VIEW intersection retain their existing guards. Final assertions verify no users, branches or requests remain. Catalog additions are synthetic transaction-only and rolled back.
- No new API/UI changes; no revoke/deactivation HTTPS/browser acceptance claim. Historical C143 Start/Resume lock evidence is separate.

Changed files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleImpactTests.cs, InternalRequestRoleSqlTests.cs; receipt, UC matrix and validation logs. No generated entity/schema change and no shared/production database mutation.

New COMPLETE_VERIFIED UCs: 0; total stays 24/144. UC006/010 remain PARTIAL for actual mutation/session rollback, remaining role-dependent responsibilities and writer coordination. Next: validate current receiving-department responsibility without conflating read/process authority; complete actual gated mutation acceptance once prerequisites are proven; continue approved business lifecycle work. KPI-03 period selection question remains pending, not a whole-sprint blocker. Goal stays active.
