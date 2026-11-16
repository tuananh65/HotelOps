# C184 ? Effective-block boundaries in creation/cancellation

Baseline f013cd90eac1adcb6a8f306f5f90ceddd1d8019c. New completed UCs: 0; source covered 29/144, pending QA 2, historical COMPLETE_VERIFIED 27.

Fixed a real workflow mismatch: Create Cleaning treated every unreleased block as effective, including future-start blocks, and ignored blocks whose release timestamp is still in the future. It now uses BlockedAt <= now and (ReleasedAt is null or ReleasedAt > now), consistent with GBR-044/current readiness. The creation transaction obtains now from SQL server for deadline recheck, task timestamp and readiness evidence. Legacy cancellation eligibility now also ignores future-start blocks. Canonical restoration still delegates to effective readiness. Authorization, concurrency and lifecycle guards remain intact; no schema change.

Checks: backend build PASS (0 warnings/errors); solution test command exits 0; explicit creation/lifecycle/cancellation/readiness regression 58 PASS. Read-only SQL virtual-row boundary check: future start false, exact start true, exact release false, future release true, all matched expectations. This verifies the predicate, not full Create/Cancel API acceptance. No SQL mutation or frontend change this batch.

The existing cancellation HTTPS harness is pinned to the old FS018 trial database/fixture chain. It was inspected but not repointed or run against shared data. Recovery migrations/source proof are verified from C182/C183; actual current command/session/CSRF/delivery acceptance remains pending. No new COMPLETE_VERIFIED claim.

Changed files: src/HotelOps.Api/Services/Housekeeping/CleaningTaskCreationService.cs; CleaningCancellationService.cs; this receipt and HUMAN_QA_HANDOFF.md. Next: recovery-owned end-to-end command/delivery setup while continuing source gaps; do not fabricate legacy trial markers or relax authentication.
