# DEV2 PR25 — current integration reconciliation with UC138 source gates

DRAFT / NOT_PUBLISHED / FRONTEND_VERIFICATION_PENDING. Host must resolve frontend receipts before publishing this body.

This reconciliation preserves accepted UC006 inactive-branch responsibility coverage, UC016 Asset/Incident placement coordination, UC012/022/026 readers and FE12 Maintenance facts while retaining new DEV3 UC138 SecureShiftPreview and gated Save/Assign source.

Feature parent: 2140522457e89b974cf2d65c64515ec15d733fa9.
Integration parent: 00bdc481cb224d7117a79c95669abc7016f51ba4.
Verified staged source tree: 85bd4cfa8816736231449abeb985a5b573fb79ab.
Merge SHA/final feature HEAD: HOST_TO_FILL_AFTER_COMMIT_PUSH.
Conflicts:0. Production source corrections:none. Original24 PR blobs unchanged. Integration contributes122 staged paths vs original feature, including PR63 two-commit12-path drift from8fd30f2. Additional evidence paths/count are exact in05_EXACT_DIFF_AND_HASHES.json.

Program.cs retains3 DEV2 scoped additions and all integration registrations. DI16 PASS; Asset/Incident trusted authority and existing lock/session/transaction guards preserved. UC138 controller remains NonController; Save PendingReleaseGates immutable/pre-SQL; Workday writes/Notification producer OFF. DEV2 Maintenance adapter unchanged. No producer implementation, runtime grant, release or business completion is inferred.

Fresh build exit0,0errors,2NU1900 warnings. Fresh backend/DI593 PASS includes existing535 rerun plus SecureShiftPreview58. Boundary discovers67. Solution-level test exit0 discovers no tests and is excluded.
Frontend4 selected files currently exit1 before assertions because esbuild cannot read an ancestor directory in sandbox;0PASS/4failed files/0SKIP, first and second attempts retained. HOST_TO_REPLACE_WITH_REAL_HOST_RERUN_RESULTS_BEFORE_PUBLICATION. No old PR63 frontend330 result reused.

Evidence: docs/integration-notes/PR25-FINAL-COORD-001/receipts/upstream-00bdc481/verification_summary.json and05_EXACT_DIFF_AND_HASHES.json. Prior535 receipts tied to tree14f4595 remain historical, snapshot manifest hash2903d759931429874b2466265900fe0d7208518ab4d7f901cb45393cf80b9d3a.
NU1900 advisory checks unavailable;3 existing EF20602 index warnings documented; mappings unchanged.

Historical DEV3 acceptance: https://github.com/tuananh65/HotelOps/pull/25#pullrequestreview-5824903849.
New DEV3 final review:PENDING_NOT_SENT. Recommendation:HOLD until frontend verified, publication checked and DEV3 final source approval; PL retains merge decision.
SQL19 NOT_RUN; business activation BLOCKED. No live API/SQL/HTTPS/race/rollback acceptance, migrations, shared configuration/schema changes, PR25/51 merge or deployment.

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
