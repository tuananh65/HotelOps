# FS008 changeset / review inventory

Baseline: ac0780105503291386ffa39858b0fc18ae80dde6, feature/dev1-room-ops.

Review commits:
- 03f0867: UC028 transactional create/options/current-cycle/event/audit, narrow explicit-recipient source read, UC029 dependency fail-closed, feature fingerprint, lifecycle SQL and unit tests.
- 6d344dc: real Create and source-header frontend, exact shared transport extraction, independent assignment command preparation, Node/interception/live tests.
- 8d1010e: isolated synthetic SQL/DPAPI/TLS harness, guarded three-table Notification proposal and six rollback-only constraint checks (not runtime acceptance).
- Documentation commit following these groups: original provided DEV2 handoff preserved, integration reconciliation, module reports, exact sanitized evidence and manifests.

Source file inventory (A=new, M=modified; generated/private files excluded):

```text
M	frontend/playwright.dev1.config.ts
A	frontend/playwright.fs008-live.config.ts
A	frontend/src/api/dev1/cleaningAssignmentCommand.ts
A	frontend/src/api/dev1/cleaningCreate.ts
M	frontend/src/api/dev1/cleaningProgress.ts
M	frontend/src/api/dev1/client.ts
A	frontend/src/screens/housekeeping/AssignedCleaningHeader.tsx
M	frontend/src/screens/housekeeping/CleaningTaskDetailScreen.tsx
M	frontend/src/screens/housekeeping/CleaningTaskListScreen.tsx
M	frontend/src/screens/housekeeping/CreateCleaningTaskScreen.tsx
A	frontend/tests/cleaning-assignment-command.test.mjs
M	frontend/tests/cleaning-assignment.spec.ts
A	frontend/tests/cleaning-create.spec.ts
A	frontend/tests/cleaning-create.test.mjs
A	frontend/tests/fs008-live.spec.ts
A	scripts/database/release/cleaning-lifecycle/01_Install.sql
A	scripts/database/release/cleaning-lifecycle/02_NotificationProposal.sql
A	scripts/database/release/cleaning-lifecycle/03_VerifyNotificationProposal.sql
A	scripts/database/release/cleaning-lifecycle/04_RecoveryNotificationProposal.sql
A	scripts/database/release/cleaning-lifecycle/05_VerifyLifecycle.sql
A	scripts/database/release/cleaning-lifecycle/06_RecoveryLifecycle.sql
A	scripts/verification/FS008/.gitignore
A	scripts/verification/FS008/FS008.Verification.csproj
A	scripts/verification/FS008/Install-Lifecycle.ps1
A	scripts/verification/FS008/New-ApprovedTrial.ps1
A	scripts/verification/FS008/Program.cs
A	scripts/verification/FS008/Start-LiveEnvironment.ps1
A	scripts/verification/FS008/Stop-LiveEnvironment.ps1
A	scripts/verification/FS008/Test-NotificationProposal.ps1
A	src/HotelOps.Api/Controllers/CleaningTaskLifecycleController.cs
A	src/HotelOps.Api/DTOs/Housekeeping/CleaningTaskCreationDtos.cs
A	src/HotelOps.Api/Services/Housekeeping/AssignedCleaningReadService.cs
A	src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentCommandIdentity.cs
A	src/HotelOps.Api/Services/Housekeeping/CleaningTaskCreationService.cs
M	src/HotelOps.Api/Services/Housekeeping/HousekeepingAssignmentsService.cs
A	tests/HotelOps.Dev1.RegressionTests/CleaningCreationTests.cs
```

Documentation/evidence inventory is the docs/fs008 tree, docs/integration-notes/DEV1_FS008.md and original docs/external-handoff/DEV2_FS007_UC029_AUTH_01.md. SOURCE_MANIFEST.json records exact normalized Git blobs plus original worktree SHA256. Mandatory accepted Notification executable source still missing; no accepted publisher/result interface invented. No ledger credit, PR, merge or shared deployment.
