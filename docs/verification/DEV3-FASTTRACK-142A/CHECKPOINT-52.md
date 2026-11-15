# FASTTRACK-142A checkpoint 52: reinspection conclusion versus concurrent room blocks

New COMPLETE_VERIFIED UCs: **0**. Total remains **6 COMPLETE_VERIFIED / 138 PARTIAL**. UC047's concurrent-block acceptance gap is now covered by direct SQL execution; checklist-version and final composed-demo acceptance remain. Product authorization/lifecycle code is unchanged from C51.

## Direct concurrency evidence

`checkpoint52-block-race-pass.json` and `checkpoint52-block-race-failed.json` both PASS for normal and blocked rooms. The existing canonical browser/HTTPS flow creates and submits rework, then assigns the exact reinspection. Its real restricted-principal service concludes while another physical SQL connection attempts a block write. This is a service/SQL race test, not a claim that the concurrent conclusion itself was submitted by the browser. C51's separate browser/HTTPS conclusion evidence remains applicable and historical.

| Conclusion | Racing write | Observed lock wait | Immutable receipt readiness | Fresh terminal-read readiness |
|---|---|---:|---|---|
| Passed | Add effective block | 3062 ms | READY | BLOCKED |
| Passed | Release effective block | 3054 ms | BLOCKED | READY |
| Failed | Add effective block | 3085 ms | REWORK_REQUIRED | BLOCKED |
| Failed | Release effective block | 3060 ms | BLOCKED | REWORK_REQUIRED |

The wait is deliberately synchronized with a disposable three-second trigger; it is proof of retained locking, not a performance measurement. The trigger runs only for the exact bound inspection's ASSIGNED-to-terminal write. Its installation/drop is rehearsed under rollback. It adds no business authority and does not disable or replace existing triggers. The competing block change must wait until the conclusion transaction commits. Two concurrent identical conclusion calls still produce the same protected receipt and one audit. Terminal reread must reflect the newly committed block facts without rewriting that receipt or its ETags. The helper restores only the fixture's prior block facts before the existing terminal-source and executor/delivery assertions, and removes its own incident/block/delay trigger.

Both runs also retain the existing conclusion audit-failure rollback, stale aggregate/inspection rejection, invalid/missing/foreign defect rejection, invalid-photo cleanup, registered projection guards, lifecycle/history persistence, replay conflict, current authorization, terminal source/evidence and Passed-delivery checks. No source guard or permission was weakened to obtain PASS.

## Validation and delivery

- Solution build and ModelProbe build: **PASS, 0 compiler warnings/errors**. `dotnet test HotelOps.slnx` succeeds but contains no test project. Product source is unchanged from C51; its 532 backend PASS/11 SKIP and 130 focused frontend PASS were not redundantly rerun. Frontend build/typecheck/dependency installation were not rerun for this test-harness-only change.
- The harness rejects simultaneous SQL-race and browser-conclusion modes before creating its context/fixture, preventing a misleading race PASS flag. `checkpoint52-mode-guard.txt` records the actual nonzero CLR exit and absent report. Initial shell assertions incorrectly expected exit 1, then matched wrapped PowerShell error output; the guard itself worked. Corrected observation captures unformatted stderr. No business fixture was created by those negative invocations.
- `checkpoint52-cleanup-final.txt` independently verifies exact disposable identity, zero users/branches/reworks/attachments/intervals/notifications/room blocks/incidents/private files, absent race trigger and trial bindings/writers/principals/fault constraints, enabled original projection triggers and trusted original workflow CHECK. Both HTTPS hosts are stopped. No shared DB mutation, production seed or deployment.
- Existing runtime EF warnings (unmapped room-type indexes and row-limit query without ordering) remain; frontend warnings are unchanged historical C51 evidence.

TASK_ID=DEV3-FASTTRACK-142A. Canonical branch `integration/dev1-dev2-dev3-unblock-20261114`; initial local/remote SHA `e2ec295517a1a027d3055d3be437a05026e11656`. Pre-delivery fetch confirmed 0 ahead/0 behind. Changed code: `SprintReinspectionBlockRaceProbe.cs`, `SprintReinspectionConclusionProbe.cs`, `SprintApprovalBrowserProbe.cs`. Remaining changed paths are evidence and the UC matrix.

Next: validate retirement after a valid Published-version assignment against GBR-057's immutable historical version rule; resolve the separate UC041 dependency where the original version is already Retired before assignment; finish the composed next-round demo and UC047 acceptance ledger. Do not remap old defect criteria by labels or silently broaden an original inspection's mutation authority. Sprint remains active.
