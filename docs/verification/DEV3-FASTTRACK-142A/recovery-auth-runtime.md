# Recovery synthetic HTTPS authentication

2026-11-16. Direct focused HTTPS/SQL results; no full business UC completion claim.

Added RecoveryAuthProbe, isolated from the legacy SprintAuthProbe. It accepts only an output path, pins the recovery server/database/GUID and localhost HTTPS port, uses encrypted integrated SQL and default TLS validation, generates a random synthetic account/password without printing credentials, and creates no roles, grants or branch access. Production authentication/session/CSRF services remain unchanged.

Executed successfully: login without CSRF 400; bootstrap 200; login with CSRF 200; exactly one live session persisted in the verified database; session GET 200; Material Request and replenishment reads without grant 403; logout without CSRF 400 with session still valid; valid logout 204; subsequent session GET 401. SQL confirms revocation and LOGIN audit. Evidence: recovery-auth-20261116.json (12 checks, success=true, cleanup=true).

Cleanup uses an independent bounded Serializable transaction, verifies the exact new user ID/username/email, deletes only this user's sessions and its own users-entity audit, then the synthetic user. No FK/trigger bypass or cleanup of other users. The temporary API listener was stopped after the trial. Workers stayed disabled. No shared/production database mutation.

Backend solution build PASS, zero warnings/errors. ModelProbe build PASS with the three existing EF1002 warnings in SprintCredentialScenario.cs; that scenario was not invoked. Solution test command executed no tests. Runtime retains the three previously recorded RoomTypeBranchV1 mapping warnings. No frontend changes or browser execution this increment.

This proves the recovery authenticated transport and no-grant boundary; it does not prove positive actor/branch permissions, Material Request/replenishment business lifecycle, or browser acceptance. Next: isolated positive grants and synthetic workflow data with the same identity and ownership cleanup constraints. UC099/100/101 remain partial.
