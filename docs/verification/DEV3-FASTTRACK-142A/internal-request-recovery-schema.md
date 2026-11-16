# Internal Request recovery schema installation

2026-11-16. Direct disposable SQL evidence, not UC acceptance.

Preflight found no Internal Request schema version markers or request rowversion; base assignment status/unique active index existed. Added recovery-specific InternalRequestRecoveryV1.sql from four hash-pinned existing installers: department management authority, request decision, assignment and completion. Original files are unchanged. The bundle substitutes an exact recovery server/database/GUID guard and enforces recent checksummed full backup plus actual separate disposable restore. It retains source schema checks, takes a transaction-owned exclusive application lock, and wraps all nested migration transactions in one outer transaction. Default mode rolls back.

Trial completed and returned Applied=false; all seven new tables were absent after rollback. Applied the identical reviewed bundle, then reapplied successfully. Seven tables now exist; department mappings remain zero. Existing requests were not relabelled, no grants or business rows were inserted. The decision installer adds rowversion and the previously approved ACCEPTED/PENDING_ACCEPTANCE states with a checked constraint; no FK/trigger disabled or cascade delete introduced. Backup set 4 and the separate restore database remain available. No shared/production mutation.

Re-ran production EF zero-row probe: notification, waiting material requests, and linked internal requests all PASS. This resolves the prior missing-table error, not full lifecycle/auth acceptance. Preserve earlier failed artifacts. JSON GUID fields still show zeros despite matching nonzero exact identity in console output; do not use those fields as identity proof.

Evidence: internal-request-recovery-install-20261116.json and material-replenishment-queries-schema-ready-20261116.json. No C#/frontend changes this increment. SQL rollback, Apply, idempotent reapply and EF query execution are direct results; HTTPS/browser, positive/negative authorization and business-row materialization remain unverified. No newly COMPLETE_VERIFIED UC.
