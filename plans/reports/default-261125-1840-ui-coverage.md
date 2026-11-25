# UI coverage audit

Read-only audit of Master part 4 and existing runners; no TEST/database mutation, no API server started.

## Added helper

`scripts/verification/v2-refill-rejection-browser.mjs` exports `verifyRefillRejectionBrowser(fixture, requestId, reason, directory)`.

It loads the actual pending Storekeeper request, checks required reason, rejects via confirmation popup, verifies persisted response/history and rendered reason, captures desktop/mobile, checks overflow/page errors. All API requests route to isolated TEST5779; realtime disabled explicitly. Returns the rejected detail so caller keeps stock/custody SQL assertions. `node --check` PASS; runtime NOT RUN.

Integration point: `v2-housekeeping-runtime.mjs`, test `insufficient refill rolls back; SK rejection requires reason and preserves inventory`. Replace only the `refillAction('store', ..., 'reject', ...)` expression with a gated dynamic import/call to this helper. Keep preceding failed issue/no-reason checks and following history/SQL assertions. Suggested flag `HOTELOPS_REFILL_REJECTION_UI=1`; run after fresh backup with current build and Vite5174. No runner edited because ownership was new helpers only.

## Evidence already stronger than index

- E2E-16 Master says End Shift returns correct supplies. Existing 25-check HK run already covers UI end/settlement, exact original batches/expiry, reusable READY return, history and idempotency. DIRTY/DAMAGED are useful additional branches, not explicit words of this single Master case. Can label PASS tested normal-return branch, keep dirty/damaged separately unresolved.
- E2E-25 existing inventory 43-check run plus `v2-request-browser.mjs` executes OM approval through UI and verifies exact proposal ID/decider afterward; SQL before/after proves ownership reduction. SK proposal is HTTP, concurrent approval not covered. Index should not say UI OM approval missing. Full all-role UI not yet covered.
- E2E-31 existing maintenance browser verifies HKS investigation/related person and persisted actor/history. Master case is HKS verifies cause; cross-branch/inactive negatives belong broader RBAC rather than pretending this normal path absent.
- E2E-39 source OM partial approval through UI already covered. Rejection branch still lacks UI/runtime; no blanket PASS for both branches.
- E2E-11/12 assignment insufficiency/multiple reservations have atomic HTTP/SQL assertions but no matching UI action evidence.
- E2E-17 FIFO actual ISSUE chooses older receipt even with later expiry; no browser issue evidence.
- E2E-24 DAMAGED transition and ownership preserved have HTTP/SQL coverage; browser helper only READY/IN_USE/DIRTY/LAUNDRY cycle, no damage UI.
- E2E-28 new helper closes precise remaining UI gap once run; do not mark PASS from syntax alone.

No concrete product defect established in this bounded review. Remaining questions: none; unexecuted helper needs parent runtime ownership.
