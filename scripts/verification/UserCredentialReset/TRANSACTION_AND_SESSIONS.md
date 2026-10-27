> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Conditional credential reset transaction

## Independent primitive

IConditionalCredentialResetService is a low-level transaction API implemented by TemporaryCredentialService; no endpoint/DI exposure is added. Caller is responsible for server actor resolution, approved scope, trusted reauthentication and actual employee identity confirmation. The scope helper alone is not sufficient authorization to invoke a production reset.

ResetAsync requires a prepared server-generated credential, positive actor ID, eight-byte expected rowversion and a bounded non-sensitive procedure code. It rejects caller/ambient transactions and tracked entries. It snapshots mutable expected-version input before awaiting.

1. Reject self-reset. Begin ReadCommitted SQL transaction.
2. Read the target with UPDLOCK,HOLDLOCK. Reject missing/soft-deleted, non-ACTIVE or stale target before consuming preparation.
3. Reuse ApplyCoreAsync for credential hash/expiry/counter/lock update, existing SessionRevocationService and existing authentication UPDATE audit.
4. Read the actual new SQL-generated rowversion under the retained user lock.
5. Await successful commit before constructing the one-time IssuedTemporaryCredential result.

SQL rowversion changes automatically on credential UPDATE; no token column, migration, generated entity, profile/status/role/branch change or manually invented concurrency value is introduced. Holding the update lock through validation and commit prevents both same-token resets succeeding. Prepared secrets remain disposable in-memory material.

## Audit

Reuses AuthenticationAudit.ForUser and CREDENTIAL_RESET metadata. No generic audit subsystem. Legacy ApplyAsync/initial participant keep the previous metadata shape. Conditional reset adds identity_verification_method and identity_verified_at_utc to the same audit, with actual actor, server time and revoked-session count. The timestamp records server receipt of trusted identity confirmation at reset execution, not independently verified external evidence. The procedure code must come from an approved trusted orchestrator; syntax validation is not proof of identity verification and does not itself sanitize maliciously encoded personal information.

No password/hash values are placed in audit JSON or verification receipts. Existing changed-fields names may include password_hash; values never do. Output is one-time consumable and redacted through the existing credential object.

## Sessions and faults

The real existing revoker locks User before session UPDATE and participates in the same transaction. It revokes every unrevoked target session, including still-live ones. SQL validation of a previously valid protected reference denies after reset. Login/session creation also follows user-first locking and rechecks the stored hash/expiry, so old proofs cannot create valid sessions across a rotation.

Tested rollback after credential SQL, after actual session revocation, before/after audit SQL, before commit and cancellation preserves hash, expiry, original rowversion, session revoke state and audit count. Existing profile/assignments/status remain unchanged.

ApplyAsync retains its old signature, transaction ownership, guards, crypto policy and legacy reset semantics. It does not gain HTTP authorization and is not a substitute for conditional endpoint reset.

## Retry and delivery

Same-token concurrent resets produce one success and one Stale. Repeating the committed request token returns Stale with no credential, audit or rotation. A deliberate new reset with the new token leaves only its latest password valid.

Exception injection from EF TransactionCommittedAsync proves actual SQL commit followed by no credential result; retry with old token stays stale. This is NOT an actual HTTP network loss test. No plaintext replay/recovery store or automatic retry is added.

Future orchestration still needs reauthentication and scope rechecks coordinated with transaction execution; current helpers are not a combined production workflow. Cross-module authorization revocation cannot be claimed globally serialized.
