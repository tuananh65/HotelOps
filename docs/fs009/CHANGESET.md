# FS009 changeset / complete review inventory

Baseline 72b2a89160b56cb6855688a6f6d0df665ede5b16, branch feature/dev1-room-ops.

Review commits:

```text
ba1fadd95399d6d90e8b309e5ac858a8a919e28a test(fs009): verify live assignment replay security rollback and delivery recovery
8f6e80a2f243eb21cebd434d439b7acf5f185d72 feat(frontend): harden cleaning creation and gate assignment by server capability
8643753c44d9346007fb67f935aaf6d1906da1dc feat(housekeeping): add isolated provisional notification-backed assignment
```

The next documentation commit contains exact original/intermediate/final evidence, source hashes, integration reconciliation and module assessment. Four logical groups; no accepted A01 claim, shared merge, production DDL or UC credit.

Implementation inventory (A=new, M=modified):

```text
A	frontend/playwright.fs009-default-off.config.ts
A	frontend/playwright.fs009-live.config.ts
M	frontend/src/api/dev1/cleaningAssignment.ts
M	frontend/src/api/dev1/cleaningAssignmentCommand.ts
M	frontend/src/api/dev1/cleaningCreate.ts
M	frontend/src/screens/housekeeping/CleaningAssignmentScreen.tsx
M	frontend/src/screens/housekeeping/CreateCleaningTaskScreen.tsx
M	frontend/tests/cleaning-assignment-command.test.mjs
M	frontend/tests/cleaning-assignment.spec.ts
M	frontend/tests/cleaning-create.spec.ts
A	frontend/tests/fs009-default-off.spec.ts
A	frontend/tests/fs009-live.spec.ts
A	scripts/database/release/fs009-candidate/01_Install.sql
A	scripts/database/release/fs009-candidate/02_NotificationProposal.sql
A	scripts/database/release/fs009-candidate/03_VerifyNotificationProposal.sql
A	scripts/database/release/fs009-candidate/04_RecoveryNotificationProposal.sql
A	scripts/database/release/fs009-candidate/05_VerifyLifecycle.sql
A	scripts/database/release/fs009-candidate/06_RecoveryLifecycle.sql
A	scripts/database/release/fs009-candidate/07_ExecutableCandidate.sql
A	scripts/database/release/fs009-candidate/08_VerifyCandidate.sql
A	scripts/database/release/fs009-candidate/09_RecoveryCandidate.sql
A	scripts/verification/FS009/.gitignore
A	scripts/verification/FS009/FS009.Verification.csproj
A	scripts/verification/FS009/Install-Candidate.ps1
A	scripts/verification/FS009/Install-Lifecycle.ps1
A	scripts/verification/FS009/New-ApprovedTrial.ps1
A	scripts/verification/FS009/Program.cs
A	scripts/verification/FS009/Start-LiveEnvironment.ps1
A	scripts/verification/FS009/Stop-LiveEnvironment.ps1
A	scripts/verification/FS009/Test-NotificationProposal.ps1
A	scripts/verification/FS009/Verify-Candidate.ps1
M	src/HotelOps.Api/Controllers/CleaningTaskLifecycleController.cs
M	src/HotelOps.Api/Controllers/HousekeepingWorkflowController.cs
M	src/HotelOps.Api/DTOs/Housekeeping/CleaningTaskCreationDtos.cs
M	src/HotelOps.Api/DTOs/Housekeeping/HousekeepingPlanningDtos.cs
A	src/HotelOps.Api/Data/HotelOpsDbContext.NotificationCandidate.cs
M	src/HotelOps.Api/Data/HotelOpsDbContext.RoomTypes.cs
A	src/HotelOps.Api/Models/NotificationCandidate.cs
M	src/HotelOps.Api/Program.cs
M	src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs
M	src/HotelOps.Api/Services/HotelStructure/RoomTypeCreationService.cs
M	src/HotelOps.Api/Services/Housekeeping/AssignedCleaningReadService.cs
A	src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentCandidateService.cs
M	src/HotelOps.Api/Services/Housekeeping/CleaningTaskCreationService.cs
M	src/HotelOps.Api/Services/Housekeeping/HousekeepingPlanningService.cs
A	src/HotelOps.Api/Services/Notifications/ProvisionalCleaningDelivery.cs
A	src/HotelOps.Api/Services/Notifications/ProvisionalCleaningNotification.cs
A	tests/HotelOps.Dev1.RegressionTests/NotificationCandidateTests.cs
```

New documentation/evidence inventory:

```text
docs/fs009/A01_A02_RECONCILIATION.md
docs/fs009/B01_B02_C01_IMPLEMENTATION.md
docs/fs009/BASELINE.json
docs/fs009/CHANGESET.md
docs/fs009/DEFAULT_OFF_RESULTS.json
docs/fs009/FRONTEND_E2E.md
docs/fs009/IDEMPOTENCY_CONCURRENCY.md
docs/fs009/LIVE_ACCEPTANCE_SUMMARY.json
docs/fs009/LIVE_EXPANDED_RUN.json
docs/fs009/LIVE_RESULTS.json
docs/fs009/LIVE_TERMINAL_RERUN.json
docs/fs009/LIVE_UI_FINAL.json
docs/fs009/MERGE_READINESS.md
docs/fs009/MODEL_SQL_PARITY.json
docs/fs009/MODULE_PROGRESS.md
docs/fs009/NEXT_ACTIONS.md
docs/fs009/NOTIFICATION_SQL_RESULTS.json
docs/fs009/NOTIFICATION_TRANSACTION.md
docs/fs009/README.md
docs/fs009/REGRESSION.md
docs/fs009/ROUTE_INVENTORY.md
docs/fs009/SECRET_SCAN.json
docs/fs009/SOURCE_MANIFEST.json
docs/fs009/SOURCE_MANIFEST_BASE.json
docs/fs009/SOURCE_READ_SECURITY.md
docs/fs009/SQL_ACCEPTANCE.md
docs/fs009/UC028_ACCEPTANCE.md
docs/fs009/UC029_EXECUTABLE_STATUS.md
docs/fs009/VALIDATION.json
docs/fs009/VERIFY_SQL_RESULTS.json
docs/integration-notes/DEV1_FS009.md
```

SOURCE_MANIFEST.json records normalized Git blobs and original worktree SHA256 for all48 implementation/SQL/harness/test files. SECRET_SCAN.json checks actual synthetic passwords in memory plus token/private-key patterns; no private artifacts tracked. Source inventory and documentation together cover79 files (19 modified,60 added).

Build PASS0warnings; actual xUnit119/119; frontend build PASS, Node46/46, interception47/47. Enabled live12 distinct cases have latest PASS across full10/11 plus documented focused reruns/additional checks; not one uninterrupted12/12. Separate default-off1/1. Read LIVE_ACCEPTANCE_SUMMARY.json/VALIDATION.json for precise scope and failures. Candidate/shared acceptance remains pending.
