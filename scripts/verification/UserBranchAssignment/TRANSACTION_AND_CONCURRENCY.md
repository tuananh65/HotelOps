# Transaction and concurrency boundary

The service requires a clean SQL Server DbContext and owns its transaction. It uses the established Credential Reset authority-lock helper without modifying that shared implementation.

Lock sequence:

1. Actor and target Users in ascending ID order with UPDLOCK/HOLDLOCK.
2. Existing helper: actor UserRoles, referenced Roles/RolePermissions, USER_MANAGE permission, actor/target membership rows and absent ranges, relevant Branches.
3. Target UserRoles including absent key ranges, their Roles and RolePermissions; destination Branch.
4. Session validation and full destination/target authorization; target state, token and safety predicates.

Retained SQL locks conflict with ordinary UPDATE/INSERT/DELETE writers, rather than relying on an unenforced new DEV2 protocol. Actual SQL DMV observations verify `LCK_M_*` waits and blocking session IDs for both Add-first and writer-first schedules. This can create contention or deadlocks under broader workloads; failures roll back and must be retried with newly read state under existing conventions.

A conditional User update using the supplied rowversion advances the aggregate token without changing profile/status. The new membership and narrow CREATE audit then persist in the same transaction. Audit includes server actor, target, destination, old/new membership values and server time. Final session/authorization checks precede commit; only committed results are returned. Any failure before commit rolls back token, membership and audit together. The unique `(user_id,branch_id)` index covers revoked rows too; the service does not regrant them.

SQL verification includes concurrent Add with the same token; actor assignment/role/grant/membership/session revocation; target role insert/revoke; writer-first role insertion/source revocation; source-authority expiry during the User update; audit failure, before-commit failure and after-commit failure. A postcommit retry with the previous token is stale. Real network response loss is not verified.

The locks serialize competing role writers through this transaction. A role assignment made after Add commits is a separate operation, whose own authorization and OPERATIONS_MANAGER invariants belong to that writer/UC-010. These tests do not approve unrestricted direct SQL grant writers or claim a permanent global invariant against future out-of-contract mutations.
