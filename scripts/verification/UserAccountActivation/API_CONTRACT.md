# Activate HTTP contract

`PATCH /api/users/{userId}/activate` over HTTPS, existing HotelOps cookie authentication and CSRF header/cookie required. UC-006 is route-agnostic; the user explicitly preferred this activation-only route. It follows existing protected user mutation/ETag/problem conventions and leaves the broader Task35 `/status` proposal unimplemented.

```json
{ "reason": "Employee returns to work" }
```

Exactly one, case-sensitive reason string; trim before validation/audit; at least10 characters after trim. Reject duplicate/unknown properties, null/non-string/short/whitespace reason, malformed JSON and client-supplied status/actor/credential fields. `userId` must be a positive Int64. Transport body cap16KiB bounds parsing/audit resources without inventing an additional SRS business maximum. Oversized body returns413 REQUEST_TOO_LARGE.

If-Match is mandatory: one strong quoted16-uppercase-hex rowversion, matching UserProfile/Reset conventions. Wildcard, weak, combined/multiple or malformed values are rejected. Token freshness and target state are evaluated only after actual SQL session and scope validation.

| Outcome | HTTP / code |
|---|---|
| Success, committed INACTIVE -> ACTIVE | 200, new ETag, Cache-Control:no-store |
| Missing If-Match | 428 IF_MATCH_REQUIRED |
| Malformed header/input | 400 INVALID_IF_MATCH / VALIDATION_FAILED; model binding uses existing400 convention |
| Invalid/revoked/expired/inactive actor session | 401 SESSION_INVALID or existing cookie middleware code |
| Missing scope, OWN, self, restricted actor | 403 ACTIVATION_NOT_ALLOWED or existing restricted-session code |
| Missing/soft-deleted visible target | 404 USER_NOT_FOUND |
| Stale token | 412 STALE_USER_VERSION |
| ACTIVE no-op or unknown source status | 409 INVALID_STATUS_TRANSITION |
| Unhandled activation/audit/SQL failure | Existing500 INTERNAL_SERVER_ERROR, sanitized ProblemDetails/no-store |
| Session store unavailable | Existing503 AUTH_SERVICE_UNAVAILABLE; local body-limit filter does not intercept it |

Success body is exactly `{ "userId": 123, "accountStatus": "ACTIVE" }`. It contains no EF entity, credentials, hash, role/grant graph, session or token secret. ETag is concurrency metadata, not an authentication proof. No identity checkbox or reauthentication approval from UC005 is silently imposed on UC006; the approved UC006 session/permission gates apply.

Only AccountStatus, UpdatedAt and SQL rowversion change, plus the narrow transactional audit. Roles/grants, branch/department, employment/profile, password/hash/temporary expiry, failed-login count, LockedUntil, SQL sessions and operational assignments remain unchanged. Inactive locked/expired-credential targets may be marked ACTIVE, but existing login lockout/temporary-expiry rules still reject login; activation does not reset them or create a session.

Old-token retry after commit returns412; new-token ACTIVE request returns409. No idempotency key, replay, password reset or recovery contract is introduced. Actual network response loss is not verified. Deactivate/status routes remain absent (verified404); posting status INACTIVE to this route is rejected400 before mutation, and the service itself still refuses deactivation.
