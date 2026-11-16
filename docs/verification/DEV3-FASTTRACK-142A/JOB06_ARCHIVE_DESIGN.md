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
