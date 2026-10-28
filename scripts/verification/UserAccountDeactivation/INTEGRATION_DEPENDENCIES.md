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
