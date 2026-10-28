# Security design

## Password reauthentication

The new route requires the existing Cookie scheme, validated SQL session, CSRF and unrestricted/non-temporary session. Actor/session identifiers come only from the protected cookie and SQL validation, not JSON fields. Any normal valid session can verify its own credential; proof grants no USER_MANAGE or other authority. Reset still evaluates authority independently.

Request is exactly { "currentPassword": "..." }. Reject unknown/duplicate keys, wrong root/type, blank or over-256 input. Password is not trimmed. HTTPS is required; success response contains only expiresAtUtc and no-store.

AdminReauthenticationService begins a clean ReadCommitted SQL transaction, validates the session, verifies the password with existing CredentialLockoutService/Identity V3, then validates session again after hashing. Existing user-first SQL locks prevent credential rotation/revocation overtaking the check. It neither creates a second session nor signs in a new cookie.

Wrong-password attempts persist the existing failed-login counter/lockout policy (five attempts, existing lock duration); no proof is issued, no session row is replaced/revoked. Normal HTTP session-touch middleware retains its existing activity behavior. Restricted temporary sessions cannot reauthenticate. Correct-password reauth while locked returns 423.

Success audit uses the existing authentication UPDATE convention with REAUTHENTICATED event, session ID, server time and expiry; no credential values. The audit commits before proof publication. Transaction failure grants no new proof. A new attempt forgets that session's prior cached proof; failed attempts do not extend its privileged window.

## Proof and reset gates

Proof is server memory only, never accepted from an HTTP flag, timestamp, checkbox or token. It is bound to user ID + SQL session ID + hashed session credential and the user's SQL rowversion. Validation always rechecks the actual SQL session under the sensitive transaction. Expiry/revocation/logout/inactive/credential changes fail closed.

The approved offline identity checkbox is independent of reauthentication. Reset body is exactly identityVerificationConfirmed:true; backend fixes the procedure code and timestamp. Audit explicitly records ADMIN_ATTESTATION and SUCCEEDED, not a claim that backend directly verified employee documents.

Reset authority uses the Task 32 helper: unbound USER_MANAGE/ALL or exact BRANCH grant with effective actor/target membership in the same active branch. OWN, self-reset, branchbound ALL and role-name bypass are denied. ROLE_MANAGE is not substituted. Target must be ACTIVE and nondeleted, with current rowversion. Scope/proof checks run before exposing target state.

The credential service locks actor and target one at a time in ascending user-ID order, calls the gate inside its transaction, applies existing rotation/revoke/audit, then rechecks the gate immediately before commit. Expiry during SQL work causes rollback with no handoff. No network delivery occurs while the transaction is open.

Task 34 additionally retains relevant assignment/role/grant/permission/membership/branch rows and key ranges with HOLDLOCK through commit. Ordinary authorization writers must serialize; six real SQL blocking races cover revocation at the final commit boundary. This closes the prior READ COMMITTED authorization-read gap without modifying the shared evaluator or catalog.

## Secret handling and compatible changes

Plaintext is only currentPassword in request memory or the success-only temporary credential response. Existing secret containers remain single-consumption/redacted. No password/hash/proof/session secret is logged, audited or written to verification artifacts. SQL session-key hashes exist only in trusted server state/SQL as before, never response DTOs.

No hashing, cookie scheme, shared authentication middleware, generated entity, schema or catalog changes. Program adds scoped services and one singleton store. Legacy ApplyAsync and initial transaction participant keep their original behavior and audit shape.

Managed strings are not guaranteed zeroized. Current Information-log leak checks cover the actual test host configuration; future request/response-body logging must exclude both credential routes.

The proof is a reusable five-minute session window, not an implicitly approved one-time token. Reset replay is prevented by rowversion, not proof consumption. No idempotency/replay cache or plaintext recovery store exists.
