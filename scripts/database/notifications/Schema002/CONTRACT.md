# NOTIF-FE04-SCHEMA-002 executable contract

Base consumer: e76d6fc2ad9915bf03bd5640a4ee6f63c1806550. No FE04 caller, C# runtime guard, generated mapping, N01 foundation or producer/worker configuration changes.

## Exact target and installation

Only DESKTOP-JI8U50T\SQLEXPRESS / HotelOps_ENVNOTIF001_Trial_20261109, execution identity DESKTOP-JI8U50T\hax. Approval: ENV-NOTIF-001, owners DEV1 ENV-001 / DEV2. Never use HotelOpsDB, FE04/FE05 databases, or copy ownership markers into an unrelated database.

Run from this worktree, as hax:
~~~powershell
$e = 'D:\SEP490\Projects\HotelOps-Integration\obj\envnotif001-evidence'
# Offline preparation:
./scripts/database/notifications/Schema002/Provision.ps1 -EvidenceDirectory $e
# NEW target only; actual creation requires the explicit PL approval already recorded in this task:
./scripts/database/notifications/Schema002/Provision.ps1 -EvidenceDirectory $e -Execute
./scripts/database/notifications/Schema002/Install.ps1 -Stage Install -Manifest "$e\manifest.json" -EvidenceDirectory "$e\reconciliation" -ConsumersStopped
./scripts/database/notifications/Schema002/Install.ps1 -Stage Verify -Manifest "$e\manifest.json" -EvidenceDirectory "$e\reconciliation"
~~~

Seed.ps1 / Seed.sql create only synthetic personnel and master room data, using the existing credential helper; private credentials are hax-DPAPI protected in an ACL-restricted directory outside Git. No task, assignment, inspection, work interval or notification is seeded. Start-Runtime.ps1 launches a separate accepted-consumer binary on HTTPS 7029 after catalog/GUID/lease verification; it explicitly disables NotificationDelivery and all public cleaning candidate switches. It reuses an existing protected PFX without changing certificate stores. Do not use expired leases or restart another runtime. A successful health GET does not open any mutation gate.

Do not rerun creation against an existing target. Resume requires the captured database GUID and ownership token, matching completed-stage hashes, and no manifest/lease overwrite. A partial unmarked stage stops for owner review; no automatic DDL repair/adoption or destructive reset. Successful foundation provisioning records the actual UTC lease start and end (+24h); reconciliation uses the remaining lease and never extends it.

Provision renders exact-target COPIES of existing scripts; originals stay unchanged. The HotelOpsDB exclusion in the rowversion patch is preserved. Guards are retained, not removed. The frozen N01 forward/recovery copies replace only the two original exact-target literals; rendered/source hashes must be retained in evidence. No new server grants or login. The original FS013 boundary creates its existing NOLOGIN database test principal; it is not mapped to a runtime login or enabled public producer.

## Preconditions and expected catalog

The ordered baseline stages are executable in Provision.ps1. They provide RBAC/reference catalogs, user rowversion, room-type/checklist/housekeeping/progress schema, FS013 lifecycle + notification proposal + executable source verifier + SQL boundary + execution schema, then FS014/FS015/FS016. No business tasks/assignments/inspections are fabricated by provisioning.

Before reconciliation: FS013 NotificationProposal=FS013-PROPOSED-V1, FS016 Rework=PROVISIONAL_NOT_ACCEPTED, IntegrationTrial=DEV2-INTEGRATION-02-SQL-PROVISIONAL; N01 absent. CK_fs016_publication_event/source trusted and enabled. All existing graph FKs/CHECKs trusted; source/publication/operation immutability triggers enabled. Prior source trigger must be readable, standard ANSI_NULLS/QUOTED_IDENTIFIER ON, unsigned and without object-level permissions/extended properties. Otherwise stop for a broader recovery contract.

Exact unique ordered index keys:
- PK_notification_operation_v1: actor_user_id, operation_key.
- UX_fs009_publication_operation: actor_user_id, operation_key; unfiltered BEFORE; event_type='CLEANING_TASK_ASSIGNED' AFTER, same name retained.
- UX_fs015_assignment_publication: assignment_id, filtered to CLEANING_TASK_ASSIGNED.
- UX_fs015_occurrence: occurrence_key, filtered IS NOT NULL.
- UQ_delivery_publication_recipient_v1: publication_id, user_id, delivery_channel.

After: N01 marker IMPLEMENT06-FS016-V1; reconciliation marker FE04-P0-FS013-N01-CANDIDATE-V1; CK_n01_publication_event/source and CK_n01_operation_metadata trusted; FK_n01_publication_inspection trusted. Additional columns: publication inspection_id bigint NULL, n01_payload_hash binary(32) NULL; operation n01_operation_hash binary(32) NULL, n01_receipt_json nvarchar(max) NULL.

One graph only: notification_operation_reservations -> notification_publications -> notification_delivery_intents. No notification_intents_c04 queue, alternative operation ledger or new event values. fingerprint/result_json/completed remain caller-owned; N01 metadata stays separate. Assignment operation uniqueness remains one publication; one N01 operation may contain its existing distinct typed events. Existing occurrence uniqueness and intent uniqueness remain.

Trigger partition:
- CLEANING_TASK_ASSIGNED / CLEANING_BLOCKER_REPORTED / CLEANING_TASK_COMPLETED retain original assignment/task revision + dueAtUtc validation.
- REWORK_SUBMITTED uses rework rowversion, PENDING_REINSPECTION/IN_PROGRESS, original inspection/task/room/current cycle, occurrence key and camelCase payload identities; assignment_id and N01 metadata must be NULL.
- inspection.failed / cleaning.rework.requested retain original N01 source arm verbatim.
- All other families rejected. Typed CHECKs still enforce nullable source arms. Immutability guards are not replaced.

## Safe rollback

Stop owned consumers and hold the same exclusive runner lease. Restore reconciliation FIRST, then frozen N01 metadata recovery:
~~~powershell
./scripts/database/notifications/Schema002/Install.ps1 -Stage Rollback -Manifest "$e\manifest.json" -EvidenceDirectory "$e\reconciliation" -ConsumersStopped
~~~

Capture before DDL: database GUID/token, exact sys.sql_modules.definition, SQL SHA256 over UTF-16LE bytes, owner-approved prior hash. Capture installed definition/hash immediately after reconciliation. Recovery verifies both captures and live installed hash; transactional DROP/CREATE restores the original definition bytes and checks its hash. No hard-coded prior trigger, history deletion, data rewrite or arbitrary trigger text.

Rollback refuses N01/REWORK_SUBMITTED history or duplicate operation groups; N01 recovery additionally refuses any N01 publication/operation metadata. Preserve data and request forward reconciliation rather than deleting history. If N01 alone committed before reconciliation failure, recovery only proceeds when the live trigger still matches the approved prior hash and the reconciliation marker is absent. Missing installed capture with a changed trigger is BLOCKED, not guessed.

Cleanup is NOT part of Install/Rollback. After lease expiry, retain evidence, obtain owner confirmation, gracefully stop only owned consumers, verify zero target sessions and exact GUID/token, then use a separately approved exact-target cleanup. Never KILL or force disconnect.

## 15 SQL acceptance cases (NOT_RUN)

AcceptanceCases.json retains the existing 15 case identities and adds required observations. Every runner must acquire the session application lock HotelOps:NotificationSchema002:Runner in this database, check GUID/lease, use a reviewed restricted writer, real authorized actor/session and owned fixtures. Do not run while producer/worker or another mutation runner is active. Any SQL1205 is FAIL; run both relevant concurrent orderings and preserve IDs/counts/hashes and audit/receipt evidence.

Catalog verification proves deployed shape, not writer acceptance, trigger positives/negatives, persisted rollback, concurrency, delivery or Inbox correctness. No UC029 ACCEPTED claim follows.

Frozen N01 identity remains 30bf39ba76e793bab9ad1777a5563d593b6f37239316b0877bcc011c50217392 (package identity, not this Git commit). Original source package identity b7b4dbf1232a4d8988d868197f754efc5ecb2cfd92714755ce1f876c39629e2d is also not a Git commit. Original N01 files and runtime source remain byte-identical to the accepted consumer.

Remaining runtime blocker: DEV1 AvailableAsync and DEV2 IntegrationSqlGuard/CleaningAssignmentStore still restrict different original targets; this approved schema trial does not satisfy those guards. No guard was weakened. Producer/writer acceptance remains closed pending an explicit common target contract implemented by the relevant owners.

