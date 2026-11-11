# PR25 â€” reconcile accepted DEV2 source with current fullstack integration

This source-only reconciliation preserves the accepted UC006 inactive-branch responsibility fix, UC016 Asset/Incident placement coordination, UC012/022/026 dependency readers and FE12 Maintenance adapter while retaining DEV3 UC138 Secure Shift Preview and gated Save/Assign source.

Final merge SHA and feature HEAD:HOST_TO_FILL_AFTER_COMMIT.
Expected merge parents:2140522457e89b974cf2d65c64515ec15d733fa9 and00bdc481cb224d7117a79c95669abc7016f51ba4.
Tested combined source tree before evidence staging:85bd4cfa8816736231449abeb985a5b573fb79ab.
Conflicts resolved:0. Business-source corrections:none. Original24 DEV2 paths are unchanged.122 staged integration paths remain upstream-owned; PR63 contributes two commits/12 paths since8fd30f2.
Final evidence:168 paths. Proposed PR delta:192 paths (24original + evidence), exact list/hashes in05_EXACT_DIFF_AND_HASHES.json. Host must verify actual published PR paths after push.

Program.cs retains exactly3 additive DEV2 scoped registrations plus integration services. DI16 PASS verifies reader/CurrentUser/session/permission resolution and trusted Asset/Incident authority. User-first/Branch/resource locks, caller transactions/final authorization, inactive/unscoped responsibilities, fingerprints and unknown-policy/CanDeactivate=false gates remain. UC138 controller remains NonController; Save/Assign gates immutable/pre-SQL; Workday writes and Notification producer remain OFF.

Validation on the exact combined source: build Passed, exit0,0errors,2NU1900 warnings. Backend/DI593 PASS; host frontend95 PASS (API27, secure browser7, preview browser12, preview rerun49). Total688 final successful checks,0FAIL/0SKIP. BoundaryTests discover67; solution-level test discovers none and is excluded. Historical sandbox bundling and host missing-entry failures are retained, not counted again. Canonical production entry and inputs match staged source; no production source was changed to resolve scratch setup.

Evidence:docs/integration-notes/PR25-FINAL-COORD-001/; final commands/exit/counts/SHA256 in receipts/upstream-00bdc481/verification_summary.json and05_EXACT_DIFF_AND_HASHES.json. Prior535 results remain tied to previous tree14f4595 and are not used as fresh evidence. NuGet vulnerability metadata unavailable;3 existing EF20602 index warnings documented, mappings unchanged.

Historical DEV3 acceptance:https://github.com/tuananh65/HotelOps/pull/25#pullrequestreview-5824903849.
New DEV3 final review:PENDING_NOT_SENT until successful publication.
SQL acceptance19 NOT_RUN. Business activation BLOCKED. No SQL, migrations, deployment, runtime lease, business-complete claim, Workday/Notification activation or PR51 merge.
Recommendation:source validated and host commit ready; source-only merge decision remains with PL after publication verification and DEV3 final approval. Do not self-merge.

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
