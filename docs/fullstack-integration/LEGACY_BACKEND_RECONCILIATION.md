# Backend reconciliation

Retained from develop: Authentication/Users/RBAC; AccountWriterSessionGate; richer cancellation/responsibility/deactivation checks; hotel structure/room master data; housekeeping/inspection/progress implementations; all DEV2 services/configuration. A deletions are not treated as authorization to delete these implementations.

Imported A source (origin e859006, handover28f0a5e): CleaningMonitor service/DTO; TurnoverHistory service/schema/boundary/private files; RoomTypeDirectory service/schema; BranchAdministration service/schema/dependency reader; WorkTime entities/mapping/participant/readers/service/DTO; controller definitions; Task87 xUnit project and runner. Exact file decisions are in BACKEND_SOURCE_DECISIONS.json. Ownership/provenance for the complete source tree is in SOURCE_FILE_OWNERSHIP.csv.

Five imported controller definitions are NonController: BranchAdministration, RoomTypeDirectory, TurnoverHistory, CleaningMonitor, Workdays. They compile and their services resolve, but their recovered HTTP graph is not activated. Existing branch/room-type/turnover routes remain owned by develop controllers. Direct controller unit tests do not prove route activation. UC038/UC142 recovery is preserved, not accepted as integrated HTTP behavior.

Applied shared changes: approved A1 source model with one ConfigureWorkTime call; additive Program DI; interface/concrete WorkTime participant share the same scoped instance; Workday resolver delegates existing cleaning bindings. No SQL execution, generated legacy entity edit or migration.

Deferred A differences: fallback authenticated policy, anonymous Development OpenAPI/health classification, WeatherForecast removal, SqlSessionPolicy UTC fix, route replacements. User approved only DI/WorkTime. Existing develop Users/PermissionService/GlobalAdmin/responsibility source is retained rather than losing its stronger session gates and evidence. DEV3 must reconcile these differences.

Not activated: Notification B01/B02/C01 proposals, room lifecycle coordinator/schema/current-cycle contracts, Workday B1.2, full UC138 workflow, scratch/package/sanitized archival code. Recovery references: origin/handoff/codex-a-20261105 and historical cf8fb020362f8f64192e7f2f14a10547cebe5b91. Local-only archives stay local with inventory/hash references. No important source is silently discarded.
