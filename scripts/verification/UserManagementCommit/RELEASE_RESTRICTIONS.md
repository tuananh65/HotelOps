# Release restrictions — mandatory

- The new binary requires dbo.users.row_version SQL rowversion NOT NULL, length 8. Apply a reviewed target-specific patch before admitting the new binary to traffic.
- Production patch execution is not approved. Existing forward/rollback scripts are trial-only; do not remove their guards or run them against HotelOpsDB.
- The startup guard only rejects incompatible/unverifiable schema before the listener starts. It does not migrate, seed, repair or continuously monitor the database.
- Least-privilege whole-application deployment and old-binary compatibility are not certified. The trial SELECT-only metadata test does not certify all DML privileges, production TLS or DataProtection.
- A Git commit or future push is not approval to deploy.
- Deployment sequence: approved backup/patch plan, controlled DB-first apply, schema/data verification, guarded backend startup, approved smoke tests, then traffic.
- Stop/drain token-dependent binaries before any approved schema rollback. Dropping/recreating rowversion invalidates ETags.
- UC-011 remains blocked; no new grants, account mutations or DEV2 workflow changes are included.

Remaining environmental notes: NU1900 on verification projects; cold-process timings are reported separately from SQL timeout. NuGet security auditing stays enabled. Comprehensive DEV2 workflows and concurrent revocation serialization remain outside this package's verification.
