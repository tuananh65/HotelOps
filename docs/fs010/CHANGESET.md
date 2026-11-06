# Changesets

- ad30eb0ea671518ff8469d06ab2e64362eddc4f7: runtime commit guard/recovery + xUnit (7 files).
- 9778872658737777184a2b2eae10732cce7c466f: isolated SQL/harness/browser/restart/workbook generation (30 files).
- Documentation/evidence/XLSX follow in a separate commit. Exact final HEAD/remote verified after push in final response; no self-referential commit SHA fabricated.

Isolation requires FS010-specific SQL/harness copies; FS009 source/artifacts and trial remain untouched. No deletion, generated EF edit, role/permission change or production activation. No separate application architecture project.

Files modified under src:

- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs
- src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentCandidateService.cs
- src/HotelOps.Api/Services/Housekeeping/CleaningTaskCreationService.cs
- src/HotelOps.Api/Services/Notifications/CandidateCommitGuard.cs
- src/HotelOps.Api/Services/Notifications/ProvisionalCleaningNotification.cs

All changed/created artifacts versus baseline (ignored private/bin/obj excluded):

- docs/fs010/A01_A02_RECONCILIATION.md
- docs/fs010/ACTUAL_RESTART_RESULTS.json
- docs/fs010/AMBIGUOUS_COMMIT_RESULTS.md
- docs/fs010/BASELINE.json
- docs/fs010/C01_COMMIT_GUARD.md
- docs/fs010/CHANGESET.md
- docs/fs010/COMMIT_FAULT_RESULTS.json
- docs/fs010/DEFAULT_OFF_RESULTS.json
- docs/fs010/FINAL_REPORT.md
- docs/fs010/FULL_SUITE_REPEATABILITY.md
- docs/fs010/HELPER_TIMINGS.jsonl
- docs/fs010/LIVE_RESULTS.json
- docs/fs010/LIVE_RUN_01_TLS_FAILED.json
- docs/fs010/MODULE_MERGE_READINESS.md
- docs/fs010/NEXT_ACTIONS.md
- docs/fs010/NOTIFICATION_RUNTIME_RESULTS.md
- docs/fs010/NOTIFICATION_SQL_RESULTS.json
- docs/fs010/PHYSICAL_GUARD_RESULTS.json
- docs/fs010/README.md
- docs/fs010/REPEATABILITY.json
- docs/fs010/SCHEMA_SAFETY_RESULTS.json
- docs/fs010/SECRET_SCAN.json
- docs/fs010/SOURCE_MANIFEST.json
- docs/fs010/SOURCE_MANIFEST_BASE.json
- docs/fs010/SQL_LIVE_ACCEPTANCE.md
- docs/fs010/TESTER_HANDOVER_STATUS.md
- docs/fs010/UC028_UC029_REGRESSION.md
- docs/fs010/VALIDATION.json
- docs/fs010/defaultoff_DEFAULT_OFF_RESULTS.json
- docs/fs010/run02_LIVE_RESULTS.json
- docs/fs010/run03_LIVE_RESULTS.json
- docs/fs010/run04_LIVE_RESULTS.json
- docs/integration-notes/DEV1_FS010.md
- docs/test-handover/FE-04/KNOWN_ISSUES.md
- docs/test-handover/FE-04/TESTER_FEEDBACK_TEMPLATE.md
- docs/test-handover/FE-04/TESTER_TEST_CASES.xlsx
- docs/test-handover/FE-04/TEST_ENVIRONMENT.md
- docs/test-handover/FE-04/TEST_EVIDENCE/WORKBOOK_VALIDATION.json
- docs/test-handover/FE-04/TEST_EVIDENCE/fs010.trx
- docs/test-handover/FE-04/TEST_EXECUTION_GUIDE.md
- docs/test-handover/FE-04/TEST_HANDOVER_README.md
- docs/test-handover/FE-04/UC_TEST_COVERAGE_MATRIX.md
- docs/test-handover/FE-04/UNIT_TEST_CASES.xlsx
- docs/test-handover/FE-04/UNIT_TEST_REPORT.md
- frontend/playwright.fs010-default-off.config.ts
- frontend/playwright.fs010-live.config.ts
- frontend/playwright.fs010-restart.config.ts
- frontend/tests/fs010-default-off.spec.ts
- frontend/tests/fs010-live.spec.ts
- frontend/tests/fs010-restart.spec.ts
- scripts/database/release/fs010-candidate/01_Install.sql
- scripts/database/release/fs010-candidate/02_NotificationProposal.sql
- scripts/database/release/fs010-candidate/03_VerifyNotificationProposal.sql
- scripts/database/release/fs010-candidate/04_RecoveryNotificationProposal.sql
- scripts/database/release/fs010-candidate/05_VerifyLifecycle.sql
- scripts/database/release/fs010-candidate/06_RecoveryLifecycle.sql
- scripts/database/release/fs010-candidate/07_ExecutableCandidate.sql
- scripts/database/release/fs010-candidate/08_VerifyCandidate.sql
- scripts/database/release/fs010-candidate/09_RecoveryCandidate.sql
- scripts/verification/FS010/.gitignore
- scripts/verification/FS010/Build-TestHandover.cjs
- scripts/verification/FS010/CommitProof.cs
- scripts/verification/FS010/FS010.Verification.csproj
- scripts/verification/FS010/Install-Candidate.ps1
- scripts/verification/FS010/Install-Lifecycle.ps1
- scripts/verification/FS010/New-ApprovedTrial.ps1
- scripts/verification/FS010/PhysicalGuardProof.cs
- scripts/verification/FS010/Program.cs
- scripts/verification/FS010/Run-LiveSuite.ps1
- scripts/verification/FS010/Start-LiveEnvironment.ps1
- scripts/verification/FS010/Stop-LiveEnvironment.ps1
- scripts/verification/FS010/Test-NotificationProposal.ps1
- scripts/verification/FS010/Test-SchemaSafety.ps1
- scripts/verification/FS010/Verify-Candidate.ps1
- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs
- src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentCandidateService.cs
- src/HotelOps.Api/Services/Housekeeping/CleaningTaskCreationService.cs
- src/HotelOps.Api/Services/Notifications/CandidateCommitGuard.cs
- src/HotelOps.Api/Services/Notifications/ProvisionalCleaningNotification.cs
- tests/HotelOps.Dev1.RegressionTests/CandidateCommitGuardTests.cs
