# Replenishment session revalidation integration fix

2026-11-16. Real HTTPS/SQL Create/read/Close PASS; full UC acceptance remains partial.

Installed the existing Inventory concurrency contract on exact recovery disposable SQL: seven non-null rowversion columns, default rollback trial, Apply and idempotent reapply under backup/restore and identity guards. No shared database changes, grant seeding or legacy status rewriting. InventoryConcurrencyRecoveryV1.sql retains the original rowversion validation.

The first workflow attempt then reached authorized shortage discovery (200), rejected missing CSRF (400), but Create returned 500. Method-only first-chance diagnostics identified SqlSessionService.RunAsync during ReplenishmentWriteContract.Validate. Session service deliberately rejects tracked entities, while persistence revalidation runs with the aggregate/audit tracked. This was a source integration defect.

Replenishment writes now use a clean session-validation context sharing the exact existing SQL connection and transaction. The same SqlSessionService validates current user/session/expiry under locking isolation. Business tracking is not flushed, detached or cleared; outer action still owns final commit/rollback. Scope is limited to replenishment writes; no authentication safeguard removed.

Post-fix scenario PASS: shortage discovery, missing-CSRF rejection, Create 201, duplicate rejection 400, detail/list 200, independent MR_VIEW unavailable, Close 200, persisted Closed/coverage/audit, revoked-role read 403, logout/session revocation and fixture cleanup. Evidence: recovery-replenishment-session-fix-20261116.json. All grants belonged to the unique synthetic role; production role grants unchanged. Temporary HTTPS host stopped.

Two diagnostic attempts ran before a delayed build finished and failed to connect; their artifacts are retained as failures. Diagnostic host printed method names only, no exception messages/credentials/payloads. ModelProbe build ultimately passed with three existing EF1002 warnings and transient file-copy retries. Backend build PASS, zero warnings/errors; solution test command executed no tests. Runtime warnings: RoomTypeBranchV1 unmapped indexes, Take without OrderBy and multiple-collection single-query performance warning. No browser, stock receipt posting, concurrency races or full UC acceptance claim. No newly COMPLETE_VERIFIED UC.
