# JOB06: approved preservation policy (2026-11-17)

This section supersedes the historical proposal below. Project Leader decision: ARCHIVE_RETENTION=365_DAYS from ArchivedAt; LEGAL_HOLD=BLOCK_PURGE; AUDIT_LOG_PURGE=DISABLED; RECEIPT_PURGE=DISABLED_BY_DEFAULT.

## Implemented audit storage

AuditPreservationV1.sql classifies audit records ARCHIVED_IN_PLACE in audit_archive_state. Original audit IDs, payloads, branch, actor and receipt foreign keys remain unchanged. ArchivedAt, system actor, reason, policy version and RetentionUntil (at least ArchivedAt + 365 days) form an immutable archive record. A transaction, exclusive job lease, source/key-range locks and primary key provide bounded idempotent batches. Old records stay readable through the same SYS_ADMIN + AUDIT_VIEW scope and redaction; no raw archive endpoint or new grant is introduced.

Audit update/delete and archive metadata update/delete are rejected by database triggers. New JSON audit payloads are checked recursively for credential properties. EF audit insertion uses trigger-compatible SQL. Original existing payloads are not rewritten. Historical SQL fixture writer fe05_reassign_reinspection V1 is incompatible with append-only audit; installer guards prevent combining them. Read-only sys.sql_modules inspection found no installed audit UPDATE/DELETE writer in the confirmed development DB. Upgrading that obsolete fixture is not an authorization to weaken audit preservation.

JOB06 ArchiveAuditEnabled defaults false. When explicitly enabled by deployment configuration, a daily worker classifies bounded batches older than the 365-day hot period; it never deletes business records, audit, notifications or receipts. The archive record itself is the per-record history. Job diagnostics contain counts only, no payloads. The audit UI displays archive dates, minimum retention, hold and PURGE_BLOCKED reason.

## Holds and deletion

Every audit row is PURGE_BLOCKED / AUDIT_LOG_PURGE_DISABLED, including after RetentionUntil. Expiring a dated hold never overrides that stronger prohibition. Indefinite holds cannot expire automatically. No public hold-release or purge endpoint exists, and AUDIT_VIEW grants no mutation authority. No approved purge policy exists for other business entities: this implementation never archives/deletes their active or historical records, incident history, SLA history, assignment/handover receipts or notification audit. Their future policies must recheck current state, unclosed dependencies, holds, retention and concurrency before deletion, and preserve evidence. No CASCADE DELETE, disabled FK/trigger, relocated audit identity or receipt purge is used.

Development installer applied only to the identity-verified HotelOpsDB (GUID BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E). Readback: all three guards enabled, archive FK trusted/NO_ACTION, archive row count zero. Worker not enabled; no archive backfill or purge executed. Existing preflight copy-only backup remains available. Installer supports transaction rollback via ApplyChanges=0; do not roll back preservation by dropping guards over a live audit store.

Validation: compilation and TypeScript checks only. Unit/integration/browser tests NOT_RUN by explicit instruction. No QA_VERIFIED or RUNTIME_VERIFIED claim. Legacy data is preserved, not retroactively certified secret-free.

---

## Historical design (superseded; not execution instructions)

# JOB06 — Archive before purge

Status: design only. User approved OPTION 2 and skipping referenced rows when safe archival is unavailable. No archive schema, worker, migration or reader was installed in checkpoint84. No shared/production mutation.

## Verified dependencies

`TaskSubmissionIsolated.sql`, `TaskVerificationIsolated.sql` and `TaskReopenIsolated.sql` contain unique audit-ID FKs. Verification references submission; reopen references verification. The reopen provenance trigger also joins `audit_logs`. Copying payload to another table does **not** make the original safe to delete. Discover all inbound FKs and audit joins from actual schema/source before implementation; these three references are not an exhaustive allowlist.

Never cascade, disable FK/immutability triggers, rewrite receipt IDs or delete receipts to bypass retention. Preserve the generated entities and existing single-project architecture.

## Proposed storage and access policy

- Use a separately permissioned archive schema in the same verified SQL database, so copy, verification and eligible deletion share one transaction. This is proposed storage, not an existing capability.
- Preserve original audit ID and every original field: branch, actor, entity, action, old/new values, changed fields, IP, request ID, user agent and timestamp. Add archive time, policy version, batch ID, finite expiry and integrity digest as separate metadata. Define a versioned digest encoding distinguishing null/empty, Unicode and timestamp precision. Also verify typed field equality and row counts; a hash alone is insufficient.
- Require a data-owner-approved **finite archive TTL**, purpose and expiry/hold policy before activation. No unlimited default; the live 365-day cutoff does not define the archive TTL. Missing policy disables audit archive/purge and reports a configuration blocker. A hold needs an owner, purpose and finite review date. Blocked expiry is an explicit reviewed backlog, not silent indefinite retention. Receipt lifetime and lawful personal-data disposal need a compatible contract before deleting linked evidence.
- Application identities cannot read raw archive payload or update/delete it. The retention identity receives only narrow archival execution rights. Inspection retains UC136/137 Administrator restriction, existing audit permission, branch/global scope and redaction. Owner/task-assignee/notification-recipient status grants no archive access. Audit archive access itself; expose no new raw export.
- Verify encryption, backup/restore access and finite backup/export retention before activation. Job logs contain counts and internal correlation IDs, never personal payloads. Primary-copy deletion alone does not complete data disposal.

## First safe implementation slice

1. Validate configured policy and schema capability; default worker off. Use UTC with strict `occurred_at < cutoff`, bounded deterministic batches and a single-run lease.
2. Within one transaction, lock candidate rows, recheck cutoff and all inbound references. A concurrent receipt insertion must either commit first and cause a skip, or be prevented from referencing a deleted parent by retained locks/FKs.
3. **Skip referenced rows**, with blocker counts and internally traceable IDs. Do not claim full JOB06 compliance or invent an archive provenance mapping. This is the explicit user-approved fallback.
4. For unreferenced rows, insert an exact archive copy idempotently by original ID. A differing existing copy fails the batch. Verify fields/policy, then delete only still-unreferenced verified originals. Copy/delete/FK failure rolls back everything. Commit and record copied/deleted/skipped counts and retry outcome without payload leakage.
5. Notification retention remains independent, but delivery retry must first respect the event-age cutoff: deleting an old notification must not make durable producers recreate it as undelivered. Verify this before activating notification purge.

Checkpoint85 implementation covers this prerequisite for the seven FE11 dispatchers (submission, verification, decline, assignment, plan/cancel, series stop, reopen), using the inbox retention configuration at discovery and again after acquiring delivery locks. FE05 and provisional cleaning delivery still need equivalent review/verification. This does not activate JOB06, archive audit, or authorize any deletion outside the synthetic trial.

This slice addresses unreferenced audit only; receipt-linked audit remains an explicit JOB06 gap.

## Referenced-audit migration path

A separate reviewed storage contract must provide stable audit identity independent of hot/archive location, with database-enforced payload integrity. Migrate every dependent reader/writer and the reopen provenance trigger together. A read fallback alone cannot resolve FKs targeting the hot table. Keep current FKs/triggers until replacement constraints and provenance checks are proven on disposable SQL; no constraint-disable backfill.

Before concrete DDL: inventory dependencies, define identity/payload constraints, receipt/personal-data lifetime, activation and rollback. Preserve original IDs and schema-compatible fields. Use an additive idempotent installer, reject incompatible objects, verify full backfill before capability activation. Restore original hot payloads before rollback; refuse rollback when reconstruction cannot be proven. Do not change generated EF entities or deploy automatically.

## Disposable verification gate

Verify instance/database GUID, ownership and schema; use synthetic data. Test cutoff boundaries, exact copy, unreferenced purge, referenced skip, differing existing copy, duplicate retry, concurrent receipt insertion, archive/delete failure rollback, original-ID provenance and reopen guards after archival, finite expiry/holds, denied non-Administrator/foreign-branch access, redacted access audit, notification retry after retention, and cleanup fingerprints. Test UI/API scope if subsequently exposed. These are planned tests, not executed results.

UC054 additive dependency (2026-11-16): MaintenanceDecline.sql defines immutable maintenance_decline_events with an audit_logs FK; scoped history and notification source require that provenance. Installer preview/outer rollback only, not persistently installed. Include this table in inbound-reference discovery and archive design; skip referenced audit rather than cascading/deleting receipts. No finite retention policy is invented or activated by UC054.
