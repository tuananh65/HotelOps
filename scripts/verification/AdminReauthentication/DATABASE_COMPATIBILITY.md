# Database compatibility and safety

Existing UserSession fields contain session issuance/activity/expiry/revocation data. They have no suitable reauthentication semantics. LastUsedAt/CreatedAt are not repurposed, and no generated entity or DbContext mapping is changed.

Process-local proof state avoids a schema dependency. No new table/column, EF migration, forward/rollback patch or release SQL modification is needed. Existing audit_logs metadata supports UPDATE events and non-sensitive JSON. Existing User.RowVersion is the already-approved concurrency dependency, not a Task 33 schema addition.

StartupSchemaValidator remains unchanged. The new binary has the same SQL schema prerequisites as Task 31/32. Existing clients/routes/cookies continue working. New reauth/reset routes add behavior; no second session/JWT/bearer token is introduced. Restart clears proof state and requires another password confirmation.

Tests only write newly created owned canonical trials:
- HotelOps_Canonical_20261027_111609_fac44932_Fresh: patched fresh for inherited full HTTP regression.
- HotelOps_Canonical_20261027_111613_b8c01721_Fresh: patched upgrade for reauth/reset and technical regressions.
- HotelOps_Canonical_20261027_115500_33abcdef_Fresh: negative startup fixture; later retains deliberate wrong-type token.

Guarded setup connects to master only to create these new databases. HotelOpsDB is not targeted, read, altered or migrated. Original SQL files are not modified.

HTTP authorization tests create temporary owned BRANCH/OWN/branchbound-ALL role/grant fixtures inside the disposable trial only, matching existing console-harness conventions. They do not modify official role/catalog rows, shared seed scripts or production grants. Fixtures are removed in finally; owned users/memberships/admin fixture assignments remain retained. Test SQL CHECK constraint is dropped in finally. All owned live sessions are revoked after tests.

Fresh/upgrade/replay/rollback schema dependency suite passed 26 checks. Actual startup suite passed with unpatched/wrong-type/inaccessible negatives, restricted metadata and object-SELECT-only principal; details in VERIFICATION_RESULTS.md.

Production rollout, least-privilege SQL, TLS/DataProtection and multi-instance routing are separate operational checks, not certified by the trial.
