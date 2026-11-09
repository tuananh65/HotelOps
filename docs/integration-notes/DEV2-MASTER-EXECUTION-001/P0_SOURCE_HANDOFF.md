# DEV2 MASTER EXECUTION 001 — P0 source checkpoint

Base: 5411c1b86006d767fbad9aaf4c78cc23c9ece3e4, cached origin/integration/fullstack-base.
Fresh GitHub fetch failed (github.com:443 connection unavailable); current remote HEAD/advance cannot be verified.
This source checkpoint requires remote-base revalidation before opening/merging a PR.

Source build PASS, 0 warnings/errors. Actual DEV3-Unblock executable: 38 PASS, SQL connections=0.
Actual selected-base BoundaryTests: 67 PASS; historical 24 count not reused.
Source scope/secret-pattern scan and protected Notification/DEV1/DEV3 baseline hashes PASS.

UC016 routes unchanged: POST /api/assets/{id}/location, /api/assets/{id}/location-transfers, POST /api/incidents.
Existing DTOs preserved. Three DEV2 actions now require existing HotelOps cookie and antiforgery validation.
Writer binds supplied actor to current protected ticket, reuses SqlSessionPolicy and existing branch permissions
(ASSET_MANAGE initial placement, ASSET_TRANSFER transfer, INCIDENT_CREATE report); missing authority fails closed.
No permission assignments, shared policies or middleware changed; anonymous requests cannot become placement writes.

Caller writer begins local SQL transaction, retains eligible actor User first, validates same session/permission,
then retains DEV3-compatible Branch UPDLOCK/HOLDLOCK before Asset and authoritative Room/Floor/Area reads.
Initial Asset branch discovery is not proof: locked Asset is reread and branch compared.
Checks active Branch, Room, Floor, Area and exact same-branch/parent relationships; standalone Areas remain allowed.
Incident validates both explicit location and locked Asset's current location before number allocation/domain writes.
Final actor/session/permission reread occurs before owner commit. Errors/cancellation dispose uncommitted transaction.
Existing audit/domain transitions and Maintenance ReturnWork code remain unchanged.

DEV3 owner: HotelStructureUpdateService.Run already begins SERIALIZABLE then Users -> Branch and rereads
StructureDeactivationDependencies.ReadAsync. Its deactivation path remains WriterCoordinationRequired;
DEV2 does not remove that gate or edit DEV3 writer. Owner must accept three-writer protocol, review all other
reference writers, use final dependency reread, then verify both SQL race orders before activation.
Existing transfer expected location is checked; no client parent-rowversion/ABA guarantee is invented.
If strict parent If-Match semantics are required, owner must define transport/version contract first.

SQL_ACCEPTANCE_PENDING / FINAL_WRITER_COORDINATION_PENDING / BUSINESS_ACTIVATION_BLOCKED.
SQL/HTTPS/race/rollback/SQL1205/fixture cleanup NOT_RUN. No SQL target, fixture, DDL, worker or producer activation.
Original placement SQL probe cannot be ported as-is: IntegrationSqlGuard is absent from selected base;
the source runner refuses SQL until a registered owner runtime adapter and owned fixture approval exist.

Initial 11 paths reconciled individually. Additional owned changes: AssetService constructor injection,
Dev2PlacementAuthority, and the two controllers' three action boundaries. Program.cs has only three scoped
dependency registrations; existing auth/DbContext/middleware preserved, no Notification foundation imported.
Reader/FE12 adapters are internal source candidates, not new HTTP APIs or business release decisions.
FE12 has persisted facts reader; actual cycle binding remains closed because selected base lacks accepted cycle source.

Consumer readiness: READY_FOR_FE_IMPLEMENTATION for reviewing DEV2 internal contracts and wiring a candidate.
READY_FOR_API_INTEGRATION is conditional on authenticated endpoint/runtime tests and owner compatibility.
READY_FOR_BUSINESS_ACTIVATION=false. No automatic merge/deploy or feature activation authorized.

P1 review addendum: paired Asset/Incident writer User-first corrections are required alongside this checkpoint; see P1_SOURCE_HANDOFF.md. The original P0 commit alone does not resolve the full writer census.
