# Verification — 2026-10-29

## Task48 — package verification

| Gate | Result |
|---|---|
| dotnet build HotelOps.slnx | PASS, zero warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit0; no discovered tests; console suites below supply actual coverage |
| Parser/session policy | PASS 26 assertions |
| Cleaning ownership subset / SQL translation | PASS 34 assertions |
| Approved DEV1 SQL predicates | PASS 30 assertions; task48-predicates.local.json |
| LastAdmin SQL foundation | PASS 26; task48-lastadmin.local.json |
| Authorization Foundation | PASS 61 synthetic-cookie/loopback checks; no --live/HotelOpsDB access |
| UC004 + Profile/List/Detail + Startup | PASS 546 on clean retry; task48-uc004-profile-startup-retry.local.json |
| StartupSchemaGate dedicated | PASS 377; task48-startup.local.json |
| UC006 Activate | PASS 169; task48-uc006.local.json |
| UC007 Add Branch | PASS 183; task48-uc007.local.json |
| UC010 Assign Role | PASS 226 on isolated trial; task48-uc010-isolated.local.json |
| UC005 reauthentication/reset | PASS 165; task48-uc005.local.json |
| Existing schema preparation | PASS 26 disposable fresh/upgrade patch checks |
| Exact manifest / whitespace | PASS, 19 source/docs files; staged check required before commit |

Failures retained and resolved without weakening assertions:

- First UC004/Profile/Startup attempt timed out in its 45-second negative-startup
  child-process probe. The dedicated startup suite passed; a new clean trial retry
  passed all 546 checks. No startup/API source or timeout assertion was changed.
- First UC010 attempt used the same trial after UC006/007, whose retained fixture
  administrators violated the isolated-single-admin scenario. A newly created dedicated
  trial passed all 226 checks; no old fixture was deleted and no assertion was changed.
- Initial negative-trial preparation had an invalid PowerShell-composed name; the
  existing script rejected it before SQL access. Corrected names created new trials.

Harness builds passed. Several report NU1900 because NuGet vulnerability metadata is
unreachable; package vulnerability audit is not certified. Solution build itself has
zero warnings. Git reports configured LF-to-CRLF normalization; whitespace checks pass.

All actual SQL runs used guarded canonical trial databases (server/name/Task17 ownership
verified). Trials and non-sensitive receipts are retained outside the commit. Fault
catalog/schema fixtures exist only inside disposable verification trials; shared SQL
schema/RBAC sources and HotelOpsDB were not changed/accessed. No production data test.

Current package excludes Task46 DeactivationResponsibilityQueries.cs because its actual
DEV2 predicate fixture coverage is NOT RUN. No packaged source calls it. Full Deactivate
mutation, coordinated assignment races, runtime load/plan testing and endpoint readiness
remain BLOCKED; passing existing workflow regressions does not close those gates.

The following Task46/47 sections retain development history; current package scope is
FILE_MANIFEST.txt, current contract is V1_BUSINESS_CONTRACT.md, and integration plan is
DEVELOP_INTEGRATION_PLAN.md. No PR/push/merge is performed.

## Task47 — current results

Post-approval run: PASS 30 SQL predicate assertions (supersedes the initial 17-case
count below), plus the same 26 parser/session and 34 cleaning-subset assertions.
Evidence: task47-approved-predicates.local.json. New source:
Final rerun after missing-parent LEFT JOIN change also PASS (30 SQL cases):
task47-approved-final.local.json. Final solution build PASS, 0 warnings/errors;
verification build PASS with existing NU1900 warning. Whitespace checks PASS.
Services/Users/ApprovedDev1ResponsibilityQueries.cs; expanded Dev1PredicateSqlSuite.cs.
Tests add approved PENDING cleaning, active-ended inconsistency, ambiguous terminal
mapping, designated rework/inspection/defect PENDING/IN_PROGRESS, completed history,
active/inactive department manager, unassigned defect unrelated to historical inspector,
unrelated user, unresolved modules remaining, and query failure propagation.
All fixtures rollback in the guarded disposable trial. No new LastAdmin integration
path changed; preceding 26-case LastAdmin foundation result remains applicable.
Missing-parent/unknown-state legacy handling is implemented conservatively; no SQL
fixture disables FK/CHECK constraints to manufacture invalid production schema.

| Check | Result / evidence |
|---|---|
| Solution build | PASS, zero warnings/errors |
| Verification project build | PASS after correcting a new harness compile error: ReferenceValue key is ReferenceId, not ReferenceValueId. NU1900 warning remains (NuGet vulnerability service unreachable). |
| dotnet test HotelOps.slnx --no-restore | Exit0; no test discovery; NOT test coverage |
| Parser/session-policy | PASS 26 assertions |
| DEV1 known cleaning predicate | PASS 34 in-memory/provider-translation assertions, including comparison with existing resolver |
| Actual SQL predicate | PASS 17 assertions; task47-predicates.local.json |
| LastAdmin foundation | PASS 26 actual SQL assertions; task47-lastadmin.local.json |
| Startup compatibility | PASS in LastAdmin guarded trial preflight; dedicated negative StartupSchemaGate suite NOT RUN |
| Deactivation/assignment races | BLOCKED: no integrated mutation/assignment writer, no complete business contract |
| UC004/005/Activate/007/010 HTTP/SQL regressions | NOT RUN; existing reachable workflows unchanged, only new unregistered helper and opt-in verification mode added |
| git diff --check / new-file whitespace | PASS; Git warns LF will normalize to CRLF |

Trial: HotelOps_Canonical_20261028_173107_1eb02a2d_Fresh, created with existing guarded
Prepare-Database.ps1 and existing guarded rowversion-forward.sql. Host UTC supplies the
trial identifier, not the user-facing date. Actual server/name/Task17 ownership verified
before fixtures. Trial retained; no HotelOpsDB access. Initial sandbox SQL attempts failed
with SSPI context error; outside-sandbox execution succeeded. No credential change made.

SQL cases cover current execution in another primary branch, other user, terminal history,
future/exact assignment time, parent states, inactive room, no tracking, cancellation,
injected SELECT failure and caller transaction ownership/rollback. All Task47 predicate
fixtures rolled back. Fault uses a test interceptor; no production failure handling changed.
No ambiguous duplicate ACTIVE SQL fixture inserted: release schema enforces filtered
unique index. In-memory ambiguity remains tested for legacy/schema drift.

LastAdmin foundation independently collected actual lock wait, grant-revoker blocking,
serialization/count visibility, concurrent capability loss, last-admin status rollback,
audit FK rollback, timeout, cancellation, retry and deadlock-victim rollback evidence.
These are existing guard tests, NOT evidence that Deactivate writers are integrated.
No new complete current-responsibility eligibility decision is certified.

Reproduce after creating a NEW guarded, patched trial (never HotelOpsDB):

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet build scripts/verification/UserAccountStatus/UserAccountStatus.Verification.csproj
dotnet scripts/verification/UserAccountStatus/bin/Debug/net10.0/UserAccountStatus.Verification.dll --dev1-predicates
dotnet scripts/verification/UserAccountStatus/bin/Debug/net10.0/UserAccountStatus.Verification.dll --dev1-predicates <owned-trial> <new-predicate-receipt>
dotnet build scripts/verification/LastAdminProtection/LastAdminProtection.Verification.csproj
dotnet scripts/verification/LastAdminProtection/bin/Debug/net10.0/LastAdminProtection.Verification.dll <owned-trial> <new-lastadmin-receipt>
```

Task47 source changes: new Dev1DeactivationPredicates.cs; new Dev1PredicateSuite.cs
and Dev1PredicateSqlSuite.cs in the existing UserAccountStatus verification project;
Program.cs adds an explicit opt-in mode, preserving old activation arguments. No new
project, route, DI registration, generated entity, schema source or DEV2 edit.
Documentation: new DEV1_PREDICATE_DECISIONS.md, DEV1_WRITER_LOCK_MATRIX.md,
DEV2_INTEGRATION_CHECKLIST.md; updated README, REMAINING_BLOCKERS and this file.
Two new .local.json files are local evidence, not source deliverables.

## Task46 — historical results (not Task47 limitations)

- Solution build: PASS, zero warnings, zero errors.
- dotnet test HotelOps.slnx --no-restore: exit 0, no test discovery/output. Solution
  contains only the API project. This is NOT test coverage.
- New helper: compiled against existing entities/mappings. No SQL execution or
  predicate fixture/race verification performed. SQL translation/runtime unverified.
- UserAccountStatus console harness: PASS, 26 parser/session-policy assertions;
  no SQL arguments supplied. NU1900 warning: NuGet vulnerability index unreachable.
- git diff --check: PASS for tracked changes (none). New Task46 files checked
  separately with git diff --no-index --check against NUL.
- HTTP Deactivate: not registered; no successful mutation path added.
- No connection to HotelOpsDB or any other database made in this task.
- Required real-SQL cases (all blockers, historical exclusions, session/audit atomicity,
  writer-first/deactivate-first, revoked authority, concurrent admins, cancellation,
  deadlock/timeout/retry and lock-manager evidence): NOT RUN; integration gate open.
- UC004 Create, UC005 Profile/Reset, UC006 Activate SQL/HTTP, UC007 Add Branch,
  UC010 Assign Role, LastAdmin, Authorization Foundation and Startup Schema Gate
  SQL/HTTP regressions: NOT RUN. Prior receipts are not counted as this task's results.

No production-readiness claim. Remaining blockers are listed separately.
