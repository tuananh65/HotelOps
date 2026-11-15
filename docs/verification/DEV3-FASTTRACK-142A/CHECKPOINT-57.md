# FASTTRACK-142A checkpoint 57: Failed reinspection through the next Passed round

New COMPLETE_VERIFIED UCs: **0**. Total remains **8 COMPLETE_VERIFIED / 136 PARTIAL**. This closes the outstanding composed FE05 demo from a Failed reinspection through the next rework and Passed reinspection. It does not certify all UC041, UC048 or the whole FE05 module.

## Direct workflow evidence

`checkpoint57-second-round-final.json`: PASS for both normal and effectively blocked rooms. The existing fixture creates the initial Reject/rework and validates submission; its retained first submission is the restricted SQL service occurrence. From that committed source, real canonical browser/HTTPS actions assign and conclude the first reinspection Failed, request the next Pending Acceptance rework, then execute its Accept, Start, per-defect remediation/photo submission, assignment and Passed conclusion. The second round uses the remaining defects created by the first Failed inspection, the same cleaning cycle and published checklist, with independent SQL verification of the exact binding chain.

The executor opens the specific delivered assignment notification, uses the existing authorized Working workday and PLANNED shift, and closes exactly one second-round work interval on submission. Its three executor audits are persisted. Missing CSRF/ETag, stale versions, invalid input and changed replay remain denied. Explicit replay preserves the accepted receipt. Browser reload and private evidence reads are exercised.

Both rounds exercise assigned-inspector notification delivery, current authorization, rollback, deduplicated retry and inbox-to-source navigation. Second-round submission delivery and Passed notification checks also execute. The executor and inspector can reload the second terminal result. Delivery is invoked explicitly; hosted scheduler timing is not claimed. The supervisor submission notification's uncomposed source remains fail-closed, as documented by the existing submission acceptance probe.

The first Failed inspection and rework row versions and conclusion audit remain unchanged after the second Passed. A protected historical read still reports Failed while recomputing current room readiness. Normal room readiness is READY; a room with an effective block remains BLOCKED/MSG17. Removing that owned block in the read regression gives READY without changing the historical receipt, then restores the block. Current inspector revocation, tampered audit, foreign evidence and attempts to reconclude a terminal inspection remain denied.

## Implementation and recovery

Added `SprintSecondReworkRoundProbe` and the opt-in `DEV3_REINSPECTION_COMPLETE_NEXT_ROUND=1` composition hook. This mode requires browser Failed/next-round mode and the original published version/inspector. The exact disposable HTTPS role temporarily receives the existing submission writer role and required interval/remediation/evidence DML; protected proof/binding/receipt write denials remain. No product authorization, generated EF entity, production schema or shared database change was necessary.

Cleanup now validates ownership of at most one direct child by source proof and matching task/room/branch/version/inspector/executor before deleting child evidence and restoring the outer fixture baseline. Unrelated bindings cause refusal. The original Failed source is retained until its dependent rework is removed by the outer fixture. Original guards/CHECK definitions are restored normally.

`checkpoint57-second-round-attempt1.json` preserves the initial failure: the assignment inbox browser selector assumed only one notification with that title. Both notifications were legitimate. The test now selects the exact notification ID, as does the second rework receive browser. No product assertion was removed. Independent cleanup passed after the failed attempt; the final rerun passed both cases.

## Validation and limits

- `dotnet build HotelOps.slnx` and ModelProbe build: PASS, 0 compiler warnings/errors. The first sandboxed restore was denied access to the user's NuGet.Config; the authorized retry passed. This was an environment access failure.
- `dotnet test HotelOps.slnx --no-restore`: exit 0, but the solution contains no test project. Actual backend regression: **542 PASS / 11 SKIP / 0 FAIL**.
- Focused rework/reinspection frontend tests: **21 PASS**. Auth/Router plus notification source tests: **51 PASS**. Node syntax checks pass. The existing esbuild import.meta/IIFE warning remains.
- Frontend npm ci/typecheck/production build were not rerun: no frontend product source or dependency change. Their latest C56 PASS is historical evidence, not a C57 execution result.
- `checkpoint57-cleanup-final.txt`: exact server/database/GUID/run verified; zero synthetic rows and private evidence files; no trial bindings/writers/principals/fault constraints; original projection guards enabled and rework CHECK enabled/trusted. Owned runtime processes stopped. Existing EF unmapped-index warnings remain.

Only `HotelOps_DEV3_142A_20261114_cc78c13f` was mutated. Runtime still requires the registered protected workflow capabilities and narrow isolated schema compatibility setup. Nothing was installed on a shared or production database. Retirement before a new assignment remains denied; cross-version compatibility is still an implementation gap.

Next highest-impact work: UC041 initial assignment/reassignment and UC048 history acceptance, followed by FE11/FE13 and UC006/010 lifecycle gaps. The sprint goal remains active; this checkpoint is not sprint completion.

Canonical branch: `integration/dev1-dev2-dev3-unblock-20261114`. Initial local/remote SHA: `766859fa99b046cbd06988084e8fa86438ec8d69`. Changed paths are the two browser scripts, composed probe/hook/cleanup/read assertions and this checkpoint's decision/matrix/acceptance/evidence files. Normal commit/push follows changed-path and remote-HEAD verification.
