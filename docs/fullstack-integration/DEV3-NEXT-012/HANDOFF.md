# UC010 Revoke V1 — DEV3-NEXT-012

UC010_REVOKE_SOURCE_STATUS: READY_FOR_REVIEW (candidate only).
BUSINESS_ACTIVATION: BLOCKED. SQL_ACCEPTANCE: NOT_RUN. No COMPLETE_VERIFIED claim.
Approval: user-supplied UC010-REVOKE-CONTRACT-001, source implementation only; issue44 reference. This NEW decision supersedes Assign-only exclusions for this isolated candidate; it does not open business activation.
Base22cff049c343b1e742ec6068dc7f6fb96202f83f; branch feature/dev3-uc010-revoke.

## Contract and delivered backend source

Candidate DELETE /api/users/{userId:long}/roles/{userRoleId:long}, NO body, HTTPS/cookie/CSRF. Strong quoted uppercase16hex target User rowversion; missing428, malformed400, stale412. Future committed204 must include new User ETag. No hard delete: proposed mutation sets exact UserRole.IsActive=false. No replacement/regrant, roleCode identity or new permission.

UserRoleRevocationCandidateController is NonController and has no DI/Program registration. Its direct action validates HTTP/cookie/ETag/body shape then calls service. Explicit antiforgery metadata and existing middleware conventions retained; metadata tests are not live antiforgery acceptance.

UserRoleRevocationService validates ticket/current actor/session equality, restriction/self denial and then ALWAYS returns ReleaseAcceptanceRequired before SQL for otherwise eligible direct calls. PendingReleaseGates is a read-only collection, not injected/configurable; CompleteOpenWorkCoverageAccepted also returns false before any future mutation and before commit. No empty dependency read, client flag or test double can bypass these fuses.

Below the immutable fuse, the future SQL writer structure is compiled for review: own clean local transaction; existing GlobalAdminSerialization then retained Users universe/User locks before feature locks; dual effective global USER_MANAGE/ROLE_MANAGE and SQL session gates; exact target/assignment/version; sole current branch-bound assignment with active target/masters/current membership and five-role exact delegation envelope; conditional User version update, soft revoke, existing SessionRevocationService in the SAME context/transaction, UPDATE audit, durable-admin poststate check, final actor/session gate and commit. Existing EF User/UserRole/UserSession/AuditLog mappings inspected; no generated model/schema edits.

IMPORTANT: this SQL writer structure is unreachable, NOT integration-ready or SQL-accepted. Complete cross-module open-work predicates/writer census and their initial/final transactional rereads have NOT been implemented because owners have not accepted those contracts. Removing the top fuse alone still cannot write. Enabling release requires a reviewed source change that implements these missing checks, revalidates complete target/assignment/scope/poststate at the final boundary and proves atomic session/audit/rollback behavior. Do not replace false with true or supply an injected clear evaluator.

Pure ValidateAssignment checks server-fact identity/branch/history/state/version only; null means these facts pass, NEVER authorization, open-work clearance or mutation approval. Error precedence in the locked candidate: invalid actor401, self/restricted403; otherwise release503 before data access. State-specific404/409/412 paths exist in dormant writer and pure validation, not currently reachable runtime endpoints.

## Frontend

Standalone RevokeUserRoleCandidate, not imported by any production route. Reuses accepted AuthProvider, profile GET ETag and effective-access assignment identity. These two reads are separate snapshots and grant NO revoke authority. Explicit Review/Confirm; default dispatchEnabled=false disables confirm and sends no CSRF/DELETE. Only explicit synthetic test callers enable dispatch; server fuse still denies actual success.

Typed revokeUserRoleApi has synchronous same-instance exclusion, relative URL, bodyless DELETE, credentials include/no-store, current identity/AbortSignal checks before and after CSRF/dispatch, no automatic queue/retry/replay. 204 requires a fresh strong User ETag. Errors expose only safe reason/status, never raw ProblemDetails/token/exception. CSRF bootstrap HTTP status retained; network/malformed preflight invents no status. Known exact409 is rejected; conflicting identifiers are unknown. 5xx/network/malformed success -> unknown. UI removes protected snapshot and confirmation after attempted mutation, never replays;401 offers explicit revalidation,412 requires a new read/review flow. Session/selector replacement remounts leaf, aborts old work and hides late results. No storage, permission inference or credentials.

## Actual receipts

- dotnet build HotelOps.slnx: exit0,0warnings/errors.
- dotnet run --project scripts/verification/UserRoleRevocation/UserRoleRevocation.Verification.csproj:42/42 checks PASS exit0. Actual MVC exclusion, bodyless/header/cookie/HTTPS preflight; direct-call immutable fuse/no SQL connection; self/restricted/session mismatch; cancellation; pure assignment valid/inactive/expired/future/foreign/cross-branch/unbound/history/stale rules. Concurrent direct calls both stay release-blocked; NOT one-success SQL race proof.
- dotnet test HotelOps.slnx --no-build: exit0, no framework cases reported; not counted as acceptance.
- node --test tests/revoke-user-role-api.test.mjs tests/revoke-user-role-browser.test.mjs tests/user-role-assignment-api.test.mjs tests/auth-http.test.mjs tests/auth-state.test.mjs:429/429 PASS exit0 (42 new revoke tests including synthetic204 success, double-confirm and stale identity;293 Assign and94 Auth regressions).
- npm.cmd run typecheck/build exit0. Build retains two inherited React Router use-client warnings.
- Scoped C# whitespace format verification and Prettier: exit0.
- Exact10file allowlist, staged whitespace and secret scan before publication.

No SQL connection opened by backend harness (parameterless SqlConnection with empty connection string, remains closed). Browser intercepts all external requests and synthetic fetch implements only fixtures. No live API/SQL performed. Real SQL success, stale mutation races, LastAdmin poststate enforcement, provider failure/audit rollback, complete dependency coverage and actual target-session invalidation: NOT_RUN, release BLOCKED. We do not call pure validation or mock204 a successful backend revoke.

## Remaining release gates / owners

1. DEV1/DEV2/DEV3 module owners + PL: accept exact cleaning/rework/operational task/incident/internal request/maintenance/other responsibility predicates and all reference writers, with final transactional rereads. DEV2PR25 remains separate.
2. DEV3: complete/prove atomic role+User version+target sessions+audit and last-admin preservation; fresh source review before release. Existing primitive reuse does not itself prove this orchestration.
3. PL + named trial owner: approve exact disposable SQL identity, schema prerequisites, lease and mutations; no shared DB assumed.
4. Approved trial owners: actual HTTPS/CSRF/auth, concurrency/exactly-one-success, stale/conflict, audit fault rollback, session invalidation, timeout/deadlock/uncertain commit acceptance. Deadlock1205 is FAIL, not masked PASS.

No existing tracked files changed. Auth/Users Assign, FE02, FE12, Program.cs, DbContext/entities, shared permissions and DEV1/DEV2 writers preserved. Workday writes OFF/NonController, UC008 Update version-blocked, no merge/deploy/migration.

## SHA256 of source/test files

7461AA26DBF7DBC1CEFE91E14496AAA66ACAEE2611ACCE1E401F36D73D6A5E64  src/HotelOps.Api/DTOs/Users/RevokeUserRoleContracts.cs
BA893B471FC8324C395A81156906AE610E057EF4C36EF657B070A37B26808E24  src/HotelOps.Api/Services/Users/UserRoleRevocationService.cs
12A0CAA817D647D93F41765B5CA8E1B00416C5B4B111619C0B9642B096362094  src/HotelOps.Api/Controllers/UserRoleRevocationCandidateController.cs
A4B06494B0369F55275839DC282F2014AF4BF62EAC68C93979A1580A640C9208  frontend/src/users/revokeUserRoleApi.ts
DE81300EF771B46CC878CD37C6F2137E5AAB9058C2356B9C3FBB1D8EF670C345  frontend/src/users/RevokeUserRoleCandidate.tsx
E9417A1CD3CB8692DFB64D181D52EBFCD3818D5EA58FB9D10EFDAB585BF03D44  frontend/tests/revoke-user-role-api.test.mjs
50A105D46FE3BEBF61EB60CB73793F660C803610EE4E50A15EFF95D34A21FFC9  frontend/tests/revoke-user-role-browser.test.mjs
4DDDA84D86559C486B1686C3D07BBE6388E13A0945D81188C80CEDB55EFD8348  scripts/verification/UserRoleRevocation/Program.cs
720478BD353C02A24462E2FE4F00BD713CC9ABD45A42AE42F904BDF140B13A50  scripts/verification/UserRoleRevocation/UserRoleRevocation.Verification.csproj
