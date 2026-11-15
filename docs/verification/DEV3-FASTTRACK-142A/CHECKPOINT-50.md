# FASTTRACK-142A checkpoint 50: Failed reinspection starts the next rework

New COMPLETE_VERIFIED UCs: **0**. Total remains **6 COMPLETE_VERIFIED / 138 PARTIAL**. UC047's Failed alternative now has committed browser → HTTPS → SQL evidence through UC046 and the next assignee inbox/source. The next task is Pending Acceptance; this is not a claim that the second round has already been executed and Passed.

## Business workflow verified

`checkpoint50-next-round-final.json` is PASS for both normal and blocked rooms. After the canonical Failed conclusion and terminal reload, the inspector submits the preselected remaining defects, eligible housekeeper, deadline and reason using Request Rework. The API returns **201 Created** with Pending Acceptance. SQL confirms the new rework references the Failed reinspection, only its remaining defects, the correct executor/branch and a committed FE05 authority proof. Duplicate submission returns 409 and reload shows the existing open rework. Durable assignment delivery is replayed without duplicate notices; the executor opens the new task from its own inbox and reloads the proven Pending Acceptance source.

The existing creation rollback/concurrency probe now targets the latest Failed inspection after this handoff. It injects an audit failure and proves no partial source/links/proof/operation/audit/notice, then proves one successful concurrent request and one duplicate response. Its deliberate synthetic setup of a historical rework occurs after the browser handoff; it is not presented as real second-round execution.

No product authorization, state transition, schema or generated entity change was needed for this handoff. The trial app-role gains only the existing rework insert/defect-link/registered-writer permissions needed by the canonical UC046 endpoint; direct writes to protected proof/operation/binding tables remain denied. User permissions and CSRF stay unchanged.

## Fixture repairs and retained failures

- `checkpoint50-next-round.json`: selector timeout before request; use the existing form's label matching rather than exact matching including select-option text. Cleanup PASS.
- `checkpoint50-next-round-retry.json`: real creation succeeded with HTTP201, but the new assertion expected 200. Corrected to the controller's Created contract. Cleanup of the committed descendant PASS.
- `checkpoint50-next-round-created.json`: browser creation, notification and executor source succeeded; the later authority test correctly refused a source whose photo had already been removed by inner fixture cleanup. Keep retained inspection/photo source until the outer authority cleanup, then delete descendants in FK order. No proof validation was weakened. Cleanup PASS.
- Final rerun above passed all checks. The original trigger bodies remain enabled throughout; cleanup normally drops only the owned trial proof tables after exact database and proof-lineage checks. It never disables the immutability guard to delete an individual protected source.

## Validation and remaining work

ModelProbe and final `dotnet build HotelOps.slnx`: **PASS, 0 compiler warnings/errors**. Product source is unchanged from C49, whose direct regression evidence remains 532 backend PASS/11 SKIP and 15 focused frontend PASS, plus typecheck/build PASS; these suites were not redundantly rerun for test-harness-only changes. Existing runtime EF and Vite warnings remain as recorded in C49.

`checkpoint50-cleanup-final.txt` independently confirms the exact disposable server/database GUID/run, zero users/branches/reworks/attachments/intervals/notifications/private files, absent trial bindings/writers/principals/fault CHECKs, restored enabled ordinary projection guards and trusted original workflow CHECK. Both HTTPS hosts stopped. No shared database mutation, production seed or deployment.

Changed implementation files: `frontend/scripts/sprintReinspectionAssignmentBrowser.mjs`, `frontend/scripts/sprintReinspectionExecutorBrowser.mjs`, and ModelProbe's `SprintApprovalBrowserProbe.cs`, `SprintReinspectionAssignmentProbe.cs`, `SprintReinspectionExecutorProbe.cs`, `SprintReinspectionHttpsProbe.cs`, `SprintReworkAuthorityFixture.cs`, `SprintReworkFixture.cs`. Evidence and UC matrix accompany them.

Next highest-impact work: compose the assigned reinspector's exact original-inspection read/link, including a different eligible inspector; verify concurrent block changes and checklist-version exceptions; exercise the second-round execution to Passed where needed for the final demo. Keep UC047 PARTIAL until applicable acceptance is fully evidenced. The current descendant cleanup deliberately admits the same synthetic inspector/version/room/cycle only; any alternate-inspector fixture must declare and validate its additional owned identity explicitly. Continue the existing sprint from this checkpoint without a broad audit or clock restart.
