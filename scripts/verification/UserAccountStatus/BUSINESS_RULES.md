# SRS rules and dependency inventory

Sources: AGENTS.md; historical `docs/requirements/HotelOps_SRS_DRAFT.md` UC-006, GBR-012/026/027/028 and User transition appendix; SRS_CHANGE_NOTES; Task21 UserManagement authorization/outstanding decisions; Task24 UserManagementMutation decisions; Task26 UserProfileUpdate; Task32 UserCredentialReset; Task33 AdminReauthentication; Task34 CredentialResetRelease; current entities/DbContext/services; explicit Task35 approval in this session.

Confirmed: ACTIVE and INACTIVE account transitions only; reason >=10 after trim for both; reject no-op; preserve employment/profile/credential/lock/roles/branch/department. Reactivation must not clear LockedUntil or recreate sessions. Deactivation must revoke all effective sessions atomically and retain history. Self activation/deactivation is denied. Minimum last-admin capability policy is recorded in AUTHORIZATION.md; no broad SYS_ADMIN/OWNER protection was invented.

GBR-028 requires reassignment/handover before deactivation, not cancellation/deletion. No operational assignment is changed by this implementation.

## Exact metadata observations, not an open-work predicate

`Inspect-Dependencies.ps1` selected SQL metadata from a guarded canonical Task35 trial: **71 foreign keys to users across 53 tables**, and **45 CHECK constraints** for referencing/related tables. The local JSON contains exact constraint/FK names, referenced columns and disabled/trust flags; it contains no user/session payload. Reproduce rather than commit the runtime receipt. These observations describe the trial baseline, not HotelOpsDB or all external writers.

Relevant allowed values observed in actual CHECK definitions:

| Table / status column | Allowed values (no open/closed classification implied) |
|---|---|
| cleaning_assignments.assignment_status | ACTIVE, COMPLETED, REASSIGNED, CANCELLED |
| cleaning_tasks.status; cleaning_reworks.status | PENDING, IN_PROGRESS, COMPLETED, CANCELLED |
| operational_task_assignments.assignment_status | ACTIVE, COMPLETED, REASSIGNED, CANCELLED |
| operational_tasks.status | TODO, IN_PROGRESS, BLOCKED, COMPLETED, CANCELLED |
| internal_request_assignments.status | ACTIVE, COMPLETED, REASSIGNED, CANCELLED |
| internal_requests.status | DRAFT, SUBMITTED, IN_PROGRESS, COMPLETED, CLOSED, REJECTED, CANCELLED |
| incidents.status | OPEN, IN_PROGRESS, RESOLVED, CLOSED, CANCELLED |
| maintenance_works.status | PENDING, IN_PROGRESS, ON_HOLD, COMPLETED, CLOSED, CANCELLED |
| material_request_approval_cycles.status | PENDING, NEEDS_INFORMATION, APPROVED, REJECTED, SUPERSEDED |

Assignment EndedAt plus parent status must be reconciled; no assumption that only assignment_status=ACTIVE suffices. Incident reported_by is reporter history, not a proven current assignee relation. Maintenance COMPLETED can still await review. Material approval assigned_approver_id and AssetHandover.to_user_id/from_user_id are real dependencies needing owner-confirmed predicates. AssetHandover has handover_status/accepted_at, not an invented Status field; no CHECK for handover status was returned by this inventory. Logical links such as operational_tasks.source_type/source_id are not discovered by FK-to-user enumeration and require explicit feature review.

All FK-to-user columns observed (every row references users.user_id):

| Table | Columns |
|---|---|
| asset_condition_history | changed_by |
| asset_handovers | created_by, from_user_id, to_user_id |
| asset_location_history | moved_by |
| assets | custodian_user_id |
| attachments | uploaded_by |
| audit_logs | actor_user_id |
| cleaning_assignments | assigned_by, assignee_user_id |
| cleaning_progress_logs | logged_by |
| cleaning_reworks | assignee_user_id |
| cleaning_tasks | created_by |
| departments | manager_user_id |
| incident_events | actor_id |
| incidents | reported_by |
| inspection_checklist_versions | published_by |
| inspection_checklists | created_by |
| inspection_defects | assigned_to |
| internal_request_assignments | assigned_by, assignee_id |
| internal_request_events | actor_id |
| internal_request_feedback | submitted_by |
| internal_requests | requester_id |
| maintenance_work_logs | logged_by |
| maintenance_works | assignee_id, created_by, reviewed_by |
| material_fulfillments | fulfilled_by, received_by |
| material_replenishment_receipt_lines | linked_by |
| material_replenishments | closed_by, requested_by |
| material_request_approval_cycles | assigned_approver_id, submitted_by |
| material_request_approvals | approver_id |
| material_requests | remainder_closed_by, requester_id |
| notification_recipients | user_id |
| notifications | created_by |
| operational_task_assignments | assigned_by, assignee_id |
| operational_task_progress | reported_by |
| operational_tasks | created_by |
| preventive_maintenance_runs | performed_by |
| preventive_maintenance_schedules | created_by |
| role_permissions | granted_by |
| room_blocks | blocked_by, released_by |
| room_inspections | inspector_user_id |
| room_status_history | changed_by |
| shift_assignments | assigned_by, user_id |
| shift_attendance | verified_by |
| stock_documents | approved_by, created_by, posted_by |
| stock_movements | created_by |
| stock_transfer_receipts | received_by |
| stocktake_lines | counted_by |
| stocktakes | approved_by, counted_by |
| system_settings | updated_by |
| user_branch_access | granted_by, user_id |
| user_roles | assigned_by, user_id |
| user_sessions | user_id |
| users | manager_user_id |
| warehouses | manager_user_id |
| work_shifts | created_by |

FK presence alone does not make historical log authors an open responsibility; treating all references as active work would block every audited user forever. Nor does absence of an FK prove no responsibility.

## Writers inspected and required coordination

| Responsibility | Current source / evidence | Gate |
|---|---|---|
| Maintenance assignment | MaintenanceWorkService.Commands.AssignWorkAsync -> RequireUserAsync only AnyAsync(user_id); work/incident locks are separate | DEV2 must review ACTIVE eligibility + retained assignee lock/order |
| Maintenance review/reopen | MaintenanceWorkService.Workflow; COMPLETED review, ReviewedBy changes | Confirm pending-review ownership and reopen coordination |
| Material approval cycle | MaterialRequestService.Cycle creates assigned_approver_id/PENDING; Commands create/update/resubmit paths | DEV2 must review approver eligibility/locking and NEEDS_INFORMATION lifecycle |
| Asset handover/custody | AssetService.Handover Create/Finalize; ValidateHandoverTarget calls existence-check convention; sets assets.custodian_user_id | DEV2 confirm handover/custody predicate and serialized receiver eligibility |
| Cleaning/rework/operational/internal-request assignments | Entities and current resource readers exist; rg found no assignment writer in current Services for these entities | Need future BE1 writers plus external/SQL/import job inventory; absence is not certification |
| Inspection, shifts, branch/department/warehouse management, replenishment/stock responsibilities | Real FK inventory above; not a certified dependency adapter | Feature owner must decide current responsibility vs retained historical actor |

This is a concrete inventory and coordination proposal, **not a complete writer certification**. All external jobs/direct SQL writers and future modules remain unverified. No DEV2 file was changed and no unsafe deactivation query/route was introduced.
