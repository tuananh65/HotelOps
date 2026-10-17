# HotelOps — Database Rules & Open Decisions (DRAFT)

> **Status: provisional database scripts; this is NOT an approved Permission Matrix or SRS amendment.** All statements below distinguish what the SQL scripts encode from still-open business decisions.

## 1. Room lifecycle encoded in uploaded patch

Patch header documents the following intended normal flow:

```text
INSPECTION --(accepted)--> READY --(Reception release)--> AVAILABLE
AVAILABLE --(Reception check-in)--> OCCUPIED
OCCUPIED --(Reception check-out)--> DIRTY
DIRTY --> CLEANING --> INSPECTION
Any necessary room block/incident may involve OUT_OF_SERVICE (recovery needs approved rules).
```

Allowed stored `rooms.operational_status`/history statuses **per the patch check constraints**: `AVAILABLE`, `OCCUPIED`, `DIRTY`, `CLEANING`, `INSPECTION`, `READY`, `OUT_OF_SERVICE`.

Patch replaces the original default (`AVAILABLE`) with **`DIRTY`** for new rooms.

Allowed `room_status_history.trigger_type`: `INIT`, `RELEASE`, `CHECKIN`, `CHECKOUT`, `CLEANING_TASK`, `INSPECTION`, `ROOM_BLOCK`, `MAINTENANCE`, `MANUAL_CORRECTION`, or NULL.

**Boundary:** Database CHECK constraints validate allowed *values*, not a full graph of allowed transitions; service logic and authorization checks are still required.

## 2. Reception in provisional sample data

`HotelOpsDB_SampleData_Final.sql` contains role `RECEPTION` (role_id 11), and room permissions as follows:

| Permission | Data scope | Intended use in seed/patch |
|---|---|---|
| `ROOM_RELEASE` | `BRANCH` | READY → AVAILABLE |
| `ROOM_CHECKIN` | `BRANCH` | AVAILABLE → OCCUPIED |
| `ROOM_CHECKOUT` | `BRANCH` | OCCUPIED → DIRTY |

`RECEPTION` is **not granted `ROOM_STATUS_UPDATE`** in the sample; patch describes this permission as exceptional/manual correction.

**Important:** These are *proposed implementation data* in provisional scripts. The current original SRS was written before Reception was requested; the revised permission matrix, final actors, and exceptional operations still require review/approval. Do not silently implement the seed as policy.

## 3. Role/department terminology conflicts needing decision

- Sample seed roles include `DEPT_MANAGER` (label/description: a generic head of an individual department) and `GENERAL_MANAGER`.
- The user clarified that the SRS role **Department Head** is responsible for managing **multiple departments within one hotel branch/facility**, NOT simply heading one department. The `DEPT_MANAGER` sample description is **not equivalent by default**.
- The original SRS also refers to `Operations Manager`, but that is **not a matching role code** in this sample role-code list. Do not equate it to `GENERAL_MANAGER`, `HK_MANAGER` or `DEPT_MANAGER` without review.
- `departments.manager_user_id` provides a per-department manager association; it does not by itself implement a multi-department manager authorization scope.
- Schema models scope through `users`, `user_roles`, `user_branch_access`, `role_permissions.data_scope`; the exact policy for Department Head must be agreed.

## 4. RBAC data structures

| Table | Function |
|---|---|
| `users` | User identity, primary branch/department fields, lifecycle and password hash |
| `roles` | Named role definitions |
| `permissions` | Permission codes, module/resource/action descriptions |
| `user_roles` | Role assignment to user and optional branch |
| `role_permissions` | Role-permission grant with patch-added `data_scope` |
| `user_branch_access` | Additional branch access and primary flags |
| `user_sessions` | Refresh token/session records |
| `audit_logs` | Append-style activity records |

Patch check for `role_permissions.data_scope` allows: `OWN`, `DEPARTMENT`, `BRANCH`, `ALL`. Scope meaning must be enforced in application query authorization; SQL check only restricts allowed labels.

## 5. Other patch checks and filtered uniqueness

Patch includes specific CHECK constraints for statuses on cleaning tasks/assignments/progress/reworks, inspection and defects, incident/maintenance, material requests, internal requests, and operational tasks. Refer to the **actual patch** for all allowed values; do not invent additional statuses from the original SRS.

Unique filtered indexes added by patch:

- `UX_cleaning_assignments_active`: one ACTIVE cleaning assignment per cleaning task.
- `UX_internal_request_assignments_active`: one ACTIVE assignment per internal request.
- `UX_operational_task_assignments_active`: one ACTIVE assignment per operational task.
- `UX_room_blocks_active`: one unreleased room block per room.
- `UX_user_branch_access_primary`: one non-revoked primary branch access per user.

The patch adds a check that `users.manager_user_id` must not equal the same `user_id`.

## 6. Setup / validation caveats

- **Do not run scripts automatically on an existing HotelOpsDB.** Base script uses unconditional `CREATE TABLE`; running again can fail. Patch `WITH CHECK` constraints can fail if legacy data violates new values.
- Seed file explicitly uses `SET IDENTITY_INSERT`, fixed IDs, and is intended **only for an empty database**; re-running can violate PK/UNIQUE or duplicate business data.
- Sample includes illustrative fake user data and placeholder password hashes. Do not assume test login works with the supplied hashes; do not export real credentials to version control.
- Sample contains validation SELECTs for room/history consistency and Reception grants. Those queries demonstrate sample expectations but are not full integrity or application tests.
- `HotelOpsDbContext`/`Entities` from the shared repo may have been scaffolded from an earlier database version; **compare against live SQL schema** (especially `role_permissions.data_scope` and room statuses) before implementing or re-scaffolding.
- The original SRS, SQL scripts, EF scaffold, and actual developer database could represent different versions; record the mismatch, seek confirmation, and do not silently reconcile.

## 7. What still requires team approval

- Final Reception vs Operations Manager permission split (and which role codes should represent each).
- Department Head exact role code, branch-scoped multi-department permissions, and reporting hierarchy.
- Final Permission Matrix by Use Case, role, branch/department/data scope.
- Authoritative room lifecycle transitions including exceptions/incident recovery and audit behavior.
- Whether these three scripts are the approved baseline and whether EF entities have been scaffolded from their resulting schema.

## 8. Codex behavior

For any feature touching RBAC, room statuses, or Department Head scope: read `docs/requirements/SRS_CHANGE_NOTES.md`, the relevant SRS sections, and both of these database reference documents. Inspect actual EF entities/DbContext and request confirmation when sources disagree. Do not commit code, change schema, run seed scripts, or claim approval from these draft documents.
