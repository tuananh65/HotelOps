# [HotelOps DEV1 FE04] FS-020 — PARTIAL

UC029 adapter implemented in DEV1 ownership; production producer/worker remain OFF. No shared acceptance or COMPLETE_VERIFIED claim. Runtime integration is blocked by the exact 06R1 incompatibilities below. This checkpoint does not request another A01/A02 review or a repackaging.

## Source and scope

- Branch `feature/dev1-room-ops`, starting HEAD `06372a2a9e7889a9e90aa341f3c15a66cb3bcee4`, clean preflight. No branch/index/commit/push/merge operations.
- Input `C:/Users/Hax/Downloads/DEV1-NOTIFICATION-06R1-REVIEW-ONLY.zip`, ZIP SHA256 `73ce6eed207d9b688e353316424d11fe943f417532789eb6ddf83b02434c42f9`.
- Package reports original 402-file identity `d647a63f41fb8e7d5a4d1d51914195c27cecd8a670fd99f5c16aac1f48a509e4`. The delivered minimal 24-file derivative has its own identity `e969fa5f5efbb4d3c9921d1577bd3ec270095ecffd45cc42b55c4b4798d701d6`; these identities are not interchangeable. Only the supplied seal and 14 core/dependency file hashes are independently verified here, not the absent full 402-file closure.
- Read UC029 SRS and Change Notes. Existing CLEANING_VIEW recipient evaluator is reused; no pending Reception/Department Head permission assumptions. No schema, generated entities, shared configuration, Program, controllers, FS013 graph or DEV2 source changes.

## Changed files

1. `src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentNotificationSource.cs`: read-only persisted FS013 projection, graph validation, content and explicit-user source READ.
2. `src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentNotification06R1Adapter.cs`: actual `INotificationEventProfile` and caller-owned `NotificationTransactionParticipant` bridge, compiled only with `HOTELOPS_NOTIFICATION_06R1` in disposable composition.
3. `tests/HotelOps.Dev1.RegressionTests/CleaningAssignmentNotificationTests.cs`: persisted graph, replay identity and invalidation tests.
4. `tests/HotelOps.Dev1.RegressionTests/CleaningAssignmentNotification06R1Tests.cs`: actual DEV2 publisher/policy/participant/guard integration using a no-SQL connection. Expected incompatibilities are asserted, not disguised as successful delivery.
5. `scripts/verification/FS020/Test-NotificationAdapter.ps1`: sealed package reconstruction, exact core SHA checks, disposable mapping hook, build and focused no-SQL integration.
6. This report.

## Adapter, occurrence and READ

OccurrenceId is the persisted positive `cleaning_assignments.assignment_id` (long). It is never derived from the operation GUID by hashing/truncating. `CLEANING_TASK_ASSIGNED`, version 1, reuses the existing FS013 event identity. Full operation/publication GUIDs, fingerprint/version, durable result, source revision, original recipient and task/cycle/room/branch remain in the original graph.

The loader joins existing assignment/task/room/current-cycle/publication/operation and checks linked audit and IN_APP intent. Payload and durable result must match persisted identities. New staging additionally requires ASSIGNED and exact current rowversion/due time. Ended/superseded assignment, cancellation, changed recipient/branch, inactive room or a different current cycle invalidates the occurrence. It cannot be retargeted to a new assignee.

Source READ calls existing `CanReadAssignedCleaningAsync` with the persisted recipient and CLEANING_VIEW, never producer authority or CLEANING_EXECUTE. It is valid in ASSIGNED and does not require IN_PROGRESS. Historical occurrence content stays immutable; later ordinary progress revision/due changes do not by themselves revoke READ while the same assignment remains active/current. Cancellation/reassignment/current-cycle changes still revoke. ReadAt is inbox metadata, not business acknowledgement. This evaluator is ready for integration but **is not plugged into A02 list/count/detail/read**, because 06R1 has no Cleaning policy extension point.

## Transaction, rollback and replay limits

The bridge checks the exact scoped physical connection/transaction, validates actor/operation/fingerprint against persisted command evidence, then calls the existing DEV2 participant. It does not create/flush/commit a transaction. Pre-validation exceptions poison the current transaction through the same DEV2 guard; participant/publisher failures propagate. Caller must flush required intents, verify before commit and commit asynchronously with both existing FS013 guards and the DEV2 guard retained.

Existing UC029 command authorization, operation lookup, fingerprint conflict, durable result replay, audit and SQL write boundary are untouched. No second publisher or ledger is introduced. The live call site is intentionally not rewired: doing so would either add C04 intents beside FS013 or replace the unapproved graph. No successful combined rollback/replay/SQL durability claim is made. No-SQL tests prove guard control flow/provider commit refusal, not SQL Server atomicity or unique-key concurrency.

## Exact external blockers

Paths are in the delivered 06R1 core under `src/HotelOps.Api/`:

| Source | Concrete incompatibility | Evidence / boundary |
| --- | --- | --- |
| `Services/Notifications/NotificationPublisher.cs:42` | Hard-coded `/maintenance/tasks/{SourceId}` rejects the genuine Cleaning action path before policy/SQL. | Actual publisher test expects NotificationContractUnavailableException, then guard rejects verify/commit. Cannot safely fix by falsifying a Maintenance URL in DEV1. |
| `Services/Notifications/NotificationSourcePolicy.cs:21,28` | Sealed concrete policy only handles maintenance.view.v1/Maintenance sources, including inbox visibility query. | Actual AllowsAsync test returns false for Cleaning policy without contacting SQL. DEV1 cannot extend delivery and list/count/detail/read through a supplied interface. |
| `Services/Notifications/NotificationDelivery.cs:38` | Inbox SourceType is always maintenance_work. | Source finding; no successful Cleaning delivery test claimed. |
| `Services/Notifications/NotificationPublisher.cs` and `Data/HotelOpsDbContext.Notifications.cs` | Writes notification_intents_c04; participant accepts NotificationEvent only, not existing operation/publication/intent graph. | Preserving FS013 forbids substituting C04 or adding a competing graph. Approved compatible graph integration is needed before wiring the live producer. No DDL run. |

No DEV2 files were edited and no blocker message was sent externally. Final-source DEV2 SQL/HTTPS receipts remain a full-acceptance dependency, not the reason adapter implementation stopped.

## Validation

Results are recorded after the commands complete. No shared SQL, DDL, HTTPS, worker activation or production flags are used by these commands.

```powershell
dotnet build HotelOps.slnx
dotnet build HotelOps.slnx --artifacts-path artifacts/fs020
dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --artifacts-path artifacts/fs020 --filter 'FullyQualifiedName~CleaningAssignmentNotification|FullyQualifiedName~NotificationCandidateTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/fs020
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verification/FS020/Test-NotificationAdapter.ps1 -Package C:/Users/Hax/Downloads/DEV1-NOTIFICATION-06R1-REVIEW-ONLY.zip
```

Default build and initial focused run encountered denied write to the pre-existing `obj/Debug/net10.0/HotelOps.Api.sourcelink.json`. No ACL changes/deletions were made. Retry solution build using separate artifacts: **Passed, 0 errors, 2 NU1900 warnings** (NuGet vulnerability feed unavailable). Disposable solution build: **Passed, 0 errors, 2 NU1900 warnings**.

DEV1 focused run: **34 Passed / 0 Failed / 0 Skipped** (25 new source-rule cases + 9 existing NotificationCandidate cases). Disposable 06R1 integration: **31 Passed / 0 Failed / 0 Skipped** (25 source-rule cases + 6 actual DEV2/adapter cases). These runs overlap; no combined unique test count is claimed. No-SQL assertions use an in-process DbConnection that throws on SQL command creation, with real EF transaction interceptor callbacks; they do not connect to SQL Server.

Event profile and transaction adapter are separate to avoid a publisher→profile→participant→publisher DI cycle. Final source passed both solution builds with `--no-restore` (one NU1900 warning each, zero errors) and disposable focused tests **31/31 Passed**. All 14 DEV2 core files and 4 DEV1 source/test files were SHA-compared after the final run: zero drift. Other tracked files remain unchanged; six new files only. Branch/HEAD unchanged.

Receipts: `TestResults/fs020/focused.trx`, `TestResults/fs020/integration-final.trx` (local ignored). Original disposable workspace: `C:/Users/Hax/AppData/Local/Temp/hotelops-fs020-5441673835684953a425ca37ce5c1891`; receipts under its `receipts/` folder. Reconstruction script supports Windows PowerShell 5.1. NU1900 remains unresolved; no vulnerability scan is claimed.

No endpoint contracts changed. Live API/HTTPS, SQL projection execution, database authorization/locking/concurrency, successful A01→A02 delivery/restart/revocation and full acceptance: **Not Run**. Solution contains the API project only; focused tests explicitly target the existing regression project. No full test sweep requested or run.

## Conditions for focused SQL/HTTPS acceptance

Resolve the concrete Cleaning path/policy/SourceType and FS013 graph integration blockers without editing DEV2 source in this lane. Then use the final compatible source, approved isolated target/schema and exclusive central lease, developer-local runtime secrets/TLS/fixtures, both transaction guards and worker OFF by default. Exercise assignment+audit+mandatory notification atomic rollback, same-command durable replay/conflict, source revision/current-cycle, cancellation/reassignment, grant revocation across delivery and inbox count/list/detail/read, retry/restart and SQL1205. Obtain final-source DEV2 SQL/HTTPS receipts separately. Shared database and production activation remain unauthorized. Stop at this checkpoint.
