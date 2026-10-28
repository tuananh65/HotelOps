# Task44 - remaining boundaries after technical approval

The developer explicitly approved P1-P12 for the documented narrow Assign Role v1. The former
missing-delegation blocker is resolved for this technical scope only. See VERIFICATION_RESULTS.md
for implementation gates; formal SRS/PO approval is not claimed or replaced by this task.

Intentional v1 exclusions (not unimplemented approved features):

- Every target with any UserRole history is refused, including inactive/expired/future paths,
  exact duplicate tuples, OM and mixed-role combinations. No existing row is reactivated,
  rebound, rescheduled or revoked. A second role needs a separately approved SoD/combination slice.
- Only the five exact compiled permission/scope envelopes may be delegated in the existing
  primary Branch/Department. Secondary-Branch roles, changed envelopes, arbitrary/custom roles,
  SYS_ADMIN, OWNER, OPERATIONS_MANAGER, DEPT_MANAGER and RECEPTION are refused.
- OM single-Branch vs historical multi-Branch SRS wording remains unresolved for future OM
  assignment; v1 neither assigns OM nor adds a role to an OM target.
- No membership creation/regrant, Department/primary changes, permission grants, credential/status
  changes, session revocation, Role Revoke, Deactivate or bootstrap/recovery administrator path.
- No dates supplied by the client; server AssignedAt UTC, IsActive=true and ExpiresAt=null only.

Operational limits:

- Global-first Users TABLOCKX is conservative and blocks reads/login/writes while held. Controlled
  real SQL races pass; load, least-privilege deployment and universal external-writer deadlock
  freedom remain unverified. Resource provider/deadlock failures use sanitized500; failed global
  acquisition returns503. Retry must read current state and must not replay a committed grant.
- External SQL/catalog/import/job writers can change grants after commit without the approved
  application protocol. Time-driven expiry is not prevented by a transaction lock. Published
  LastAdmin external-writer/future-expiry limitations remain in force.
- Schema checks were against guarded disposable trials and existing EF mappings, not HotelOpsDB.
  Existing production rowversion deployment remains a prerequisite; this task introduces no
  production schema patch, migration or deployment authorization.
- Existing cookie/CSRF/session middleware remains unchanged. The Assign service validates its
  actor session at both gates without touching/revoking sessions. Existing middleware may touch
  the actor session as before. No multi-instance/session-policy redesign is claimed.
- Real network response-loss recovery and infrastructure failover are not certified. Old-token
  retry is stale; no idempotency/regrant feature is added.

No production catalog, DEV2, generated entity, mapping or existing module refactor. Task44 made
no commit; Task45's separately authorized release is recorded in RELEASE_SUMMARY.md. No pushes,
merges or worktree changes. Initial blocked review documents are historical; they no
longer imply missing technical approval for P1-P12, and they do not authorize broader UC-010.
