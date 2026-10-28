# Release summary - Tasks 41-43

DEV1 / FE-01, 2026-10-28 Asia/Saigon.
Intended controlled commit: feat(users): protect global admin eligibility across account mutations
Parent: 6599692b578410ef1ea8642a7d1bf0827f32c195 on feature/BE1-database-integration.
Commit identity is reported by git log/final handoff; it is not embedded in its own source tree.

Global-admin eligibility now remains nonzero across participating HTTP reset, raw reset,
legacy Apply AdminReset, Create identifier insertion and Profile email changes. The guard
uses caller-owned transactions and global-first locking, snapshots effective ALL grants and
regular/unambiguous login eligibility, rejects loss of the final durable admin, and rolls back
credential/session/audit changes together. Existing hash/temporary-credential/session policies,
permission codes and scope decisions are unchanged. No Deactivate or Role Revoke endpoints.

Task attribution:

- Pre-Task43: four dirty tracked files (AdminCredentialResetController,
  IConditionalCredentialResetService, PasswordCredentialService, TemporaryCredentialService),
  untracked GlobalAdminSerialization and the existing LastAdminProtection source/docs/harness.
  These are Task41/42 work explicitly included in the user-authorized Task41-43 release.
- Task43: raw/legacy guard, Create/Profile integration/status mapping/authority locks, Users X
  lock acquisition, new writer/SQL races, current coverage documentation and this release manifest.
  Preflight SHA256 snapshot is retained locally, excluded from commit.

Exact allowlist: FILE_MANIFEST.txt, 25 files (11 production source, 3 harness/project, 11 docs
including the manifest). No directory-wide staging. Existing tracked dependencies (DbContext,
entities, DI, authorization evaluator, reset workflow/access, session implementation) are already
in the parent and are not restaged. No DEV2, RBAC Catalog, schema/migration or shared configuration.

Excluded: every local JSON receipt/hash snapshot, WAV, bin/obj, temporary SQL database resource,
certificate, secret, unrelated verification/deactivation/release document, other worktree or
untracked artifact. All remain local and are preserved. No git add . / git add -A, push or merge.

Validation gates and failed-attempt explanations: INTEGRATION_VERIFICATION.md. SQL foundation
is separate from actual HTTPS/SQL integration. dotnet test solution has no discovered framework
tests; executable harnesses are run explicitly. NU1900 vulnerability-metadata warnings on some
harness builds are reported, not suppressed; solution build has no warnings/errors.

Release limits: raw/legacy primitives still require trusted caller authorization. External
SQL/catalog/job writers and time-driven grant expiry remain outside the contract. Conservative
User table X locking increases blocking; load and universal deadlock freedom are unverified.
No system-wide guarantee, production deployment, HotelOpsDB access or deactivation approval.
See REMAINING_UNPROTECTED_PATHS.md. The user authorizes one commit only after scoped gates pass;
staged paths, secret/whitespace scan and absence of DEV2 source must be checked before commit.
