
# HotelOps - SRS Change Notes

## 1. Document Status

Status: Draft - Pending SRS Revision

The current Software Requirements Specification (SRS) is not finalized.

This document records confirmed requirement clarifications and pending changes that must be considered before implementing affected features.

These notes do not replace the complete SRS or the final approved Permission Matrix.

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
