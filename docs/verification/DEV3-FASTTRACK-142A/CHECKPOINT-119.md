# FASTTRACK-142A checkpoint119 — SQL responsibility regression

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=dfaa678ef2bac329f3209d4120d13267047799f7
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate; not reverified here)
SHARED_DB_MUTATION=NO

Added InternalRequestRoleSqlTests exercising the real EF responsibility reader against the exact identity-guarded disposable database. Synthetic branch, department, user, request type, request, assignment and temporary permission paths are enclosed in a Serializable transaction with unconditional rollback. Post-rollback assertions verify Users, Branches and InternalRequests are empty.

Direct result: SQL regression 1 PASS, 0 skipped. Covers loss of current handler capability, surviving alternative grants remaining Unknown, expired alternative grant, future assignment inconsistency, closed request uncertainty, historical assignment exclusion, and retained revoke release gates. An earlier fixture attempt failed because the disposable request-type catalog was empty; the fixture now creates its own synthetic request type inside the rollback transaction.

Current UC123 decision matches existing InternalRequestDirection and ReadService: Sent=RequesterId; Received=current unique active handler OR mapped DEPT_MANAGER with current IR_PROCESS; both filter an independently IR_VIEW-authorized query. Current direction/read-access/role-impact regressions: 16 PASS. SQL and browser direction evidence remains checkpoint107 trial3 historical evidence, not a new E2E run in this checkpoint.

Backend build: PASS, 0 warnings, 0 errors. dotnet test HotelOps.slnx exited 0 but does not discover the regression project; the explicit regression commands above provide actual test evidence. Frontend unchanged; frontend build and browser tests not rerun.

No whole UC newly completed. UC006/010 remain PARTIAL: actual mutation/session invalidation, complete responsibility coverage and retained-lock races still require acceptance. UC123 material visibility remains unresolved. No revoke fuse removed, no shared/production schema changed, no API or browser mutation acceptance claimed.

Files: tests/HotelOps.Dev1.RegressionTests/InternalRequestRoleSqlTests.cs and this receipt.
Next highest impact: close FE13 acceptance gaps under approved contracts; retain fail-closed handling for unresolved material visibility and handover read scope.
