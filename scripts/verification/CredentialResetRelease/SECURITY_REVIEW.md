# Security review and Task 34 fix

## Reauthentication

PASS by source review and actual SQL/HTTPS tests: current password uses existing Identity V3 verifier and five-attempt lockout. SQL session validated before/after hashing; actor/session derived from protected server identity. No new login session, JWT, bearer proof, client timestamp or proof boolean. CSRF, HTTPS and no-store enforced. Failed password does not revoke/replace the active SQL session.

Proof key is actor ID + exact SQL session ID + existing hashed session credential; value is copied User.RowVersion and backend verification/expiry times. Cache is private synchronized process memory, capped at10,000 entries, with expired-entry cleanup and fail-closed capacity eviction. No plaintext/password hash in proof storage. Touch cannot refresh proof. Invalid/revoked/expired/logout/inactive/restricted/changed-credential states deny through real SQL revalidation and version binding. Restart or other instance has no usable proof.

A reusable five-minute window is implemented, not one-time semantics. Role/profile/login counter changes can invalidate authority/proof conservatively. Production clock/time synchronization remains a trusted backend prerequisite.

## Reset contract

PASS: USER_MANAGE/unbound ALL or valid BRANCH with effective actor/target membership in the same active branch; no OWN/self/branchbound ALL/role-name bypass or grant fabrication. Target ACTIVE/nondeleted. Body is exactly identityVerificationConfirmed:true. Audit records the backend fixed procedure code, server receipt time, ADMIN_ATTESTATION and SUCCEEDED; it does not claim backend document verification. Missing If-Match428, stale412, no plaintext replay.

Existing update/revoke/audit core is reused in one ReadCommitted transaction. Rowversion changes via credential UPDATE. Profiles/status/branch/department/roles are untouched by reset. Only confirmed commit permits plaintext response. Errors/rollback never hand off credential.

## Finding F1 - authorization revoke could overtake final check (fixed)

Before Task 34, user/session locks protected credential/session state, but READ COMMITTED grant/membership queries released read locks. A writer could revoke authority after the final boolean check and before commit. This was a commit blocker.

Minimal fix in UserCredentialResetAccess.LockAuthorityAsync, invoked from the internal reset gate:
- HOLDLOCK actor UserRole rows/ranges, all referenced Role rows and RolePermission rows/ranges, USER_MANAGE Permission row/range.
- HOLDLOCK actor/target membership rows/ranges and relevant Branch rows.
- Keep existing ordered actor/target UPDLOCK/HOLDLOCK and actual session validation locks.
- Evaluate proof/authority under these locks and recheck immediately before commit, including time-based eligibility.

No schema, catalog, shared evaluator or writer changes. Standard SQL UPDATE/DELETE/INSERT locks conflict with retained read/key-range locks. A reset that obtains the locks first commits before the revoker; a revoked state cannot authorize the next request. Role/grant/membership/session/credential revokers were each observed waiting on the reset SPID with actual LCK_M_% waits at the commit boundary, then subsequent reset denied. Assignment revocation is also covered.

These locks can reduce throughput and introduce ordinary SQL deadlock-victim failures under unrelated writer lock orders; such failures abort/rollback, never grant. No claim of cancelling a reset that serialized before revocation. No NOLOCK/snapshot or privileged constraint-disabling writer protocol is introduced/supported.

## Secrets and compatibility

Audits/logs/artifacts contain no plaintext credentials, password hash values, raw session secrets or proof material. Actual Information logs are checked in memory. CurrentPassword and temporary handoff remain excluded from body telemetry. Managed strings are not guaranteed zeroized.

Legacy ApplyAsync, initial participant, hashing, cookie/session middleware and schema conventions retain compatibility. Existing low-level unconditional APIs are not wired directly to HTTP. No DEV2 or RBAC catalog source changes. No unaddressed security blocker remains for the approved scoped contract after passing final checks.
