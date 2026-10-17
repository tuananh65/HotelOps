# HotelOps — Database Schema Reference (DRAFT)
> **Source-derived snapshot, not a verified live database.** This document is generated from the uploaded scripts in the stated order. DB design, role/permission assignments, and SRS are still provisional. Never assume the SQL files have been successfully applied to the target database.

## SQL inputs and execution order

1. `HotelOpsDB_SQLServer.sql` — initial `HotelOpsDB` schema. (Uploaded basename: `HotelOpsDB_SQLServer(2).sql`.)
2. `HotelOpsDB_Patch_v3_1_Fixed.sql` — constraints, status semantics, RBAC `data_scope`, filtered indexes. **This is the actual uploaded patch filename**, although its header refers to `HotelOpsDB_Patch_v3_Final.sql`.
3. `HotelOpsDB_SampleData_Final.sql` — illustrative seed records. **Only on a new/empty database.**

**Do not automatically run these scripts.** The application is Database First, and the current EF Core mappings must be checked against the database that is actually deployed.

## Inventory

- Schema from initial script: **72 tables**, **201 explicit foreign-key declarations**, **48 indexes** (not counting PK constraints).
- Patch adds **5 filtered unique indexes** and several check constraints.
- Sample includes `INSERT` statements for **72 tables**; values are synthetic/illustrative and not approval of business rules.
- Primary schema: `dbo`. Database: `HotelOpsDB`.

## Tables by functional area

Groups below are for navigation, **not a strict assignment of table ownership** to BE1 or BE2.

### Organization & rooms

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `branches` | `branch_id` | 8 | — |
| `floors` | `floor_id` | 7 | `branch_id` → `branches` |
| `areas` | `area_id` | 7 | `branch_id` → `branches`, `floor_id` → `floors` |
| `room_types` | `room_type_id` | 6 | — |
| `rooms` | `room_id` | 9 | `branch_id` → `branches`, `floor_id` → `floors`, `area_id` → `areas`, `room_type_id` → `room_types` |
| `departments` | `department_id` | 7 | `branch_id` → `branches`, `manager_user_id` → `users` |
| `room_status_history` | `history_id` | 8 | `room_id` → `rooms`, `changed_by` → `users` |
| `room_blocks` | `block_id` | 7 | `room_id` → `rooms`, `incident_id` → `incidents`, `blocked_by` → `users`, `released_by` → `users` |

### Access & settings

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `users` | `user_id` | 13 | `primary_branch_id` → `branches`, `department_id` → `departments`, `manager_user_id` → `users` |
| `roles` | `role_id` | 6 | — |
| `permissions` | `permission_id` | 7 | — |
| `user_roles` | `user_role_id` | 6 | `user_id` → `users`, `role_id` → `roles`, `branch_id` → `branches`, `assigned_by` → `users` |
| `role_permissions` | `role_permission_id` | 4 | `role_id` → `roles`, `permission_id` → `permissions`, `granted_by` → `users` |
| `user_branch_access` | `access_id` | 6 | `user_id` → `users`, `branch_id` → `branches`, `granted_by` → `users` |
| `user_sessions` | `session_id` | 6 | `user_id` → `users` |
| `system_settings` | `setting_id` | 9 | `branch_id` → `branches`, `updated_by` → `users` |

### Housekeeping & inspection

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `cleaning_tasks` | `cleaning_task_id` | 9 | `room_id` → `rooms`, `cleaning_type_id` → `reference_values`, `priority_id` → `reference_values`, `created_by` → `users` |
| `cleaning_assignments` | `assignment_id` | 6 | `cleaning_task_id` → `cleaning_tasks`, `assignee_user_id` → `users`, `assigned_by` → `users` |
| `cleaning_progress_logs` | `progress_id` | 7 | `cleaning_task_id` → `cleaning_tasks`, `blocker_type_id` → `reference_values`, `logged_by` → `users` |
| `cleaning_reworks` | `rework_id` | 7 | `cleaning_task_id` → `cleaning_tasks`, `inspection_id` → `room_inspections`, `assignee_user_id` → `users` |
| `inspection_checklists` | `checklist_id` | 7 | `room_type_id` → `room_types`, `created_by` → `users` |
| `inspection_checklist_versions` | `version_id` | 5 | `checklist_id` → `inspection_checklists`, `published_by` → `users` |
| `inspection_checklist_items` | `item_id` | 8 | `version_id` → `inspection_checklist_versions` |
| `room_inspections` | `inspection_id` | 9 | `room_id` → `rooms`, `cleaning_task_id` → `cleaning_tasks`, `version_id` → `inspection_checklist_versions`, `inspector_user_id` → `users` |
| `inspection_results` | `result_id` | 5 | `inspection_id` → `room_inspections`, `checklist_item_id` → `inspection_checklist_items` |
| `inspection_defects` | `defect_id` | 9 | `inspection_id` → `room_inspections`, `checklist_item_id` → `inspection_checklist_items`, `severity_id` → `reference_values`, `assigned_to` → `users` |

### Incidents & maintenance

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `incidents` | `incident_id` | 13 | `branch_id` → `branches`, `room_id` → `rooms`, `area_id` → `areas`, `asset_id` → `assets`, `severity_id` → `reference_values`, `priority_id` → `reference_values`, `reported_by` → `users` |
| `incident_events` | `event_id` | 7 | `incident_id` → `incidents`, `actor_id` → `users` |
| `maintenance_works` | `maintenance_work_id` | 12 | `incident_id` → `incidents`, `assignee_id` → `users`, `department_id` → `departments`, `priority_id` → `reference_values`, `reviewed_by` → `users`, `created_by` → `users` |
| `maintenance_work_logs` | `log_id` | 7 | `maintenance_work_id` → `maintenance_works`, `material_issue_id` → `stock_documents`, `logged_by` → `users` |
| `preventive_maintenance_schedules` | `schedule_id` | 11 | `asset_id` → `assets`, `room_id` → `rooms`, `area_id` → `areas`, `assigned_department_id` → `departments`, `created_by` → `users` |
| `preventive_maintenance_runs` | `run_id` | 6 | `schedule_id` → `preventive_maintenance_schedules`, `maintenance_work_id` → `maintenance_works`, `performed_by` → `users` |

### Assets

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `asset_types` | `asset_type_id` | 5 | — |
| `assets` | `asset_id` | 18 | `asset_type_id` → `asset_types`, `branch_id` → `branches`, `current_room_id` → `rooms`, `current_area_id` → `areas`, `current_department_id` → `departments`, `custodian_user_id` → `users` |
| `asset_location_history` | `location_history_id` | 8 | `asset_id` → `assets`, `moved_by` → `users` |
| `asset_handovers` | `handover_id` | 10 | `asset_id` → `assets`, `from_user_id` → `users`, `to_user_id` → `users`, `from_department_id` → `departments`, `to_department_id` → `departments`, `created_by` → `users` |
| `asset_condition_history` | `condition_history_id` | 7 | `asset_id` → `assets`, `related_incident_id` → `incidents`, `changed_by` → `users` |

### Inventory & materials

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `warehouses` | `warehouse_id` | 8 | `branch_id` → `branches`, `department_id` → `departments`, `manager_user_id` → `users` |
| `item_categories` | `item_category_id` | 5 | — |
| `units_of_measure` | `uom_id` | 5 | — |
| `inventory_items` | `item_id` | 9 | `category_id` → `item_categories`, `base_uom_id` → `units_of_measure` |
| `uom_conversions` | `conversion_id` | 6 | `item_id` → `inventory_items`, `from_uom_id` → `units_of_measure`, `to_uom_id` → `units_of_measure` |
| `stock_thresholds` | `threshold_id` | 7 | `warehouse_id` → `warehouses`, `item_id` → `inventory_items` |
| `stock_documents` | `document_id` | 15 | `source_warehouse_id` → `warehouses`, `destination_warehouse_id` → `warehouses`, `created_by` → `users`, `approved_by` → `users`, `posted_by` → `users`, `reversal_of_document_id` → `stock_documents` |
| `stock_document_lines` | `line_id` | 11 | `document_id` → `stock_documents`, `item_id` → `inventory_items`, `uom_id` → `units_of_measure` |
| `stock_movements` | `movement_id` | 7 | `document_line_id` → `stock_document_lines`, `warehouse_id` → `warehouses`, `item_id` → `inventory_items`, `created_by` → `users` |
| `stocktakes` | `stocktake_id` | 8 | `warehouse_id` → `warehouses`, `counted_by` → `users`, `approved_by` → `users`, `adjustment_document_id` → `stock_documents` |
| `stocktake_lines` | `stocktake_line_id` | 8 | `stocktake_id` → `stocktakes`, `item_id` → `inventory_items`, `counted_by` → `users` |
| `material_requests` | `request_id` | 10 | `branch_id` → `branches`, `department_id` → `departments`, `requester_id` → `users`, `source_warehouse_id` → `warehouses`, `priority_id` → `reference_values` |
| `material_request_lines` | `line_id` | 9 | `request_id` → `material_requests`, `item_id` → `inventory_items`, `uom_id` → `units_of_measure` |
| `material_request_approvals` | `approval_id` | 6 | `request_id` → `material_requests`, `approver_id` → `users` |
| `material_fulfillments` | `fulfillment_id` | 9 | `request_id` → `material_requests`, `stock_document_id` → `stock_documents`, `warehouse_id` → `warehouses`, `fulfilled_by` → `users`, `received_by` → `users` |
| `material_fulfillment_lines` | `fulfillment_line_id` | 8 | `fulfillment_id` → `material_fulfillments`, `request_line_id` → `material_request_lines`, `item_id` → `inventory_items`, `uom_id` → `units_of_measure` |

### Operational tasks, shifts & internal requests

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `internal_request_types` | `request_type_id` | 8 | `default_department_id` → `departments`, `default_priority_id` → `reference_values` |
| `internal_requests` | `internal_request_id` | 15 | `branch_id` → `branches`, `request_type_id` → `internal_request_types`, `requester_id` → `users`, `requester_department_id` → `departments`, `receiving_department_id` → `departments`, `room_id` → `rooms`, `area_id` → `areas`, `related_incident_id` → `incidents`, `priority_id` → `reference_values` |
| `internal_request_assignments` | `assignment_id` | 6 | `internal_request_id` → `internal_requests`, `assignee_id` → `users`, `assigned_by` → `users` |
| `internal_request_events` | `event_id` | 7 | `internal_request_id` → `internal_requests`, `actor_id` → `users` |
| `internal_request_feedback` | `feedback_id` | 6 | `internal_request_id` → `internal_requests`, `submitted_by` → `users` |
| `operational_tasks` | `task_id` | 17 | `branch_id` → `branches`, `department_id` → `departments`, `category_id` → `work_categories`, `priority_id` → `reference_values`, `room_id` → `rooms`, `area_id` → `areas`, `parent_task_id` → `operational_tasks`, `created_by` → `users` |
| `operational_task_assignments` | `assignment_id` | 6 | `task_id` → `operational_tasks`, `assignee_id` → `users`, `assigned_by` → `users` |
| `operational_task_progress` | `progress_id` | 8 | `task_id` → `operational_tasks`, `reported_by` → `users` |
| `operational_task_dependencies` | `dependency_id` | 5 | `predecessor_task_id` → `operational_tasks`, `successor_task_id` → `operational_tasks` |
| `shift_templates` | `shift_template_id` | 9 | `branch_id` → `branches` |
| `work_shifts` | `shift_id` | 9 | `branch_id` → `branches`, `department_id` → `departments`, `shift_template_id` → `shift_templates`, `created_by` → `users` |
| `shift_assignments` | `shift_assignment_id` | 6 | `shift_id` → `work_shifts`, `user_id` → `users`, `assigned_by` → `users` |
| `shift_attendance` | `attendance_id` | 7 | `shift_assignment_id` → `shift_assignments`, `verified_by` → `users` |

### Cross-cutting

| Table | Primary key | Columns | Foreign-key references |
|---|---|---:|---|
| `reference_values` | `reference_id` | 7 | — |
| `work_categories` | `category_id` | 6 | `department_id` → `departments` |
| `notifications` | `notification_id` | 9 | `created_by` → `users` |
| `notification_recipients` | `recipient_id` | 6 | `notification_id` → `notifications`, `user_id` → `users` |
| `attachments` | `attachment_id` | 11 | `uploaded_by` → `users` |
| `audit_logs` | `audit_id` | 12 | `branch_id` → `branches`, `actor_user_id` → `users` |

## Per-table columns (as declared in the base script)

**Important:** These are **base-script declarations**. Patch v3.1 subsequently adds `role_permissions.data_scope` and changes room/status constraints.

### `dbo.branches`

Columns: `branch_id`, `branch_code`, `branch_name`, `address`, `phone`, `email`, `timezone`, `is_active`.

### `dbo.floors`

Columns: `floor_id`, `branch_id`, `floor_code`, `floor_name`, `floor_number`, `description`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`.

### `dbo.areas`

Columns: `area_id`, `branch_id`, `floor_id`, `area_code`, `area_name`, `description`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `floor_id` → `dbo.floors(floor_id)`.

### `dbo.room_types`

Columns: `room_type_id`, `type_code`, `type_name`, `description`, `standard_capacity`, `is_active`.

### `dbo.rooms`

Columns: `room_id`, `branch_id`, `floor_id`, `area_id`, `room_type_id`, `room_number`, `operational_status`, `description`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `floor_id` → `dbo.floors(floor_id)`, `area_id` → `dbo.areas(area_id)`, `room_type_id` → `dbo.room_types(room_type_id)`.

### `dbo.departments`

Columns: `department_id`, `branch_id`, `department_code`, `department_name`, `manager_user_id`, `scope_description`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `manager_user_id` → `dbo.users(user_id)`.

### `dbo.reference_values`

Columns: `reference_id`, `group_code`, `value_code`, `display_name`, `description`, `sort_order`, `is_active`.

### `dbo.work_categories`

Columns: `category_id`, `category_code`, `category_name`, `department_id`, `description`, `is_active`.

Foreign keys: `department_id` → `dbo.departments(department_id)`.

### `dbo.cleaning_tasks`

Columns: `cleaning_task_id`, `task_no`, `room_id`, `cleaning_type_id`, `priority_id`, `instruction`, `status`, `completion_note`, `created_by`.

Foreign keys: `room_id` → `dbo.rooms(room_id)`, `cleaning_type_id` → `dbo.reference_values(reference_id)`, `priority_id` → `dbo.reference_values(reference_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.cleaning_assignments`

Columns: `assignment_id`, `cleaning_task_id`, `assignee_user_id`, `assigned_by`, `assignment_status`, `reason`.

Foreign keys: `cleaning_task_id` → `dbo.cleaning_tasks(cleaning_task_id)`, `assignee_user_id` → `dbo.users(user_id)`, `assigned_by` → `dbo.users(user_id)`.

### `dbo.cleaning_progress_logs`

Columns: `progress_id`, `cleaning_task_id`, `progress_percent`, `status`, `blocker_type_id`, `note`, `logged_by`.

Foreign keys: `cleaning_task_id` → `dbo.cleaning_tasks(cleaning_task_id)`, `blocker_type_id` → `dbo.reference_values(reference_id)`, `logged_by` → `dbo.users(user_id)`.

### `dbo.cleaning_reworks`

Columns: `rework_id`, `cleaning_task_id`, `inspection_id`, `assignee_user_id`, `reason`, `status`, `note`.

Foreign keys: `cleaning_task_id` → `dbo.cleaning_tasks(cleaning_task_id)`, `inspection_id` → `dbo.room_inspections(inspection_id)`, `assignee_user_id` → `dbo.users(user_id)`.

### `dbo.room_status_history`

Columns: `history_id`, `room_id`, `from_status`, `to_status`, `trigger_type`, `trigger_id`, `changed_by`, `reason`.

Foreign keys: `room_id` → `dbo.rooms(room_id)`, `changed_by` → `dbo.users(user_id)`.

### `dbo.inspection_checklists`

Columns: `checklist_id`, `checklist_code`, `checklist_name`, `room_type_id`, `description`, `is_active`, `created_by`.

Foreign keys: `room_type_id` → `dbo.room_types(room_type_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.inspection_checklist_versions`

Columns: `version_id`, `checklist_id`, `version_no`, `status`, `published_by`.

Foreign keys: `checklist_id` → `dbo.inspection_checklists(checklist_id)`, `published_by` → `dbo.users(user_id)`.

### `dbo.inspection_checklist_items`

Columns: `item_id`, `version_id`, `criterion`, `description`, `pass_condition`, `is_required`, `sort_order`, `is_active`.

Foreign keys: `version_id` → `dbo.inspection_checklist_versions(version_id)`.

### `dbo.room_inspections`

Columns: `inspection_id`, `inspection_no`, `room_id`, `cleaning_task_id`, `version_id`, `inspector_user_id`, `status`, `result`, `summary_note`.

Foreign keys: `room_id` → `dbo.rooms(room_id)`, `cleaning_task_id` → `dbo.cleaning_tasks(cleaning_task_id)`, `version_id` → `dbo.inspection_checklist_versions(version_id)`, `inspector_user_id` → `dbo.users(user_id)`.

### `dbo.inspection_results`

Columns: `result_id`, `inspection_id`, `checklist_item_id`, `outcome`, `note`.

Foreign keys: `inspection_id` → `dbo.room_inspections(inspection_id)`, `checklist_item_id` → `dbo.inspection_checklist_items(item_id)`.

### `dbo.inspection_defects`

Columns: `defect_id`, `inspection_id`, `checklist_item_id`, `severity_id`, `description`, `corrective_action`, `status`, `assigned_to`, `resolution_note`.

Foreign keys: `inspection_id` → `dbo.room_inspections(inspection_id)`, `checklist_item_id` → `dbo.inspection_checklist_items(item_id)`, `severity_id` → `dbo.reference_values(reference_id)`, `assigned_to` → `dbo.users(user_id)`.

### `dbo.incidents`

Columns: `incident_id`, `incident_no`, `branch_id`, `room_id`, `area_id`, `asset_id`, `incident_title`, `description`, `severity_id`, `priority_id`, `status`, `reported_by`, `closure_note`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `room_id` → `dbo.rooms(room_id)`, `area_id` → `dbo.areas(area_id)`, `asset_id` → `dbo.assets(asset_id)`, `severity_id` → `dbo.reference_values(reference_id)`, `priority_id` → `dbo.reference_values(reference_id)`, `reported_by` → `dbo.users(user_id)`.

### `dbo.incident_events`

Columns: `event_id`, `incident_id`, `event_type`, `from_status`, `to_status`, `note`, `actor_id`.

Foreign keys: `incident_id` → `dbo.incidents(incident_id)`, `actor_id` → `dbo.users(user_id)`.

### `dbo.maintenance_works`

Columns: `maintenance_work_id`, `work_no`, `incident_id`, `assignee_id`, `department_id`, `title`, `instruction`, `priority_id`, `status`, `resolution_note`, `reviewed_by`, `created_by`.

Foreign keys: `incident_id` → `dbo.incidents(incident_id)`, `assignee_id` → `dbo.users(user_id)`, `department_id` → `dbo.departments(department_id)`, `priority_id` → `dbo.reference_values(reference_id)`, `reviewed_by` → `dbo.users(user_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.maintenance_work_logs`

Columns: `log_id`, `maintenance_work_id`, `work_note`, `spent_minutes`, `material_issue_id`, `status`, `logged_by`.

Foreign keys: `maintenance_work_id` → `dbo.maintenance_works(maintenance_work_id)`, `material_issue_id` → `dbo.stock_documents(document_id)`, `logged_by` → `dbo.users(user_id)`.

### `dbo.preventive_maintenance_schedules`

Columns: `schedule_id`, `schedule_code`, `asset_id`, `room_id`, `area_id`, `frequency`, `frequency_interval`, `instruction`, `assigned_department_id`, `is_active`, `created_by`.

Foreign keys: `asset_id` → `dbo.assets(asset_id)`, `room_id` → `dbo.rooms(room_id)`, `area_id` → `dbo.areas(area_id)`, `assigned_department_id` → `dbo.departments(department_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.preventive_maintenance_runs`

Columns: `run_id`, `schedule_id`, `maintenance_work_id`, `result`, `note`, `performed_by`.

Foreign keys: `schedule_id` → `dbo.preventive_maintenance_schedules(schedule_id)`, `maintenance_work_id` → `dbo.maintenance_works(maintenance_work_id)`, `performed_by` → `dbo.users(user_id)`.

### `dbo.room_blocks`

Columns: `block_id`, `room_id`, `incident_id`, `reason`, `blocked_by`, `released_by`, `release_note`.

Foreign keys: `room_id` → `dbo.rooms(room_id)`, `incident_id` → `dbo.incidents(incident_id)`, `blocked_by` → `dbo.users(user_id)`, `released_by` → `dbo.users(user_id)`.

### `dbo.asset_types`

Columns: `asset_type_id`, `type_code`, `type_name`, `description`, `is_active`.

### `dbo.assets`

Columns: `asset_id`, `asset_code`, `asset_name`, `asset_type_id`, `branch_id`, `serial_number`, `brand`, `model`, `purchase_date`, `warranty_expiry_date`, `acquisition_cost`, `condition_status`, `lifecycle_status`, `current_room_id`, `current_area_id`, `current_department_id`, `custodian_user_id`, `description`.

Foreign keys: `asset_type_id` → `dbo.asset_types(asset_type_id)`, `branch_id` → `dbo.branches(branch_id)`, `current_room_id` → `dbo.rooms(room_id)`, `current_area_id` → `dbo.areas(area_id)`, `current_department_id` → `dbo.departments(department_id)`, `custodian_user_id` → `dbo.users(user_id)`.

### `dbo.asset_location_history`

Columns: `location_history_id`, `asset_id`, `from_location_type`, `from_location_id`, `to_location_type`, `to_location_id`, `moved_by`, `reason`.

Foreign keys: `asset_id` → `dbo.assets(asset_id)`, `moved_by` → `dbo.users(user_id)`.

### `dbo.asset_handovers`

Columns: `handover_id`, `handover_no`, `asset_id`, `from_user_id`, `to_user_id`, `from_department_id`, `to_department_id`, `condition_note`, `handover_status`, `created_by`.

Foreign keys: `asset_id` → `dbo.assets(asset_id)`, `from_user_id` → `dbo.users(user_id)`, `to_user_id` → `dbo.users(user_id)`, `from_department_id` → `dbo.departments(department_id)`, `to_department_id` → `dbo.departments(department_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.asset_condition_history`

Columns: `condition_history_id`, `asset_id`, `old_condition`, `new_condition`, `note`, `related_incident_id`, `changed_by`.

Foreign keys: `asset_id` → `dbo.assets(asset_id)`, `related_incident_id` → `dbo.incidents(incident_id)`, `changed_by` → `dbo.users(user_id)`.

### `dbo.warehouses`

Columns: `warehouse_id`, `branch_id`, `department_id`, `warehouse_code`, `warehouse_name`, `location_description`, `manager_user_id`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `department_id` → `dbo.departments(department_id)`, `manager_user_id` → `dbo.users(user_id)`.

### `dbo.item_categories`

Columns: `item_category_id`, `category_code`, `category_name`, `description`, `is_active`.

### `dbo.units_of_measure`

Columns: `uom_id`, `uom_code`, `uom_name`, `description`, `is_active`.

### `dbo.inventory_items`

Columns: `item_id`, `item_code`, `item_name`, `category_id`, `base_uom_id`, `description`, `specification`, `is_consumable`, `is_active`.

Foreign keys: `category_id` → `dbo.item_categories(item_category_id)`, `base_uom_id` → `dbo.units_of_measure(uom_id)`.

### `dbo.uom_conversions`

Columns: `conversion_id`, `item_id`, `from_uom_id`, `to_uom_id`, `conversion_factor`, `is_active`.

Foreign keys: `item_id` → `dbo.inventory_items(item_id)`, `from_uom_id` → `dbo.units_of_measure(uom_id)`, `to_uom_id` → `dbo.units_of_measure(uom_id)`.

### `dbo.stock_thresholds`

Columns: `threshold_id`, `warehouse_id`, `item_id`, `minimum_qty`, `maximum_qty`, `reorder_qty`, `alert_enabled`.

Foreign keys: `warehouse_id` → `dbo.warehouses(warehouse_id)`, `item_id` → `dbo.inventory_items(item_id)`.

### `dbo.stock_documents`

Columns: `document_id`, `document_no`, `document_type`, `source_warehouse_id`, `destination_warehouse_id`, `status`, `reference_type`, `reference_id`, `document_date`, `reason`, `note`, `created_by`, `approved_by`, `posted_by`, `reversal_of_document_id`.

Foreign keys: `source_warehouse_id` → `dbo.warehouses(warehouse_id)`, `destination_warehouse_id` → `dbo.warehouses(warehouse_id)`, `created_by` → `dbo.users(user_id)`, `approved_by` → `dbo.users(user_id)`, `posted_by` → `dbo.users(user_id)`, `reversal_of_document_id` → `dbo.stock_documents(document_id)`.

### `dbo.stock_document_lines`

Columns: `line_id`, `document_id`, `line_no`, `item_id`, `uom_id`, `conversion_factor`, `requested_qty`, `actual_qty`, `base_qty`, `unit_cost`, `note`.

Foreign keys: `document_id` → `dbo.stock_documents(document_id)`, `item_id` → `dbo.inventory_items(item_id)`, `uom_id` → `dbo.units_of_measure(uom_id)`.

### `dbo.stock_movements`

Columns: `movement_id`, `document_line_id`, `warehouse_id`, `item_id`, `signed_base_qty`, `unit_cost`, `created_by`.

Foreign keys: `document_line_id` → `dbo.stock_document_lines(line_id)`, `warehouse_id` → `dbo.warehouses(warehouse_id)`, `item_id` → `dbo.inventory_items(item_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.stocktakes`

Columns: `stocktake_id`, `stocktake_no`, `warehouse_id`, `status`, `counted_by`, `approved_by`, `adjustment_document_id`, `note`.

Foreign keys: `warehouse_id` → `dbo.warehouses(warehouse_id)`, `counted_by` → `dbo.users(user_id)`, `approved_by` → `dbo.users(user_id)`, `adjustment_document_id` → `dbo.stock_documents(document_id)`.

### `dbo.stocktake_lines`

Columns: `stocktake_line_id`, `stocktake_id`, `item_id`, `system_qty`, `counted_qty`, `variance_qty`, `discrepancy_reason`, `counted_by`.

Foreign keys: `stocktake_id` → `dbo.stocktakes(stocktake_id)`, `item_id` → `dbo.inventory_items(item_id)`, `counted_by` → `dbo.users(user_id)`.

### `dbo.material_requests`

Columns: `request_id`, `request_no`, `branch_id`, `department_id`, `requester_id`, `source_warehouse_id`, `purpose`, `priority_id`, `status`, `cancellation_reason`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `department_id` → `dbo.departments(department_id)`, `requester_id` → `dbo.users(user_id)`, `source_warehouse_id` → `dbo.warehouses(warehouse_id)`, `priority_id` → `dbo.reference_values(reference_id)`.

### `dbo.material_request_lines`

Columns: `line_id`, `request_id`, `line_no`, `item_id`, `uom_id`, `requested_qty`, `approved_qty`, `fulfilled_qty`, `note`.

Foreign keys: `request_id` → `dbo.material_requests(request_id)`, `item_id` → `dbo.inventory_items(item_id)`, `uom_id` → `dbo.units_of_measure(uom_id)`.

### `dbo.material_request_approvals`

Columns: `approval_id`, `request_id`, `approval_level`, `approver_id`, `decision`, `decision_note`.

Foreign keys: `request_id` → `dbo.material_requests(request_id)`, `approver_id` → `dbo.users(user_id)`.

### `dbo.material_fulfillments`

Columns: `fulfillment_id`, `fulfillment_no`, `request_id`, `stock_document_id`, `warehouse_id`, `fulfilled_by`, `received_by`, `status`, `note`.

Foreign keys: `request_id` → `dbo.material_requests(request_id)`, `stock_document_id` → `dbo.stock_documents(document_id)`, `warehouse_id` → `dbo.warehouses(warehouse_id)`, `fulfilled_by` → `dbo.users(user_id)`, `received_by` → `dbo.users(user_id)`.

### `dbo.material_fulfillment_lines`

Columns: `fulfillment_line_id`, `fulfillment_id`, `request_line_id`, `item_id`, `issued_qty`, `received_qty`, `uom_id`, `note`.

Foreign keys: `fulfillment_id` → `dbo.material_fulfillments(fulfillment_id)`, `request_line_id` → `dbo.material_request_lines(line_id)`, `item_id` → `dbo.inventory_items(item_id)`, `uom_id` → `dbo.units_of_measure(uom_id)`.

### `dbo.internal_request_types`

Columns: `request_type_id`, `type_code`, `type_name`, `default_department_id`, `default_priority_id`, `sla_minutes`, `description`, `is_active`.

Foreign keys: `default_department_id` → `dbo.departments(department_id)`, `default_priority_id` → `dbo.reference_values(reference_id)`.

### `dbo.internal_requests`

Columns: `internal_request_id`, `request_no`, `branch_id`, `request_type_id`, `requester_id`, `requester_department_id`, `receiving_department_id`, `room_id`, `area_id`, `related_incident_id`, `subject`, `description`, `priority_id`, `status`, `cancellation_reason`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `request_type_id` → `dbo.internal_request_types(request_type_id)`, `requester_id` → `dbo.users(user_id)`, `requester_department_id` → `dbo.departments(department_id)`, `receiving_department_id` → `dbo.departments(department_id)`, `room_id` → `dbo.rooms(room_id)`, `area_id` → `dbo.areas(area_id)`, `related_incident_id` → `dbo.incidents(incident_id)`, `priority_id` → `dbo.reference_values(reference_id)`.

### `dbo.internal_request_assignments`

Columns: `assignment_id`, `internal_request_id`, `assignee_id`, `assigned_by`, `status`, `assignment_note`.

Foreign keys: `internal_request_id` → `dbo.internal_requests(internal_request_id)`, `assignee_id` → `dbo.users(user_id)`, `assigned_by` → `dbo.users(user_id)`.

### `dbo.internal_request_events`

Columns: `event_id`, `internal_request_id`, `event_type`, `old_status`, `new_status`, `event_note`, `actor_id`.

Foreign keys: `internal_request_id` → `dbo.internal_requests(internal_request_id)`, `actor_id` → `dbo.users(user_id)`.

### `dbo.internal_request_feedback`

Columns: `feedback_id`, `internal_request_id`, `rating`, `comment`, `is_satisfied`, `submitted_by`.

Foreign keys: `internal_request_id` → `dbo.internal_requests(internal_request_id)`, `submitted_by` → `dbo.users(user_id)`.

### `dbo.operational_tasks`

Columns: `task_id`, `task_code`, `branch_id`, `department_id`, `category_id`, `title`, `description`, `priority_id`, `room_id`, `area_id`, `parent_task_id`, `source_type`, `source_id`, `status`, `progress_percent`, `completion_note`, `created_by`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `department_id` → `dbo.departments(department_id)`, `category_id` → `dbo.work_categories(category_id)`, `priority_id` → `dbo.reference_values(reference_id)`, `room_id` → `dbo.rooms(room_id)`, `area_id` → `dbo.areas(area_id)`, `parent_task_id` → `dbo.operational_tasks(task_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.operational_task_assignments`

Columns: `assignment_id`, `task_id`, `assignee_id`, `assigned_by`, `assignment_status`, `note`.

Foreign keys: `task_id` → `dbo.operational_tasks(task_id)`, `assignee_id` → `dbo.users(user_id)`, `assigned_by` → `dbo.users(user_id)`.

### `dbo.operational_task_progress`

Columns: `progress_id`, `task_id`, `progress_percent`, `old_status`, `new_status`, `description`, `blocker_reason`, `reported_by`.

Foreign keys: `task_id` → `dbo.operational_tasks(task_id)`, `reported_by` → `dbo.users(user_id)`.

### `dbo.operational_task_dependencies`

Columns: `dependency_id`, `predecessor_task_id`, `successor_task_id`, `dependency_type`, `lag_minutes`.

Foreign keys: `predecessor_task_id` → `dbo.operational_tasks(task_id)`, `successor_task_id` → `dbo.operational_tasks(task_id)`.

### `dbo.shift_templates`

Columns: `shift_template_id`, `branch_id`, `shift_code`, `shift_name`, `start_time`, `end_time`, `break_minutes`, `crosses_midnight`, `is_active`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`.

### `dbo.work_shifts`

Columns: `shift_id`, `branch_id`, `department_id`, `shift_template_id`, `shift_date`, `required_staff_count`, `status`, `note`, `created_by`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `department_id` → `dbo.departments(department_id)`, `shift_template_id` → `dbo.shift_templates(shift_template_id)`, `created_by` → `dbo.users(user_id)`.

### `dbo.shift_assignments`

Columns: `shift_assignment_id`, `shift_id`, `user_id`, `assignment_status`, `assigned_by`, `cancellation_reason`.

Foreign keys: `shift_id` → `dbo.work_shifts(shift_id)`, `user_id` → `dbo.users(user_id)`, `assigned_by` → `dbo.users(user_id)`.

### `dbo.shift_attendance`

Columns: `attendance_id`, `shift_assignment_id`, `attendance_status`, `late_minutes`, `overtime_minutes`, `note`, `verified_by`.

Foreign keys: `shift_assignment_id` → `dbo.shift_assignments(shift_assignment_id)`, `verified_by` → `dbo.users(user_id)`.

### `dbo.users`

Columns: `user_id`, `employee_code`, `username`, `email`, `password_hash`, `full_name`, `phone`, `avatar_url`, `primary_branch_id`, `department_id`, `manager_user_id`, `employment_status`, `account_status`.

Foreign keys: `primary_branch_id` → `dbo.branches(branch_id)`, `department_id` → `dbo.departments(department_id)`, `manager_user_id` → `dbo.users(user_id)`.

### `dbo.roles`

Columns: `role_id`, `role_code`, `role_name`, `description`, `is_system_role`, `is_active`.

### `dbo.permissions`

Columns: `permission_id`, `permission_code`, `permission_name`, `module_code`, `resource_code`, `action_code`, `description`.

### `dbo.user_roles`

Columns: `user_role_id`, `user_id`, `role_id`, `branch_id`, `assigned_by`, `is_active`.

Foreign keys: `user_id` → `dbo.users(user_id)`, `role_id` → `dbo.roles(role_id)`, `branch_id` → `dbo.branches(branch_id)`, `assigned_by` → `dbo.users(user_id)`.

### `dbo.role_permissions`

Columns: `role_permission_id`, `role_id`, `permission_id`, `granted_by`.

Foreign keys: `role_id` → `dbo.roles(role_id)`, `permission_id` → `dbo.permissions(permission_id)`, `granted_by` → `dbo.users(user_id)`.

**Patch addition:** `data_scope VARCHAR(20) NOT NULL DEFAULT 'BRANCH'` (check values `OWN`, `DEPARTMENT`, `BRANCH`, `ALL`).

### `dbo.user_branch_access`

Columns: `access_id`, `user_id`, `branch_id`, `access_level`, `is_primary`, `granted_by`.

Foreign keys: `user_id` → `dbo.users(user_id)`, `branch_id` → `dbo.branches(branch_id)`, `granted_by` → `dbo.users(user_id)`.

### `dbo.user_sessions`

Columns: `session_id`, `user_id`, `refresh_token_hash`, `device_info`, `ip_address`, `user_agent`.

Foreign keys: `user_id` → `dbo.users(user_id)`.

### `dbo.notifications`

Columns: `notification_id`, `notification_type`, `title`, `message`, `priority`, `source_type`, `source_id`, `action_url`, `created_by`.

Foreign keys: `created_by` → `dbo.users(user_id)`.

### `dbo.notification_recipients`

Columns: `recipient_id`, `notification_id`, `user_id`, `delivery_channel`, `delivery_status`, `failure_reason`.

Foreign keys: `notification_id` → `dbo.notifications(notification_id)`, `user_id` → `dbo.users(user_id)`.

### `dbo.attachments`

Columns: `attachment_id`, `entity_type`, `entity_id`, `file_name`, `original_file_name`, `mime_type`, `file_size_bytes`, `storage_key`, `file_url`, `description`, `uploaded_by`.

Foreign keys: `uploaded_by` → `dbo.users(user_id)`.

### `dbo.audit_logs`

Columns: `audit_id`, `branch_id`, `actor_user_id`, `entity_type`, `entity_id`, `action`, `old_values`, `new_values`, `changed_fields`, `ip_address`, `request_id`, `user_agent`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `actor_user_id` → `dbo.users(user_id)`.

### `dbo.system_settings`

Columns: `setting_id`, `branch_id`, `setting_group`, `setting_key`, `setting_value`, `data_type`, `description`, `is_sensitive`, `updated_by`.

Foreign keys: `branch_id` → `dbo.branches(branch_id)`, `updated_by` → `dbo.users(user_id)`.

## Schema-specific cautions

- `roles` and `permissions` define reference records; exact grants are seeded in `role_permissions`, and can change independently of table structure.
- `user_roles.branch_id`, `user_branch_access`, `users.primary_branch_id`, and `users.department_id` support different parts of access scoping; the current schema by itself does not specify all authorization semantics.
- `rooms.operational_status` defaults to `AVAILABLE` in the **base script**; the patch explicitly replaces the default with `DIRTY` and adds a status check. Do not trust the base script alone.
- `room_status_history` records transitions but the SQL patch **does not itself enforce the complete state transition graph or authorize actors**. Those require approved rules and application logic.
- The sample contains placeholders for password hashes and settings; do not use sample credentials as production authentication.
- Changes to SQL Server schema require coordination and re-scaffolding/review of generated EF Core classes; do not hand-edit generated entities unless authorized.
