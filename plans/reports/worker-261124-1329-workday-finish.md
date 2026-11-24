# HK manual shift closure

## Implemented

- `ShiftExpiryService.cs`: exclude actual HK_STAFF shift templates from automatic expiry; non-HK unchanged.
- `WorkdayService.cs`: API active shift now exposes `canEnd` and `endBlockedReason`; same-operation-key start/end retries return existing state before stale-version validation; manual end uses actual current timestamp and returns supplies in the same transaction.
- `StaffSupplyService.cs`: EndBlocker checks all active employee task assignments (not only cleaning), open reservations and unfinished typed refill requests. Employee completion awaiting supervisor inspection/reinspection/verification does not block. End rechecks under supply lock. Added optional `allowFinishing` on ActiveShift/AllocationActive; default remains strict scheduled window for new reservation/refill/allocation.
- `frontend/src/compact/workday.tsx`: End only visible when backend canEnd; confirmation dialog, blocked explanation, updated HK manual closure description.
- `scripts/verification/p0-workday-finish-contract.mjs`: source contracts and isolated TypeScript transpilation syntax check.

## Integration required by parent (outside owned files)

1. **Mandatory:** in `StaffSupplyService.Reservations.cs`, inside `Consume` only, change `await AllocationActive(a,ct);` to `await AllocationActive(a,ct,allowFinishing:true);`. Keep Reserve strict. That file was outside assigned ownership.
2. WorkService's execution guard must allow already assigned HK tasks to start/resume/complete while their original HK workday remains WORKING after scheduled boundary. Keep CurrentCleaningShift for new assignments strict. No auto-extension/new assignment/new refill after boundary.
3. Recheck other UI End buttons/dashboard components: apply backend canEnd, not just local WORKING status.
4. Generic task assignment/end race must use consistent workday/supply locking. End checks are inside Atomic serializable and supply lock; parent should verify generic WorkService assignment path uses compatible transaction isolation.

## Validation

No C# build, server startup or SQL changes performed, per integration owner's request to avoid parallel builds. Parent must run required full backend build/test and runtime scenarios: overtime existing task, no new task/refill, pending refill blocking, post-inspection completion exception, return once and same-key retry. Static checks only, not runtime proof.

## Unresolved business questions

None. Pending implementation hooks above are explicit integration work, not approval blockers.
