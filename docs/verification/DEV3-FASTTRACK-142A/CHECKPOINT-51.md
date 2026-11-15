# FASTTRACK-142A checkpoint 51: assigned reinspector opens the exact original inspection

New COMPLETE_VERIFIED UCs: **0**. Total remains **6 COMPLETE_VERIFIED / 138 PARTIAL**. This checkpoint closes UC047 normal-flow step 2's broken original-inspection link for a different assigned reinspector. It does not declare whole-UC acceptance from test counts.

## Implemented workflow

The original link previously opened the generic inspection execution route, which correctly denies anyone other than that inspection's original inspector. The canonical reinspection now links to `/reinspections/:branch/:id/original`, available before and after conclusion. The read-only page shows the original checklist/results, summary, defects and private inspection evidence, and returns to the current reinspection.

New GET endpoints under `/api/branches/{branch}/reinspections/{id}/original` derive the original identity only from the protected assignment/submission/conclusion chain. Current session, INSPECTION_VIEW, active supervisor eligibility, branch access and exact assigned inspector remain mandatory. The endpoint accepts no original-inspection ID from the caller. It filters out cleaning-task files, remediation files and reinspection conclusion files. Every download repeats authorization and proof checks. The existing original-inspector execution permission is unchanged; no new permissions, generated EF edits, schema changes or mutation endpoints were added.

Frontend response validation binds branch/reinspection IDs, ETag, criterion/defect/evidence membership and locally constructed evidence URLs. Session loss removes private content; pending reads are aborted or ignored after authority/route changes. The original page exposes no mutation controls.

## Direct evidence

- `checkpoint51-distinct-pass-retry.json`: PASS for normal and blocked rooms. Two different real synthetic supervisor accounts perform the original inspection and reinspection. Browser follows/reloads the exact original link, downloads private evidence, returns, concludes Passed and verifies terminal source, notification delivery/inbox/source and SQL persistence. Generic original execution remains HTTP403 for the different reinspector.
- `checkpoint51-distinct-failed.json`: PASS for normal and blocked rooms, including the same original-source link, Failed conclusion, terminal reload and Request Rework form admission. This distinct-inspector trial does not create a descendant rework; C50's committed next-round handoff remains separate direct historical evidence.
- Both runs include restricted-principal service tests for original-source/attachment rejection after branch revocation or audit tampering, foreign branch/inspection denial, executor denial and rejection of remediation/conclusion files through the original endpoint. Assigned and terminal reads are covered. Synthetic identity creation/cleanup is rehearsed under rollback before commit.
- `checkpoint51-distinct-pass.json` retains the first failed run: the new fixture incorrectly required the original supervisor's `primary_branch_id`, while the actual fixture grants access through `user_branch_access`. Corrected the fixture ownership predicate; no product authorization was relaxed. Failure cleanup and an independent zero-row query passed before retry.
- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors. ModelProbe build: PASS, 0 warnings/errors. `dotnet test HotelOps.slnx`: successful invocation but no test project in the solution. Actual `dotnet test tests/HotelOps.Dev1.RegressionTests --no-restore`: **532 PASS, 11 SKIP, 0 FAIL**. Live trial evidence does not relabel skipped tests.
- Final frontend typecheck and production build: PASS. Focused original/execution client, canonical execution route and shared AuthRouteHost tests: **130 PASS, 0 FAIL**. Full frontend suite was not rerun; the older C47 full-suite result is historical evidence only. Dependencies/lockfile unchanged, so npm ci was not rerun.
- Warnings retained: Vite ignores React Router `use client` directives and reports a 656.60 kB chunk; runtime EF reports three existing unmapped room-type indexes and a row-limit query without ordering. Compiler warnings remain zero.
- `checkpoint51-cleanup-final.txt`: exact disposable server/database GUID verified; zero users, branches, reworks, attachments, work intervals, notifications and private files; trial bindings/writers/principals/fault CHECKs absent; original projection triggers enabled and workflow CHECK enabled/trusted. Owned HTTPS hosts stopped. No shared database mutation, production seed or deployment.

## Delivery and remaining acceptance

TASK_ID=DEV3-FASTTRACK-142A. Branch: `integration/dev1-dev2-dev3-unblock-20261114`. Initial local/remote SHA: `cb5035690db7312bbdd164e957417d3c665ff648`. Pre-delivery fetch confirmed 0 ahead/0 behind. Commit/push receipt follows in the checkpoint response.

Product paths: `ReinspectionOriginalReadService.cs`, `ReinspectionWorkflowDtos.cs`, `HousekeepingWorkflowController.cs`, `reinspectionOriginal.ts`, `ReinspectionOriginalPage.tsx`, `ReinspectionExecutionPage.tsx`, `SharedAuthHost.tsx`, `returnUrl.ts`. Verification paths: the original-client and execution-route tests, `sprintReinspectionAssignmentBrowser.mjs`, ModelProbe's `SprintDistinctReinspectorFixture.cs`, assignment/HTTPS/terminal-read probes and `SprintApprovalBrowserProbe.cs`. Decision51, this receipt and the UC matrix document the scope.

Next highest-impact work: verify UC047 conclusion against concurrent effective-block changes and checklist-version exceptions; finish its acceptance ledger/demo without treating a historical receipt as current room-use authority. Then close UC045 notification source/negative/rollback acceptance and continue the FE11/FE13/UC006/010 lifecycle priorities. The complete second-round execution-to-Passed demo remains outstanding, beyond C50's already verified Pending Acceptance handoff. Sprint remains active.
