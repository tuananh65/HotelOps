> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Blocking work and remaining limits

## Endpoint blocker

The user explicitly requires backend-verified recent reauthentication, bound to the same actor and current session (maximum five minutes), and instructs: if trusted reauthentication is unsupported, report BLOCKED and do not skip/fake it.

No such reusable primitive exists in the inspected backend. Existing login and transaction-local credential proof are for new session creation; SQL session timestamps/activity do not prove reauthentication of the current session. Therefore POST /api/users/{userId}/reset-credential is NOT implemented or enabled. No DI/controller bypass is provided.

## Required next work

Implement and approve the trusted reauthentication flow and proof lifecycle before endpoint integration: credential verification/lockout, actor/session binding, backend time/freshness, replay/consumption/reuse policy, expiry/revocation/credential-change handling, CSRF and secret-safe errors/logging. Do not infer these from client flags or create a fake proof.

Then integrate scope/actor/target checks and reauthentication with conditional transaction execution, employee identity confirmation and an approved procedure-code mapping. The existing helpers have not proven combined transaction-time authorization. Provide strict HTTP input/visibility/status/ETag/handoff behavior and full HTTP negative tests before opening the route.

The bounded method code and server receipt timestamp added to conditional-reset audit are metadata from a trusted caller; they do not technically prove employee identity or administrator reauthentication. Do not expose arbitrary free-form client evidence to this primitive.

## Other limits

- Missing If-Match 428, stale HTTP 412, no-store handoff and unauthorized target non-disclosure remain endpoint tests NOT RUN, rather than incorrectly marked PASS by primitive tests.
- Actual lost HTTP delivery and provider commit-acknowledgement failure remain NOT VERIFIED. Deterministic post-SQL-commit fault passes. No plaintext replay/idempotency cache or automatic recovery rotation exists.
- Reauthentication workflow implementation was not added to shared session/cookie/schema infrastructure. It requires its own concrete reviewed design and verification; this task has not silently widened scope.
- SQL rowversion changes from other authentication/profile writers can make a previously loaded token stale; clients must reload through an authorized workflow.
- Current scope tests use real memberships with grant-path doubles; no official RBAC grant/catalog changes. In-flight concurrent authorization revocation is not globally serialized.
- Existing NU1900 metadata warning affects legacy verification builds; new reset harness and solution builds pass without warnings.
- Production SQL least privilege, TLS/DataProtection and APM/body logging remain outside certification. Managed secret strings are not guaranteed zeroized.
- Existing low-level unconditional AdminReset wrapper remains for backward compatibility. It has no HTTP authorization/reauthentication and must not be used to bypass the new endpoint gates.

Final state: PARTIAL independent implementation and verification; HTTP endpoint BLOCKED by missing reauthentication primitive. No commit/push/merge.
