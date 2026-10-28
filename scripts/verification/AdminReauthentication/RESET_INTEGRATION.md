# UC-005 reset integration

The Task 32 missing-reauthentication blocker is resolved by the tested server proof and transaction gate. The developer explicitly approved the identity attestation request in this session.

## Requests

POST /api/auth/reauthenticate
- Existing valid normal SQL-backed cookie + CSRF, over HTTPS.
- Exactly { "currentPassword": "current administrator password" }.
- 200 no-store body: { "expiresAtUtc": "UTC timestamp" }. No proof token or new session.
- 400 invalid body/CSRF; 401 invalid credential/session; 403 restricted session; 423 existing account lockout.

POST /api/users/{positive userId}/reset-credential
- Same valid actor/session, CSRF and HTTPS.
- Required single strong If-Match ETag using existing quoted uppercase 16-hex SQL rowversion convention.
- Exactly { "identityVerificationConfirmed": true }.
- No actor, password, timestamp, proof flag, role, scope, hash or verification-method input.

Success: 200 + Cache-Control no-store + new ETag, body containing only temporaryCredential { password, expiresAtUtc }. It is constructed after confirmed transaction commit and consumed once from the existing credential container.

Errors:
- 400: malformed input/token, missing/false identity confirmation or CSRF.
- 401: invalid/expired/revoked session.
- 403: missing/expired/wrong-session proof or unauthorized scope/self/OWN.
- 404: missing/soft-deleted target after authorized visibility/proof checks.
- 409: inactive target within authorized visibility.
- 428: missing If-Match.
- 412: stale token, including exact retry after successful reset.
- Existing sanitized generic 500/503 conventions apply on database/audit failures; never a credential handoff.

Validation ordering may reject malformed input before scope/proof evaluation, without returning target information. Self requests always deny. Scope/proof gates run before state-based target disclosure.

## Atomic workflow

Server resolves the actor from CurrentUserAccessor and protected session reference. Conditional credential service owns the SQL transaction, locks both users in deterministic order, and calls an internal gate. That gate performs real SQL session/proof validation and the approved ALL/BRANCH access helper. After target active/version checks, existing core performs credential rotation, counter/lock reset, session revoke-all and one authentication audit. Gate is re-evaluated before commit.

Audit identifies actor and target, method INTERNAL_PROCEDURE_CONFIRMATION, server receipt timestamp, ADMIN_ATTESTATION source, SUCCEEDED result and revoked-session count. It neither asserts backend document verification nor contains password/hash/session secrets. Failed transaction leaves no success audit.

Roles, branch, department, account status and profile are unchanged. Rowversion changes automatically with the credential UPDATE. No extra schema token, role grant or self-service reset.

## Concurrency and recovery

Same-token concurrent admins yield one 200 and one 412. A stale retry returns no credential and performs no second rotation/audit. Deliberately reloading a token and requesting another reset is a new operation; only the latest password is valid.

Actual network-disconnect/commit-acknowledgement loss is NOT VERIFIED. Existing post-SQL-commit fault injection demonstrates no result and stale retry, not exactly-once network delivery. No plaintext replay endpoint or automatic rotation on transport retry. If delivery is lost, use an explicitly confirmed new authorized reset with a fresh token/proof as required.
