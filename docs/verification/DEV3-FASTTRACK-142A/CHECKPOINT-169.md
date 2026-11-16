# C169 — UC038 turnover source completion

Canonical branch; initial local/remote baseline `dc9309114ecfba5524a6b88619dc7c9be4b6f49e`. No database/schema mutation. Previous turn made source progress, not a wait.

`NEW_IMPLEMENTED_PENDING_QA=[UC038]`; `TOTAL_IMPLEMENTED_PENDING_QA=2`; `TOTAL_COMPLETE_VERIFIED=27/144`; `TOTAL_SOURCE_COVERED=29/144`.

SRS4.11 path now covers cycle list/date/result filters, assignees, start/end, rework count, cancellation reason and timeline, with canonical scoped inspection-history navigation to checklist/defects/photos. Reuses existing read transaction, session/user locks and final branch/source reauthorization. INSPECTION_VIEW is independently required even for result filtering; denied source data stays hidden.

Latest inspection follows the existing history ordering (inspected/scheduled/created timestamp, then ID), including a pending round: it does not reuse an older PASS. Only COMPLETED PASS/FAIL/CONDITIONAL is exposed. It is historical evidence, never effective readiness or permission to use a room. Added inspection timeline event. Fixed cancellation reason lookup: canonical writer stores audit Action=UPDATE and JSON action=CANCEL_CLEANING; unrelated UPDATE reasons must not appear. Exact legacy CANCEL remains supported. Existing private attachment controls and UC048 read path reused.

Files: `TurnoverCycleReadService.cs`, new `TurnoverInspectionProjection.cs`, `frontend/src/api/dev1/turnoverHistory.ts`, `RoomTurnoverHistoryScreen.tsx`; focused `TurnoverInspectionProjectionTests.cs` and `turnover-history.test.mjs`; existing `TurnoverHistorySqlTests.cs`/`TurnoverLiveTests.cs` expectations updated for new filter/coverage (not executed against SQL). Matrix and single human-QA handoff updated.

Checks: backend build PASS0 warnings;16 focused backend tests PASS (including SQL translation without connection),6 frontend contract tests PASS; mocked browser list result/filter/cycle/detail-link smoke PASS; typecheck/build PASS. Existing Vite use-client and bundle-size warnings remain. SQL/HTTPS, full inspection-detail navigation, physical file reads, cancellation query execution and full business/security matrix deferred to human QA. No COMPLETE_VERIFIED promotion or live SQL claim.

QA: [UC038 row in handoff](HUMAN_QA_HANDOFF.md); use compatible disposable cycle/inspection/rework/evidence fixtures. Verify Failed→Passed history, pending round, correct branch, revoked inspection grant, unauthorized filter403, malformed/foreign file, cancellation provenance, UTC pagination and read-only fingerprint. Optional room-status-history cycle association is still unavailable and explicitly labeled; it is not used to infer room readiness.

Next batch: FE04 fresh Start/Complete/Cancel source gaps, then independent FE13 target adapters and approved UC050/052 category/SLA source. UC006/010 gates remain. Deadline unchanged; about14h remain at03:50 UTC on2026-11-16.
