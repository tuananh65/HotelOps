# FASTTRACK-142A-ACCELERATOR — Checkpoint 81

NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.

UC130 acknowledgment now has a vertical implementation: explicitly flagged delivered recipient notifications can be selected and acknowledged; timestamp/read marker and audit commit atomically. Read and acknowledgment remain distinct: a read alert stays highlighted until acknowledged. Acknowledged filtering and reload persistence are integrated. Selected IDs are bounded/deduplicated, fully scoped before writing, and serialized by the existing User lock; retries preserve timestamp and produce one audit per recipient. No source action is performed.

Schema review: only two additive fields, notifications.requires_acknowledgment bit NOT NULL default0 and notification_recipients.acknowledged_at datetime2 NULL, plus a trusted consistency CHECK and capability marker. No generated EF entity, mapping or migration change. Existing rows are not inferred to require acknowledgment. DismissedAt retains its existing meaning. The capability is checked server-side; without it inbox/read still work and acknowledge returns503. Install/idempotency/rollback scripts are restricted to exact instance/database/GUID/ownership and an empty notification fixture.

Producer limitation: this batch uses an explicit synthetic producer flag to verify recipient behavior. No real Critical incident/escalation producer has been certified or automatically reclassified. This is not full UC126/127/130 acceptance, deployment, or a new notification-type business contract.

[Acknowledgment trial](checkpoint81-trial1-runtime.json):50 shared records PASS,11 browser stages. Covers CSRF, invalid selection, foreign recipient, non-required notification, injected audit failure rollback, concurrent/idempotent retries, one audit, timestamp/filter persistence, read-versus-acknowledgment distinction and unchanged source notification. [Schema-absent trial](checkpoint81-trial2-runtime.json):42 records PASS; inbox/read/browser remain operational, acknowledgment503. Both cleanup=true.

Build: backend/ModelProbe0 warnings/errors;634 backend PASS/12 SKIP;131 frontend PASS (5 notification client +126 shared Auth/Router). TypeScript and production build PASS; existing Vite use-client/large-chunk warnings remain. Solution test exits0 without discovered test projects; npm ci unchanged lockfile, not rerun.

Both columns, CHECK, capability and injected trigger removed after isolated trial; [schema cleanup](checkpoint81-schema-cleanup.txt), [source cleanup](checkpoint81-source-cleanup.txt), [host/files](checkpoint81-cleanup.json). No shared/production mutation. Reproduce using scripts/verification/Invoke-Dev3AcknowledgmentTrial.ps1 after builds; it restores isolated schema in finally.

Remaining: actual Critical incident/escalation producer integration, full status/filter/retention acceptance and JOB06. UC129/130 remain PARTIAL. Next implement verified producer behavior or the independent notification retention workflow without inferring alert codes from names.
