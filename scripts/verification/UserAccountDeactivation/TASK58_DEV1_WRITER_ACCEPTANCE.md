# Task58 — Existing DEV1 writer SQL acceptance

Ngày 2026-10-30; branch feature/BE1-database-integration; HEAD
`4a187320643d0c82f232c6b9dd5a667b8401c280`. **Scoped acceptance PASS; UC006 release PARTIAL/BLOCKED.**
Không triển khai deferred workflows, activate SA57, thay permission/schema hoặc mở Deactivate.

## 1. Executed evidence và trial prerequisites

- Backup: `obj/task58-backup-20261030-010531`, manifest/checksum 366 preexisting modified/
  untracked files, recoverable source/document copies. Index empty; HEAD/upstream/four worktrees
  giữ nguyên. Không commit/push/merge/reset/clean hoặc xóa artifacts/worktrees.
- Baseline/fault/regression DB: `HotelOps_Canonical_20261030_010658_af93dfdc_Fresh`.
  Accepted writer DB: `HotelOps_Canonical_20261030_012434_cc6a1808_Fresh`.
  Cả hai tạo bằng unchanged UserDirectory/Prepare-Database.ps1: canonical full schema, approved
  seed/reference và Inventory/MR/Replenishment/Maintenance patches; unchanged guarded
  UserProfileUpdate/rowversion-forward.sql. Exact server/name/trial-marker guard và
  StartupSchemaValidator Compatible chạy trước SQL suite. Không truy cập HotelOpsDB.
- [Accepted receipt](task58-writers-accepted.local.json): **112 PASS / 0 FAIL, 83 DMV-confirmed
  lock waits**, 12 actual service operations. After owned cleanup, mọi mapped table bằng
  pre-run rows, gồm credentials/access/sessions/audits/assignments/catalog; SQL identity và
  global rowversion counters có thể tăng. Sensitive snapshots chỉ so sánh trong memory.
  Profile regression sau đó giữ fixture riêng theo harness contract, không tuyên bố DB cuối
  task vẫn trống. Hai disposable databases và mọi receipt/log đều được giữ.

Reproduce trên **fresh guarded compatible trial**, receipt chưa tồn tại:

```powershell
dotnet build HotelOps.slnx --no-restore
dotnet build scripts/verification/UserAccountDeactivation/UserAccountDeactivation.Verification.csproj --no-restore
dotnet scripts/verification/UserAccountDeactivation/bin/Debug/net10.0/UserAccountDeactivation.Verification.dll <trial-name> <new-receipt.json> --writers
```

Không sửa machine/database guards để dùng DB chung. `--writers` có budget 12 phút;
DMV wait observation ~6.4s, barrier 18s, SQL lock timeout 700ms, command bounds hữu hạn.
Deadlock injector tạo inverse cycle **test-only**, không phải protocol được deploy.

## 2. Actual writer coverage và lock/transaction review

G = GlobalAdmin applock → Users TABLOCKX,HOLDLOCK → grant/catalog snapshot. U = retained
distinct numeric Users ascending, UPDLOCK/HOLDLOCK. S = UserSession after User. Tất cả dùng
caller scoped DbContext; không context/connection phụ để mutation. Không acquire G sau feature.
IDs dưới đây là server-side actor/target/related references, không hard-code ID catalog.

| Actual writer | Participants / all User-lock sites | Authoritative reread sau wait và order | Transaction/rollback và SQL evidence |
|---|---|---|---|
| UserAccountCreationService.CreateAsync | Actor + new identity; G covers entire User namespace, including cross-column username/email collisions. StageInitial/SessionRevocation reenter new User lock in same tx | G → actor SQL session revalidation (Task58 fix) → permission paths → branch/department/role locks → insert → credential participant → LastAdmin + final session/permission check | Own tx; credential handoff only after commit; failed audit/final expiry rolls whole identity/role/membership/credential back. 11 acceptance cases; participant/post-commit regression separately |
| UserAccountActivationService.ActivateAsync | Actor/target U ascending; sessions.Validate reenters actor; target conditional update retains X | U → authority ranges/session → target status/version reread → audit → final authority/session | Own tx; target INACTIVE → ACTIVE only. 10 cases; actual Deactivate-first fixture returns Conflict after holding Users universe, not deactivation success |
| UserRoleAssignmentService.AssignAsync | G then actor/target U; actor session reentry; target reference/dept membership/grants | G → U → authority/destination/dept → current target version/account + exact role grants/no conflicting historical assignment; repeat final gate | Own tx; conditional User version touch + role + audit atomic; 10 cases. No Role/Permission/RolePermission catalog mutation |
| UserBranchAssignmentService.AddAsync | Actor/target U ascending; actor session; target role/catalog/membership ranges | U → authority/roles/branch → target/version + destination/no dormant role/OM conflict → final gate excludes inserted membership as authorization source | Own tx; account-version/membership/audit rollback together; 10 cases. Membership writer does not create task responsibility |
| UserProfileService.UpdateAsync | G covers actor/target and identifier namespace; Task58 actor session retained after Users; permission authority ranges | G → SQL session → authority → profile/version reread; name/email/phone allowlist only; LastAdmin and final session/permission boundary | Own tx; 12 cases including session expiry after actual audit flush; credentials/roles/membership unchanged; real HTTPS/SQL regression 329 assertions |
| AdminCredentialResetWorkflow.ResetAsync → TemporaryCredentialService.ResetWithGateAsync | G → sorted actor/target U → authority/reauth session; ApplyCore target U; SessionRevocation target U then target sessions | Fresh SQL actor session + version-bound real reauth proof, target status/version, final proof/scope; LastAdmin poststate when durable admin affected | Concrete real workflow, no fake gate/proof. 11 cases; actual raw trusted ResetAsync rejects sole durable admin with complete rollback (not HTTP authorization/UC006 poststate certification) |
| AuthenticationLoginService.LoginAsync → CredentialLockout.VerifyAsync/CreateSessionAsync | Candidate discovery read releases RC locks; authoritative candidate User U, then session insert; same User reentry for proof consumption | Under User lock recheck identifier candidate set, credential/hash/eligibility/counters/expiry; same-transaction single-use proof; LastLogin/rehash before commit | Own tx; 8 cases; session-create/login audits exact counts. Failed-counter/lockout branches additionally covered by reauth regression; no automatic retry |
| AuthenticationCredentialWorkflow.LogoutAsync → RevokeSessionAsync | Target User U → matching session U | SQL session hash/user/revocation predicate; logout deliberately idempotent, does not require active account to release session | Own tx; revoke + audit rollback; 7 cases; null-actor authentication audit included in snapshots/cleanup |
| AuthenticationCredentialWorkflow.ChangePasswordAsync | Target User U → session; revoke-all reenters same User; CreateSession proof reentry | Current password, session/hash/temporary expiry/active state reread after hashing; revoke old sessions, create fresh normal session | Own tx; 8 cases. Exactly two old sessions revoked, one new session and two auth audits; failures roll all credential/session writes back |
| SqlSessionService.TouchSessionAsync | Caller-owned tx, User U → matching session U | SqlSessionPolicy + conditional last-used/expiry update from fresh no-tracking row | 8 cases; caller commits/rolls back in harness per public contract. No audit is required for touch; its fault case is post-UPDATE commit failure, not fabricated audit failure |
| SqlSessionService.RevokeAllSessionsAsync → SessionRevocationService.RevokeAllAsync | Caller-owned tx, target User U before sessions; helper reenters same User | Fresh SQL User/session set, revoke predicate; one SESSION_REVOKE_ALL audit | 7 cases; both session revocations + audit atomic. Direct helper does not independently own/commit a transaction |
| AdminReauthenticationService.ReauthenticateAsync | Actor User U → exact session; CredentialLockout reenters same actor | Valid unrestricted SQL session → password/counter state → session recheck → user version; publish in-memory proof only after SQL commit | Own tx; 8 cases; cancel/deadlock/audit fault cannot publish committed proof; existing 165-assertion regression covers proof expiry, binding and real HTTP reset |

Create/Profile session validation uses the already authenticated **server-side** CurrentUserInfo
binding, SQL actor+SessionId and existing SqlSessionPolicy deadlines. It does not authenticate
client-supplied IDs, grant permissions, touch sessions or change API DTO/status contracts.
New locks occur after the retained Users universe; existing auth/session writers take User
before Session. No newly observed inverse cycle in the schedules exercised here. This is
bounded evidence, not proof of all external writers/plans/isolation combinations.

## 3. Exact concurrency/fault assertions

| Scenario | Executed outcome / boundary |
|---|---|
| Writer-first (12) | Actual service held User lock through business/audit flush; standalone UserEligibilityLock contender waited, DMV matched SPIDs/blocker/LCK_M type, then reread after real commit. Probe is not full UC006 |
| Actual Deactivate-first (12) | Actual service paused after acquiring Users TABLOCKX; writer waited. Deactivate later rolled back at ResponsibilitiesUnresolved, or Conflict for inactive Activation fixture; writer then committed. No INACTIVE Deactivate commit simulated |
| Stale target (5), inactive actor (6), revoked actor session (6), inactive authentication user (4) | Committed competing owned edits reread after real SQL wait; writer rejected with unchanged post-competing aggregate. Five ETag writers return Stale; post-wait actor/session authority is SQL-backed |
| Actor expiry (Create/Profile), final expiry (Create/Profile) | Expired actor rejected after wait. Advancing test clock after **real audit INSERT** expires actor before final boundary; transaction rolls all staged business/access/credential/audit state back |
| Cancellation (12), timeout1222 (12) | DMV-confirmed wait first; cancellation propagates OCE, actual SQL 1222 propagates when not cancelled; owned service transaction disposed; no partial data or hidden retry |
| Deadlock1205 (12) + explicit fresh retry (12) | Owned sentinel audit-row lock deliberately closes test-only cycle after service flush; actual provider 1205, rollback, then fresh-context explicit retry commits once with exact effects/audit counts. Not evidence that canonical production ordering is deadlock-free |
| Audit failure (11) / touch commit fault (1) | Interceptor fails actual audit SQL command after preceding business changes, or commit after Touch UPDATE; all aggregate/session/audit state rolls back. NULL ActorUserId authentication events included |
| LastAdmin | Actual raw trusted credential reset rejects the sole durable admin and rolls back password/session/audit. Existing --contracts verifies one remaining eligible admin allowed, zero rejected in foundation; actual Deactivate poststate still BLOCKED |
| Preservation | Every mapped table compared before/after owned cleanup, plus immutable RBAC catalog snapshot. No production/deferred assignment writes or catalog edits |

## 4. Reproduced defects, fixes and unsuccessful attempts retained

[Baseline receipt](task58-writers-baseline.local.json): **101 PASS / 6 FAIL**. Two failures
were actual Create/Profile commits after session revocation during wait; four were provider
SqlException on cancellation in GlobalAdminTransaction's Users-universe query.

1. [AccountWriterSessionGate](../../../src/HotelOps.Api/Services/Users/AccountWriterSessionGate.cs)
   + Create/Profile initial/final checks now reject revoked/expired/restricted/missing SQL
   session or inactive actor. Permission/LastAdmin guards remain. No optional bypass flag.
2. [GlobalAdminSerialization](../../../src/HotelOps.Api/Services/Users/GlobalAdminSerialization.cs)
   normalizes SqlException to OCE **only when the supplied CT is cancelled** at the Users
   snapshot read. Noncancelled 1222/1205 remain faults; caller retains rollback responsibility.
3. New Task58WriterAcceptanceSqlSuite and --writers mode use actual services, real SQL role/
   session data and bounded DMVs. Creation participant/Profile regression fixtures now use
   actual SQL SessionId; the participant-only fake grant test remains labeled technical.

The first post-fix run [receipt](task58-writers-final.local.json) had 111 PASS but preceded
the NULL-actor audit snapshot/cleanup correction; **accepted evidence is the later 112-case
receipt on a fresh DB**, not the earlier cleanup claim. Earlier trial may retain those audit
fixture artifacts; it is preserved, never used as production evidence.
Initial harness compile failed for DTO constructor syntax (corrected to actual init properties).
Two extended-snapshot attempts stopped before fixture writes: EF default-schema metadata
lookup, then DBNull for empty FOR JSON result; corrected without changing schema/production.
All logs retained under obj/task58-*. Initial sandbox SQL setup failed SSPI, authorized local
SQL execution succeeded; no schema or test bypass used to remedy that environment failure.

## 5. Relevant verification and release disposition

| Run | Result / receipt |
|---|---|
| Writer acceptance | 112 PASS, 83 observed waits; task58-writers-accepted.local.json |
| Actual Deactivate fault/authorization/session suite | 53 PASS; task58-service.local.json; success mutation still blocked |
| Contracts + actual Activation + LastAdmin foundation | 64 PASS; task58-contracts.local.json; five runtime contracts/two Unsupported retained |
| Actual ReturnWork integration/fault regression | 57 PASS; task58-returnwork.local.json; standalone inactive-first and actual denial integration distinguished |
| Reauth/reset and HTTP pipeline | 165 PASS; task58-reauth.local.json |
| Credential participant/Create committed-failure retry | 26 PASS; task58-create-participant.local.json; technical grant isolation, not HTTP Create certification |
| Profile/directory HTTPS + SQL regression | 329 PASS; task58-profile.local.json; catalog unchanged |
| Solution + targeted harness builds | PASS, zero errors; final solution one NU1900 warning (NuGet vulnerability feed unavailable); no warning suppressed |
| dotnet test HotelOps.slnx --no-restore | Exit 0; solution contains API only, no discoverable test projects. Console SQL suites above are the executed tests |
| git diff --check / preservation | PASS: all 366 preexisting modified/untracked files retained, only intended edits/additions; index/HEAD/upstream/four worktrees and DEV2 unchanged; 55 local links valid. Receipt obj/task58-preservation.local.json; LF/CRLF warnings retained |

**DEV1_WRITER_COORDINATION remains PENDING**: E08 existing-account subset has executable
evidence, not approval for deferred business writers/external deployments. DEV2 integrated
and global Users/feature order gates remain PENDING pending [precise handoff](TASK58_DEV2_HANDOFF.md)
and reviewed complete applicable coverage. No gate closed, SA57 activated or runtime
responsibility requirement cleared. Full successful internal UC006/HTTP remains BLOCKED.

## 6. Files changed in Task58

Runtime: Services/Users/AccountWriterSessionGate.cs (new), GlobalAdminSerialization.cs,
UserAccountCreationService.cs, UserProfileService.cs. No Authentication-core/DEV2/generated
mapping source edit. Harness: UserAccountDeactivation/Task58WriterAcceptanceSqlSuite.cs (new),
UserAccountDeactivation/Program.cs, UserAccountCreation/Program.cs, UserProfileUpdate/Program.cs.
Documentation: this new acceptance file, new TASK58_DEV2_HANDOFF.md, GATE_RESOLUTION_MATRIX.md,
REMAINING_BLOCKERS.md, VERIFICATION_RESULTS.md. Nine new task58-*.local.json SQL receipts,
plus local backup/build/setup/validation logs under obj. All earlier source/artifacts retained.
