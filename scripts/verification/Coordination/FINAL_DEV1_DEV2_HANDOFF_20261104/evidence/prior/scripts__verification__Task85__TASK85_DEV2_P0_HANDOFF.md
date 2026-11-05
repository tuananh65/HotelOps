# Task85 Phase A — DEV1 → DEV2 P0 handoff

Ready to forward as a **source-evidence package**, not Notification V1 integration acceptance.
Repository `D:\SEP490\Projects\HotelOps`, branch `feature/BE1-database-integration`,
HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Current local patches are essential;
HEAD alone lacks untracked A1/B1.1/Monitor and restored RoomType directory source.
Snapshot cf8fb020362f8f64192e7f2f14a10547cebe5b91 is historical evidence, not restored runtime.

## Package

1. TASK85_DEV2_B12_WORKDAY_EVIDENCE.md — exact B12 path/hash/approval, Task79 receipt counts,
   own Workday API and A1 transaction/locking contract, B1.2/PM dependencies.
2. TASK85_DEV2_PRODUCER_SOURCE_INVENTORY.md — six UCs, actual signatures/lines, transaction/
   audit/permission boundaries, historical vs current availability, recipient proof gaps.
3. TASK85_DEV2_EVENT_PERMISSION_MATRIX.md — event/source/recipient/access/transaction matrix,
   missing joint-decision evidence and distinction between business approval/shared diffs.
4. CURRENT_SOURCE_LINES.txt / HISTORICAL_SOURCE_LINES.txt / SRS_SOURCE_LINES.txt — numbered
   source evidence with current SHA256 or immutable snapshot blob IDs; no copied fake runtime.
5. PRESERVATION_BASELINE.json / PRESERVATION_RESULT.json — hashes/Git preservation checks.

## DEV2 có thể bắt đầu ngay

- Review exact reusable IWorkTimeTransactionParticipant and source identity/transaction requirements;
  design DEV2-owned PM adapter using same scoped context/participant and caller-owned transaction.
- Review Notification V1 publisher/outbox/replay interface against these actual producer boundaries;
  make durable intent/replay/rollback/delivery retry decisions explicit in the joint contract.
- Define immutable source/recipient facts for assignment/inspection/rework notifications and test
  retry without recipient recomputation. Confirm real recipient-readable routes/permission behavior;
  Monitor's CLEANING_VIEW is not automatically granted to all assignees.
- Prepare DEV2 implementation/acceptance for durable inbox, restart/retry/dedup/failure and
  same-transaction rollback. Existing source inventory suffices for contract review; runtime producer
  integration still depends on missing writers/restoration and bounded approvals.

No message sent to DEV2 automatically. User will forward this package.

## P0 gaps requiring concrete evidence

| Missing | Owner / next action |
|---|---|
| Product & Architecture Decisions V1.0, Joint Review, Addendum V0.2 actual documents | Project owner/DEV2 supply exact path/blob + approval provenance; NOT FOUND locally, no invented contents |
| Final cleaning/rework completer persistence | DEV1 completion writer contract; current timestamp/assignee/progress/interval closer insufficient; reviewed design needed before UC044/045 recipient acceptance |
| Unique supervisor and branch-DH selection/current recipient read access | Joint business rule and bounded technical review; eligibility is not selection, own Workday GET cannot serve DH viewing another employee |
| UC029/041/046 historical writers absent current | DEV1 selective restoration proposal/schema/DI compatibility review later; no blind restore |
| UC037 cancellation, UC044 approval, UC045 rejection | DEV1 writers + room/effective-block coordination; not implemented, no hook exists today |
| Publisher/outbox/replay/worker actual source + acceptance | DEV2 provides identifiable implementation and joint-approved interface; Task79 checkpoint says not implemented at that time, not a live assertion about external work today |
| PM Pause All participant, supported kind identity, rework pause transition | DEV2 owns maintenance/PM source; DEV1 owns reviewed cleaning/task/rework adapters; no maintenance duplicate model |
| Task79 SQL instance in receipts | Historical receipts prove catalog only; source uses explicit env. Need retained execution metadata to prove exact instance, not current secrets inference |

## Verification and progress

Task85: source/document inspection only; no SQL connection, HTTPS, build or framework test run.
Historical Task79 after-fix43 HTTPS/SQL +53 deterministic +26 credential SQL +199 isolated security
are documented with scope and failed stale-assembly probe; not relabeled Task85 results.
Task78 scheduled B1.1 and Task84 UC142 restoration evidence preserved; Task82 UC037 remains PARTIAL.
Task83 historical-vs-runtime distinction retained. No UC038 restoration in this phase.
UC139 BLOCKED_EXTERNAL; UC029/037/041/046 PARTIAL; UC044/045 NOT_IMPLEMENTED.
Ledger unchanged: **26/75 COMPLETE_VERIFIED_V1,60/256 points=23.4375%; delta0**.
No PARTIAL counted COMPLETE and no Task85 ledger rewrite needed.

Exact preservation/diff-check outcomes are in PRESERVATION_RESULT.json. Production/source/schema/
RBAC/Program/DEV2 changes0. No HotelOpsDB/protected DB access, Git mutation or worktree operation.
Phase A ends here; forward package, reconcile missing joint decision text, then review any exact
shared integration diff separately. UC038 is explicitly deferred to a subsequent instruction.
