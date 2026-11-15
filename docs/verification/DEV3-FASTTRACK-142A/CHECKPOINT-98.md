# FASTTRACK-142A checkpoint98 — private progress evidence

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=caa25e89f75486b7fbf7345c83bd1e1e5726a337
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121
SHARED_DB_MUTATION=NO

## Implemented

UC117 progress accepts up to five complete JPG/PNG/PDF uploads with SRS baseline10MiB per file, matching extension/MIME/content signature, bounded filename and base64 validation. Canonical UI reads selected files before POST, sends them with progress/ETag/CSRF and displays persisted evidence download links. No client storage path or public FileUrl is accepted.

Files use generated private keys and the existing attachments table, linked to the exact PROGRESS event. Attachment IDs are included in the same progress audit transaction. Existing EvidenceFileLifetime tracks files created by the invocation: precommit rollback removes those files; an uncertain commit retains files for reconciliation. Downloads recheck current session and request IR_VIEW under Serializable, require exact event/request association, reject alternate paths/reparse points, validate stored size/content, and return attachment disposition with nosniff. DTOs expose canonical download paths, not storage keys. Current evidence view lists the latest100 with total count; older-file pagination remains an acceptance gap.

## Narrow schema correction

Actual SQL CHECK `CK_attachments_entity_type` initially rejected the new discriminator. Trial2 revealed this before public browser work; cleanup passed. Reusing REQUEST would risk collisions with other request attachments, so `InternalRequestEvidenceIsolated.sql` preserves every existing value and adds only INTERNAL_REQUEST_PROGRESS. Exact server/database/GUID/run identity and exact old CHECK definition guard installation. WITH CHECK retains trust and existing data validation; repeated installation validates capability. Upload fails closed with503 if the capability is missing. No generated EF edits, FK/trigger disabling or shared DB change.

This is a disposable migration only. A future rollback must first prove there are no attachments using the new discriminator, then restore the verified old constraint; never delete historical evidence to force rollback. Production migration, retention and reconciliation require their own reviewed delivery. No production seed/deploy is authorized here.

## Executed evidence

[Final trial4](checkpoint98-trial4-runtime.json): success=true, cleanup=true. Real browser upload in the50% update, persisted attachment visible after100%, authenticated download matches byte length/PNG signature and attachment headers; wrong request source404. SQL verifies private storage/FileUrl null. Revoked read grant denies file download. Injected outbox failure verifies unchanged snapshot/event/audit/version, rolled-back attachment row and deletion of only the newly created file. Previous progress/notification/authority concurrency regression also runs.

REGRESSION_STATUS=backend build PASS0 warnings/errors; solution test exits0 without discovered tests; focused FE13 backend42 PASS; EvidenceFileLifetime5 PASS; frontend18 PASS; TypeScript/production build PASS. Existing Vite use-client/chunk and three credential-probe EF1002 warnings remain.

Trial1 failed on SQL timeout before the feature scenario; trial3 failed login with503 due SQL timeout. Both cleanup=true. Trial2 failed the inspected attachment CHECK and prompted the narrow correction above. The live read-only monitor during successful trial4 recorded a DMV timeout and U-lock waits in DB8, including sessions54→51 and51→54, followed by update waits59→52. This is a lock-cycle observation, not proof of a particular deadlock victim or complete root cause. Timing/locking risk is not declared fixed. No timeout was relaxed and no SQL service restarted.

WORKFLOWS_COMPLETED=[UC117 private upload/read/download and rollback slice]
UI_API_SQL_E2E_VERIFIED=[progress upload→private persisted attachment→authorized download; wrong source/revoked read denial]
EXACT_REMAINING_GAPS=[UC117 linked-work view and older-evidence pagination; handler/deadline public acceptance; UC118 explicit links/mandatory metadata; UC120 closure/handover/escalation guards; JOB06 archive; intermittent SQL lock/timing risk]
NEW_BLOCKERS=[]
NEXT_BATCH=[UC117 linked-work registry/projection with target authorization; remaining UC117 acceptance; UC120 complete workflow]
SPRINT_TIME_REMAINING=approximately22h18m at2026-11-15T19:32Z; original deadline2026-11-16T17:50Z unchanged.

Changed files: InternalRequestEvidence.cs, ProgressService/Controller/Panel, guarded evidence SQL, progress browser/probe and focused tests, matrix and receipts. No whole UC newly completed; sprint remains active.
