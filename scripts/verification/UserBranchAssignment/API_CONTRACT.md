# Add membership API

`POST /api/users/{userId}/branches` over HTTPS, authenticated with the existing SQL-backed cookie session and CSRF enforcement. No client actor ID or credentials are accepted.

```json
{ "branchId": 123 }
```

Exactly one `branchId` property is accepted: a positive Int64 JSON number. Unknown/duplicate fields, strings, fractions, invalid JSON and nonobject bodies are rejected. The request size limit is 16 KiB.

`If-Match` is required: one strong quoted uppercase 16-hex User rowversion token, matching existing mutation conventions. A successful `201` returns `{accessId,userId,branchId,accessLevel,isPrimary}` and the updated aggregate User `ETag`, with no-store. No membership GET route or Location URL is invented.

| HTTP | Meaning |
|---|---|
| 400 | Invalid body/token, unavailable destination Branch, invalid CSRF or HTTPS requirement |
| 401 | Missing, invalid or revoked session |
| 403 | Restricted/self/OWN/insufficient destination or target authority |
| 404 | Missing/soft-deleted target, after applicable visibility checks |
| 409 | Inactive target, existing membership including revoked, existing destination role path, or unsafe OPERATIONS_MANAGER scope |
| 412 | Stale User rowversion |
| 428 | Missing If-Match |
| 500/503 | Existing sanitized error conventions; no successful partial mutation |

Scope denial precedes target-state disclosure. An out-of-scope caller is not given target existence/status details. Stale token is checked before duplicate/conflict handling for an otherwise eligible target; retry after a committed success with the old token returns 412. A fresh-token duplicate returns 409. There is no idempotent replay response or credential delivery in this API.

Only new `STANDARD`, `IsPrimary=false` rows are inserted. Neither revoked regrant nor existing assignment edits are supported. The User rowversion changes; primary Branch, Department, roles, credentials, lockout and account status remain unchanged. Normal sessions remain valid; authorization reads current SQL data on later requests.
