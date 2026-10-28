# Remaining limits

- No business gate remains open for this approved six-role/single-branch/single-department UC-004 slice. It is not full multi-branch/multi-department provisioning or assignment maintenance.
- Lost/uncertain HTTP delivery requires separately authorized credential reset. This task deliberately supplies no reset endpoint or plaintext replay. Operations must have an approved recovery process before production use.
- Binary still requires the existing rowversion patch and startup gate. Current SQL forward/rollback scripts are trial-only; production execution/rollout is not approved by this implementation task.
- Production TLS/DataProtection, whole-app least-privilege SQL deployment, old-binary coexistence and comprehensive DEV2 workflows remain uncertified.
- NU1900 persists on verification projects because vulnerability metadata is unavailable; auditing stays enabled. Cold startup timing is reported separately from SQL validation timeout.
- Internal credential release relies on trusted orchestration awaiting commit. No public endpoint accepts a pending object or client assertion that a transaction committed.
- Authorization rechecks do not cancel requests already in flight when another writer revokes grants. No new cross-module revocation locking protocol is introduced.
- Default Information log checks cover the actual trial host configuration, not arbitrary future telemetry/body logging; do not enable credential response-body logging.
- No commit/push/merge, HotelOpsDB access, catalog edit, schema migration or DEV2 change was performed.
