# FE10 disposable installer preparation

Prepared `scripts/verification/Install-ReplenishmentDisposable.ps1` without changing the original patch or generated EF entities. It pins the SHA-256 of `database/patches/20261021_replenishment_contract.sql` and extracts only its additive table/index DDL. It does not replace the original shared-database guard or execute the original patch as a whole.

Guards:

- Exact recovery instance, database name and GUID; encrypted integrated SQL without trusting an unverified certificate.
- Default Inspect mode issues only metadata SELECTs.
- Every referenced FK column must exist as bigint with an enabled, unfiltered single-column unique key.
- Refuses existing or partial FE10 tables; no overwrite, drop, cascade or legacy row rewrite.
- TrialRollback and Apply require a recent checksum full backup from this database GUID and a recorded completed restore into a separate online DEV3 disposable database.
- Serializable transaction and existing FE10 schema application lock; rechecks table absence under lock.
- TrialRollback rolls back and checks table absence. Apply commits only the three-table DDL. Permission grants are outside this installer.

Direct results on 2026-11-16:

- PowerShell parser: PASS, including the final rollback verification addition.
- Running the file in Inspect mode: BLOCKED by machine execution policy. Policy unchanged; installer not executed, no DDL attempted.
- Independent encrypted SQL SELECT with exact identity guard: 7/7 FK prerequisites compatible. Evidence: `replenishment-dependencies-readonly-20261116.json`.
- TrialRollback, Apply, backup/restore verification and lifecycle tests: NOT RUN.
- No database mutation; no UC acceptance claim.

When execution is permitted by the environment, use a fresh evidence path with `-Mode Inspect` first. TrialRollback/Apply additionally take `-VerifiedBackupSetId` and `-VerifiedRestoreDatabase`; neither flag nor a backup filename substitutes for the checked msdb history. This preparation does not authorize changing execution policy, shared SQL, or production grants.

Remaining: run the guarded installer in an environment where script execution is allowed, establish backup/restore evidence, verify rollback before Apply, then execute production-authorized synthetic HTTPS workflows. The seven FK checks prove dependency compatibility only, not complete schema or business acceptance.
