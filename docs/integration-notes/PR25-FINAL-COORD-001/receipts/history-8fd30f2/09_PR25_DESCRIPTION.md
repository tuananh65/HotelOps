# DEV2 PR25 — reconciled dependency readers and placement coordination

Prepared PR body; NOT_PUBLISHED. Host must fill merge/final SHA after normal commit and push.

PR25 preserves the accepted UC006 inactive-branch responsibility fix, UC016 Asset/Incident placement coordination, UC012/022/026 dependency readers and FE12 Maintenance facts adapter while synchronizing integration source.

- Final feature HEAD: HOST_TO_FILL_AFTER_PUBLICATION.
- Merge commit: HOST_TO_FILL; parents 2140522457e89b974cf2d65c64515ec15d733fa9 and 8fd30f2f12e9dae102de2826e0be20e99e6a14ad.
- Tested combined staged source tree: 14f4595c0edeb1f6349d636ec1a08a22a287f569, before evidence staging.
- Conflicts resolved: 0. Corrective production source changes: none.
- Original 24 PR paths remain identical to accepted feature source. Integration contributes 111 staged paths relative to feature; these remain upstream-owned.
- Evidence adds paths enumerated in 05_EXACT_DIFF_AND_HASHES.json; final PR file count must include them.

Program.cs retains exactly three additive DEV2 scoped registrations and all integration registrations. Executable DI probe resolves trusted Asset/Incident authority, readers, current user, session and permission dependencies. User-first/Branch/resource ordering, active location ancestry, caller-owned transactions and final authority rereads remain. Inactive branches, unscoped facts, unknown policy, fingerprints and CanDeactivate=false remain fail-closed.

Validation on combined source: backend build Passed exit 0, 0 errors, 2 NU1900 warnings (NuGet advisory endpoint unreachable). Required offline slices: 350 PASS = 92 + 67 + 12 + 54 + 29 + 25 + 27 + 44. Additional DepartmentChange/ShiftSchedulePlanning/ShiftPlanPreview/ScheduledWorkday: 169 PASS. DI composition: 16 PASS. Total successful checks 535; final failures/skips 0. BoundaryTests discovered 67 xUnit cases. Solution-level dotnet test exit 0 discovers no test project and is not counted. DI harness failures and fixes are retained in receipts; 3 existing EF model index warnings remain documented.

Evidence: docs/integration-notes/PR25-FINAL-COORD-001/; exact commands/exit codes/counts/hashes in receipts/verification_summary.json and 05_EXACT_DIFF_AND_HASHES.json. Whitespace and changed-content secret-pattern results are in receipts/source_checks.json.

Historical DEV3 acceptance: https://github.com/tuananh65/HotelOps/pull/25#pullrequestreview-5824903849.
New DEV3 final review: PENDING. One targeted review requested only after publication.

SQL acceptance 19 NOT_RUN. Business activation BLOCKED. No live API/SQL/HTTPS/race/rollback acceptance, new runtime lease, migrations or schema changes. Workday writers and Notification activation remain OFF; deactivation owner acceptance gates remain. PR51 remains independent MERGE_HOLD.

Recommendation: source validation passed; await publication verification, DEV3 final APPROVED_SOURCE_ONLY and Project Leader merge decision. Do not self-merge or deploy.

## Original 24 PR paths

- docs/integration-notes/DEV2-MASTER-EXECUTION-001/P0_SOURCE_HANDOFF.md
- docs/integration-notes/DEV2-MASTER-EXECUTION-001/P1_SOURCE_HANDOFF.md
- src/HotelOps.Api/Controllers/AssetsController.cs
- src/HotelOps.Api/Controllers/IncidentsController.cs
- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Services/Assets/AssetService.Condition.cs
- src/HotelOps.Api/Services/Assets/AssetService.Handover.cs
- src/HotelOps.Api/Services/Assets/AssetService.Location.cs
- src/HotelOps.Api/Services/Assets/AssetService.Retirement.cs
- src/HotelOps.Api/Services/Assets/AssetService.Update.cs
- src/HotelOps.Api/Services/Assets/AssetService.cs
- src/HotelOps.Api/Services/Dependencies/DependencyContracts.cs
- src/HotelOps.Api/Services/Dependencies/Dev2DependencyQueries.cs
- src/HotelOps.Api/Services/Dependencies/Dev2DependencyReader.cs
- src/HotelOps.Api/Services/Dependencies/Dev2PlacementAuthority.cs
- src/HotelOps.Api/Services/Dependencies/Dev2PlacementGuard.cs
- src/HotelOps.Api/Services/Incidents/IncidentService.cs
- src/HotelOps.Api/Services/Notifications/Fe12MaintenanceDependencyAdapter.cs
- tests/HotelOps.Dev3Unblock.NoSql/ExpansionChecks.cs
- tests/HotelOps.Dev3Unblock.NoSql/HotelOps.Dev3Unblock.NoSql.csproj
- tests/HotelOps.Dev3Unblock.NoSql/Program.cs
- tests/HotelOps.Dev3Unblock.NoSql/SqlPlacementProbe.cs
- tests/HotelOps.Dev3Unblock.NoSql/Uc006InactiveBranchChecks.cs
- tests/HotelOps.Dev3Unblock.NoSql/WriterOrderChecks.cs
