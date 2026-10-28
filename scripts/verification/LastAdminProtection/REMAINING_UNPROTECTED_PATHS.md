# Task 43 - remaining unprotected paths

Existing decreasing application writer entries now participating in GlobalAdminSerialization:
gated HTTP admin reset, raw conditional reset, legacy Apply AdminReset, Create and Profile.
See MUTATION_PATH_MATRIX.md for reachability, classification and exact methods.

Remaining boundaries:

- Raw/legacy primitives protect the invariant but do not authorize a supplied actor ID or
  establish identity confirmation, session validity or reauthentication. Their existing trusted
  caller contract remains mandatory. No production caller bypassing the HTTP workflow was found.
  Future internal orchestration without commit-boundary authorization is BLOCKED; the raw API
  must not be presented as a fully authorized administrative endpoint.
- External SQL, release/catalog scripts, imports and jobs can still change Users, UserRoles,
  Roles, RolePermissions or Permissions after a guarded commit. No writer protocol for those
  actors is established. Their last-admin safety remains BLOCKED / NOT VERIFIED.
- Grant expiry is time-driven. FUTURE EXPIRY NOT GUARANTEED; no scheduler/renewal/recovery policy.
- Deactivate/soft delete, Role Revoke/rebind, catalog-management APIs, remove/transfer membership
  are not opened. Future implementation needs its own approved business/authorization/open-work
  contracts and end-to-end proof. Absence today is not certification of a future writer.
- No permanent lockout representation is implemented; finite LockedUntil remains durable under
  Task 41 policy. Hash format support does not prove password knowledge or service availability.
- Conservative Users TABLOCKX serializes identifier writes and avoids User shared-to-update
  inversion with authentication-style U locks. It increases blocking for login/read workloads.
  Load, least-privilege deployment and every mixed external/catalog lock schedule are NOT VERIFIED.
  Actual timeout/deadlock failure rolls back; caller retries use fresh transactions/preparation.
- Existing process-local reauthentication/multi-instance and transport-loss limits are unchanged.

DEV2 source and RBAC Catalog are unchanged. Future deactivation still needs cross-module
open-work evidence from DEV1/DEV2; this task neither certifies missing handoffs nor changes
those workflows. HotelOpsDB is never used for verification. No universal system-wide guarantee.
