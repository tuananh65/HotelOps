# Remaining scope and limits

1. Only the UC-141 list slice is implemented. Account detail/effective permissions (UC-005/011), user mutations, password reset and assignment administration remain separate tasks.
2. Scoped-administrator user-directory visibility is not finalized. This endpoint supports the existing global USER_VIEW contract only and denies BRANCH/OWN/branch-bound ALL. No role is given new grants. Branch filter uses the target's primary branch, not every membership.
3. SRS/Permission Matrix Reception/DH/OM revisions remain pending. This implementation neither resolves them nor changes the catalog. Role display is active/time-valid assignment information, not a target-user permission evaluator.
4. Source/EF compatibility and application behavior passed against a new independent combined-schema trial. No claim is made that the main HotelOpsDB has the same schema or was upgraded by this task. No main-database connectivity was required.
5. Tests used the available developer SQL identity on the independent trial. They do not certify deployment under a least-privilege application principal. No existing read-only guard was disabled and no SQL permissions were granted.
6. Directory count/page are separate read-committed queries. Concurrent writes may change totals between them; this task does not introduce snapshot isolation or rowversion/schema changes.
7. The existing session-touch middleware can update activity/expiry on authenticated GETs. Business user/role data is read-only; the endpoint creates no sessions. Authentication writes in verification are limited to owned test fixtures.
8. Console harnesses require explicit execution; `dotnet test HotelOps.slnx` discovers no tests. Reproducibility requires a new isolated database and a new evidence filename each run; retained fixture databases are never reset automatically.
9. Fixture cleanup revokes sessions and stops only the spawned API, but a process crash can interrupt cleanup. Retain the trial name and inspect only that database; never reset HotelOpsDB to recover a test run.
