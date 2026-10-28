# Outstanding issues and limits

1. Real-data upgrade remains unverified: synthetic paths do not establish safe conversion of a restored HotelOpsDB with personnel, existing patches, URGENT references, historical assignments or business transactions. Stop on conflicts; review backup/restore, exact schema and per-record conversion before any main deployment.
2. Positive login, session lifecycle, permission scope and API persistence scenarios require separate personnel/master-data fixtures. No default admin was added. The Task 15 probe verifies Auth schema and permission queries, not the entire Authentication/RBAC security protocol.
3. Review the 165 EF-convention index advisories for performance with DEV2. No blanket index DDL or mapping change is included.
4. Historical SRS/change notes/handoff documents describe pending permissions or pre-merge state. This package uses explicit Task 15 decisions and existing catalog; it does not finalize unrelated room lifecycle permissions or rewrite the Permission Matrix.
5. Canonical PRIORITY now follows SRS GBR-085. Old main data uses URGENT; the new constraint intentionally fails if applied to such data without a reviewed conversion. No main data was converted. SEVERITY CRITICAL stays separate.
6. Trial creation was executed with the available sysadmin identity. Least-privilege application access has not been certified. No grants or permission bypass were introduced.
7. SQL setup is not one global transaction: database creation and already committed patches remain after a later failure. Retain trial evidence and use a new unique RunId; do not reset or overwrite an existing database.
8. Existing SQL patches are not generally replayable; only canonical data seed repeatability is promised. Original installer/upgrade/patch files remain unchanged.
9. Inventory Adjustment/Reversal workflow, DEV2 API authorization changes and business endpoints are outside this task and remain untouched.
