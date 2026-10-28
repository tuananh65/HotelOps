# Task 36 verification — 2026-10-27

Baseline: branch feature/BE1-database-integration, HEAD/upstream `1a969e3731d12d430a250e219caa1e1fbfeb52b9`. Index and tracked worktree were clean; Task35 source/docs/harness and other runtime artifacts were already untracked. All existing artifacts/worktrees are preserved. Only Program has a tracked diff (two scoped registrations); new/untracked feature dependencies are explicitly listed in README. No stage/commit/push/merge.

| Executed check | Result / evidence |
|---|---|
| Solution build | PASS,0 errors/0 warnings |
| dotnet test HotelOps.slnx | Exit0; solution contains API only, no framework tests discovered; console suites executed explicitly |
| New Activate HTTPS/SQL race suite | **PASS169**; final source: activation-release.local.json; earlier passing receipts retained |
| Task35 parser/transition/session policy | PASS26; executed before SQL primitive checks |
| Task35 SQL activation/helper/commit-boundary | PASS27; activation-primitive.local.json |
| UC005 reauthentication/reset HTTP+SQL | PASS165; reauth-reset.local.json |
| Reset transaction/scope | PASS45; reset-primitive.local.json |
| Initial credential/legacy/commit-response boundary | PASS26; credential-legacy.local.json |
| UC004 + List/Detail/Profile/PATCH + Startup real HTTP/SQL | PASS546; create-read-startup.local.json |
| CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / Hardening | PASS14/43/61/49/21 =188 |
| Fresh/upgrade schema compatibility | PASS26; ../UserProfileUpdate/schema-c40d16a266ed4425a54dd6d90c81392a.local.json |
| git diff --check + CRLF-aware checks of all15 changed/new source/harness/docs | PASS |

169 includes21 assertions over7 real SQL revocation races. The final HTTP suite verifies ALL and BRANCH activation, cross-branch/OWN/self/bound-ALL/OWNER/no-grant/restricted denial, inactive role/assignment, revoked grant/membership, future/expired assignment, inactive/deleted/revoked/expired actor session, strict JSON/duplicate/unknown/status input, missing/malformed/stale If-Match, no-op/unknown transition, missing/deleted target,16KiB body cap, CSRF, concurrency one200/one412, SQL audit fault rollback and absent status/deactivate routes. Response whitelist/no-store/ETag matches committed SQL. Audit actor equals the actual authenticated caller, with exact before/after/reason whitelist. Preserved state includes a real operational task assignment, role/membership, password/hash/expiry/lock, profile and revoked SQL session. Activated locked/expired-credential accounts still fail real login with423/403 respectively.

Final actual API Information logs were scanned **in memory** against passwords, hashes, cookies, CSRF and temporary certificate password; PASS. Receipts contain case labels/counts only, not responses/headers/secrets. Initial credential/reset regressions preserve their one-time handoff/no-replay rules.

## Test findings resolved

Initial HTTP runs stopped at oversized-body assertions: model-binding BadHttpRequestException reached the existing generic500 middleware. First expectations413 and400 both failed; these runs are not reported as PASS. A route-local exception filter now translates only body-limit failure to sanitized413 REQUEST_TOO_LARGE. An intermediate broad filter was narrowed during final review to retain the existing middleware's generic500 and session-unavailable503 behavior. Existing middleware logs only trace ID and exception type, never exception/request/cookie values. Full suite rerun covers413 and SQL rollback500.

Initial harness build also emitted EF1003 for dynamic DROP CONSTRAINT. Cleanup now uses explicit DbCommand with an internally generated ASCII identifier, matching its guarded trial-only CREATE; EF1003 no longer occurs. Harness builds retain NU1900 because NuGet vulnerability metadata cannot be reached. Warnings are not suppressed. Main solution build is clean.

An auxiliary no-index whitespace scan initially treated existing CRLF endings as whitespace; a CRLF-aware rerun passed all15 files without renormalizing them. Standard git diff --check passes. All186 pre-existing untracked paths were confirmed present; final index stays empty and all worktree HEADs are unchanged.

## Database safety and limitations

Only these disposable Task36 databases were used:

- HotelOps_Canonical_20261027_155531_ddaa2fd9_Fresh — unused patched personnel trial for UC004/read/PATCH HTTP regression.
- HotelOps_Canonical_20261027_155536_f7dcdb83_Fresh — patched technical trial for Activate/UC005/credential checks, with unique fixture prefixes each run.
- HotelOps_Canonical_20261027_160000_36abcdef_Fresh — separate negative startup trial intentionally malformed by the inherited harness.

No HotelOpsDB read/write command, production migration, RBAC catalog source edit or DEV2 source edit. Existing approved schema-trial scripts patch only guarded disposable databases; backend continues to require users.row_version. Test users/work/history and trial DBs remain. Harness-owned sessions are revoked and only owned temporary grant roles/fault CHECKs are removed; existing local artifacts are not deleted. JSON/bin/obj remain local artifacts and are excluded from source packaging.

SELECT-only cleanup checks on all three trials returned0 live sessions,0 T36 temporary roles,0 T36 fault CHECKs,62 permissions and12 roles. The final Activate rerun performs the same finally cleanup; the technical trial is checked again after it completes.

Real network response-loss/commit-ack ambiguity is NOT VERIFIED. Postcommit injected exception and stale retry are covered by the primitive suite; no idempotency/recovery workflow is invented. Deactivation, its open-work/last-admin invariant and shared DEV2 writer protocol remain BLOCKED/NOT VERIFIED and are outside Task36. Deployment approval is separate from implementation/test results.
