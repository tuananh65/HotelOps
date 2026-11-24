# Legacy cleaning reconciliation

Status: IMPLEMENTED source; runtime and C# build NOT_TESTED (integration owner controls builds and database writes).

## Contract

`POST /api/v2/work/{id}/reconcile-supplies`, existing ActionRequest body `{rowVersion, operationKey, reason}`.

- Only HOUSEKEEPING_SUPERVISOR with CLEANING_ASSIGN and a current working supervisor shift in the same branch.
- Only CLEANING in IN_PROGRESS/ON_HOLD with a recorded actual start and current room turnover.
- Active assignee must remain eligible HK_STAFF. Requires a real, open supply allocation linked to WORKING attendance.
- Reject any existing RESERVED/CONSUMED supply rows; supervisor cannot replace existing supply accounting with this endpoint.
- Reserve actual available personal supplies under the existing serializable transaction and supply application lock; shortage fails atomically. No fabricated stock, issue, consumption or history.
- Existing work may be reconciled after its employee's scheduled end while their attendance/allocation stays open. Supervisor must be on duty.
- Preserve assignment timestamps, accepted time, progress, actual start, due date and all work intervals. Audit records supervisor, reason, employee and assignment; touch aggregate row version.
- Same operation key/reason returns idempotently without double reservation.
- Existing execution attendance guard accepts the explicit reconciliation event plus reservation belonging to that current attendance. It does not generally remove the assignment-time guard.
- Unstarted legacy work uses existing `actions/assign`, even to the same person, with duration 10–30. This establishes a fresh assignment/attendance and reservation and keeps prior assignment history.

Detail now returns `capabilities.canReconcileCleaningSupplies`. UI integration still needed: supervisor drawer button “Đối soát vật tư công việc”, modal with reason, POST then refresh. New errors CLEANING_RECONCILIATION_NOT_ALLOWED / CLEANING_SUPPLIES_ALREADY_RECONCILED need Vietnamese mapping. No frontend files changed because other workers own UI.

## Files

- New Services/WorkService.CleaningReconciliation.cs.
- New Controllers/CleaningReconciliationController.cs.
- Services/WorkService.Cleaning.cs: scoped attendance exception after explicit recovery.
- Services/StaffSupplyService.Reservations.cs: internal allowFinishing parameter, default false; only recovery opts in.
- Services/WorkService.cs: detail capability only.
- New tests/HotelOps.Compact.Tests/CleaningReconciliationTests.cs (11 policy cases).
- New scripts/verification/p0-cleaning-reconciliation-scenario.mjs: actual API scenario accepting explicitly prepared synthetic legacy work/allocation IDs; verifies staff denial, preserved history, real reservation ledger and retry. Does not create SQL fixtures or alter dates.

## Validation

`node --check scripts/verification/p0-cleaning-reconciliation-scenario.mjs` passed. C# build, unit tests and runtime scenario not run. No DB writes, server/process changes or deploy performed. Integration owner must build and run tests, prepare legacy TEST fixture before migration, exercise insufficient personal balance, wrong branch/role, stale row version, two competing recovery calls, paused execution/resume, completion and final return.

## Remaining integration blocker

A WORKING attendance migrated from pre-P0 can have **no staff allocation at all**. This endpoint intentionally returns STAFF_SUPPLY_ALLOCATION_REQUIRED; it does not mint a balance or reinterpret historical stock. Existing WorkdayService start rejects WORKING (or returns its historical idempotent result), so simply retrying start does not hydrate this missing allocation. Integration owner must provide explicit audited current-shift initialization using actual stock before this recovery can work for that case. If already past shift end and no supplies, fulfillment is currently disallowed by the refill policy: do not silently waive stock/shift rules. This is a separate migration-path gap, not a reason to reset work/history or report full migration PASS.

No new business permission/schema was invented. No unresolved user question was raised; existing authorized migration scope permits an explicit real-stock recovery path, but that path still requires implementation/validation.
