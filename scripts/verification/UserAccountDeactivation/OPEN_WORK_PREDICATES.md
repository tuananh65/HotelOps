# Open-work predicates

Task48 packaging: the DEV2 query helper described below is a local Task46 artifact,
excluded from this commit because SQL fixture coverage is pending. This document retains
the approved DEV2 contract for future integration. Current packaged DEV1 predicates are
defined in DEV1_PREDICATE_DECISIONS.md; V1 intent is in V1_BUSINESS_CONTRACT.md.
References to the earlier Task40 matrix are historical external context, not a package
dependency; current decision and writer tables here stand alone.

Implemented in DeactivationResponsibilityQueries.ReadApprovedDev2BlockersAsync:

| Code | Exact predicate |
|---|---|
| MAINTENANCE_ASSIGNEE | maintenance_works.assignee_id = target; status PENDING/IN_PROGRESS/ON_HOLD |
| WAREHOUSE_MANAGER | warehouses.manager_user_id = target; is_active |
| ASSET_CUSTODIAN | assets.custodian_user_id = target; lifecycle_status IN_USE/UNDER_REPAIR |
| ASSET_RECEIVER | asset_handovers.to_user_id = target; handover_status PENDING |
| MATERIAL_REQUEST_REQUESTER | material_requests.requester_id = target; status DRAFT/NEEDS_INFORMATION |
| MATERIAL_REQUEST_APPROVER | assigned_approver_id = target; cycle PENDING; parent PENDING_APPROVAL; no greater revision for same request |

MaterialRequestService.Latest/Cycle select greatest RevisionNo. HotelOpsDbContext
maps unique UQ_mr_cycles_revision (RequestId, RevisionNo). The predicate searches
for newer cycles regardless of status, excluding historical/superseded pending rows.
No branch filter is applied to dependencies: a target may retain duties elsewhere.
The helper is internal and exposes no resource identifiers or API response.

Historical completed/closed/cancelled maintenance, inactive warehouses, RETIRED custody,
ACCEPTED/REJECTED handovers and superseded approvals do not match these predicates.

DEV1: see ../Dev1Deactivation/CURRENT_RESPONSIBILITY_MATRIX.md for exact columns,
schema vocabulary and missing writers. This task does not approve unresolved semantics:
cleaning assignment end/status precedence and rework, inspection conclusion/defects,
task review/reopen, internal requester/assignee/acknowledged handover, shift status/time
boundaries and attendance, active department stewardship and subordinate manager duties.
The absent writers do not prove legacy data is safe. No empty DEV1 predicate is installed.
