# Task 38 verification results

Final implementation: approved Add-only endpoint enabled. All required gates exercised below passed on the current API. No Commit, Push or Merge performed.

| Verification | Result | Evidence |
|---|---|---|
| Task 38 schema/workflow/SQL-race/real HTTPS | PASS, 183 assertions | `http-sql-final.local.json` |
| UC-004, User List/Detail/Profile/PATCH, startup-schema HTTP/SQL | PASS, 546 assertions | `final-create-read-startup.local.json` |
| UC-005 reauthentication/reset, including SQL revocation races | PASS, 165 assertions | `final-reauth-reset.local.json` |
| UC-006 activation, including SQL revocation races and no Deactivate route | PASS, 169 assertions | `final-activation.local.json` |
| CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / Hardening | PASS, 14 / 43 / 61 / 49 / 21 checks | Console verification projects rerun against current source |
| Fresh/upgrade rowversion trial | PASS, 26 checks | `../UserProfileUpdate/schema-6f56f331479c4e95bbf47f4f48b0d152.local.json` |
| `dotnet build HotelOps.slnx` | PASS, 0 warnings/errors | Current source build |
| Task 38 verification project build | PASS, 0 warnings/errors | Current harness build |
| `dotnet test HotelOps.slnx --no-restore` | Exit 0, no framework test cases discovered/run | Relevant tests are executable verification projects, run separately above |
| Tracked/cached diff checks and new-file whitespace checks | PASS | No staging performed |

The old UC-004 HTTP and Activation harness project rebuilds each reported NU1900: NuGet vulnerability metadata was unavailable from api.nuget.org. Both built and executed successfully. This does not constitute a completed dependency vulnerability scan. An initial Task 38 harness compile failed on Span lifetime across await and emitted a nullable warning; both were corrected before the clean build and successful final run. No failed result is counted as PASS.

## Task 38 coverage

- ALL and independently valid source/destination BRANCH success; OWN, self, source-only, destination-only, branch-bound ALL and restricted denial.
- Real SQL-backed HTTPS login, cookie/CSRF enforcement, invalid/revoked session and actor role/membership denial.
- Strict JSON duplicate/unknown/mass-assignment/type/overflow rejection; If-Match missing 428, malformed 400, stale 412.
- Inactive/nonexistent Branch, inactive/soft-deleted target, duplicate and revoked membership refusal.
- Existing destination roles, including dormant/expired paths, conflict; effective OPERATIONS_MANAGER expansion refused even for ALL plus ROLE_MANAGE.
- Real SQL unique constraint covers revoked membership; child INSERT alone does not advance the User token; the service explicitly advances it atomically.
- Concurrent SQL and HTTPS Add: one success, one stale; old-token retry after commit cannot duplicate writes.
- Actual DMV lock waits for actor role assignment, role, grant, membership and session revocation, target role insert/revoke. Writer-first target role insertion and source membership revocation also serialize and deny Add after the writer commits.
- Controlled backend time advancement during a real SQL User update proves expired source authority cannot bootstrap final authorization through the new membership.
- SQL/audit and precommit faults roll back membership/token/audit. Injected postcommit failure leaves one committed mutation and stale retry. HTTP audit constraint fault returns sanitized 500 with complete rollback.
- Successful responses and Information logs exclude tested password/hash/cookie/CSRF/certificate secret values. Business snapshots preserve credentials, roles, status, primary Branch, Department and normal sessions.
- Fresh SQL permission evaluation observes membership/revocation changes without permission cache; an existing target session stays valid at its next HTTP request.

Core Authorization Foundation regressions use their documented doubles/synthetic HTTP fixtures. They supplement, rather than replace, Task 38's real SQL/HTTPS and lock-wait tests.

## Trial database safety

Task 38 technical SQL/HTTPS used `HotelOps_Canonical_20261027_165037_5c4e2c2b_Fresh`. Final UC-004/read/startup used new patched `HotelOps_Canonical_20261027_171611_8a6c5913_Fresh` plus separate unpatched negative trial `HotelOps_Canonical_20261027_171700_38bcdef0_Fresh`. Final reset/activation regressions used the patched upgrade trial `HotelOps_Canonical_20261027_171624_3bf69d82_Fresh`. Negative startup tests intentionally leave their owned negative trial malformed; they do not repair it.

SELECT-only postverification on the Task 38 technical trial confirmed: canonical roles 12, permissions 62; temporary Task 38 roles 0, unrevoked Task 38 sessions 0, Task 38 UserRole fixture rows 0, Task 38 audit fault constraints 0. Fixture users/membership rows and local receipts remain preserved. HotelOpsDB was not a test target and was not modified.

Real network response loss remains **NOT VERIFIED**. Broader load/deadlock behavior, production deployment and future UC-010 writer policy are not certified by this run.
