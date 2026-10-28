# Task 43 - current release verification

2026-10-28 Asia/Saigon, DEV1 / FE-01. Parent HEAD 6599692b578410ef1ea8642a7d1bf0827f32c195,
feature/BE1-database-integration, upstream origin/feature/BE1-database-integration (0 ahead/0 behind
at preflight), empty initial index. Existing four tracked changes and untracked Task41/42 source/
docs were preserved and reviewed; SHA256 preflight snapshot remains local. No branch/worktree
operations, DEV2, RBAC Catalog, entity/mapping or production schema changes. One controlled commit
is explicitly authorized by Task43 after these scoped gates. Exact allowlist: FILE_MANIFEST.txt.

## Executed final gates

| Scope | Result | Source/method and local-only receipt |
|---|---|---|
| Complete actual HTTPS/SQL reset + writer integration | PASS 108 assertions | Program --reset-http; ResetHttpIntegration.Run / WriterCoverage; task43-http-verified.local.json |
| Focused new raw/legacy/Create/Profile writer races | PASS 46 assertions | Program --writers-http; task43-writers-verified.local.json; subset also in full run, counts are not additive |
| Independent SQL foundation | PASS 26 assertions | Program default; task43-foundation.local.json; fixture status/role mutations, NOT production Deactivate/Revoke HTTP |
| UC-004 / Profile / List / Detail / Auth / Startup Schema Gate | PASS 546 assertions | UserAccountCreation.Http.Verification; task43-create-profile-startup.local.json |
| UC-005 reset / reauthentication / authority races | PASS 165 assertions | AdminReauthentication.Verification; task43-reauth.local.json |
| UC-006 Activate | PASS 169 assertions | UserAccountActivation.Verification; task43-activate.local.json |
| UC-007 Add Branch | PASS 183 assertions | UserBranchAssignment.Verification; task43-branch.local.json |
| Raw reset transaction/scope compatibility | PASS 45 assertions | UserCredentialReset.Verification; task43-raw-reset.local.json |
| Authorization Foundation | PASS | CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / AuthorizationHardening, executed with dotnet run --project <suite> --no-restore; doubles/loopback, not live SQL certification |
| Existing disposable fresh/upgrade schema verification | PASS 26 assertions | Verify-Schema.ps1; ../UserProfileUpdate/schema-997d17c979fb4ed6a9dc4684592765bc.local.json |
| Solution build | PASS, zero warnings/errors | dotnet build HotelOps.slnx after final C# changes |
| Solution test command | Exit 0, no framework tests discovered | dotnet test HotelOps.slnx; console harnesses executed separately above |
| Working-tree whitespace | PASS | git diff --check; separate new-file checks and staged checks at commit gate |

Some harness builds report NU1900 because NuGet vulnerability metadata cannot be reached.
Compilation passes; dependency vulnerability scanning is NOT VERIFIED. Git reports LF -> CRLF
conversion notices; no whitespace errors are hidden. No warning suppression was introduced.

## New writer and actual concurrency evidence

- Raw conditional reset returns LastAdministrator with no credential and unchanged SQL hash,
  marker, rowversion, sessions and audit. Stale input remains Stale. Legacy Apply throws the
  invariant exception and rolls back the same state. Concurrent raw/legacy resets affecting
  two admins leave exactly one durable regular administrator. Nonadmin resets pass with zero
  baseline admins. These are real service/SQL tests, not HTTP tests for nonexistent raw routes.
- Profile changing the final admin's only usable identifier returns409 LAST_DURABLE_ADMIN and
  preserves credential/session/token/audit. Ordinary employee profile succeeds; existing SQL
  same-column email uniqueness still returns409. Full regression covers stale If-Match and
  profile audit rollback, existing ALL/BRANCH/OWN and unchanged response/CSRF contracts.
- Create inserting a cross-column collision with the final usable admin identifier returns409,
  rolling back User, initial credential, role, membership and audits. With another eligible
  admin the same collision remains allowed, preserving the existing identifier contract.
  The guard evaluates every affected principal, not only the mutation target.
- Actual Create is paused at its SQL audit INSERT while holding the global/snapshot locks;
  a concurrent actual HTTP reset waits on that SPID. After Create201 commits its identifier
  collision, waiting reset409 refuses loss of the remaining admin. DMV observations prove
  blocking; no mock or assumption substitutes for SQL concurrency.
- For Create AND Profile: revocation committed while HTTP waits for the first global lock wins
  and returns403 without mutation. Conversely, a revoker waits on retained SQL authority locks
  after HTTP reaches audit; authorized mutation commits, revocation finishes, next HTTP403.
- Snapshot now requests Users TABLOCKX before catalog/feature locks. A real authentication-style
  User UPDLOCK holder completes its U-to-X update/commit while snapshot waits, avoiding the
  retained shared-lock conversion inversion. Controlled legacy Branch/Department/Role UPDLOCK
  probe still permits progress, then observes User INSERT waiting until protected reset commit.
- Existing HTTP cases retain actual two-reset concurrency, effective grants instead of role
  names, restricted credentials, finite lockout, ambiguous identifiers, stale input, audit fault
  rollback, DMV lock wait, 503 timeout, cancellation, intentional SQL deadlock500 and fresh retry.
  No credential/session semantics, automatic retry/replay or authorization policy is changed.

Raw/legacy authorization remains the documented trusted-caller responsibility, not an invented
permission decision. Their current production HTTP call sites are absent; public DI/internal
entry points still enforce last-admin preservation. Fully gated HTTP reset retains its existing
session/proof/authority checks; Create/Profile recheck their existing permission decisions.

## Failed attempts and corrections

- Old Task42 technical trial refused before fixtures because UserRole assignments remained.
  It was preserved; new isolated trials were created instead of resetting existing data.
- Expanded harness initially failed compilation with a local variable shadowing error (CS0136).
  Test-local identifier was corrected; final harness builds pass.
- The first mixed-writer probe required two simultaneous applock waiters within the existing
  5-second timeout. It failed, with observed Create503 / Reset200 and no two-waiter evidence.
  This is not counted as PASS and no timeout/policy was relaxed. Replaced with deterministic
  actual Create audit pause and DMV-observed reset wait; final schedule yields Create201 /
  Reset409 and proves the required invariant under concurrent requests.
- An added Profile race setup initially overlapped a Tag query and DMV query on the same
  observer DbContext. ETag is now materialized before starting the request; final focused and
  complete suites pass. No production concurrency policy was weakened to fix the test.
- Earlier 93-assertion pass preceded the expanded races and is not the final release evidence.

## Reproduction and safety boundary

Use README commands and existing guarded disposable canonical setup. Current Task43 trials:
HotelOps_Canonical_20261028_062431_543a230d_Fresh (writer/foundation/005/006/007/raw, sequential),
HotelOps_Canonical_20261028_062426_736b0c6d_Fresh (empty patched Create/Profile/Startup), and
HotelOps_Canonical_20261028_062600_43abcdef_Fresh (separate unpatched negative startup).
Names follow existing UTC timestamp convention; this report date is Asia/Saigon. Harnesses
validate exact server/database/Task17 ownership/schema before mutations. HotelOpsDB is never
accessed. Trials, retained users/audits, receipts, WAV, other artifacts and all worktrees remain;
only each harness's own temporary roles/grants/sessions/fault/certificate/process are cleaned.

NOT RUN / NOT VERIFIED: Deactivate/Revoke HTTP (not implemented), production SQL, load/least-
privilege deployment, universal mixed external-writer deadlock freedom, future grant-expiry
protection, multiple-instance proof behavior. System-wide protection is still partial.
Current coverage: MUTATION_PATH_MATRIX.md; remaining boundaries: REMAINING_UNPROTECTED_PATHS.md.

The following Task42 report is historical and superseded for raw/legacy/Create/Profile coverage,
Users table lock mode and controlled-commit authorization. Its results are not reused as Task43
execution results.

---
# Task 42 â€” executed integration verification

2026-10-28, Asia/Saigon. Current source reviewed on
`feature/BE1-database-integration`, HEAD `6599692b578410ef1ea8642a7d1bf0827f32c195`.

## Results and evidence

| Scope | Result | Method / new local receipt in this directory |
|---|---|---|
| Actual HTTPS/SQL protected reset | **PASS, 75 assertions** | `Program.cs --reset-http`, `ResetHttpIntegration.Run`; task42-reviewed-http-final.local.json |
| Independent SQL foundation | **PASS, 26 assertions** | `Program.cs` default mode; task42-reviewed-foundation.local.json |
| Controlled legacy Create lock-order probe | **PASS, 13 assertions** | `--reset-create-order`; task42-reviewed-create-order.local.json; also included in full HTTP run |
| UC-004 / List / Detail / Profile / authentication / Startup Schema Gate | **PASS, 546 assertions** | UserAccountCreation.Http.Verification; task42-reviewed-create-profile-startup.local.json |
| UC-005 reset / reauthentication / SQL authority races | **PASS, 165 assertions** | AdminReauthentication.Verification; task42-reviewed-reset.local.json |
| Raw/legacy reset transaction and scope compatibility | **PASS, 45 assertions** | UserCredentialReset.Verification; task42-reviewed-raw-reset.local.json; this does not certify last-admin protection for raw callers |
| UC-006 Activate | **PASS, 169 assertions** | UserAccountActivation.Verification; task42-reviewed-activate.local.json |
| UC-007 Add Branch | **PASS, 183 assertions** | UserBranchAssignment.Verification; task42-reviewed-branch.local.json |
| Authorization Foundation | **PASS** | Executed CurrentUserAccessor, PermissionService, Authorization, ResourceAuthorization and AuthorizationHardening via `dotnet run --project <suite> --no-restore`; isolated doubles/loopback suites, not live production SQL certification |
| Existing fresh/upgrade rowversion schema verification | **PASS, 26 assertions** | UserProfileUpdate/Verify-Schema.ps1; ../UserProfileUpdate/schema-d26e4c15db0f483c87fa73a3e632d482.local.json |
| Solution build | **PASS, zero warnings/errors** | `dotnet build HotelOps.slnx` after final source comment update |
| Solution test command | **Exit 0; no framework cases discovered** | `dotnet test HotelOps.slnx`; actual executable harness results above are reported separately |
| Whitespace | **PASS** | `git diff --check`; Git also reports existing LF -> CRLF conversion notices for controller/interface |

Some harness builds (LastAdminProtection, Activation, Create HTTP) emit **NU1900** because
api.nuget.org vulnerability metadata is inaccessible. Compilation succeeds; dependency
vulnerability scanning is not certified. No warning is suppressed.

## HTTP/SQL assertions

The child runs the actual API assembly over ephemeral pinned loopback HTTPS. It uses actual
login, SQL sessions, cookies, CSRF, password reauthentication, If-Match and the existing reset
route. No new production endpoint, fake authorization or fixture-only reset service is used.

- Last durable admin: 409/no-store, with unchanged hash, temporary marker, finite lockout,
  rowversion, sessions and audit; no credential handoff.
- Two eligible admins concurrently reset by separate authorized actors: exactly one 200 and
  one 409, with one regular full-session eligible admin surviving in SQL.
- Nonadmin target reset succeeds even with zero baseline admins. Successful credential verifies
  with the unchanged Identity V3 implementation; committed ETag matches SQL rowversion;
  target sessions revoked and old cookie rejected.
- Capability uses custom role grants; an actor role labelled SYS_ADMIN has USER_MANAGE only
  and does not count. The real target's differently named role has both required grants.
- Temporary/restricted or SQL-ambiguous second administrator cannot protect the final regular
  administrator. Finite LockedUntil survivor does count durably and its lock is not cleared.
- Stale If-Match and old-token retry return 412 without rotation or audit.
- Actual audit CHECK failure produces sanitized HTTP 500/no-store and complete rollback.
- DMV-observed HTTP wait on global applock; timeout returns 503 unchanged; fresh retry succeeds.
- Client cancellation reaches blocked SQL, cancels waiter while owner retains its lock, leaves
  target unchanged and permits fresh retry.
- Deliberately inverted role writer creates a real SQL deadlock: victim HTTP 500, whole reset
  rollback and successful fresh retry. No automatic retry/replay is installed.
- Revoke actor assignment while HTTP waits for global lock: recheck returns 403 unchanged.
  Conversely, revocation waits on retained SQL locks after reset reaches audit INSERT, reset
  commits, revocation completes and next HTTP request returns 403 without mutation.
- Create-style Branch/Department/Role UPDLOCK permits snapshot progress. Its User INSERT waits
  until protected reset commits. This is a SQL fixture schedule plus actual reset HTTP, not a
  claim that concurrent Create HTTP and every other legacy writer are deadlock-free.
- API logs checked for generated passwords, hashes, cookies, CSRF and certificate secret.

Foundation's 26 assertions separately exercise caller transaction/isolation/tracker requirements,
snapshot binding, split effective grant intersection, temporary/hash/identifier classification,
finite horizon, fixture status/role removal rollback, audit FK fault, actual blocking, lock
timeout/cancellation/release/retry and intentional deadlock. Those are **SQL foundation tests**;
their fixture mutations are not production Deactivate/Role Revoke HTTP tests.

## Failed attempts retained in this report

Initial sandbox SQL connection failed before fixture creation with SqlClient encryption/runtime
error 20. Running the same guarded disposable-only command outside the sandbox succeeded.
No authentication/TLS policy or connection encryption was weakened.

The first full HTTP run reached and passed the reducing-mutation/failure cases but failed its
Create-order probe with HTTP 500. Its test-only audit trigger conflicted with EF's OUTPUT
INSERTED behavior. The harness was corrected to pause the real INSERT using a transaction-held
AuditLogs table X lock, removing the need for a trigger. Standalone probe passed, then the
complete expanded HTTP suite passed all 75 assertions. The failed attempt was not promoted
to a success receipt and is not included in the final PASS count.

## Safe reproduction and database boundary

Build API and LastAdminProtection.Verification first. From repository root:

```powershell
dotnet scripts/verification/LastAdminProtection/bin/Debug/net10.0/LastAdminProtection.Verification.dll <guarded-patched-trial> <NEW-local-receipt>
dotnet scripts/verification/LastAdminProtection/bin/Debug/net10.0/LastAdminProtection.Verification.dll <guarded-patched-trial> <NEW-local-receipt> --reset-http
```

Harness verifies canonical database name, exact local server, Task17 extended-property marker,
startup schema and absence of pre-existing UserRole assignments before fixtures. Foundation
and HTTP/reset regressions ran sequentially on existing disposable trial
`HotelOps_Canonical_20261027_175714_000e5f91_Fresh`. New empty patched trial
`HotelOps_Canonical_20261028_061154_2207a195_Fresh` supports Create/Profile/Startup; separate
unpatched negative trial `HotelOps_Canonical_20261028_061400_42abcdef_Fresh` supports startup
rejection. Activate/Add run sequentially on new patched upgrade trial
`HotelOps_Canonical_20261028_061201_eb4135c5_Fresh`. Timestamp components follow existing UTC
setup convention; this report uses Asia/Saigon.

No HotelOpsDB access. Existing artifacts/trials/worktrees preserved; test runs only clean their
own transient roles/grants/sessions/fault constraint/certificate/process resources according to
the existing harness contracts, retaining fixture users/audits and local receipts. No DEV2
source, production schema, generated entity, mapping, RBAC Catalog or shared configuration edit.

## Source manifest and limits

Production pending diff reviewed: Controllers/AdminCredentialResetController.cs,
Services/Authentication/IConditionalCredentialResetService.cs, PasswordCredentialService.cs,
TemporaryCredentialService.cs; untracked Services/Users/GlobalAdminSerialization.cs.
Task 42 verification source: Program.cs (HTTP modes), ResetHttpIntegration.cs and existing
LastAdminProtection.Verification.csproj. This turn corrected the supplied unfinished HTTP
harness, added authority-race tests, clarified the primitive's caller comment, created the
three Task 42 documents and added supersession notices to README, MUTATION_PATH_MATRIX,
LOCK_PROTOCOL, INTEGRATION_BLOCKERS and SQL_VERIFICATION_RESULTS. Existing preflight source
edits are distinguished in INTEGRATION_DECISION.md. Runtime receipts/bin/obj are artifacts.

NOT RUN / NOT VERIFIED: Deactivate/Role Revoke HTTP (no endpoints), live production database,
load/least-privilege deployment, all legacy/external writer combinations, multi-instance proof
behavior and future scheduled expiry guarantee. One protected workflow is implemented and
verified; system-wide protection remains partial. See REMAINING_UNPROTECTED_PATHS.md.
No Commit/Push/Merge; final index empty, HEAD/branch/worktrees unchanged.
