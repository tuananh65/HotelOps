# Task 33 actual verification

Date: 2026-10-27 (Asia/Saigon). All listed passes were executed for the current implementation, not copied from Task 32.

| Check | Result |
|---|---|
| Solution build, final | PASS: 0 warnings / 0 errors |
| New reauthentication/reset harness build | PASS: 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; no framework-test discovery in this solution, console suites run separately |
| New reauth/reset service + actual HTTPS/SQL suite | Final 147/147 PASS; initial narrower run 137/137 PASS |
| Task 32 reset transaction/scope regression | 45/45 PASS |
| Credential initial/legacy/commit-boundary regression | 26/26 PASS |
| UC-004 + Directory/Detail/Profile/PATCH + Authentication/Startup HTTP/SQL regression | 546/546 PASS |
| CurrentUserAccessor | 14/14 PASS |
| PermissionService | 43/43 PASS |
| Authorization | 61/61 PASS |
| ResourceAuthorization | 49/49 PASS |
| AuthorizationHardening | 21/21 PASS |
| Fresh/upgrade/replay/rollback dependency | 26/26 PASS |
| git diff --check, final | PASS |
| SELECT-only cleanup audit on all three trials | PASS: zero live sessions, temporary roles/principals, fault CHECK constraints or triggers |
| Actual HTTP disconnect / provider commit acknowledgement loss | NOT VERIFIED |

Counts are mixed assertions, not independent endpoint scenarios or all reset-only tests.

## New suite coverage

Real current-password verification, no additional SQL session, correct actor/session binding, cross-session rejection, process-store-loss denial, correct five-minute limit and no touch extension. Wrong password preserves active session rows; existing five-attempt lockout prevents proof. Concurrent reauth passes under SQL locking. Real logout, inactive actor/reactivation, reset and expired SQL session invalidate proof. Reauth commit failure publishes no proof and rolls back audit.

Expiry uses a controlled backend TimeProvider with real SQL service/workflow tests: valid at four minutes, invalid at exactly five; expiry during reset UPDATE is detected by the final transaction gate and rolls back rotation. This is not client-supplied time, a production debug hook or a claim that the child HTTPS process's wall clock was advanced.

Actual HTTPS child API tests: login/logout, anonymous/expired/revoked/inactive/restricted reauth denial; current password success/wrong password; CSRF missing/invalid; strict unknown/duplicate/type/client-proof input. Reset tests cover no proof, other-session proof, actual ALL and BRANCH grants/memberships, crossbranch/OWN/self/branchbound ALL/no-permission denials, inactive/deleted/missing target visibility, required confirmation, no-store/ETag/response allowlists, 428/412, two admins racing, audit SQL failure rollback, target cookie revocation and restricted first login using the delivered temporary password. Temporary password cannot establish admin reauth. Default Information logs are checked in memory against supplied/current/generated credentials and stored fixture hashes.

Task 32 regression retains six rollback/cancellation points and post-real-SQL-commit fault/retry. Its obsolete endpoint-absence assertion is now an authenticated-controller assertion, not an arbitrary removal of a failing security test; the new HTTP suite verifies the full gates.

## Evidence and limits

Local JSON only: reauth-reset.local.json (137), reauth-reset-final.local.json (147), reset-primitive-regression.local.json (45), credential-legacy-regression.local.json (26), create-read-startup-regression.local.json (546).
Schema receipt: ../UserProfileUpdate/schema-5f474d54a56e4bd7b8033517c41e1213.local.json.
No passwords, cookies, CSRF values, proof data, tokens or password hashes are persisted in receipts. API logs are captured/inspected in memory, not written to artifacts.

Inherited startup negative whole-process timings: 15720ms / 31600ms / 18796ms. These include startup/runtime overhead and are not SQL duration or a production SLA.

Initial new harness build emitted EF1003 for a concatenated trial CHECK DDL call. Replaced it with a narrowly guarded DbCommand using only SQL-generated positive Int64 formatted as invariant digits; no warning suppression. Final new harness has zero warnings/errors. No runtime assertion failure occurred in the completed Task 33 suites.

Existing UC-004 harness projects retain NU1900 due unavailable NuGet vulnerability metadata. Auditing stays enabled. Previous approved restores outside sandbox in this session reproduced that environmental warning; no false vulnerability-clean claim.

Production multi-node proof reuse, production TLS/least-privilege/telemetry and actual network delivery failure remain outside certification. See OUTSTANDING_ISSUES.md.

## Workspace preservation

All 164 pre-existing Git status paths are retained (including Task 32's uncommitted work). No deleted paths or worktrees; root HEAD remains 92f14634b92d8fda6e6b044b92bf318e76ee3168 and index is untouched. The only tracked-file changes are Program.cs and the already-modified TemporaryCredentialService.cs. New/current Task 32 feature files and historical-document notices are listed in README.md. No other-worktree source was accessed or changed.
