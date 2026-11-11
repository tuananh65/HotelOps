# UC139 new-tree targeted verification â€”456 PASS

Filename retains historical338 for compatibility. Current tree:b3d8dfe61a528c68002ce6d725d47840604d2e27; integration70e1d4ee2c4b63b14543f90c1e315bec39585d80.

Build:dotnet build HotelOps.slnx --nologo; exit0,0errors,2NU1900 warnings. Restore completed/local assets available; vulnerability endpoint unavailable. Fresh receipt:receipts/uc139-70e1d4ee/build.txt.

| Current offline suite | PASS | FAIL | SKIP | Exit |
|---|---:|---:|---:|---:|
| DEV2 NoSql |92|0|0|0|
| BoundaryTests, actual xUnit discovery |67|0|0|0|
| WorkdayReadRouting |44|0|0|0|
| ScheduledWorkday |51|0|0|0|
| ShiftSchedulePlanning |24|0|0|0|
| ShiftPlanPreview |57|0|0|0|
| SecureShiftPreview |58|0|0|0|
| DI composition |16|0|0|0|
| Backend/DI subtotal |409|0|0|all0|
| UC139 Windows host browser |47|0|0|0 (host attested)|
| Current-tree total |456|0|0|all0|

Strategy is exactly eight mandatory targeted suites plus build, justified by UC139 claim lifecycle, read routing, shared WorkTime transaction participant, existing planning/save gates, DEV2 dependencies and DI. No automatic full historical rerun. ScheduledWorkday51 includes new7 query behavior cases and1 EF translation check; no real SQL connection. No source fixes or assertion changes.

New-tree ScheduledWorkday browser:47 tests discovered,47PASS,0FAIL,0cancelled,0SKIP,0todo. Raw receipt:receipts/uc139-70e1d4ee/host-frontend/scheduled-workday-browser.host.txt. SHA256=97ee27ccba1f12d50f9d12d01f46302f21684384e1c28ee683d8643896703700. Exit0 is explicitly host-attested; no exit line is embedded in raw Node output. Exact command/cwd/provenance in adjacent .command.json. Executed in bin/PR25-UC138-95d870a47f684d9c9ad4425afdbf4950; all415 canonical frontend files independently matched pinned integration blobs. Host reported307 scratch restorations, no business-source correction. No sandbox rerun. Prior95 frontend and full688 remain previous-tree-only.

Acceptance project, README, guard script and template inspected; no acceptance executable, --prepare, --execute or test-guards.mjs run. Default deny is a source finding, not a newly executed test count. UC139_ACCEPTANCE_RUNTIME=NOT_RUN. No solution-level restore-only test command used as project discovery substitute.

Exact commands/cwd/tree/exit/counts/warnings/SHA256:receipts/uc139-70e1d4ee/verification_summary.json. Historical initial block,535/593/688, sandbox/host retries and DI failures remain in original paths/snapshots; none counted again.

Raw receipts preserve historical trailing spaces. source_checks.json lists narrow .txt/.tsv whitespace exceptions; functional code and current reports/tooling remain strict. Worktree SHA256 hashes raw bytes; gitNormalizedBlob predicts Git clean-filtered blob IDs under autocrlf=true, while stagedGitBlob is null for untracked evidence until host stage. No global attributes change or silent CRLF cleanup.

Known limits:2NU1900;3 existing EF20602 index warnings during DI. SQL/HTTPS/concurrency/rollback/production startup and SQL19 acceptance NOT_RUN.
