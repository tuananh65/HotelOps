# UC132 — KPI-05 processing cycle decision

Decision owner: DEV3. Approved explicitly in the current FASTTRACK-142A conversation, 2026-11-16. This is a clarification of the SRS formula for multiple processing cycles, not a claim that the original SRS specified these details.

- A cycle starts at its first Start and ends at the final Submit subsequently accepted by the verifier. Return/resubmit stays in the same cycle.
- Sum elapsed cycle durations per task. Divide by the number of current Closed tasks whose latest completion falls in the selected UTC period, once per task.
- Exclude waiting after that accepted Submit and the gap between Closed and Reopen. Pause and earlier returned submissions remain inside elapsed cycle duration.
- KPI-06 continues to sum closed effective work intervals in the selected period, excluding Pause. It is not the KPI-05 formula.
- Do not overwrite historical `ActualStartAt`. Use immutable submission/verification/reopen receipts, the Reopen interval cutoff, original interval opening timestamps and the Start audit referencing the first interval.
- Missing capability, unresolved completion lifecycle, invalid provenance or a missing cycle/Start yields N/A with a KPI-05 missing-source reason. Do not average a known subset of completion candidates. No Closed tasks yields N/A (zero denominator).
- All reads share the existing authorized dashboard serializable transaction and server snapshot. No schema, role catalog, generated entity, or shared database changes are required.

Implementation: `TaskCompletionDashboard` loads scoped receipts and validates raw event counts against provenance-checked readers. `TaskCycleDuration` checks cycle boundaries and sums elapsed durations. Archived/purged Start audit evidence currently causes N/A; JOB06 archive access is a separate unresolved storage contract, never reconstructed from `ActualStartAt`.

Drill-down: task-count and KPI-05 links use the explicit task population endpoint under existing TASK_VIEW authorization. The Closed population includes each current Closed task once, even without a current assignee; employee workload remains a separate historical participation view. C151 extends links to every selected branch with independent current snapshots/pagination and preserves the full UTC period. No branch is silently selected and no period is truncated. Other workday consumers retain their existing range validation.

Verification evidence and remaining whole-UC acceptance are recorded in CHECKPOINT-150.md and CHECKPOINT-151.md. Actor compatibility between UC132 (Operations Manager/Owner) and UC113 (Department Head/Operations Manager) is awaiting an explicit decision; no Owner grant or actor-policy expansion is inferred here. This decision record alone does not mark UC132 COMPLETE_VERIFIED.
