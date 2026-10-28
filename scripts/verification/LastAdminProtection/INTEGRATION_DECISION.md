# Task 42 — integration decision

Date: 2026-10-28, Asia/Saigon. DEV1 / FE-01 / UC-005.

## Decision and authority

Choose existing `POST /api/users/{userId}/reset-credential`, implemented by
`AdminCredentialResetWorkflow.ResetAsync` -> `TemporaryCredentialService.ResetWithGateAsync`.
Regular -> temporary credentials remove durable full-session eligibility; a restricted
password-change session cannot administer users/roles. This is a meaningful reducing mutation.
Task 42 explicitly authorizes this integration; Task 41's definition supplies durable policy.
SRS UC-005, GBR-007/008/023 and User Detail describe atomic reset/audit, 24-hour credentials
and session revocation. GBR-026 is background; Task 41/42 supply the capability-based guard.
`SRS_CHANGE_NOTES.md` leaves role permissions pending: no pending role rules are implemented.

Activate and Add Branch do not reduce the chosen invariant. Deactivate, role revoke and
catalog-management mutation APIs are absent and remain unopened. Employment status is not
an authentication exclusion in the inspected login/permission code; none is invented.
Create/Profile can affect identifier ambiguity but need a separate contract/order review.

## Implemented boundary

The scoped reset service owns its local ReadCommitted transaction. It acquires the global
transaction applock before any feature locks, reads the baseline, locks actor/target and
rechecks existing authority/proof and If-Match. Existing ApplyCore rotates hash, sets the
24-hour marker, clears finite lockout, revokes sessions and writes the existing audit.
After the final proof/authority check, a target present in the baseline durable set requires
`EnsurePreservedAsync`; then proof/authority are checked again immediately before commit.

A target outside that set cannot lose durable eligibility from this credential-only write.
Its reset remains allowed even when the baseline count is already zero. All gated resets
still acquire serialization/snapshot locks. Last-admin denial returns 409 LAST_DURABLE_ADMIN;
negative applock result returns 503 GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE. Early returns and
exceptions dispose the uncommitted transaction, rolling back hash, marker, sessions and audit.
No credential is handed off until commit; failures expose no plaintext. Raw public ResetAsync,
legacy ApplyAsync and initial provisioning retain their existing semantics and are explicitly
unprotected. The gate is internal and its only production caller is the existing workflow.

## Lock and transaction review

- Existing order: transaction -> actor/target Users ascending -> UserRoles -> Roles ->
  RolePermissions -> Permissions -> UserBranchAccess ascending -> Branches -> session/proof
  checks -> target credential/session changes -> AuditLogs -> final checks -> commit.
- Integrated order: transaction -> global applock -> snapshot Users TABLOCK/HOLDLOCK ->
  UserRoles, Roles, RolePermissions, Permissions ROWLOCK/HOLDLOCK -> existing order above.
  Snapshot must precede narrow User update locks to avoid a later broad-lock upgrade after
  unrelated resource acquisition. No applock is inserted between existing resource locks.
- Every guard command uses the same context connection and original EF DbTransaction.
  AppLock owner is Transaction; all retained locks end on commit/rollback, with no early release.
  Snapshot is no-tracking; the audit is detached in ApplyCore's finally before poststate read.
- Middleware SQL session touch finishes before this transaction. Hash preparation is before
  transaction acquisition. Authority/proof reads inside the workflow reuse its scoped context.
- Legacy Create holds Branch/Department/Role UPDLOCK before User insert. Shared catalog row/range
  locks remain compatible with update locks in the observed probe; a catalog table S lock would
  conflict with intent locks. Real probe holds those Create-style locks, pauses HTTP at audit,
  observes User INSERT blocking, releases reset, then finishes INSERT and rolls it back.
  This is a controlled SQL lock-order probe, not full UC-004/UC-005 concurrent HTTP certification.
- Other legacy/external writers may still deadlock with broad snapshot locks. The deliberate
  inverted role writer produces an actual SQL deadlock and sanitized HTTP 500 with rollback;
  fresh caller retry succeeds. There is no automatic credential replay/retry or deadlock-free claim.
- Cancellation rolls back through async disposal; no canceled token is used for an explicit
  rollback call. Timeout/deadlock scopes are never reused. Finite lockout semantics are unchanged.

## Database/source preflight

Inspected `HotelOpsDbContext` User, UserRole, RolePermission, UserSession and AuditLog mappings,
generated User/UserRole/RolePermission/UserSession entities, existing hash validator, login
identifier resolver, reset controller/workflow/access, creation/profile services and Task 41's
26-case foundation source. No entity, mapping, production schema, catalog or DI changes.

Preflight HEAD: `6599692b578410ef1ea8642a7d1bf0827f32c195`;
branch `feature/BE1-database-integration`; index empty. Four tracked files were already dirty:
AdminCredentialResetController, IConditionalCredentialResetService, PasswordCredentialService,
TemporaryCredentialService. GlobalAdminSerialization and the LastAdminProtection folder,
including an unfinished Task 42 HTTP harness, were already untracked. They were reviewed as
existing work, not attributed as newly authored in this turn. Existing artifacts preserved.
Worktrees recorded at root, obj/task11-dev2-rehearsal, obj/task12-pr7-resolution and
HotelOps-AI-Loop; no worktree/branch/commit/push/merge operations performed.

See INTEGRATION_VERIFICATION.md for executed evidence and REMAINING_UNPROTECTED_PATHS.md
for the system-wide boundary. No DEV2 source modification or dependency is required for reset;
DEV2 deactivation/open-work dependencies remain outside this decision.
