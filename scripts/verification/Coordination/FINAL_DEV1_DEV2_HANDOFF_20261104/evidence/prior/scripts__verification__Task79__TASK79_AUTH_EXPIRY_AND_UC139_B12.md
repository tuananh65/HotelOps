# Task79 — Authentication expiry fixed; UC139 B1.2 contract gates

Date:04/11/2026. DEV1, feature/BE1-database-integration.
**P0 implemented/build/live regression verified. B1.2 not implemented; UC139 BLOCKED_EXTERNAL.**

## Final closure after DEV2 coordination confirmation

**Task79 closed: P0 fix retained; UC139 B1.2 remains BLOCKED_EXTERNAL.** User confirms
DEV2 branch `feature/DEV2-coordination-contracts`, checkpoint worktree
`C:\Users\Admin\Documents\GitHub\HotelOps_DEV2_Coordination`, HEAD
`4a187320643d0c82f232c6b9dd5a667b8401c280`. Draft path:
`scripts/verification/Coordination/NOTIFICATION_DELIVERY_RETRY_CONTRACT_DRAFT.md`,
blob `dc5ceb10121f752351e56e4b7150277c96271e1c`.

Status is **DRAFT_READY_FOR_JOINT_REVIEW**, not joint-approved. DEV2 has not implemented
publisher, durable outbox, replay adapter or worker; no implementation commit is confirmed.
No additional search in cf8fb02, fetch, checkout or cherry-pick is needed/performed.
The supplied external path and blob are unavailable locally; draft content was not read.
Metadata/status is user-confirmed evidence, not a claim of technical draft review.

[B12_APPROVAL_SCOPE.md](B12_APPROVAL_SCOPE.md) now records exact proposed interfaces,
dependency ownership and handoff/resumption criteria. Resume only when DEV2 supplies the
approved publisher/replay contract, actual source implementation and acceptance evidence;
maintenance Pause All requires its approved same-transaction participant. Rework transition
and shared DI scope still need specific review. No fake adapter, separate queue, duplicate
DEV2 code, shared schema/RBAC or DEV2 source changes were introduced.

This closure changes documentation/ledger metadata only; P0 and B1.1 source and acceptance
receipts remain intact. Build/SQL/HTTPS suites were not rerun for documentation-only closure.
`git diff --check` and preservation/hash checks recorded in
[TASK79_CLOSURE_VALIDATION.json](TASK79_CLOSURE_VALIDATION.json). Original implementation
elapsed13m36 and [TASK79_VALIDATION.json](TASK79_VALIDATION.json) remain historical receipts.

## 1. Root cause and precise scope

Actual chain: CredentialLockoutService reads SQL `users.temporary_password_expires_at`;
SQL Server datetime2(7) stores UTC-by-contract and EF materializes DateTimeKind.Unspecified.
SqlSessionService.CreateSessionAsync calls SqlSessionPolicy.AbsoluteDeadline with UTC now
and that SQL expiry. The policy correctly chooses min(created+15 minutes,temporary expiry)
but originally returned the selected DateTime's Kind unchanged. For a short credential,
the result was Unspecified. HotelOpsCookieTicket.Create constructs DateTimeOffset from it,
which interprets Unspecified as the machine's local UTC+7 and shifts ticket expiry backwards.
CookieAuthenticationHandler then rejects the ticket as expired before SQL principal validation.

This is Kind metadata loss, not an incorrect min, double UTC conversion, SQL tick loss or
intended expiry. Temporary credential validity is separate from idle/absolute session expiry:
restricted absolute lifetime remains min(15 minutes,credential deadline); normal remains12h
absolute/30m idle. Touch and cookie must not extend either cap.

One production file changed: `src/HotelOps.Api/Services/Authentication/SqlSessionPolicy.cs`,
AbsoluteDeadline return is wrapped in DateTime.SpecifyKind(...,Utc). The selected ticks and
all constants are unchanged; no ToUniversalTime on UTC-by-contract SQL values. This also
normalizes the selected created+cap when validation receives SQL-created Unspecified values.
Exact applied diff: [AuthExpiry.fix.patch](AuthExpiry.fix.patch).

Task79 explicitly authorizes DEV1-owned authentication code. This is the FE01 service policy
fix within that ownership, not a change to protected Cookie infrastructure, authentication
middleware/config, CurrentUserAccessor, SQL mapping, lifetime policy or role permissions.
No such additional shared changes were needed/applied. B1.2 shared-contract/DI changes remain
unapproved and unapplied.

## 2. Reproduction before and after

All live work used exactly the already approved disposable
`HotelOps_Canonical_20261104_063549_81d4bc64_Fresh`, validated DB name + A1 package marker.
Unique-prefix disposable authentication fixtures only; no existing user credential overwritten,
no new DDL/role grants or DB cleanup. Host = actual API DLL/Program.cs, real loopback TLS,
production DP-protected cookie, SQL sessions, production CSRF/auth controllers and middleware.

| Evidence | Actual execution |
|---|---|
| Before deterministic |21 minimum/lifetime assertions,20 UTC/UTC+7 timezone-model cases;5 observed cookie-skew cases. Valid minimum ticks unchanged but Kind lost |
| Before live |5 reproduction checks: actual SQL Kind Unspecified, production startup, CSRF/login200, valid10-minute credential cookie round trip401 on UTC+7 |
| After deterministic |**53 PASS**: same20 timezone-model cases with zero skew; min/lifetime preserved, plus32 exact policy assertions for one tick before/at/after cap and Touch never extending cap, for UTC and SQL-Unspecified inputs |
| After live |**43 PASS** production HTTPS/SQL: normal/longer-than15/exact15 fixture/short temporary login+session, restricted403 enforcement, persisted SQL caps, logout/revoke, expired403/no session, Change Password/session rotation/revoke/clear-temp,8-second near-expiry valid then401 after cap and403 on expired credential re-login |

Real wall-clock login occurs after fixture creation; the live exact15-minute fixture cannot
claim an exactly frozen15-minute boundary. Exact equality/one-tick boundaries are deterministic
policy tests. UTC and UTC+07 were tested through explicit deterministic timezone models;
actual HTTPS OS environment is **SE Asia Standard Time (UTC+7)**. **Separate UTC OS acceptance
NOT RUN**; no OS timezone/config change or fictitious second environment is claimed.

Receipts: [before live](auth-expiry-before.local.json), [after live](auth-expiry-after.local.json),
[before matrix](boundaries-before.local.json), [final policy matrix](boundaries-final.local.json).
Intermediate after matrix21 retained; do not add21+53 as separate final policy coverage.
These local receipts are gitignored evidence, not committed artifacts.

## 3. Regression/build and execution limits

- **26 User credential SQL transaction checks PASS** using existing UserAccountCreation runner,
  rebuilt against fixed API. Actual SQL credential/audit/concurrency; authorization/session
  collaborators of this runner remain doubles, not additional full User endpoint acceptance.
- **199 security PASS**:93 authorization/production fallback,43 PermissionService,14 current-user,
 49 resource authorization. Existing isolated collaborators; not199 live SQL business cases.
- `dotnet build HotelOps.slnx` PASS,0 errors,2 NU1900 warnings. Initial verification restore builds
  have4 NU1900 entries; final reference-only builds2. NuGet vulnerability feed unavailable.
- `dotnet test HotelOps.slnx --no-restore` exit0/no framework cases: NOT AVAILABLE, not tests PASS.
- `git diff --check` PASS. No new framework test project or production layer; local verification
  projects live under ignored obj and compile the reviewable verification source files here.

One post-fix boundary probe was started too early, before the API build completed, and copied
the stale API assembly; it failed UTC Kind assertion. Log kept at
`obj/task79-boundaries-after-stale-assembly.local.log`. After waiting for API build completion,
the probe was rebuilt/rerun with current assembly; final53 PASS. This failed execution is not
reported PASS and no production code change was made to weaken an assertion.

Build/runtime logs: `obj/task79-build.local.log`, `task79-live-before.local.log`,
`task79-live-after.local.log`, `task79-{before,after}-production.{stdout,stderr}.local.log`,
`task79-boundaries-{before,after,final}.local.log`, `task79-user-sql-regression.local.log`,
`task79-{authorization,permission,current-user,resource}-regression.local.log`.

## 4. UC139 B1.2 — actual investigation and exact gate

Read current A1/B1.1 source/report/receipts, current ledger, SRS UC139/GBR136–143,
branch/DH coordination doc and actual Cleaning/Rework/Task entities/history. Read Task73
notification handoff directly from cf8fb02 without bulk restoration.

The requested **INotificationPublisher and joint notification contract were not found** in
the inspected working tree, available local/remote-tracking refs/history, two rehearsal
worktrees, AI-Loop src/docs or known snapshot cf8fb02. That does not prove they do not exist
in DEV2's external/unfetched work. No fetch/checkouts or assumed interface implementation.
A text question requested its exact branch/commit/path while P0 continued independently.
That question is now answered by the confirmed draft-only checkpoint in the closure above.

Concrete bounded review: [B12_APPROVAL_SCOPE.md](B12_APPROVAL_SCOPE.md). It contains exact
feature files/interfaces/signatures, unchanged existing permission bindings, DEV1/DEV2 ownership,
proposed event and payload, unique branch-DH relationship, transaction/User lock order,
durable replay prerequisite, source pause participant contract and required acceptance.

Specific unresolved gates:

1. Actual DEV2 publisher signature/DI/event registry and same-transaction durable intent API.
   Do not create a fake INotificationPublisher, direct notifications insert or independent queue.
2. Approve explicit single active branch DEPT_MANAGER relationship for recipient selection;
   no broadcast, guessed ID, Department.ManagerUserId shortcut or global-role fallback.
3. Durable operation->Workday/result mapping and replay retention. A1 has no operation-key store;
   event dedup alone cannot distinguish retry after prior Workday Ended from a new Start.
   No unapproved schema or audit-log-as-operation-store workaround applied.
4. Rework pause status/history/permission contract and DEV2 maintenance/PM same-transaction
   participant. Cleaning ON_HOLD and Task PAUSED exist in SRS, but current tree lacks reusable
   full source pause participants. Unsupported kinds must remain fail closed.

A1 CloseInterval validates source IN_PROGRESS; the proposed adapter holds source locks,
closes via A1 while source remains valid, applies approved pause/history/audit, then existing
End in the same transaction. No close-only shortcut, silent source status update or separate
maintenance time model. DH candidate must be re-resolved after sorted actor+recipient locks;
candidate changes require rollback/retry, not lock-set extension out of order.

## 5. Implementation and handoff status

Only the P0 policy fix and verification/report/ledger artifacts implemented. No B1.2 production
contract or mutation is active. B1.1's existing503 branches for Unscheduled/Pause All remain.
Existing scheduled API and its159 Task78 checks are historical evidence, not a new159 rerun.
Notification inbox delivery/retry/replay and source Pause All/rollback acceptance: **NOT RUN /
BLOCKED** until contracts supplied/approved. Do not mark any supported-kind subset as full
Pause All or full UC139. Local DEV2 handoff is in the B1.2 scope; no messages sent automatically.

## 6. Files, progress, elapsed time and next action

Production modification: SqlSessionPolicy.cs only. Added AuthExpiryBoundaries.cs, AuthExpiryLive.cs,
AuthExpiry.fix.patch, B12_APPROVAL_SCOPE.md, this report, UC_LEDGER_TASK79.json and validation
receipt. All other existing source/patches, Program.cs, HEAD/index/four-worktree registrations
and heads preserved; no DEV2/deactivation edits, shared schema/RBAC changes, commits or pushes.

[UC_LEDGER_TASK79.json](UC_LEDGER_TASK79.json): **26/75 UC;60/256 points=23.4375%; delta0**.
UC111/139 remain BLOCKED_EXTERNAL; UC138 NOT_IMPLEMENTED. Authentication fix improves existing
completed UCs without awarding a new UC. All75 classifications/weights/points preserved.

Elapsed and exact hash/preservation evidence: [TASK79_VALIDATION.json](TASK79_VALIDATION.json),
measured from saved preflight to final validation (not a fabricated estimate).
Next action: wait for approved publisher/replay contract, actual implementation and acceptance
evidence from DEV2, as specified in the final closure and dependency handoff above.
Only after those decisions can event adapter/source participants and exact DI activation diff
be finalized and tested. P0 does not wait on DEV2 and is already fixed/verified.
