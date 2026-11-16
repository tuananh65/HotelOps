# Recovery replenishment workflow attempt

2026-11-16. FAIL before business API creation; not acceptance.

Added an opt-in recovery replenishment scenario to the exact-identity synthetic auth runner. It creates its own branch, warehouse, item/category/unit/threshold and a unique synthetic grant role. The actor also receives an existing active STOREKEEPER role assignment in the fixture branch. Production role grants are not modified. Planned checks exercise authorized shortage discovery, missing-CSRF denial, real Create, duplicate coverage rejection, detail/list quantities, independent MR_VIEW denial, Close persistence/audit and revoked role denial. Fixture creation is transactional; cleanup is constrained to synthetic IDs/codes and actor.

Executed attempt: auth and no-grant checks passed, then setup failed with Invalid column name 'row_version'. Setup transaction rolled back, scenario cleanup completed and outer user/session/audit cleanup completed (success=false, cleanup=true). The temporary HTTPS API was stopped. Evidence: recovery-replenishment-20261116.json. No Create/read/Close business assertion ran; do not count planned assertions as PASS.

The inventory contract source is database/patches/20261019_inventory_contract.sql. Compare its concurrency/schema prerequisites against the recovery database before any migration; do not remove rowversion from source or weaken authentication to pass the probe. No production/shared mutation.

Backend build PASS, zero warnings/errors; ModelProbe build PASS with three existing EF1002 warnings in SprintCredentialScenario.cs. Solution test command executed no tests. Runtime also emitted the existing RoomTypeBranchV1 mapping warnings and a Take-without-OrderBy warning. No frontend changes. UC100/101 remain partial and runtime attempt failed; no newly COMPLETE_VERIFIED UC.
