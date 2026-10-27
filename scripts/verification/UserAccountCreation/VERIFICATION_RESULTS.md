# Task 30 actual verification

## Results

| Check | Executed result |
|---|---|
| Solution build | PASS, 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; console harnesses are not discovered by this command |
| Participant/legacy credential SQL harness | Final 22/22 PASS; initial smaller suite 19/19 PASS |
| Combined actual API/SQL suite | Final 546/546 PASS; first smaller suite 533/533 PASS |
| Existing authorization foundations | 167/167 PASS: 43 PermissionService + 14 CurrentUserAccessor + 61 Authorization + 49 ResourceAuthorization |
| Existing fresh/upgrade/replay/rollback SQL suite | 26/26 PASS, repeated on newly created trials; no original SQL edits |
| Harness builds | PASS; final builds retain NU1900, no C# errors or EF1003 |
| Git whitespace | git diff --check and cached check PASS; no staged changes |

The 546 checks include inherited List/Detail/Profile/Startup assertions, helper/login assertions and UC-004 checks; they are not 546 creation-only tests. Runtime tests use real API binary, Cookie+SQL sessions and actual grants, with additional grant-path doubles clearly inherited from earlier suites. Test-only grants are created only on owned trials, never in official seed/catalog source.

## UC-004 exercised

All six approved initial role/department pairs succeed with201/no-store/Location. Actual committed records have ACTIVE account, one primary membership, one role bound to target branch, actual server actor and two audits. Returned temporary credential verifies against the unchanged Identity V3 hash; 24h expiry and restricted first-login behavior are verified. Ordinary detail does not replay the handoff. Default Information logs contain neither delivered plaintext nor corresponding hashes.

Anonymous with valid CSRF401; expired SQL session401; missing CSRF400; restricted/missing/inactive/expired/future/branch-bound grants403. USER_MANAGE alone and ROLE_MANAGE alone both deny. A branch-bound actor is denied for another branch; a valid unbound ALL actor succeeds at the other active branch. No actor role-name bypass.

SYS_ADMIN/OWNER/OPERATIONS_MANAGER/DEPT_MANAGER/HK_INSPECTOR/STAFF/unknown roles reject. Missing role, multi-role/security overposting, unknown/duplicate JSON, bad types/lengths/IDs, department mismatch and wrong-branch department reject without accounts. Allowed role inactive, department inactive and branch inactive reject based on actual SQL flags, restored afterward.

Actual username/email/employeeCode constraints reject duplicates with409 and correct confirmed duplicateFields. Exact POST retry conflicts without returning the old credential. Concurrent HTTP duplicates give one201 and one409. Independent two-connection uniqueness tests separately prove exactly one insert for each of the three unique columns.

Trial-only CHECK fault constraints target account CREATE audit insertion and the credential hash UPDATE precisely. Both return generic500 without handoff and roll back user, credential, initial role/membership and audits. Constraints are removed in finally. Parent SQL harness additionally verifies explicit caller rollback, post-update provisioning failure, audit failure, cancellation, dirty-context guard, one-time handoff and old ApplyAsync initial/reset/own-rollback behavior. No reset endpoint was added.

## Startup and existing API regression

Patched API starts and health stays200 with the existing minimal response. Missing/wrong-type/inaccessible databases fail before health/users/CSRF traffic. Hidden metadata fails closed; object-SELECT principal passes; caller cancellation and unexpected configuration classification are exercised. Final negative child times: 21,933ms / 21,904ms / 26,534ms (whole-process observations, not SQL duration or production SLA). Historical cold-start behavior is not declared solved.

List/detail/profile authorization, response allowlists, SQL projections, pagination, If-Match428/412, duplicate email409, no-op, concurrent profile update, audit rollback, real runtime BRANCH/OWN and SQL session revoke/expiry checks all execute in the combined suite.

## Failed/intermediate verification, retained honestly

- First HTTP harness compilation failed CS0136 from a local variable name collision; corrected only in the new harness.
- First runtime suite533 completed successfully, but its trigger-based fault injection could also fail because of EF OUTPUT/trigger compatibility. Final suite replaces this with precise CHECK constraints; the earlier run is not used as proof of the exact credential-failure stage.
- First expanded runtime stopped on a harness LINQ translation error while selecting an in-memory department reference inside an EF predicate. Extracted the scalar ID before the query and reran on entirely new trials. That stopped run is not a full PASS.
- EF1003 in harness DDL cleanup was resolved with constant cleanup statements and an explicit generated-fixture literal guard, not warning suppression.
- NU1900 remains after escalated restore attempts; security auditing was not disabled.

## Safety and limits

Read-only SQL cleanup on all eight Task 30 trials reports 0 unrevoked sessions, 0 test roles, 0 metadata test principals and 0 fault triggers/constraints. Every trial retains the canonical 2 active branches, 8 active departments and 10 active roles. Fixtures/databases remain for review; no reset/drop. Existing main database was not connected to, read, migrated or changed.

Local receipts contain only labels/counts/target metadata and remain untracked; no passwords/cookies/CSRF or credential hashes are written to artifacts. Temporary TLS certificates are unique owned temp files and cleaned in finally.

Actual network disconnect at commit and commit-unknown transport failures were not injected. Lost-response handling is verified by exact replay/conflict plus source/lifecycle analysis, not a claim of exactly-once network delivery. Production least-privilege/TLS/old-binary/DEV2 workflow certification remains outside this task.

Final post-verification solution build PASS0 warnings/errors. SHA-256 comparison across 645 pre-existing files finds only the two approved shared edits (Program.cs and TemporaryCredentialService.cs); the other643, including Task22/26/28/29 source/docs, canonical SQL, DEV2 and local artifacts, remain byte-identical. No deleted paths. Added four application files, four harness/project files, six reports and six local receipts. No staging/commit/push/merge; HEAD remains dda26e4.

Final verdict: IMPLEMENTED WITH NOTES — approved UC-004 slice implemented and tested; environmental and production rollout restrictions remain.
