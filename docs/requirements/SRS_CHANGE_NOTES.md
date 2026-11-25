
# HotelOps - SRS Change Notes

## 1. Document Status

Status: Draft - Pending SRS Revision

The current Software Requirements Specification (SRS) is not finalized.

This document records confirmed requirement clarifications and pending changes that must be considered before implementing affected features.

These notes do not replace the complete SRS or the final approved Permission Matrix.

### Approved Compact Project 2 revision — 25 November 2026

For `src/HotelOps.Api/Compact` and `frontend/src/compact`, the Owner has approved the [Master Prompt V2](../restructure-v2/OWNER_REQUEST.md), [decisions D01–D12](../restructure-v2/DECISION_AND_BLOCKER_REGISTER.md) and the [additional UI/workflow decisions](../restructure-v2/OWNER_UX_DELTA_20261125.md). These sources supersede conflicting draft statements below for Project 2 only. Project 1/Rebuild is outside this change.

- Consolidate Department Head into Operation Manager; OM manages branch planning/configuration and only reads ordinary interdepartmental requests. Inventory write-off and transfer approvals retain their specific workflows.
- Retain Maintenance Supervisor and Maintenance Staff. Do not convert technical staff into housekeeping staff. Internal repairs require assigned staff, a completion checklist and independent technical review before housekeeping inspection.
- Reserve the room maximum at assignment; after the actual room assessment, reserve only the shortage and release the excess. Consume only what is actually replenished. Rework must not consume the same replenishment twice.
- Distinguish consumables, reusable items and assets. Preserve FIFO/FEFO, expiry and batch provenance. Damaged reusable items remain owned until the branch OM approves and the write-off commits.
- Transfer between branches records requested, approved, shipped and actually received quantities separately. Unreceived quantities remain in transit pending reconciliation; do not invent loss or a return to source.
- Account invitations and password recovery use one-time tokens and a local SMTP sink during testing. Enforce first-login password change in the backend. Login and recovery identify accounts by **Username only**, not EmployeeCode, per the Owner's later instruction.
- Persist notifications and deliver them through authenticated SignalR with branch/recipient scope. AI provides advice only; statistical/rule fallback must be labeled. External AI and live email remain blocked by external configuration, not by missing business implementation authorization.

Implementation evidence and limitations are recorded in [API changes](../restructure-v2/API_CHANGE_REGISTER.md), [database delta](../restructure-v2/DATABASE_SCHEMA_DELTA.md), [UC mapping](../restructure-v2/UC_CHANGE_MAPPING.md) and [E2E results](../restructure-v2/E2E_TEST_RESULTS.md). Approval of requirements is not a declaration that every test has passed. The older Reception/DH pending notes below remain historical context, not a reason to postpone the approved Compact work.

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

### Project 2 explicit reservation/shift decision (23 November 2026)

Reception distinguishes unused reservation (`RESERVED`) from occupancy (`OCCUPIED`). Reserve a READY room, confirm guest arrival, or cancel the unused reservation. Moving a reservation returns the source to READY and reserves the destination. Moving/checking out an occupied guest leaves the source DIRTY and advances turnover. Existing inspection evidence/block guards remain required. Reserve/arrival/cancel use existing ROOM_CHECKIN and Reception role checks; transfer uses ROOM_OCCUPANCY_MOVE. No grants added. Reserve/arrival/cancel/checkout forms omit reason; transfer/block reasons remain.

WORKING shifts end automatically at the scheduled branch-local boundary; PLANNED shifts are untouched. System AUTO_END audit is exposed through authorized Workday history. Start is only permitted within the shift interval. The worker runs every 30 seconds. The SQL constraint/view/trigger patch for RESERVED requires a verified backup, fixture tests and primary approval. No booking dates, guest data, financial module or separate reservation entity is introduced.

### UC037 clarification (continued)

Project Leader approves the former assignee reading their own cancellation receipt after assignment becomes CANCELLED. Require current session, active user/role/grant and branch scope with CLEANING_VIEW; OWN refers only to the exact cancelled assignment receipt. This does not restore general task access or bypass the existing ACTIVE-assignment requirement for full task reads. Recheck eligibility at delivery and source opening. Use a dedicated receipt endpoint/view.

## Project 2 P0 — approved scope and implementation record (24 November 2026)

This section applies only to Compact Project 2. It preserves the historical sections above and the original SRS; it does not finalize Project 1 permissions or rewrite historical use cases. The user's explicit P0 request and subsequent answers approve the TEST-only cross-module/schema/role changes described in [the P0 requirements index](p0-261124/README.md). Source B plus its continuation overrides conflicting source A; compatible A requirements remain in scope.

### Confirmed decisions replacing earlier Project 2 assumptions

- Eight actors: SYS_ADMIN, OWNER, OPERATION_MANAGER, HOUSEKEEPING_SUPERVISOR, HK_STAFF, MAINTENANCE_SUPERVISOR, RECEPTION, STOREKEEPER. Department Head consolidates into OM; Maintenance Lead becomes Maintenance Supervisor; Maintenance Staff becomes HK Staff. Preserve account IDs, credentials and historical references.
- OM manages resources/configuration/operational tasks and the approved warehouse replenishment/transfer decisions. Ordinary interdepartmental requests and incidents are read-only to OM. Storekeeper handles material requests within the hotel with existing MR_APPROVE_L1 at BRANCH scope; ordinary creator/approver independence remains.
- HK staff may report assigned-room missing durable objects and damaged equipment. Reception may report damaged equipment. These specialized reports use IR_CREATE with room/role/workday/ownership checks; neither receives generic INCIDENT_CREATE. HK Supervisor verifies reports and performs final paper-checklist inspection. Maintenance Supervisor coordinates external repairs without internal technician accounts.
- Exactly one active warehouse per branch. Consumables have branch configuration, shift snapshots, automatic initial allocation, reservation on cleaning assignment, one-time consumption, refill routing and automatic return on manual End. Only objects already assigned to a room may be selected for missing-object reports.
- The earlier automatic-expiry statement is superseded for HK shifts: unfinished assigned work keeps the shift open after scheduled end. New assignments/refill remain restricted to the scheduled window. End is available only when employee work, reservations and open refill obligations permit closure; End atomically returns unused supplies. Non-HK expiry remains unchanged.
- A successful remediation inspection invokes shared readiness checks for all current blockers, cleaning and inspection evidence; it does not unconditionally set READY or alter occupancy/reservations. The earlier mandatory new cleaning cycle after every remediation pass is superseded by P0's readiness policy.
- Checklist remains physical; the system records pass/fail and notes. Accepted compact navigation, dialogs, mobile layout, history and color semantics remain unless explicitly superseded by P0. New request workspace uses server routing and source-linked history.

### Traceability and status

See [role-permission matrix](p0-261124/permission-matrix.md), [use cases/state/routing](p0-261124/business-workflows.md), [API contracts](p0-261124/api-contracts.md), [database/recovery](p0-261124/database-and-recovery.md) and [source/test traceability](p0-261124/traceability.md). These documents distinguish IMPLEMENTED, VERIFIED, PARTIAL, NOT_TESTED and BLOCKED; they do not claim that all 28 mandatory E2E workflows have passed.

At the documentation checkpoint, the Integration Owner reports verified backup/restore, TEST migration/rollback/idempotency and schema 3.0.0 (49 tables, 205 FKs, 97 checks, 38 triggers), plus successful prior backend/frontend build snapshots. Final source is still under integration; API/browser/concurrency/reconciliation and full acceptance evidence remain pending. Production, Project 1, existing passwords/accounts/history and Git publication are outside this P0 cutover. Source A's Action Center, asset and transfer/reporting acceptance remain tracked rather than silently omitted.

### 24/11/2026 — P0 integration verification (Project 2 TEST)

Implementation and evidence are versioned under [P0 report](p0-261124/implementation-report.md) and [validation evidence](p0-261124/validation-evidence.md). Eight-actor consolidation and workflows A–F have cumulative runtime coverage for 28 groups (1,349 assertions), supplemental legacy recovery/handler/assets/report/configuration-race checks, transfer checks, and desktop/mobile route audits. Backend build passed, 98 unit tests passed, 3 existing integration tests skipped; frontend build passed. This supersedes the initial NOT_TESTED checkpoints in the P0 audit bundle, while preserving their history. No Production cutover/deploy or Git publication performed. Read the report for exact limits; route audits do not claim exhaustive interactive UAT.
