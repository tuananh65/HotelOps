# Task 41 verification results - 2026-10-28 Asia/Saigon

Historical Task 41 report. Task 42 executed current source integration and separate SQL
foundation reruns; see INTEGRATION_VERIFICATION.md. The zero-integrated-writer rows below
are superseded only for the gated administrative HTTP credential reset.

## Independent real SQL guard

**PASS: 26 assertions**, `sql-final.local.json` (local-only). Actual SQL Server contexts/transactions, application locks and DMV blocking-session observations; no mock substitutes for lock/concurrency evidence.

- Caller transaction/locking isolation/clean-context requirements; completed/foreign snapshot refusal.
- Effective capability intersection across independent custom roles, active/time eligibility, no branch-bound ALL widening; regular supported credentials; actual SQL cross-column identifier ambiguity; one remaining unique identifier accepted.
- Finite lockout durable vs immediate classification; temporary-valid and expired restricted credentials excluded; finite-only survivor reports future-expiry risk.
- Two concurrent transactions: second waits on the actual first SPID before counting, then observes the committed reduced count; last-admin role removal and audit are rolled back. Separate last-admin status removal also refuses and rolls back.
- Real audit FK failure rolls back an otherwise permissible fixture change.
- SQL application lock timeout, cancellation/caller rollback, release after commit/rollback and fresh retry.
- Direct grant revoker waits on retained table locks until commit; next snapshot sees its committed change without cache.
- Deliberately inverted fixture creates a real SQL deadlock; victim failure and caller rollback preserve account state. No inverse-order production writer is installed.

Initial suite run **FAILED at cancellation** after preceding cases passed: SqlClient returned a cancellation-related SqlException. Primitive now normalizes that exception only when the supplied cancellation token is requested; the entire suite was rerun and completed successfully. No initial success receipt was written or promoted to PASS. The setup wrapper also initially misread a stale native LASTEXITCODE after a successful PowerShell trial creation; setup resumed on the already-created guarded trial without recreating/overwriting it.

## Required integration distinction

| Scope | Status |
|---|---|
| Reusable SQL serialization/snapshot/preservation guard with caller-owned fixture mutations | PASS, 26 assertions |
| Actual UC-006 Deactivate / UC-010 Revoke end-to-end guard integration | NOT VERIFIED / BLOCKED - no endpoint exists |
| Existing application writers acquiring/final-checking this guard | NOT IMPLEMENTED - zero retrofitted |
| HTTP actor/session/authorization revocation orchestration using this guard | NOT VERIFIED; independent grant-lock evidence is not an HTTP certification |
| Permanent lockout representation | Not present in current schema/Auth; no invented sentinel support |
| Future eligibility across expiry and unprotected writers | FUTURE EXPIRY NOT GUARANTEED; no system-wide invariant claim |
| Load/least-privilege/deadlock-free behavior across all legacy/external writers | NOT VERIFIED; conservative table locking needs integration review |

## Regression evidence

| Suite | Result | Local-only receipt |
|---|---|---|
| UC-004, List/Detail/Profile/PATCH, Authentication and Startup Schema Gate | PASS, 546 | `uc004-profile-startup.local.json` |
| UC-005 Reset/Reauthentication and authority races | PASS, 165 | `uc005-reset-reauth.local.json` |
| UC-006 Activate SQL/race/HTTPS and no Deactivate route | PASS, 169 | `uc006-activation.local.json` |
| UC-007 Add Branch SQL/race/HTTPS | PASS, 183 | `uc007-branch.local.json` |
| CurrentUserAccessor/PermissionService/Authorization/ResourceAuthorization/Hardening | PASS, 14/43/61/49/21 | Executed verification console projects |
| Fresh/upgrade schema | PASS, 26 | `../UserProfileUpdate/schema-d59701b994d94cdfbf0d52c0e49a1e66.local.json` |
| Solution build | PASS, 0 warnings/errors | `dotnet build HotelOps.slnx` |
| Foundation harness build | PASS, 1 NU1900 warning on latest build | Vulnerability metadata inaccessible at api.nuget.org; no compile errors |
| Solution test command | Exit 0, no framework test cases | `dotnet test HotelOps.slnx`; executable harnesses run separately |

The cancellation fix affects only the new, unused-by-HTTP guard; relevant foundation suite was rerun after it. Existing endpoints' source behavior and the reused hash validator algorithm were unchanged. Runtime regressions are regression evidence, not evidence that these endpoints now invoke the guard. Dependency vulnerability scanning was not completed by a passing build.

## Database/workspace safety

Independent foundation and later Add regression use guarded technical trial `HotelOps_Canonical_20261027_175714_000e5f91_Fresh`. Reset/Activate use patched upgrade trial `HotelOps_Canonical_20261027_175723_bf6daf55_Fresh`. Create/Profile/Startup use separate patched-empty `HotelOps_Canonical_20261027_180300_41abcdef_Fresh` plus unpatched-negative `HotelOps_Canonical_20261027_180301_41bcdef0_Fresh`. Names carry UTC setup timestamps; report date uses Asia/Saigon. HotelOpsDB was not accessed.

SELECT-only cleanup immediately after foundation verification returned roles=12, permissions=62, temporary T41 roles=0, UserRole assignments=0, sessions=0. Owned users/audits and runtime receipts remain local; no plaintext credential is in artifacts. Separate regression fixtures have their existing cleanup conventions. No entity, schema mapping, RBAC Catalog, DEV2 module or other worktree change; no Commit/Push/Merge or artifact deletion.

Tracked diff and all new source/harness/documents pass whitespace checks. Index remains empty and HEAD/upstream remain `6599692b578410ef1ea8642a7d1bf0827f32c195`. The only tracked edit is the hash-support validator visibility; the new primitive and verification folder are untracked. Existing Task 40 documents/artifacts are preserved.
