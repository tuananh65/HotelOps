
# HotelOps - SRS Change Notes

## 1. Document Status

Status: Draft - Pending SRS Revision

The current Software Requirements Specification (SRS) is not finalized.

This document records confirmed requirement clarifications and pending changes that must be considered before implementing affected features.

These notes do not replace the complete SRS or the final approved Permission Matrix.

### Approved Reception clarification — 20 November 2026

The user approved the check-in proposal, now archived at `RECEPTION-CHECKIN-PROPOSAL.md`.
For this bounded change, an active Reception assignment with ROOM_CHECKIN in the room's branch and ROOM_VIEW may confirm guest arrival only when the active room is recorded as READY/AVAILABLE and the current cleaning/inspection evidence derives READY with no effective block. The operation records OCCUPIED, CHECKIN room history and audit atomically, checks the source version and rejects stale or duplicate writes.

Occupancy is displayed separately from cleaning readiness. A passed inspection does not make an occupied room available for another arrival. Roles do not change after check-in. Cleaning, inspection and rework remain with their existing responsible roles; Reception's CLEANING_VIEW does not admit it to cleaning/rework inboxes or execution. Room status and turnover viewing remain available within the granted branch.

The user subsequently approved `RECEPTION-CHECKOUT-MOVE-PROPOSAL.md`: Reception with ROOM_CHECKOUT may release an occupied room to DIRTY; ROOM_OCCUPANCY_MOVE may atomically move occupancy to an eligible ready room in the same branch, leaving the source DIRTY. Both require ROOM_VIEW, source versions, session/CSRF and transactional history/audit; moves require a reason and target version. Active cleaning/inspection/rework blocks departure until the responsible role resolves it. Locks are preserved. A checkout invalidates pre-departure cleaning evidence for readiness; the housekeeping supervisor creates and assigns a new cycle. Inspection/reinspection must not overwrite OCCUPIED with READY. No role transfer, new permissions or schema changes are included.

Occupancy reversal, cross-branch moves and the remainder of the revised permission matrix remain pending. Reservations, guest records and payments are outside this bounded room-operation feature.

## 2. Reception Role

### Confirmed Changes

- A new role named `Reception` must be introduced.
- Reception is separated from the existing `Operation Manager` role.
- Reception is responsible for designated operations related to room lifecycle status updates.

### Pending Clarifications

- Exact Reception use cases and permissions.
- Which permissions remain with Operation Manager.
- Valid room lifecycle statuses and transitions.
- Authorization conditions for each status transition.
- Any required audit logging or business validation.

Do not invent these rules or implement them without clarification.

## 3. Department Head Role

### Confirmed Clarification

`Department Head` does not represent the manager of one individual department.

Department Head manages departments within a hotel branch/facility.

### Implementation Considerations

- Authorization must respect the applicable hotel branch scope.
- Department Head must not automatically be restricted to one department.
- The exact management permissions and access rules must be defined by the revised requirements.

Do not assume unrestricted access across all hotel branches.

## 4. Permission Matrix Revision

Status: Pending

The Permission Matrix in the current SRS requires revision.

The revised matrix must:

- Include the new Reception role.
- Reevaluate Operation Manager permissions.
- Reflect the clarified Department Head responsibilities.
- Match the final Use Case list.
- Specify access and permission scope where necessary.
- Be reviewed and approved before implementing affected RBAC rules.

## 5. Impacted Features

Potentially affected modules include:

- FE-01: Authentication and RBAC
- FE-02: Hotel Structure Management
- FE-03: Room Operations

Other modules may also be affected depending on the final Permission Matrix.

## 6. Instructions for AI Coding Agents

- Treat the current SRS as provisional for the changes identified in this document.
- Do not treat the existing Permission Matrix as final.
- Do not invent role permissions or room status transitions.
- Inspect the existing SQL Server schema and EF Core mappings before proposing implementation changes.
- Report conflicts between this document, the SRS, and the database schema.
- Ask for clarification before implementing any affected business rules that remain unresolved.
- Do not modify the shared database schema without explicit approval.
- Do not implement pending requirements merely because they are mentioned here.

## 7. Approval Status

Reception permissions: Pending

Operation Manager permission adjustments: Pending

Department Head permission details: Pending

Room lifecycle transition rules: Pending

Revised Permission Matrix: Pending

## Approved FASTTRACK-142A clarification — UC034 / GBR-044 (2026-11-16)

Project Leader decision: keep Pending Cleaning priority for a room that has never Passed inspection, even after the first cleaning Complete. Preserve GBR-044 ordering. The cleaning task still transitions to Pending Inspection under UC034; that lifecycle state is distinct from effective room readiness. Inactive and effective blocks retain precedence. No permission, physical status or database constraint change is authorized by this clarification.

Completion API receipts expose effectiveReadiness separately from the historical lifecycle readiness field. It is computed from authoritative room/cycle/inspection facts in the same transaction. Historical receipts without this field do not prove room readiness; consumers must reload the authorized room board and must not infer READY.
## Approved FASTTRACK-142A clarification — UC037 cancellation receipt (2026-11-16)

Project Leader approves the former assignee reading their own cancellation receipt after assignment becomes CANCELLED. Require current session, active user/role/grant and branch scope with CLEANING_VIEW; OWN refers only to the exact cancelled assignment receipt. This does not restore general task access or bypass the existing ACTIVE-assignment requirement for full task reads. Recheck eligibility at delivery and source opening. Use a dedicated receipt endpoint/view.
