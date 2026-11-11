# DEV3-FASTTRACK-036 — UC006/UC010 responsibility integration

Status: IMPLEMENTED_SOURCE_ONLY / acceptance pending. Neither UC is COMPLETE_VERIFIED.

## Provenance and scope

- Fresh integration: `6455c6c6c5de20e1dca302e47f05aa625cf30bdc` (PR25 merge).
- Existing PR47 head: `1532addea2790001c8225a9da44b810294c8ebc5`.
- Isolated worktree: `D:/HotelOps/worktrees/dev3-fasttrack036`.
- PR47 candidate merged without conflicts; its original eight files are unchanged.
- Publish this increment on the existing PR47 branch, not a duplicate candidate PR.
- New increment: four Users service files, three verification files, this handoff (8 paths).
- Complete PR delta: original 8 candidate files plus these 8 paths, 16 unique paths.

## New executable business logic

Both existing mutation services now use a concrete transaction-bound responsibility
coordinator. It retains the existing DEV1/DEV2 predicates and consumes PR25's global
DEV2 snapshot, including inactive branches and unscoped responsibilities. It does
not infer clearance from a branch-only read or an empty response.

BLOCKING and TRANSFER_REQUIRED facts deny mutation. Historical actor records alone
do not establish current ownership. Unknown classification, missing coverage,
unscoped facts, reader authorization denial and stale snapshots remain unresolved;
unresolved takes precedence over known blockers. SQL errors and cancellation propagate
to transaction disposal, never returning clearance or retrying a mutation.

The exact DEV2 global fingerprint/target/transaction lease is revalidated immediately
before writing. Final reread occurs before commit. The final responsibility digest
excludes only TARGET_USER context, because the owner itself updates its status/version;
branch coverage, branch state, all responsibility facts and policy gaps remain covered.
This avoids mistaking the owner's rowversion update for external responsibility drift.

UC006 adds a pre-write session/authority gate and final responsibility reread. It also
rejects restricted protected tickets and invalid session IDs before SQL. Its additive
optional concrete Dev2DependencyReader constructor dependency is supplied by existing
DI resolution when composed; legacy/manual callers omitting it fail closed, never clear.
No production service/controller registration is added.

UC010 composes the existing DEV2 reader from its current authority dependencies, and
uses it at initial, pre-write and final boundaries. Conditional User version/assignment
updates, retained global-first/User locks, last-admin poststate, session revocation,
audit and rollback structure remain intact. No writer participation is certified here.

## Reused dependencies and authority boundary

- Dev2DependencyReader.ReadUserGlobalInCallerTransactionAsync and matching revalidation.
- PR25 DependencySnapshot/GlobalUserDependencySnapshot and their exact classifications.
- IntegratedDeactivationResponsibilityQueries and existing DEV1/DEV2 state predicates.
- Existing UserEligibilityLock, global-admin serialization, session revocation and audit.
- DEV2 global reader independently requires global MASTER_DATA_MANAGE plus target
  CanManageAsync. USER_MANAGE/ROLE_MANAGE do not substitute for that reader authority.
  A denied reader produces unresolved coverage without exposing facts; no permission
  catalog/policy is changed or granted.

## Executed verification

| Command / scope | Actual result | Exit |
| --- | --- | --- |
| dotnet build HotelOps.slnx --no-restore --disable-build-servers | PASS, 0 warnings/errors | 0 |
| dotnet test HotelOps.slnx --no-restore --disable-build-servers | No framework test cases discovered; not behavioral evidence | 0 |
| UserAccountDeactivation verification, no arguments | 16/16 PASS, SQL NOT_RUN | 0 |
| UserDeactivationCandidate verification | 20/20 PASS | 0 |
| UserRoleRevocation verification | 84/84 PASS, including 30 new classification/fingerprint/transaction guard checks | 0 |
| HotelOps.Dev3Unblock.NoSql, no arguments | 92/92 PASS, zero SQL connections | 0 |
| UC006 API/browser + UC010 revoke API/browser + auth-http/auth-state | 178/178 PASS, zero failed/cancelled/skipped | 0 |
| npm run typecheck / npm run build | PASS; 2 inherited React Router use-client warnings | 0 / 0 |
| Scoped C# whitespace checks (4 production files + new behavioral checks) | PASS | 0 |
| Scoped Prettier on 4 original UC006 frontend files + RevokeUserRoleCandidate.tsx | Existing style warnings on all 5 unchanged files; not corrected in this business increment | 1 |

No SQL/HTTPS live test, successful mutation, SQL lock/race, audit fault injection,
last-admin concurrency or rollback acceptance was performed. Pure snapshot tests and
source assertions do not prove SQL transaction behavior. SQL_ADAPTER_ACCEPTANCE: NOT_RUN.

## Integrity and release blockers

Only DEV3 Users services/tests and handoff change. The original PR47 candidate files
retain exact Git identity. All other frontend, backend controllers, Program.cs,
DbContext/entities, DEV1/DEV2 dependency readers and writers remain integration-identical.
The existing immutable UC006/UC010 release gate lists are unchanged. Both candidate
controllers remain NonController, frontend candidates remain unmounted/dispatch OFF.
FE12 remains HOLD, WorkdaysController NonController and Workday mutations OFF.

Still required: complete DEV1 OperationalTask/Internal Request/Shift/Workday/line-manager
predicates; DEV2 receipt/replenishment/PM/notification responsibility policies; complete
writer inventory/participation; global-first lock-order acceptance; successful final
transaction, session revocation, atomic audit and last-admin poststate acceptance;
an approved disposable SQL/HTTPS identity, lease and both race orders/fault rollback.
Reader integration or zero responsibility counts cannot close these gates.

## Source SHA-256 (Git-normalized LF bytes)

| File | SHA-256 |
| --- | --- |
| UserAccountDeactivationService.cs | C9E43F0F05A44EF8CEEA0CE2BC57F5BE7E9F1CD9221701883BF33C4C622B369C |
| UserRoleRevocationService.cs | 648A4EA34E11B2DD5458B4833BE27983BF2B7B5FB88ABF954D62F45407BA97B3 |
| TransactionalUserResponsibilities.cs | CF5FE47833E28CF4144C8A7A9E3D188C3E677519B80123D20623DF88873D5929 |
| UserResponsibilitySafety.cs | F25BAF6374B59D4650C31EA532BB1B12B79C0EA92E8A2DDF00A04C090F704FE1 |
| UserAccountDeactivation/Program.cs | B1E70A766861C107B417F23D6297E2680B0168B56C875ACE43BC1274AAC50019 |
| UserRoleRevocation/Program.cs | 51531207BAD33F1BEF1CE5E762F31C1737CC290AEC22CD77A95E9084A283809A |
| ResponsibilityIntegrationChecks.cs | 9421F9F12EECB3296137378ED38ECD684D4662CB7DC2DA8D1568066B4B2D0B2F |
