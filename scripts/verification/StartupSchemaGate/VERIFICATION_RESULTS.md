# Executed verification — 2026-10-27

| Check | Actual result |
|---|---|
| Solution build | PASS, 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; console verification projects are not discovered by this command |
| StartupSchemaGate harness build | PASS, 1 NU1900 warning / 0 errors |
| Fresh + upgrade schema rehearsal | 26/26 PASS, forward/replay/rollback/reapply and row preservation/stale/ABA |
| Combined real API/SQL + startup harness | 377/377 PASS |
| Foundation regressions | 167/167 PASS: PermissionService 43, CurrentUserAccessor 14, Authorization 61, ResourceAuthorization 49 |
| EF model SQL compatibility | 82/82 entity zero-row projections execute on patched trial, including DEV2 tables; no business rows read or written |
| Git whitespace checks | git diff --check and git diff --cached --check PASS; index empty |

The combined count includes existing List/Detail/profile tests and grant-path doubles as well as actual Cookie/SQL runtime tests. It is not 377 startup-only checks. Real scoped grants exist only as test fixtures in the isolated database; official catalog fingerprints remain unchanged. Test roles and fixture sessions are cleaned in finally.

## Startup cases actually executed

- Patched: validator Compatible; actual API starts, protected anonymous request returns 401, health returns exactly 200 {"status":"ok"}; credential login, SQL sessions, list/detail/profile/PATCH function.
- Missing column: actual validator MissingRequiredSchema; API exits nonzero without health/users/CSRF responses. Column remains absent after checking; no automatic repair.
- Wrong type: deliberately added binary(8) NOT NULL fixture is rejected as MissingRequiredSchema despite correct name and byte length. API exits nonzero without HTTP responses; fixture type remains binary after checking.
- Inaccessible: connection to a unique nonexistent trial-name database returns DatabaseInaccessible; API exits nonzero without HTTP responses. Output contains the sanitized category and no full connection string/server/password key.
- Unexpected configuration error: malformed connection string returns UnexpectedValidationError without propagating provider details (validator test).
- Caller cancellation: OperationCanceledException propagated; no compatible result.
- Least-privilege metadata: actual validator passes under EXECUTE AS a trial user WITHOUT LOGIN with only object SELECT on dbo.users, no db_owner or table ALTER. Actual validator returns MissingRequiredSchema under an unprivileged user with hidden metadata. Both temporary principals removed.

Observed negative child-process rejection times: missing 44,115ms; wrong type 28,306ms; inaccessible 25,137ms. These measure whole child-process/probe/exit behavior, including cold initialization and connection probing, not SQL validation duration alone. SQL budget remains 30 seconds, command timeout 15 seconds. No historical cold-start timeout is declared resolved, and no production startup SLA is certified. Patched first-request timing was not separately recorded; protected-401 warmup remains in the inherited harness.

## Functional/security regression coverage

Real API confirms strict PATCH whitelist/unknown/duplicate/omitted/null handling; ETag; missing If-Match 428; stale 412; duplicate email 409; no-op behavior; actual two-writer stale rejection and injected audit failure rolling back profile/token/audit. Credential/status/roles/scope stay unchanged; ordinary profile writes do not revoke sessions. List/detail sensitive-field allowlists, SQL projection, pagination and global authorization remain intact.

Global USER_VIEW and USER_MANAGE stay separate. Real test-only BRANCH/OWN grants exercise common-membership scope, wrong-branch/inactive assignment/role denial, branch-bound ALL denial, OWN self-only and revoked membership. Doubles exercise additional path boundaries; they are not official runtime grants. Foundation harnesses use their documented synthetic stores/identities, no --live main database reads.

## Failures and warnings retained

Initial new harness --no-restore failed NETSDK1004 because it had no assets file; restore resolved this. First harness compile then failed CS0136 due to a local variable name copied into the larger scope; renamed within the new harness and rebuilt successfully. No application workaround was needed. NU1900 remains after an escalated restore attempt because vulnerability data is unavailable; auditing was not disabled. No failed test run is labeled PASS.

## Trial evidence and cleanup

New patched fresh and upgrade trials came from the original guarded setup; a separate new canonical trial handled missing/wrong-type cases. Evidence is local-only: startup-results.local.json and a new schema receipt under UserProfileUpdate. They contain labels/counts/hashes, not fixture passwords/cookies/CSRF. Never stage these receipts.

Read-only SQL cleanup verification on all three trials returned 0 unrevoked sessions, 0 runtime test roles and 0 temporary metadata principals. Fixture databases/rows are retained; no reset/drop. No HotelOpsDB connection, migration or real-session invalidation occurred. Production least-privilege login end-to-end, old binary coexistence and complete DEV2 business workflows were not executed.

SHA-256 preservation check: of 628 pre-existing tracked/untracked files, only the approved Program.cs changed; the other 627 remain byte-identical. No deleted paths. Task 28 adds validator, five reports, project/harness and two local receipts. All four worktrees remain intact; HEAD and remote refs unchanged. Nothing staged/committed/pushed/merged.

**Verdict: FIXED WITH NOTES.** Missing startup compatibility blocker is fixed and tested; production release approval and environmental NU1900/cold-start notes remain.
