# Deployment safety

Development code readiness and verified trial compatibility do not grant production rollout approval. HotelOpsDB was not connected to or changed. Existing trial-only forward/rollback guards and canonical release/seed scripts remain intact.

1. Obtain approval for a target-specific release patch and backup/restore plan. Current Task 26 scripts are trial-only, not production migration artifacts.
2. Drain writers/traffic; apply the approved patch through controlled maintenance using a separate DDL principal.
3. Verify actual dbo.users.row_version metadata and user data invariance with the runtime principal. Do not infer readiness from CanConnect alone.
4. Deploy the new binary. The startup gate must pass before the listener starts; any failure leaves the instance out of traffic.
5. Run approved fixture authentication/list/detail/profile smoke checks, then admit traffic.

Rollback requires stopping/draining token-dependent binaries before an approved column rollback. Dropping/recreating rowversion invalidates ETags. Never remove trial guards to run these scripts against HotelOpsDB.

Outstanding: production release artifact/backup approval, whole-app least-privilege certification, old-binary coexistence and comprehensive DEV2 workflow tests. SELECT-only metadata and hidden-metadata tests do not certify all runtime DML rights. Windows Development/pinned local TLS verification does not certify production TLS/DataProtection. NU1900 remains an environment limitation; auditing stays enabled.

The gate prevents startup on a missing/malformed rowversion, but cannot prevent later privileged DDL or serialize authorization revocations with in-flight mutations. Existing USER_MANAGE/BRANCH/OWN support does not provision grants or reopen UC-011.
