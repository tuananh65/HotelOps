# DEV3-NEXT-003 — UC139 session invalidation

Status: IMPLEMENTED_SOURCE_ONLY; independent source review pending.
Branch: feature/dev3-uc139-session-invalidation.
Base: ce53ee3ff2c70882f81e6a151381826efbf244ae.

## Implemented change

ScheduledWorkdayPanel previously retained protected snapshots and controls after a Workday GET or mutation returned 401. A session-invalid response now sets a synchronous invalidation latch, clears local snapshots/selection, cancels the in-flight mutation controller and blocks obsolete callbacks. The wrapper retains the invalidated identity independently of branch selection: changing branch or dispatch configuration cannot bypass the recovery gate. It unmounts the bound panel and offers explicit AuthProvider actions.revalidate(); no second auth store or global policy change. Only a newly confirmed session restores the flow. 403 remains an independent per-operation denial, not proof of invalid session.

Five new offline browser regressions cover shifts/current 401, late current response, mutation 401, branch-change bypass prevention, rejected session revalidation and successful revalidation without POST replay. Existing UI-401 assertion now targets the alert recovery UI instead of the old status text. Four initial new scenarios failed before the fix: 19 PASS / 4 FAIL, exit 1.

## Verification

- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs: 88/88 PASS (29 API + 24 browser + 35 read), exit 0.
- npm.cmd run typecheck: PASS, exit 0.
- npm.cmd run build: PASS, exit 0; two inherited React Router use-client warnings.
- Scoped Prettier on ScheduledWorkdayPanel.tsx and scheduled-workday-browser.test.mjs: PASS, exit 0.
- Exact staged allowlist, git diff --check and secret-pattern scan required before publication.

This branch is independent of PR #32. Its test count does not include the End calendar-validation additions pending in that PR. No backend C# changes; no backend rebuild claimed for this frontend slice.

## UC139 remaining boundaries

WorkdayService already contains Start/End and own read source, SQL session/actor recheck and per-resource ATTENDANCE_RECORD authorization. SqlWorkTimeTransactionParticipant contains retained User/worktime locks, assignment eligibility, single-working-day/open-interval checks, rowversion End update and audit inside caller transaction. These are existing implementation source, not independently SQL-verified by this task.

WorkdaysController remains NonController. Scheduled panel is not mounted in production and dispatch defaults OFF. Unscheduled notification and Pause All remain fail-closed in WorkdayService. Live runtime activation, approved disposable trial, real HTTPS/SQL, duplicate races, transaction rollback and audit-atomicity acceptance remain pending. No shared SQL, migrations, deployment or merge. UC139 remains PARTIAL, not COMPLETE_VERIFIED. No DEV2 source copied or required for this UI fix. UC008 Update remains version-blocked.

## Source hashes
frontend/src/workday/ScheduledWorkdayPanel.tsx: BA6FDE351B63FA67D31A4E2A220CEAE05CFC07DBC74A388C1CD15BED0D5BADD0
frontend/tests/scheduled-workday-browser.test.mjs: 9E4E808E55A38B4D37CCC90704D48C5AE7C63A83BA22A55EEC79652DEC127348

## DEV3-NEXT-004 merge resolution and receipts

Integration 1916bca64017918ec1d34404d53ccd7dd28b543f merged into the feature. Only conflict: scheduled-workday-browser.test.mjs, with two hunks (fixture and appended tests). Resolution keeps the session invalid/recover/revoke fixture branches AND the endedAtUtc override; concatenates session tests and malformed-calendar test. No test removed. Functional delta remains the three original files. Combined tests: 103/103 PASS (43 API + 25 browser + 35 read); Auth HTTP/state 94/94 PASS; typecheck/build exit 0. Initial scoped formatting exit 1 after merge, corrected with scoped Prettier; final check exit 0. Two inherited React Router build warnings remain. No source behavior changes after these tests, only whitespace and this receipt. Workday writes remain inactive, dispatch OFF, SQL/live NOT_RUN.
