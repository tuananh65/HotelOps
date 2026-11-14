# DEV3-NEXT-018 — UC140 blocker and UC139 confirmation workflow

UC140_SOURCE_STATUS: BLOCKED_CONTRACT (no new UC140 implementation claimed).
UC139_SOURCE_STATUS: PARTIAL / IMPLEMENTED_SOURCE_ONLY; scheduled confirmation workflow completed in existing unmounted panel.
Base: 3bc49572f6626c3609de40de8aa3509b9b0629ee, freshly fetched integration.
Branch: feature/dev3-next018, isolated worktree dev3-next018.

## Task A/B — exact UC140 boundary and pivot

Read SRS12.4 UC140, GBR144 and actual Workday/ShiftAssignment source, controllers and HotelOpsDbContext/Entities mappings. No operational ShiftHandover entity/DbSet/controller/storage contract found in reviewed src/database scripts; current DbContext AssetHandovers line21 and mapping332+ are FE07 custody, not operational shift handover. Physical SQL schema was NOT inspected; no claim that the database cannot contain other tables. Existing Workday Reads and private trusted work-time readers do not constitute an authorized complete open-item handover snapshot or next-shift receiver API.

Issue54 confirmed REVIEW_REQUIRED/PUBLIC_MUTATION_BLOCKED and no comments on retrieval. Focused DEV3 coordination posted: comment6771153168, https://github.com/tuananh65/HotelOps/issues/54. Requested owners/exit criteria: PL/business/DEV3 handover identity/version/If-Match/HTTP and state/resubmit rules; DEV1/DEV2 exact current/open/terminal/current-cycle predicates, lead-only room scope and writer/lock census; data owner approved operational handover storage and immutable snapshot/note/audit mapping; FE04 notification owner recipient/atomic enqueue/failure contract; PL/trial owner disposable schema/fixture/HTTPS lease and concurrent submit/ack/reject/resubmit/audit rollback acceptance. Same-department next-shift tie/gap/overnight rules must be explicit, not guessed.

No new dummy snapshot/503 candidate, invented schema, asset-handover reuse, public mutation, successful writer or assignment transfer created. Pending Acknowledgment/Acknowledged/Rejected requirements are documented, not implemented against unapproved storage. Per TaskC pivoted to an approved UC139 gap, without waiting for unrelated dependencies.

## Actual UC139 business gap fixed

SRS12.3 normal flow steps2-3 requires showing today's assignment and explicit user confirmation before Start. Previously existing ScheduledWorkdayPanel dispatched directly on its Start button. Existing Start/End controller/service/request/client contracts are unchanged; WorkdaysController remains NonController. No successful backend writer path or production route is added.

Start/End now open a review of the actual server-returned selected assignment or current own workday: branch, department, date and schedule or started time. No CSRF bootstrap/POST is sent when opening/cancelling review. Server remains the authority on actual time, permissions and eligibility. Confirmation consumes the synchronous review ref before awaiting; it submits the reviewed assignment/workday/If-Match and verifies current snapshot references. Cancel, selection change, refresh, branch/session replacement and 401 invalidate review. End remains independent of SHIFT_VIEW, using its existing current-workday read; open intervals/unscheduled workday remain blocked, no Pause All or handover reassignment added.

Existing synchronous mutation exclusion, identity/AbortController checks, unknown-outcome replay lock, CSRF401/403/503 handling and explicit session recovery remain. Default dispatch OFF; candidate remains unmounted. End confirmation is a UI safety step around the existing operation, not new End server semantics or a handover substitute.

## Verification receipts — all exit0, no live calls

- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs:141/141 PASS (63API +43browser +35read), preserving prior134 cases with explicit review+confirm interaction and adding7 review/cancel/invalidation cases. Prior assertions retained; mutation-trigger helper now checks opening review produces no additional request before confirm. Existing Start/End double-click cases target Confirm and still require one POST.
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs:94/94 PASS.
- dotnet build HotelOps.slnx: PASS,0 warnings/errors.
- ScheduledWorkday.Verification:43/43 offline reflection/source checks PASS; not SQL success or transaction acceptance.
- WorkdayReadRouting.Verification:44/44 focused offline checks PASS; not live HTTPS/SQL.
- npm.cmd run typecheck/build: PASS; two inherited react-router use-client bundling warnings.
- scoped Prettier: PASS; exact allowlist, whitespace and secret-pattern scan before commit.

SQL/HTTPS, concurrency, rollback/audit/notification atomicity: NOT_RUN. No disposable lease. Client-level confirmation/exclusion is not server concurrency acceptance. UC139 remains PARTIAL, not COMPLETE_VERIFIED.

## Preservation and delivery

Exactly three files: existing ScheduledWorkdayPanel, existing scheduled browser tests, and this handoff. All backend production source, DTO/API clients, WorkdaysController/flags/router, shared auth, FE01/FE02, UC138, DEV1/DEV2 writers and DB mappings/schema unchanged. No shared SQL, migrations, merge or deployment. PR51 remains OPEN/MERGE_HOLD under PathB/Issue52; its RowVersion cutover is not present in this branch and needs separately approved runtime acceptance.

NEXT_UC: UC138 public preview integration only after PL/Security approves admission/resource visibility and TL freezes HTTP contract; otherwise continue another approved existing operation. UC140 resumes only when Issue54 supplies a usable storage/authorization/transaction/notification contract; no new disconnected candidate is proposed.

## SHA-256 (LF)
- frontend/src/workday/ScheduledWorkdayPanel.tsx: 6C3EA4A0AAEE0A958CF7CE89894056C3F39354FEC892B304B5D697B3ADFD5B1C
- frontend/tests/scheduled-workday-browser.test.mjs: D3E18F15294E3D578D327DB2CB1BFC00D533F838E2405861AA2F472003CCCF39
