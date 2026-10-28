# RC-D4: responsibility writer / account-deactivation coordination

Prepared for independent code audit. SQL runtime has NOT been executed.
This checkpoint does not implement Account Deactivate or its blocker query.
This RC-D4 harness covers Maintenance/Warehouse/Asset only; MR coordination has
its separate --deactivation-coordination harness. No SQL/runtime was executed as
part of the clean-branch transplant. Schema, RBAC, Auth core and
Adjustment/Reversal are outside this patch.

## Production locking contract

UserEligibilityLock uses the same scoped DbContext as its caller. No DI registration
is needed: the helper is constructed locally from that context and has no private
transaction, commit, authorization or role logic. Calling without an existing EF
transaction fails. LockAsync distincts/sorts numeric IDs and issues separate
SQL Server UPDLOCK,HOLDLOCK reads, retained until caller commit/rollback. Its
no-tracking projection contains only eligibility fields; it never returns hashes
or credentials. The proposed future owner must exist, have DeletedAt null,
AccountStatus exactly ACTIVE and EmploymentStatus exactly ACTIVE. Historical
source/old owners are locked without requiring their eligibility for release.

| Writer | Previous order | Coordinated order |
| --- | --- | --- |
| Maintenance AssignWorkAsync | Incident -> Work -> ordinary Assignee existence check | transaction -> eligible Assignee User -> Incident -> Work -> corrective relationship/cycle/state -> assign -> commit |
| Warehouse SaveWarehouse | transaction -> Catalog -> Actor check -> Warehouse -> ordinary manager check | discovery of old manager -> transaction -> sorted old/new manager Users -> proposed-manager eligibility -> existing Catalog/Actor/Warehouse order -> reload and compare manager snapshot -> rowversion/business guards -> write -> commit |
| Asset CreateHandoverAsync | Asset -> ordinary target User check | transaction -> eligible receiver User -> Asset -> operational/department/custody/pending validation -> PENDING -> commit |
| Asset AcceptHandoverAsync | discovery -> Asset -> Handover -> ordinary receiver check | identifier discovery -> transaction -> sorted source/receiver Users -> receiver eligibility -> Asset -> Handover -> reload/revalidate IDs/PENDING/source custody -> designated actor -> atomic custody + acceptance -> commit |
| Asset RejectHandoverAsync | Asset -> Handover | same; no future-owner eligibility lock, so an inactive receiver may resolve PENDING without gaining custody |

Warehouse manager discovery is non-authoritative. After waiting for Users and the
canonical feature gates, the Warehouse is reloaded under its retained lock.
A different old manager raises a business validation error before rowversion or
write. The caller must reload/retry; the service never acquires an extra User lock
after Catalog to patch stale discovery. The optional Inventory prepareOwners hook
is unused by other writers, whose lock order remains unchanged.

Actor existence validation stays as before; no User is locked solely because it
is the action actor. Maintenance Create and Reopen still use/reset null Assignee.
Asset lifecycle/retirement semantics and historical custody FKs are unchanged.

## Exact DEV1 Asset blocker predicates (contract only)

- Current custody: assets.custodian_user_id = target AND
  assets.lifecycle_status IN ('IN_USE', 'UNDER_REPAIR').
- Future receiver: asset_handovers.to_user_id = target AND
  asset_handovers.handover_status = 'PENDING'.
- RETIRED custody, ACCEPTED and REJECTED handover FKs are historical and do not
  independently block deactivation. No blocker/deactivation endpoint/query is added.

## Prepared SQL test target

ResponsibilityCoordinationChecks.cs is linked into both existing Inventory and
IncidentMaintenance projects; no extra project was created. Those builds also
compile the Asset scenarios; no dedicated Asset integration project exists.
Normal runners are unchanged unless --responsibility-coordination is selected.
Even then, runtime refuses before SQL access unless
HOTEL_OPS_COORD_RUNTIME_APPROVED equals RC-D4-AUDITED. This marker is only a
manual test gate, not an authentication bypass or production feature flag.
Do not set it or execute SQL tests until independent audit explicitly permits it.

ConnectionStrings__HotelOpsDb must be supplied externally. Before any fixture write,
the test requires SELECT @@SERVERNAME/DB_NAME() to match exactly
KIENPC/HotelOps_Canonical_DEV2_Regression. Source HotelOpsDB and frozen
HotelOps_Canonical_DEV2_Test must never be used. Credentials are never printed.

Supply positive externally provisioned HOTEL_OPS_COORD_<NAME>_ID values for:
ACTOR, TARGET, SOURCE, INACTIVE, EMPLOYMENT_INACTIVE, WORK, WAREHOUSE,
CREATE_ASSET and HANDOVER. Target/source/inactive/employment-inactive Users must
be distinct and have usernames beginning RC_D4_; actor is distinct from them.
TARGET and SOURCE start active/nondeleted/employment-active. INACTIVE isolates
an existing approved non-ACTIVE account status with employment ACTIVE;
EMPLOYMENT_INACTIVE isolates non-ACTIVE employment with account ACTIVE. No new
status enum value is invented or provisioned by the test.

WORK needs an RC_D4_ title, synchronized corrective PENDING/OPEN pair and valid
cycle metadata. WAREHOUSE needs an RC_D4_ code, active branch/department and SOURCE
as current manager. CREATE_ASSET needs an RC_D4_ code, IN_USE or UNDER_REPAIR,
SOURCE custody, active current department, and no PENDING handover. HANDOVER needs
a separate operational RC_D4_ Asset, PENDING receiver TARGET, source SOURCE and
matching source department/custody snapshots. All fixtures are disposable,
externally prepared and reserved exclusively for this serialized test run.
The SQL login needs appropriate fixture permissions plus DMV visibility for
sys.dm_exec_requests (SQL Server version-appropriate server-state permission).

## Cases and isolation

- Helper: missing transaction fails; duplicate/reversed IDs lock once in ascending
  order; Warehouse old/new manager lock trace has the same deterministic order.
- Maintenance/Warehouse/Asset proposal: inactive and employment-inactive future
  owners reject. Writer-first reaches a pre-commit barrier with target User lock
  held and blocks a second SQL connection's deactivation lock probe. SQL DMV
  blocking evidence is required; elapsed delay alone never proves blocking.
- Deactivate-first: a fixture-only committed status transition occurs while writer
  waits on User; after release, writer must reject, even with a stale tracked User.
  This simulates lock coordination, not the real Deactivate operation/blocker query.
- Warehouse: a paused pre-User-lock writer sees a committed competing manager
  change and must reject stale discovery before rowversion validation.
- Asset: PENDING + ToUserId is asserted inside the proposal transaction. Accept
  must recheck receiver, lock source/destination in order, and atomically change
  Handover status, current department and custodian before commit. An inactive
  receiver cannot Accept but may Reject; rejection asserts no custody transfer and
  no responsibility User locks.

Production writer transactions are rolled back by a commit interceptor after
assertions, so test proposal/assignment/custody/audit writes do not persist.
Only the dedicated TARGET account-status and WAREHOUSE manager race simulations
are committed briefly and restored in finally. Warehouse rowversion naturally
advances during that simulation; it is not reset. No arbitrary business rows are
deleted, no Account Deactivate implementation runs, and no Auth/RBAC data is
provisioned. An interrupted process can leave these disposable fixtures requiring
external reset; this is another reason runtime requires a separate audit and gate.
No runtime blocking, query translation or rollback result has been claimed yet.
