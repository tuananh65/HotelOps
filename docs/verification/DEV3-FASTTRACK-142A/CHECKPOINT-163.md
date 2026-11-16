# FASTTRACK-142A checkpoint163 - retain FE13 responsibility source locks

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=54c8a0fc125620b2396f4e354e6a3d98566bb99a
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

The candidate UC010 writer runs ReadCommitted. Its responsibility reader previously retained management mapping locks but not the current internal-request assignment range or request row. Those sources could change after the responsibility query. The FE13 query now uses parameterized HOLDLOCK assignment selection and a HOLDLOCK request join. The actual request row supplies current status, branch, receiving department and owner count. Existing account, scope, assignment, actor/grant and mapping checks remain unchanged. No isolation-level change, new permission, generated entity or schema change.

Direct evidence:
- dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors.
- dotnet test HotelOps.slnx --no-restore exit0; separate regression project: 829 PASS / 15 runtime SKIP / 0 FAIL.
- Explicit disposable SQL run: 2 PASS. Existing handler capability test verifies role/membership/mapping/current-department and before/after grant-drift behavior. Expanded two-connection test verifies four cases at ReadCommitted: existing mapping revoke, absent mapping insert, request receiving-department update and active-assignment ending. SQL1222 occurs while the actual reader transaction is retained; the same competing command succeeds after reader rollback. Competing writes rollback and original mapping, department and active assignment state are rechecked. See checkpoint163-sql.txt.
- Both tests guard exact server/database/GUID/run and empty synthetic baseline. The concurrency test commits only its synthetic fixture, deletes only its recorded IDs in finally, and verifies empty users/branches/requests. The capability test rolls back its complete fixture transaction. No shared/production mutation.
- git diff --check PASS with LF/CRLF normalization notice.

No API/browser acceptance or complete global lock-order/deadlock-freedom claim. Retained locks can increase contention; current tests use one synthetic request and do not benchmark broad workloads. UC006/010 remain PARTIAL; all release fuses retained. Remaining work includes full designated responsibility coverage, other writer families and complete session/audit/rollback/HTTPS acceptance. No new COMPLETE_VERIFIED UC.

Changed files: src/HotelOps.Api/Services/Users/RevokeRoleResponsibilities.cs; tests/HotelOps.Dev1.RegressionTests/RevokeMappingLockSqlTests.cs; this receipt; checkpoint163-sql.txt; UC_STATUS_MATRIX.md. Sprint remains active.