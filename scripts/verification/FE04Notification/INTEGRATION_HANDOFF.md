# FE04-NOTIF-INTEGRATE-001 — source composed, runtime acceptance pending

Base develop: `bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c`. DEV2 package SHA256: `e8c636faa1ed4451db5d26565bfe5fca9e3237223a02fb8a117050dc72e78be7`; source identity `b7b4dbf1232a4d8988d868197f754efc5ecb2cfd92714755ce1f876c39629e2d` is a package content identity, **not a Git implementation commit**. Frozen N01 identity: `30bf39ba76e793bab9ad1777a5563d593b6f37239316b0877bcc011c50217392`.

591/591 package entries, 476/476 source files, source identity algorithm and 15-path Cleaning delta verified. `DEV2_IMPORT_PROVENANCE.json` records the exact byte-identical import closure: 15 Cleaning delta paths plus 26 N01/shared foundation dependency paths. No standalone Program/appsettings/generated DbContext replacement or whole-source import. The additive MaintenanceWorkAccessReader is a compile dependency of the imported shared source policy; no maintenance authorization resolver/registration or permissions were changed. NotificationIntent is an additive candidate entity/mapping, not an edit to generated entities.

## Executable caller composition

Program uses `AddHotelOpsFe04CleaningNotificationConsumer`, which registers `AddHotelOpsNotifications` and `AddHotelOpsCleaningNotificationCompatibility`. The same singleton NotificationCommitGuard is attached to the caller DbContext alongside the existing CandidateCommitGuard and CandidateCommandGuard. The new mapping is called from the existing handwritten model partial; generated mappings remain intact. No worker/profile/producer is enabled by this composition.

In `AssignCleaningCandidateCoreAsync`, after retained BatchAsync actor/session/branch/catalog authorization and task scope lookup, EnterAsync captures the exact physical transaction before native or EF mutation. The command uses the existing FS013 fingerprint. Replay verifies the original graph and lossless owner receipt, then returns the original CandidateAssignmentResult without mutation.

Native SQL: after AssignWithEvidenceAsync has persisted its assignment/audit/result/operation/publication/intent graph, StageAsync adopts and verifies that graph; it does not insert another graph. EF: after assignment/task transition/audit persistence, only DEV2 StageAsync is called; the old provisional StageAsync is removed from this call site. Operation/publication/assignment identities and durable result remain FE04-owned. The receipt adapter preserves all fields and replay JSON.

Before commit, existing DueHousekeeper, candidate verifier and mutation guard remain; NotificationTransactionParticipant.VerifyBeforeCommitAsync runs on the exact same connection/transaction. Staging/verification failures unwind BatchAsync and rollback before known errors are mapped, including recovery-path notification errors. Unknown integrity faults propagate with rollback. Notification guards remain active at EF async commit; there is one caller commit. Existing FS013 AvailableAsync, Work-Time, session, schema and production gates are unchanged.

## Source verification and its limits

- Exact consumer build: PASS, 0 warnings/errors. Final DEV1 xUnit caller/DI/default-off/replay/guard regression: 55 PASS, 0 FAIL, 0 SKIP. Cleaning NoSQL: 74 PASS; N01 NoSQL: 79 PASS, both rebuilt against this consumer. No standalone 197-check receipt is substituted for consumer or live SQL evidence. Local receipts are in the separate `obj/fe04-notif-handoff` directory. API-only solution `dotnet test` exits 0 and is not an additional regression count.
- NoSQL positive participant tests use explicit in-memory stores/authorization and stub provider transactions. They prove orchestration/EF interception, not persisted SQL rollback, locking, delivery, Inbox or business acceptance.
- Default registration is CleaningNotificationDisabled; N01 source/coordination remain unaccepted. NotificationDelivery worker is not registered with the existing default configuration. Public UC029 remains unavailable without accepted prerequisites. UC032 still needs legitimate assignment plus IN_PROGRESS through the authoritative UC031 flow; no SQL prerequisite fabrication is allowed.

## SQL candidate chain — NOT_RUN

Original DEV2 scripts are imported unchanged:

1. `scripts/database/notifications/N01Implement06/01_ForwardCandidate.sql` adds N01 metadata/typed arms to the existing FS013/FS016 graph; no second queue/operation graph.
2. `scripts/database/notifications/Fe04UnblockP0/01_ReconcileCandidate.sql` changes UX_fs009_publication_operation to a unique filtered CLEANING_TASK_ASSIGNED index, retaining its name, and reconciles TR_fs009_publication_source for Cleaning/N01.
3. Recovery order: Fe04UnblockP0/02_RollbackCandidate.sql, then N01Implement06/02_RollbackCandidate.sql, only if data-preservation checks pass. Never delete publications/operations to make rollback pass.

These are review-only candidates restricted to **KIENPC / HotelOps_DEV2_FullStack_Trial**. No compatible installer for a different target is asserted: no Notification runtime target/lease has been handed off. Do not retarget literals or fabricate ownership markers to run them on ENV-001, R02 authority trial or shared SQL.

Required preflight before preparing an exact target installer:

- Runtime/schema owner names the approved isolated instance/database and least-privilege runtime identity, provides exclusive central lease/expiry and owned-fixture inventory; production/shared targets forbidden. Recheck identity, ownership and lease before every mutation.
- Verify real index key columns, uniqueness/filter/disabled state for UX_fs009_publication_operation, PK_notification_operation_v1, UQ_delivery_publication_recipient_v1 and UX_fs015_occurrence; inspect existing operation/publication/intent data and duplicate operation groups without rewriting history.
- Verify existing FS013/FS016/N01 markers, columns, trusted CHECK/FK constraints and enabled immutability triggers. Reconcile a legitimate target with both DEV1 AvailableAsync and DEV2 store/runtime target guards; never remove either guard to force acceptance.
- Capture exact prior TR_fs009_publication_source definition and SHA256 in owner-preserved recovery artifacts. Forward candidate requires matching HotelOps.FE04.PreviousTriggerSha256 session context; mismatch/no-overwrite must abort.
- Rollback candidate currently contains a hard-coded prior trigger body. Owner must compare it with captured prior bytes/semantics before approving an exact recovery installer; do not run it blindly when prior definition differs. Refuse rollback if N01/multi-event history would be stranded.
- Verify no loss of original CLEANING_TASK_ASSIGNED revision/deadline guard and N01 per-event source checks, and that N01 metadata/receipt fields retain their separate existing ownership.

## Exact remaining source/runtime gaps and owners

1. **DEV1/runtime owner:** ProvisionalCleaningNotification.AvailableAsync restricts FS013 to the original canonical SQLEXPRESS target; DEV2 CleaningAssignmentStore.EnsureAsync and CleaningNotificationRuntime.RequireClean/IntegrationSqlGuard restrict their source to KIENPC/HotelOps_DEV2_FullStack_Trial. There is currently no approved Notification target satisfying the composed gates. Keep closed; wire a reviewed common target contract only when the runtime handoff exists.
2. **DEV2 schema owner:** Fe04UnblockP0/01_ReconcileCandidate.sql trigger first branch applies assignment checks to every event except inspection.failed/cleaning.rework.requested. Consequently an FS016 REWORK_SUBMITTED publication with assignment_id NULL would fail that branch. Expected future behavior: preserve validated per-family FS016 source binding, without permitting arbitrary events. Secondary producers remain OFF; this is not fixed by changing DEV1 writers or widening the trigger locally.
3. **DEV2 schema owner:** Fe04UnblockP0/02_RollbackCandidate.sql hard-coded restore must be reconciled with the actual captured prior trigger; required exact file/hunk is its CREATE OR ALTER TRIGGER block. Expected behavior: verified restoration without losing prior source checks or stranding existing data. No shared DDL was executed.
4. **Runtime owner + DEV1/DEV2:** all 15 SQL cases, positive delivery/Inbox/retry, authorization/revocation orderings, SQL1205 observation and cleanup require the authorized target. User confirmed no such handoff exists. All SQL/HTTPS results remain NOT_RUN.

Exact DEV2 trigger hunk (`Fe04UnblockP0/01_ReconcileCandidate.sql:28`):

```sql
IF EXISTS(SELECT 1 FROM inserted p
 WHERE p.event_type NOT IN('inspection.failed','cleaning.rework.requested')
 AND NOT EXISTS(/* cleaning_assignments source binding */))
```

This negative family selector also matches REWORK_SUBMITTED. DEV2 should preserve separate valid source checks for that existing family before activating it; DEV1 does not patch the upstream source or activate secondary producers. Recovery hunk is `Fe04UnblockP0/02_RollbackCandidate.sql:15` (hard-coded CREATE OR ALTER TRIGGER body).

## SQL acceptance execution plan

`SQL_ACCEPTANCE_NOT_RUN.json` enumerates all 15 cases. Execute against this consumer build, with real actor/session and owned task/assignment/notification fixtures only. Record both relevant concurrent orderings, original and final row counts/IDs/hashes, audit/operation/publication/intent graph, durable result and lease checks. Any observed SQL1205 is FAIL even if the API returns Unavailable; do not count recovery as an acceptance pass. Exercise assignment rollback and required-stage failure, same-key retry/conflict, concurrent same/conflicting commands, cancellation/supersession, recipient revocation/branch isolation, delivery retry and Inbox read authorization, transaction guard and owner-approved cleanup. Preserve unrelated fixtures and immutable operation history.

Only after the SQL matrix passes, run real HTTPS/API UC029 smoke with SQL/audit/readiness verification. UC032 write follows only a genuine assigned IN_PROGRESS task after UC031 gates are met. No UC029/UC032 ACCEPTED claim or Tester handoff follows from source/NoSQL completion.
