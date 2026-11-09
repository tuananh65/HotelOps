# FE04-UNBLOCK-R01 — consumer implementation, isolated candidate

Base develop: `bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c`.
FE05 remote verified: `feature/dev1-fe05-failed-rework`, `d87d1b9008699d4315e4427317fb273bafdf8d33`.
That source deliberately blocks Reject/Request Rework, has no committed authority issuer/receipt,
and throws before any future partial commit. FE04 does not relabel its preview or raw FAILED rows as authority.

## Contract and exact wiring

`FE05_COMMITTED_V1`, contract/issuer version 1, issuer `FE05.UC045.UC046`.
Reuse the existing `fs016_rework_source_proofs` and `notification_operation_reservations` graph;
FE04 does not issue proofs, create an authoritative operation or implement another Inspection writer.

FE05 must implement a reviewed SQL domain writer `WITH EXECUTE AS OWNER`, authorizing its real actor,
validating the frozen Inspection/defects, performing authoritative FAILED/Rework transitions, and staging
the required notification in the caller's serializable transaction. The deployment owner pins that
module's object ID, SHA256 of its UTF-16 SQL definition, exact reviewed source commit and issuer version
in `fe04_rework_authority_issuers`. The registry is empty/inactive by default. A changed module hash
invalidates its authority. A row in the registry is a deployment trust decision, not a business receipt.

After flushing persisted selected defects/evidence, call:
`RoomTypeCreationService.BuildReworkAuthoritySnapshotAsync(connection, transaction, reworkId, privateEvidenceRoot, ct)`.
Use its Binding/SourceDigest/EvidenceDigest directly; do not reimplement or accept a client digest.
The digest includes the existing ReworkSourceDigestAsync output, ordered selected criterion/result/defect
facts, association identities, attachment metadata and SHA256 of each validated private file's bytes.
An inaccessible/changed/deleted/foreign evidence source fails closed. Immutable private storage remains required.

Persist the existing operation's completed result JSON with these fields, in the SAME FE05 transaction:

```text
contract=FE05_REWORK_AUTHORITY_V1, origin=FE05_COMMITTED_V1, version=1,
issuer=FE05.UC045.UC046, issuerVersion=1, actorUserId, operationId,
writerTransactionId=CURRENT_TRANSACTION_ID(), reworkId, inspectionId,
cleaningTaskId, roomId, branchId, versionId,
inspectionETag=quoted uppercase 16-character hex rowversion,
sourceDigest/evidenceDigest=uppercase 64-character hex SHA256,
conclusion=FAILED
```

Append exactly one `cleaning_reworks` audit with the same actor/branch/rework identity and
`operationId`, `authorityContract=FE05_REWORK_AUTHORITY_V1`, `sourceDigest` in new_values.
Insert the matching typed proof through that trusted owner module. Ordinary API SQL identity must have
no INSERT/UPDATE/DELETE rights on the proof/issuer registry, must not own them or be db_owner/sysadmin,
and needs read access plus VIEW DEFINITION on the pinned module. Elevated consumer identities fail closed.
The candidate DENYs direct public DML, binds operation FK and writer transaction, and makes real proofs immutable.

The writer owns commit/rollback. Consumer reads use serializable locks, a DIFFERENT transaction ID,
current-cycle/latest Inspection/revision binding, current worker CLEANING_VIEW/source ownership, and
before-commit revalidation. Missing/invalid issuer, receipt, audit, current source or digest is denied.
Source supersession, selected-defect/evidence mutation and grant revocation invalidate READ and commands.
Existing ExecuteReworkAsync transitions, ETag/fingerprint/replay guards and notification ledger are retained.
Real-source replay must retain the matching authority origin. The old synthetic trial lane stays separate;
it never becomes real authority. Shared/production opt-ins and workers remain OFF.

## Room projection and schema decision

FE05 UC044/045 must call `InspectionRoomProjectionAdapter.ApplyAsync` inside its domain transaction,
after domain state/mandatory results and a durable operation payload exist. The payload contract is
`FE05_INSPECTION_CONCLUSION_V1`, issuer `FE05.UC044.UC045`, issuerVersion 1, with inspectionId,
cleaningTaskId, roomId, branchId, inspectionETag and conclusion PASSED/FAILED. Pin that writer module
in the same registry. FE05 must use its existing durable replay path instead of invoking projection twice.

The adapter verifies exact scoped physical connection/serializable transaction, current source/revision,
receipt/module trust, required PASS criteria, open rework and current RoomBlocks. It writes room/readiness,
room history when changed, and projection audit together; no independent commit or second endpoint.
Caller must roll back the entire domain transaction on any participant failure. SQL batch uses XACT_ABORT.

`CK_rooms_operational_status` allows AVAILABLE/OCCUPIED/DIRTY/CLEANING/INSPECTION/READY/OUT_OF_SERVICE;
it does not allow BLOCKED or REWORK_REQUIRED. Projection keeps workflow and operational/readiness/block
separate: rejection preserves operational status and emits REWORK_REQUIRED readiness. Approval emits
READY only with validated terminal source, mandatory criteria, no open rework and no operational conflict.
Active block preserves operational status and dominates effective readiness as BLOCKED.
Neither an isolated Inspection status change nor a Rework transition can create READY.

`02_ProjectionCandidate.sql` adds READY to the existing readiness CK only, preserving other values.
If the team requires BLOCKED/REWORK_REQUIRED in **operational_status**, a separate cross-module schema
and transition decision is needed; this implementation does not silently map them to DIRTY or OUT_OF_SERVICE.
Both candidate SQL files require an explicitly approved disposable target marked
`HotelOps.FE04.UnblockR01.Owner=DEV1_DISPOSABLE`, with the existing FS016 substrate. The marker is a
deployment guard, not permission to use any database. No DDL/trial creation was executed in this task.
Existing exact FS016 runtime gates are unchanged; a new database name is not automatically authorized.

## Actual validation and limits

- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors. `dotnet test HotelOps.slnx --no-build --no-restore`:
  exit 0, API-only solution, not a regression count.
- Focused test project: 104 PASS / 0 FAIL / 0 SKIP; authority/projection contracts, transaction rejection,
  existing Rework/command/commit/authorization/model tests. These are unit/no-SQL tests, not persisted authority proof.
- Frontend build PASS; Node Rework 5/5 PASS; intercepted browser Rework 9/9 PASS. Existing React Router
  use-client and NO_COLOR/FORCE_COLOR warnings, npm transitive dependency deprecations remain.
- An intermediate build failed on a raw-string JSON fallback literal; fixed to a safe JSON array fallback,
  then final build and focused tests passed. No validation failure is hidden.
- SQL persistence, same/different-key concurrency, rollback/partial-record verification, active RoomBlock
  SQL projection and authoritative FE05 HTTPS/E2E: NOT_RUN. No explicit authorized disposable connection
  was supplied. Pure contract rejection tests do not prove SQL atomicity or real FE05 commit authority.
- Local ignored receipts: `TestResults/authority-r01/authority-r01-final.trx`, frontend test stdout/results.
  No tests mutate shared SQL; no source in FE05/DEV2, generated entity/DbContext, Program/DI, appsettings,
  role catalog, inventory or existing SQL candidate was modified. No PR/merge/activation is performed.

UC035/036 consumer source is implemented; acceptance is PARTIALLY_BLOCKED by the missing FE05 committed
writer/receipt/module wiring, mandatory notification acceptance, and authorized SQL verification.
Next exact action: wire the FE05 writer to this snapshot/receipt/proof and projection contract, pin the
final executable module/source identity, then run focused SQL/HTTPS on an approved disposable target.

## Exact changed files

```text
frontend/src/api/dev1/reworkExecution.ts
frontend/src/screens/housekeeping/ReworkExecutionPanel.tsx
frontend/tests/rework-execution.spec.ts
frontend/tests/rework-execution.test.mjs
scripts/database/release/fe04-authority-r01/01_AuthorityCandidate.sql
scripts/database/release/fe04-authority-r01/02_ProjectionCandidate.sql
scripts/database/release/fe04-authority-r01/README.md
src/HotelOps.Api/DTOs/Housekeeping/ReworkLifecycleDtos.cs
src/HotelOps.Api/Services/Housekeeping/InspectionRoomProjectionAdapter.cs
src/HotelOps.Api/Services/Housekeeping/ReworkAuthorityContract.cs
src/HotelOps.Api/Services/Housekeeping/ReworkAuthorityReader.cs
src/HotelOps.Api/Services/Housekeeping/ReworkLifecycleService.cs
src/HotelOps.Api/Services/Notifications/ProvisionalReworkNotification.cs
tests/HotelOps.Dev1.RegressionTests/InspectionRoomProjectionTests.cs
tests/HotelOps.Dev1.RegressionTests/ReworkAuthorityTests.cs
```
