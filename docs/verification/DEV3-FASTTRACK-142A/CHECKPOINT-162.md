# FASTTRACK-142A checkpoint162 - physical mapping lock acceptance

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=55cb6936755079598988b5f1484829164f8e3cfa
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

Added RevokeMappingLockSqlTests to verify the real RevokeRoleResponsibilities mapping read under ReadCommitted, matching the candidate revoke writer. Earlier checkpoint161 used a single Serializable transaction and did not establish this concurrency property.

The new exact-disposable test commits a synthetic fixture before reading, so locks from uncommitted fixture insertion cannot masquerade as reader locks. The reader uses no test-added user lock or mapping lock: it invokes the actual responsibility reader. An independent SQL connection attempts (1) revocation of an existing mapping and (2) insertion into an absent mapping range. Both return SQL1222 while the reader transaction is retained. After reader rollback, the same command on the same competing transaction succeeds with one affected row. The competing mutation is then rolled back and the persisted original mapping state is checked.

Direct evidence:
- dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors, including final test-source revision.
- dotnet test HotelOps.slnx --no-restore exit0; explicit regression project executed separately.
- Regression 829 PASS / 15 runtime-dependent SKIP / 0 FAIL. The new test deliberately skips without explicit disposable opt-in; the final compile-only focused run likewise skipped it.
- Final opted-in SQL execution: 1 PASS, containing both existing-key and absent-range lock cases; checkpoint162-sql.txt. No test result is inferred from the skip.
- Exact server/database/GUID/run and empty users/branches are verified before fixture writes; installed mapping capability required. Cleanup in finally deletes only recorded synthetic IDs in FK order within a transaction. Empty users/branches/requests verified after cleanup. No catalog changes, schema changes, shared/production writes or disabled constraints.

Scope: physical mapping stability only. Not a proof of the whole revoke writer, request-routing locks, endpoint/session/audit behavior, all responsibility coverage or global lock ordering. Production source and release fuses unchanged. No new COMPLETE_VERIFIED UC; UC006/010 remain PARTIAL.

Changed files: tests/HotelOps.Dev1.RegressionTests/RevokeMappingLockSqlTests.cs; this receipt; checkpoint162-sql.txt; UC_STATUS_MATRIX.md. Next work: remaining designated responsibilities; current-request and assignment writer coordination; full candidate revoke/deactivation session, audit, rollback and HTTPS acceptance after prerequisites. Sprint remains active.