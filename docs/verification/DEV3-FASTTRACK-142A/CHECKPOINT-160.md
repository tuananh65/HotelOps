# FASTTRACK-142A checkpoint160 - revoke grant provenance final reread

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=d39d2243f1b87d608f94ee0db3edc6e1b6b8c77f
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

UC010's final reread previously compared collapsed permission/scope/branch summaries. Equal summaries can conceal a changed actor role, assignment or validity window, although feature authorization depends on those paths. The reread now compares the full eligible RevokeGrant records (assignment, role code and PermissionGrantPath) as sets at the same UTC instant. Existing audit summary strings are unchanged. Inactive paths do not grant authority; row ordering is irrelevant. This is drift detection, not a new permission grant or release authorization.

Direct validation this checkpoint:
- dotnet build HotelOps.slnx --no-restore: PASS, 0 warnings/errors.
- dotnet test HotelOps.slnx --no-restore: exit0; separate regression project run explicitly.
- Backend regression: 828 PASS, 14 runtime-dependent SKIP, 0 FAIL.
- Two new unit tests cover equal-summary role/assignment/window changes, missing role provenance, missing/additional surviving paths, ordering, inactive selected assignment and non-UTC rejection.
- Exact-disposable SQL: 1 PASS. Existing identity guards verify server/database/GUID/run and an empty baseline before synthetic writes. Serializable trial changes a surviving assignment expiry while permissions remain valid, verifies POST_REVOKE_GRANTS_CHANGED, restores it and verifies no drift. Existing before/after handler authorization checks still pass. Finally rollback and empty users/branches/requests assertions pass. See checkpoint160-sql.txt.
- git diff --check: PASS; Git reports LF-to-CRLF normalization notice for the existing SQL test file.

No shared/production database changes, schema changes, generated entities, frontend changes or endpoint changes. No new HTTPS/browser revoke/deactivation evidence; no concurrency race claim from this single-transaction test. UC006/010 remain PARTIAL. All responsibility/writer/session/race release fuses remain intact.

Changed source/test files: RevokeRoleGrantImpact.cs, RevokeRoleResponsibilities.cs, InternalRequestRoleSqlTests.cs, RevokeGrantRereadTests.cs. Documentation: this receipt, SQL output, UC matrix.

Next highest-impact tasks: complete mapped-manager/designated-responsibility evaluation; verify remaining writer coordination and final rereads; exercise actual revoke/deactivation session, audit, rollback and race acceptance before enabling release. Sprint remains active.