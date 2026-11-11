# Final actual verification —688 PASS

Filename retains historical338 for compatibility. Current tested source tree85bd4cfa8816736231449abeb985a5b573fb79ab, parents2140522457e89b974cf2d65c64515ec15d733fa9 and00bdc481cb224d7117a79c95669abc7016f51ba4.

Build: dotnet build HotelOps.slnx --nologo, exit0,0errors,2NU1900 warnings; restore completed, NuGet vulnerability endpoint unavailable. Receipt:receipts/upstream-00bdc481/02_build.txt.
No backend rerun in final documentation pass: existing14 successful same-tree backend/DI receipts are retained.

| Suite | PASS | FAIL | SKIP | Exit |
|---|---:|---:|---:|---:|
| DEV2 NoSql |92|0|0|0|
| BoundaryTests, real xUnit discovery |67|0|0|0|
| UserAccountDeactivation |12|0|0|0|
| UserRoleRevocation |54|0|0|0|
| RoomLifecycleCandidate |29|0|0|0|
| DepartmentDeactivationCandidate |25|0|0|0|
| StructureDeactivationPreview |27|0|0|0|
| WorkdayReadRouting |44|0|0|0|
| DepartmentChange |45|0|0|0|
| ShiftSchedulePlanning |24|0|0|0|
| ShiftPlanPreview |57|0|0|0|
| ScheduledWorkday |43|0|0|0|
| DI composition |16|0|0|0|
| SecureShiftPreview |58|0|0|0|
| Host shift-plan-api |27|0|0|0|
| Host secure-shift-plan-browser |7|0|0|0|
| Host shift-plan-preview-browser |12|0|0|0|
| Host shift-plan-preview rerun |49|0|0|0|
| Total |688|0|0|all0|

Backend/DI593 + host frontend95 =688 final successful checks. Console assertions and xUnit/Node discovered tests are identified by suite; no partial/failed-attempt passes counted again. Solution dotnet test exit0 discovers no test project and is not counted.

Final host files under receipts/upstream-00bdc481/host-frontend/: shift-plan-api.test.txt, secure-shift-plan-browser.test.txt, shift-plan-preview-browser.test.txt, shift-plan-preview.rerun.txt. First three exits0 come from host_exit_codes.tsv; preview rerun exit0 is explicitly attested by PL-supplied host output FRONTEND_RERUN_EXIT=0 and its receipt confirms49PASS/0FAIL/0SKIP. Original TSV row1 remains unchanged because it records the earlier failed preview attempt. Per-suite .final.command.json records this provenance rather than rewriting history.

Historical failures remain:
- Eight sandbox frontend file-level bundling failures: esbuild ancestor Access is denied before assertions, in OS temp and ignored workspace scratch. These are not business test failures.
- Host initial preview test:48PASS/1FAIL of49 because isolated scratch lacked src/hotel/main.tsx; not a production defect. Host restored exact integration entry and reran49/49. Initial48 passes excluded from final total.
- Prior-tree DI compile/environment/EventLog failures and535-check receipts retained in their original paths/snapshots; not counted as new same-tree execution.

All24 recorded original inputs match raw hashes/indexed blobs. Canonical entry raw blob df511371efb1dad1ff3b7516caa85a77a31ac009, SHA256696a2e18688fcc65ba04a17cf4509bfca49d5b6598ab5d02f4c1893d5a8f4054. All199 scratch src files match indexed decoded text;180 raw differences are encoding/CRLF/terminal newline only. No tracked source repair or test weakening.

Commands/cwd/exit/counts/receipt SHA256 and final host provenance:receipts/upstream-00bdc481/verification_summary.json. Exact evidence hashes/classifications:05_EXACT_DIFF_AND_HASHES.json. Production startup validator/listener and live API/SQL/HTTPS/races/rollback were not run. SQL_ACCEPTANCE_19=NOT_RUN. DI preserves3 pre-existing EF20602 warnings and the2NU1900 build warnings are not hidden.
