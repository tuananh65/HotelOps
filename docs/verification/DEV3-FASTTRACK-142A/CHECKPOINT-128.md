# FASTTRACK-142A checkpoint128 — effective room readiness in reports

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=0f870a9c527f644a435e994a60d2970439af6499
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

RPT01 previously exported only rooms.operational_status. Added effectiveReadiness and canUse to the room section using the existing EffectiveRoomReadiness authority within report materialization's Serializable transaction and authorized room scope. Physical status remains unchanged. Only effective READY yields canUse=true; inactive, effective blocks, incomplete/rework cleaning and unknown capability all fail closed. These values belong to the frozen report timestamp and do not authorize room use at a later time. Snapshot serialization and existing CSV/XLSX rendering carry the fields without bypassing report actor/permission admission.

Direct evidence:

- Backend solution build PASS, zero warnings/errors. ModelProbe build PASS with three existing credential-fixture EF1002 warnings. An initial probe build failed because the new test named a nonexistent renderer; corrected to the existing ReportCsvRenderer before runtime verification.
- Focused report/readiness regression: 49 PASS, one environment-gated live test SKIP. Solution test exits zero without test discovery; the explicit regression project provides the test count.
- checkpoint128-room-report-readiness-final.json: 18 SQL/report/CSV checks PASS, rollback=true. Covers never-passed, Passed, two blocks, removal of one/last block, inactive, failed/rework, frozen blocked CSV and missing housekeeping capability. Capability marker removal is confined to a savepoint and restored before full fixture rollback; no FK/trigger disabling.
- checkpoint128-reports-regression.json: 50 existing report SQL checks PASS, rollback=true, retaining scope, row-limit, frozen CSV and task/request snapshot behavior.
- checkpoint128-report-runtime.json (actual runner output checkpoint75-trial6-runtime.json): success=true, cleanup=true. Real browser/HTTPS/disposable SQL preview displays physical READY alongside effective BLOCKED and canUse=false, downloads CSV/XLSX, persists export audit and logs out. Public export remains byte-identical after block release; a new snapshot reports PENDING_CLEANING rather than READY when no passed cycle exists. Existing Owner/Operations Manager admission, Department Head denial, CSRF, frozen export and revocation scenarios also pass.
- Post-trial disposable SQL check: users=0, branches=0, housekeeping marker=1. Owned HTTPS host exited. No shared/production mutation or schema change.

No whole UC promoted: UC135 still lacks full report indicators, specialized filters and PDF. FE13 material/legacy and pending lifecycle contracts remain open. Full frontend release retry remains live in session81908; no final result or PASS claim at this checkpoint. Product frontend files did not change; only the real browser verification script was extended.
