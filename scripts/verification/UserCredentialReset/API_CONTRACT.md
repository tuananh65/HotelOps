> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Reset HTTP contract - NOT EXPOSED

Proposed route: POST /api/users/{userId}/reset-credential.
No matching controller/action is implemented. The API must not enable this route until every security gate passes. There is no replacement unsecured route or development bypass.

## Confirmed requirements

- Authenticated valid SQL session, CSRF for unsafe requests, unrestricted actor resolved server-side.
- USER_MANAGE permission/scope rules in AUTHORIZATION.md; actor != target; target ACTIVE/nondeleted.
- Recent backend-verified reauthentication bound to the exact actor/current session, maximum five minutes; client timestamps/flags are not proof.
- Explicit administrator confirmation of employee identity verification; method/time audit must contain no identity documents or sensitive evidence.
- Required strong rowversion If-Match; missing 428, invalid format 400, stale 412. Successful rotation changes the existing SQL token.
- No client-selected password/hash, actor, scope, role, profile, expiry or session metadata.
- Success-only credential handoff after confirmed commit with Cache-Control: no-store. Never return the old credential on retry or ordinary reads.

## Future orchestration requirements

Strict body allowlist/duplicate-key checks must be specified for identity confirmation and the trusted procedure-code mapping. Do not treat a confirmation checkbox as reauthentication. The technical primitive's procedure code is a trusted caller argument, not a published client input or proof. The backend records the receipt time of identity confirmation; it cannot certify the time an external procedure actually happened.

Reuse the existing ETag encoding, problem response conventions and middleware. No HTTP statuses for target visibility/inactive/reauthentication denial or success DTO are claimed tested in this task. They must be implemented in the actual endpoint, with scope checks before revealing target state; a low-level NotFound/Inactive result alone does not protect visibility.

A candidate success DTO can contain only temporaryCredential { password, expiresAtUtc }, with the committed token in ETag, but no active API contract is released here. No temporary hash, token, session secret or permission graph may be exposed.

Repeated old If-Match must fail stale without reset/replay. A new token plus a new approved confirmation represents a new reset, not idempotent replay. Actual lost HTTP delivery remains NOT VERIFIED. No idempotency key store or plaintext recovery cache is implemented.
