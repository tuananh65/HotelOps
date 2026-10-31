# Task55A current - official ReturnWork hotfix integrated

On 2026-10-29, authorized `git fetch origin` confirmed origin/develop at official merge
4a187320643d0c82f232c6b9dd5a667b8401c280. HEAD 71a17f9 was an ancestor (0 local-only / 4
incoming commits); 33 incoming paths had no collision with 343 local modified/untracked
paths. `git merge --ff-only origin/develop` advanced the existing feature branch to that
exact commit. Incoming history also includes the official Inventory authorization merge;
no DEV2 source was manually edited, cherry-picked or separately committed.

Source review: ReturnWorkAsync now requests restoreAssigneeResponsibility. The shared
executor discovers assignee/status/linkage without tracking, begins its own transaction,
uses the existing UserEligibilityLock.RequireEligibleAsync before Incident -> Work, reloads
tracked Work/Incident after waiting, rejects assignee/status/linkage drift and mismatched
states, then performs the paired transition and atomic history/audit. The retained lock
reads current SQL eligibility, so a previously tracked ACTIVE User cannot bypass INACTIVE.

New `--returnwork` mode runs actual production ReturnWork and actual internal Deactivate
against the guarded patched disposable canonical trial. Final evidence:
`task55a-returnwork-accepted.local.json`, 57 checks including readiness and 23 scoped Return
checks plus target preservation. Its six DMV records include SPIDs, LCK_M waits and UTC times.

| Acceptance layer | Result | Evidence and limit |
|---|---|---|
| DEV2 standalone Return eligibility/transaction protocol | PASS | INACTIVE-first on a separate owned User, stale tracked state, four drift cases, cancellation, timeout1222, post-flush rollback, actual injected deadlock1205 and explicit fresh retry |
| DEV1 integrated ReturnWork-first | PASS, scoped | Actual writer commits, actual Deactivate waits, concrete review sees MAINTENANCE_ASSIGNEE; public result remains ResponsibilitiesUnresolved |
| Actual Deactivate-first denial/release | PASS, scoped | Actual Deactivate holds Users, Return waits before Incident/Work; Deactivate denies unresolved contracts, Return proceeds after rollback releases locks |
| Actual successful Deactivate-first -> INACTIVE -> Return rejection | BLOCKED | Business/model gates prevent actual success. Standalone direct INACTIVE fixture is explicitly NOT this acceptance |
| Full UC-006 / all DEV2 writer races | BLOCKED | Remaining contracts/models and global feature-first actor-read compatibility |

The obsolete missing-Return-writer gate MAINTENANCE_RETURN_WRITER_COORDINATION is now closed
in the DEV1 integrated wrapper after source and SQL evidence. DEV2_INTEGRATED_SQL_ACCEPTANCE
and GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE remain; no concrete responsibility predicate
or Unsupported check was bypassed. Clean readiness now reports 0 Blockers / 8 Unresolved /
2 Unsupported. Full writer-race acceptance is not inferred from this scoped hotfix test.

The original DEV2 shared runner has a fixed KIENPC/HotelOps_Canonical_DEV2_Regression guard;
it was reviewed, preserved and NOT RUN here. Its default HTTP runner targets HotelOpsDB and
was NOT RUN. The new DEV1 harness enforces its own disposable name/server/marker/schema guards
before SQL. All newly owned business fixtures are cleaned and full before/after snapshots
of existing Work/Incident/log/event/audit and RBAC catalog rows match in memory.

# Task52 historical dependencies and exact ReturnWork acceptance

HEAD remains 71a17f9; no fetch/merge/cherry-pick was performed in Task52. The local
MaintenanceWorkService.Workflow.cs ReturnWorkAsync still lacks prepareOwners and recreates
IN_PROGRESS responsibility from COMPLETED while retaining AssigneeId. Hotfix is PENDING;
no DEV2 source is changed or writer race certified by the new service fault tests.

After the DEV2 hotfix is merged through an explicitly authorized Git task, run the following
on a freshly guarded, patched canonical trial. Use distinct active actor/assignee, a synchronized
COMPLETED Work / RESOLVED Incident, valid corrective cycle metadata and no unrelated blockers.
Use a non-assignee actor and a valid ReturnMaintenanceWorkRequest reason. Snapshot Work,
Incident, logs, audits, User/version, sessions, roles and memberships before each case.

| Case | Exact barrier / action | Required assertion |
|---|---|---|
| Ineligible assignee | Completed fixture with inactive/deleted/ineligible-employment assignee; invoke actual ReturnWorkAsync | Validation rejection; no work/incident/log/audit mutation, no responsibility recreated |
| Eligible assignee | Active/nondeleted/employment-active assignee | Actual ReturnWork returns IN_PROGRESS, retaining only the correctly locked assignee; atomic incident/log/audit |
| Writer first | Pause actual ReturnWork at TransactionCommitting AFTER its User + feature locks; start real Deactivate | DMV must show Deactivate waiter blocked by writer SPID; release commit; concrete integrated review must contain MAINTENANCE_ASSIGNEE and reject without account/session/audit mutation |
| Deactivate first | Pause actual successful Deactivate before commit; start ReturnWork with a deliberately pretracked stale active User | DMV must show writer waiting on Deactivate SPID; release commit; writer rereads inactive assignee and rejects with unchanged completed work/incident/log/audit |
| Discovery drift | Pause ReturnWork before User acquisition; competing approved assignment/cycle change commits | Authoritative reread rejects stale participants/cycle or restarts whole transaction; no late User lock after Incident/Work |
| Faults | Cancel waiter, expire lock timeout, inject work/audit failure, force deadlock | Whole caller transaction rolls back, retained locks release; explicit fresh retry rereads eligibility/version and never replays partial audit |

The Deactivate-first success test is BLOCKED until all other DEV1/DEV2 responsibility and
lock-order gates close. Do not simulate it with a direct account-status UPDATE or inject a
clear responsibility evaluator. Writer-first may currently return ResponsibilitiesUnresolved;
inspect the concrete review for the known Maintenance blocker as well, without declaring
the complete acceptance gate passed. Employment fixture values must come from approved schema,
not invented status vocabulary. Record SPIDs/wait_type/DMV time and separate operation outcomes.

# Task51 historical integration status

DEV2 commit `71a17f98b8c8e1dc0cfa54a12ca595e4e44a800d` is origin/develop HEAD and
the current feature HEAD after an authorized fast-forward. Before sync: 0 local-only /
2 develop-only commits. No local-path collision. All 291 pre-existing untracked artifacts
(including Task46 helper) match SHA-256 in `obj/task51-preflight-hashes.local.json`.

Source audit, independent of handoff:

| Path | Finding |
|---|---|
| Maintenance AssignWork | Eligible proposed User before Incident/Work; reload after wait |
| Maintenance ReturnWork | **FAIL**: COMPLETED -> IN_PROGRESS retains old AssigneeId; no prepareOwners/eligibility check |
| Warehouse SaveWarehouse | Discover old manager, lock sorted old/new Users before Catalog, revalidate manager snapshot |
| Asset CreateHandover / Accept | Receiver / sorted source+receiver locks before Asset/Handover; eligibility and discovery reread |
| MR Save / submit / request-information | Sorted actor/requester/selected approver before Catalog/Request; current revision is authoritative; drift rejects |
| Feature-first actor reads | Maintenance shared executor, Asset Reject/Retire and Inventory Actor can read Users after resources; global Users TABLOCKX compatibility NOT certified |

No DEV2 workflow was modified by Task51. Merge does not close writer coverage. The protected
Task46 helper is reused unchanged by the new concrete wrapper, with explicit unresolved gates.
Historical dependency notes follow.

# Integration dependencies — inspected 2026-10-29

Task48: remote develop verified directly and matches local ref; see
DEVELOP_INTEGRATION_PLAN.md. The preflight details below are historical Task46 data.
Full deactivation readiness remains separate from the internal DEV1 package readiness.

Branch: feature/BE1-database-integration. HEAD:
6278a8807bb72ff72c16f8173748ff48f5260794. Upstream:
origin/feature/BE1-database-integration. No fetch performed; remote freshness unknown.
Preflight had no tracked edits and numerous pre-existing untracked verification artifacts;
all preserved. Other worktrees were listed only, not read or changed.

DEV2 coordination is NOT integrated in this source:

- MaintenanceWorkService.Commands.cs: AssignWorkAsync calls RequireUserAsync inside
  ExecuteCorrectiveAsync after Incident and MaintenanceWork locks.
- MaterialRequestService.cs: Participant reads Users AsNoTracking without retained
  UPDLOCK/HOLDLOCK; Locked takes feature gates/resource locks.
- InventoryService.Masters.cs: SaveWarehouse checks ordinary Users.AnyAsync before
  persisting ManagerUserId. This is not a transaction-retained eligibility lock.
- AssetService.Handover.cs still mutates CustodianUserId through its existing workflow;
  no integrated common users-first protocol was found.

These concrete missing participants suffice to fail the activation gate. A DEV2 report
or code in another worktree cannot substitute for integrated source/race evidence.

Existing UserAccountStatusAccess provides approved ALL/BRANCH authority composition;
GlobalAdminSerialization provides the global transaction lock/poststate guard.
Neither independently proves deactivation safety. Older Task40 LastAdmin blockers
must be read with Tasks41-43 implementation; those historical blockers alone are not
the current conclusion. The missing piece is integration into a safe Deactivate workflow.
