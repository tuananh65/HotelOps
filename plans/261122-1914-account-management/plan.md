# Account management and email password reset

User asks to expand Manage account and send temporary reset passwords by email. Single agent; preserve 40-table schema and existing authorization.

- [x] Read UC-005, compact auth, account service, mappings, sessions and mail capability. User authorized checking existing local SMTP settings; never print credentials.
- [x] Implement detail/profile update and current grants in the management dialog, including existing revoke operations.
- [x] Implement USER_MANAGE-protected reset with administrator reauthentication, row version, random 24-hour temporary password, session revocation and audit without password values. SMTP failures roll back the database change.
- [x] Inspect actual SMTP settings; none exist. Supply configuration instructions and test with an injected mail transport on an isolated restored database. Never pretend local capture is external delivery.
- [x] Backup, build FE/BE, test success/error/security/concurrency cases, validate UI on desktop/mobile, review and report.
- [x] User chose Gmail and requested a simple shared send form, without requiring test execution. Add per-request Gmail sender/app password, recipient from account, retain admin reauthentication. Do not persist credentials.
- [x] Build updated FE/BE and restart owned API; review changes. Both builds passed with no warnings/errors. API now PID 43800, port 5768; FE remains 5186. Tests are not rerun for this adjustment at user's request. Real Gmail delivery remains unverified until valid sender credentials are entered. Report: `plans/reports/test-261122-1931-gmail-reset-form.md`.

External SMTP and SQL cannot commit atomically: send within the database transaction after validating/persisting changes; commit only once SMTP accepts. A rare commit failure after acceptance can produce an unusable message; API reports failure, operator retries. Do not persist plaintext passwords in SQL/logs/API or create an outbox table. Tests do not reset real staff passwords or mail their addresses.

Shared impact: register the new account/mail services in CompactHost; existing policies and schema remain unchanged. Explicit user request authorizes the feature and its necessary service registration. Retain FE 5186 and API 5768; no Git publishing.

Previous verification, before the Gmail form adjustment: BE/FE builds pass, zero BE warnings/errors; .NET 29 pass/1 unrelated worker skip on isolated copy. Browser/API management 8/8, filters 8/8, UI regression 12/12. Primary remained 40 tables/56 users/56 current role assignments/zero reset-email audits. API PID 33052 before restart. See `plans/reports/test-261122-1914-account-management.md`. No credentials printed or fake inbox-delivery claim.
