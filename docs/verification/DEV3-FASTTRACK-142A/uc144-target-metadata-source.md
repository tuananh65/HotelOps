# UC144 target metadata

PARTIAL_IMPLEMENTATION. No UC completion claim.

List/detail DTO projections now include RoomNumber, AreaName, AssetCode, AssetName, AssigneeName and PriorityName; detail also includes Source. Incident references and preventive schedule references use existing EF mappings under MaintenanceReadAccess. Priority display is constrained to PRIORITY reference group. Existing branch/source integrity restrictions remain. Only labels required by UC144 are returned, not entire asset/user records or inventory permissions.

MaintenanceReadView renders target labels, assignee name (ID fallback) and priority in the list and detail. Missing target data displays unavailable. Planned timestamps remain labeled Planned, not Due/SLA.

Direct evidence: backend build PASS, 0 warnings/errors; FE production build including TypeScript PASS with existing React Router directive and large-chunk warnings; 4 existing MaintenanceReadAccessTests PASS. These tests cover authority/lineage/admission only; SQL projection and HTTP/browser remain NOT_RUN. No database/schema/grant changes.

Open clarification: corrective due date from approved Incident.ResolutionDeadlineAt contract, preventive due date from the occurrence scheduled deadline, missing evidence N/A. Source currently has no mapped/persisted ResolutionDeadlineAt implementation, and occurrence lifecycle is incomplete. Do not substitute MaintenanceWork.PlannedEndAt or invent SLA durations. Other independent work can continue while this decision is pending.
