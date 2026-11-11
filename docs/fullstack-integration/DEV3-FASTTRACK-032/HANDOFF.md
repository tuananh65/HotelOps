# DEV3-FASTTRACK-032 — UC139 scheduled completion and acceptance lane

Baseline integration: 00bdc481cb224d7117a79c95669abc7016f51ba4. Branch feature/dev3-uc139-completion032. Worktree D:\HotelOps\worktrees\dev3-fasttrack032. SRS source: docs/requirements/srs_sections/02_use_case_specifications.md:10012, UC139 normal steps 1–12, unscheduled/hand-over alternatives and open-interval exception; GBR014/007/008/138/140/142/143. Historical draft/change-note priority is preserved; no new permissions/statuses/schema or notification type invented.

## Whole-UC completion matrix

| Requirement | Exact source / method | Source status | Remaining acceptance |
|---|---|---|---|
| Scheduled Start from assigned shift, server UTC | WorkdayService.StartAsync; SqlWorkTimeTransactionParticipant.StartScheduledWorkdayAsync | CODED; claim consumption gap fixed | NEEDS_SQL_ACCEPTANCE |
| End with strong Workday ETag and no open interval | WorkdayService.EndAsync; participant.EndWorkdayAsync | CODED | NEEDS_SQL_ACCEPTANCE |
| One scheduled claim, including after End | ScheduledWorkdayClaims.Unclaimed, participant Start and service OwnShiftsAsync | IMPLEMENTED in this delta; same query gates read and locked writer | SQL repeated/racing Start acceptance |
| Review/confirm; cookie/CSRF; no replay; stale/session protection | ScheduledWorkdayPanel; scheduledWorkdayApi | CODED; production dispatch OFF | HTTPS/browser runtime pending |
| Server result and authoritative post-write reads | ScheduledWorkdayPanel.confirm/read effect | CODED; completion receipt added, cleared on session/branch replacement and 401 | Live refresh/identity smoke pending |
| Authorization own branch + shift/workday ownership | WorkdayService.Execute/Authorize and participant EligibleAsync | CODED; User-first locks, SQL session validation and independent SHIFT_VIEW/ATTENDANCE_RECORD preserved | Live denial/session races pending |
| Transaction + audit + rowversion concurrency | Execute Serializable; participant Start/End Audit; rowversion predicate | CODED; no commit inside participant | NEEDS_SQL_ACCEPTANCE; runner audit fault/Start race prepared |
| No assigned shift: reason + Department Head notification | WorkdayService.StartAsync null ShiftAssignmentId | NEEDS_EXTERNAL_CONTRACT + NEEDS_IMPLEMENTATION; 503 UNSCHEDULED_NOTIFICATION_UNAVAILABLE | Accepted atomic notification adapter/event/recipient/rollback contract |
| End with Pause All | WorkdayService.EndAsync(pauseAll=true) | NEEDS_EXTERNAL_CONTRACT + NEEDS_IMPLEMENTATION; 503 PAUSE_ALL_UNAVAILABLE | Complete source-writer inventory, state predicates, lock order, atomic pause/close audit |
| Suggest handover for open items | Existing open-interval warning; UC140 storage/workflow not approved | NEEDS_IMPLEMENTATION / EXTERNAL_CONTRACT | Must not create fake handover or automatic reassignment |
| Production home/router mounting and public mutation activation | WorkdaysController NonController; ScheduledWorkdayPanel no production caller | RELEASE_GATE, intentionally inactive | Separate trial/runtime/release approval, owner writer coverage and live acceptance |

## Real functional delta

Scheduled assignment history now excludes a claimed assignment from own-shifts and rejects a second claim under retained actor/time/assignment locks, even after its first workday has Ended. No history is deleted or assignment status rewritten. Conflict is HTTP409 TIME_ASSIGNMENT_ALREADY_RECORDED through the existing service mapping. No new schema/index was invented; correctness depends on the existing shared User-first writer protocol, which must be race-tested on approved SQL.

A confirmed Start/End response now leaves a short-lived in-memory result displaying actual server start/end and workday/assignment identity while authoritative snapshots refresh. Session/branch remount or 401 drops it. Unknown/malformed outcomes never create a confirmation. Task effort/payroll are explicitly not inferred from shift duration.

## Verification receipts

- dotnet build HotelOps.slnx: exit0; 0 warnings/errors.
- dotnet run --project scripts/verification/ScheduledWorkday/ScheduledWorkday.Verification.csproj: 51 PASS, exit0. Existing 16 executable guard/DTO checks + 27 explicitly labeled source assertions retained. New: 7 executed mutable-history query behavior cases + 1 EF SQL translation check. No SQL connection for these checks; they do NOT prove locks, service success, audit atomicity or SQL race behavior.
- WorkdayReadRouting verification: 44 PASS, exit0; MVC reads only, no Workday mutation activation.
- dotnet build scripts/verification/ScheduledWorkdayAcceptance/ScheduledWorkdayAcceptance.csproj: exit0; 0 warnings/errors.
- node scripts/verification/ScheduledWorkdayAcceptance/test-guards.mjs: 5 PASS, exit0. Offline prepare/default-deny/hash/lease/primary-DB guards only; no SQL/HTTPS connection.
- dotnet test HotelOps.slnx --no-restore: exit0; solution contains no automated test project, so this is NOT behavioral-test evidence. Focused console programs and Node tests provide actual counts above/below.
- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs tests/auth-http.test.mjs tests/auth-state.test.mjs tests/workday-router.test.mjs: 250/250 PASS, exit0, 0 fail/cancelled/skipped; 4 new browser cases. Existing calendar/End validation, exclusion, CSRF401/403/503, recovery, identity/branch races and router regressions retained.
- Frontend typecheck/build: exit0. Two inherited React Router use-client build warnings.
- Scoped C# whitespace format / Prettier / diff-check: exit0.

Development failures disclosed: first non-elevated dotnet restore could not read user NuGet.Config; authorized build succeeded. First frontend redirection failed before execution because .test-output directory did not exist; created and reran. Initial new EF translation assertion expected explicit [dbo] although model uses SQL default schema; corrected to actual mapped [workdays] and retained NOT EXISTS/identity-column checks. Initial verification exit -532462766; final 51 checks PASS. No test expectation changed to permit invalid business behavior.

## Executable acceptance handoff

scripts/verification/ScheduledWorkdayAcceptance contains Program.cs, csproj, receipt.template.json, README.md and offline test-guards.mjs. No default execution; --prepare opens no connection. --execute requires reviewed receipt + out-of-band hash, exact GUID/server, disposable name prefix, active exclusive lease, restricted SQL principal, artifact hashes, verified TLS, explicit trial mutation and actual audit fault-injection approval. No primary HotelOpsDB, trust bypass, migrations, provisioning, automatic retry or destructive cleanup. Uses three fresh dedicated assignments; refuses consumed fixtures on rerun. Details and exact commands are in its README.

Prepared live checks: SQL metadata/identity/principal; audit SaveChanges fault after Workday insert with rollback and separate-context reread; HTTPS login/session/CSRF; denied-branch403; Start/duplicate conflict; stale End412/invalid If-Match; End/current204; restart rejection; concurrent Start exactly one winner; audit pairs; logout/current401. Actual SQL/HTTPS: NOT_RUN. Runtime owner must approve a trial-only host exposing the still-inactive candidate without altering production. Atomic final-commit-failure, all external interval races and real browser runtime acceptance remain additional gates.

## Exact external contract request

DEV2 FE14 notification owner (with DEV1 FE04 producer convention): provide the accepted transaction-enlisted unscheduled-workday Department Head notification operation/event, exact recipient predicate, event identity/deduplication, failure semantics and same-DbContext audit/outbox rollback participation. DEV1 FE03/04/11/13 and DEV2 incident/maintenance writers: provide the authoritative open-interval source inventory and a same-transaction Pause All participant, per-kind pause/hold eligibility, assignment/version reread, User-first lock sequence and audit/close semantics using the SRS reason 'end of shift'. These are requested contracts, not implemented interfaces or approvals. No fake empty readers or cross-owner mutations added.

Runtime/DEV1 owner: supply accepted schema/fixtures + GUID/isolation proof, restricted SQL identity, trusted SQL/HTTPS origins, exclusive lease and explicit trial mutation/audit-fault approval per runner README. No shared environment access requested.

## Verdict

UC139: PARTIAL, scheduled path IMPLEMENTED_SOURCE_ONLY / ACCEPTANCE_RUNNER_PREPARED. SQL_ACCEPTANCE_READY: runner built and guard-tested; environment/lease NOT_READY, actual acceptance NOT_RUN. UC_COMPLETE_VERIFIED: NO; historical 18/31 credit unchanged. Next completion gate: approved disposable scheduled trial, then integrate accepted unscheduled/Pause All participants. NEXT_UC: UC138 notification/writer integration only when its accepted producer is supplied; do not recreate existing PR63 or blocked UC140 storage.