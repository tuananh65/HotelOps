# Task73 — conditionally approved V1 contract

Developer approved disposable-only schema/migration and independent implementation in
the Task73 reply; prioritized UC042/043. Notification-dependent UC029/041/046 must remain
PARTIAL unless DEV2 approves and integration verifies genuine delivery/retry. No automatic
grants, no DEV2 source edits, no shared/production deployment. Original proposal follows
for traceability; the explicit conditional approval takes precedence.

Preflight: 713 existing files snapshotted; no schema or DEV2 source edits performed.
AGENTS requires approval for shared schema/infrastructure and unresolved business mappings.
SRS remains the repository draft with Change Notes taking precedence; no newer signed matrix
was found. Reception/DH policies and UC006/016 are excluded.

## Proposed five UCs

| UC | Exact title | Weight | Required acceptance/dependency |
|---|---|---:|---|
| UC029 | Assign Cleaning Task | 3 | Active HK_STAFF in same branch/Housekeeping department, on shift, workload selector; due time; stale-write protection; assignment/audit; durable notification/retry |
| UC041 | Assign Room Inspection | 3 | Pending Inspection/Pending Reinspection cycle, effective Published applicable checklist, eligible inspector, no cleaner/reworker self-inspection, one open inspection; due time; audit/notification |
| UC042 | Perform Room Inspection | 3 | Assigned inspector only; Start, Save Draft, Finish; exact immutable checklist version; mandatory results and Fail→described defect; photos; Pending Conclusion; audit/rowversion |
| UC043 | Record Inspection Defects | 3 | Assigned inspector; Inspecting/Pending Conclusion only; failed criterion, description/severity/location/photos; private evidence; concluded-state rejection; atomic audit |
| UC046 | Request Cleaning Rework | 5 | Failed inspection, selected defects, eligible on-shift HK staff, due time, one open rework, Pending Acceptance; atomic audit/notification |

These five avoid changing calculated Room operational status or implementing work intervals.
They accept legitimate prerequisite cycles from existing/future completion/rejection writers;
no fabricated completion/rejection or auto-passing inspections. UC028/037/044/045 need the
unresolved GBR044 room-state/DEV2 block protocol; UC031/033–036 additionally need work intervals.
UC032 needs persisted per-cleaning-item snapshot/state absent from schema. These are not
safe replacements without another approved contract. Internal-request/task/shift candidates
also retain their documented model/notification/acknowledgment blockers.

## Concrete decisions requiring approval

1. Existing SQL cleaning/inspection/rework CHECKs only support coarse PENDING/IN_PROGRESS/
   COMPLETED/CANCELLED. They cannot distinguish SRS ASSIGNED, PENDING_CONCLUSION,
   PENDING_INSPECTION, REWORK_REQUIRED, PENDING_ACCEPTANCE, PENDING_REINSPECTION.
   Add nullable `workflow_status varchar(30)` to cleaning_tasks, room_inspections,
   cleaning_reworks with CHECKs using the actual SRS workflow states. Preserve existing
   `status` values as compatible coarse mirrors. No automatic backfill/legacy conversion;
   explicit opt-in only where source meaning is proved, otherwise fail closed. Legacy
   COMPLETED cleaning rows alone do NOT establish Pending Inspection or Ready.
2. Add real `row_version` metadata to those aggregates (GBR005), inspection `due_at`,
   rework `due_at`, defect `room_location nvarchar(250) NULL`, and explicit bridges for
   rework→selected defects and defect→attachments. No JSON/audit payload used as a substitute
   for missing relational links. Generated entities untouched; controlled opt-in mappings.
3. Existing permissions only: CLEANING_ASSIGN (UC029), INSPECTION_PERFORM for inspection
   assignment/execution/defect/rework, and INSPECTION_VIEW/CLEANING_VIEW for scoped selectors.
   Confirm UC041 assignment under INSPECTION_PERFORM because no INSPECTION_ASSIGN exists.
   No grant changes or role-name authorization bypass. HK role/department/shift checks
   are additional eligibility, not substitutes for SQL permissions or branch membership.
4. Preserve Task72 nullable checklist ownership, same-branch applicability, immutable
   Published/used versions and effective active criteria. New inspection picks applicable
   effective latest Published; existing assignment retains version even after retirement.
5. Notifications require a DEV2 handoff: no notification producer/retry interface exists
   in this checkout. Proposed DEV1-owned durable outbox records business event, authorized
   recipients, source ID and retry state in the feature transaction. DEV2 must approve the
   adapter/delivery contract before notification-dependent UCs can be complete. Do not
   claim an outbox without delivery/retry verification satisfies SRS “sends in-app”. Do
   not change DEV2-owned source. If this contract is not approved, retain those UCs PARTIAL.
6. Private JPG/PNG/PDF storage uses the Task72 trusted private root and attachment policy,
   max10MiB/5 files. Source-authorized download, same-branch/assigned inspector, and
   filesystem staging/SQL transaction failure cleanup. No public FileUrl or external fetch.
7. Migration only on exact guarded disposable trials, opt-in canonical integration,
   strict schema/version verification, fresh/populated-legacy/replay/fault/rollback tests;
   rollback refuses owned business data. No startup DDL or production/shared deployment.

Pending approval, implement only independently safe preparation/read infrastructure,
preserve partial work and report no earned UC points from an incomplete SRS workflow.
