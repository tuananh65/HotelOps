// Deterministic SQL Server schema generator. No dependencies, network, or database writes.
// Run: node scripts/database/compact/Generate-Schema.mjs
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const tables = [];
const foreignKeys = [];
const indexes = [];
const checks = [];
function table(name, id, fields, { branch = false, version = true } = {}) {
  const columns = [`${id} bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_${name} PRIMARY KEY`,
    ...(branch ? ['branch_id bigint NOT NULL'] : []),
    ...fields.trim().split('\n').map(x => x.trim()).filter(Boolean),
    ...(version ? ['row_version rowversion NOT NULL'] : [])];
  tables.push({ name, id, columns });
  if (branch) { unique(name, `${id},branch_id`); fk(name, 'branch_id', 'branches', 'branch_id'); }
}
function fk(table, columns, target, targetColumns, suffix = '') {
  foreignKeys.push(`ALTER TABLE dbo.${table} ADD CONSTRAINT FK_${table}_${suffix || columns.replaceAll(',', '_')} FOREIGN KEY (${columns}) REFERENCES dbo.${target} (${targetColumns});`);
}
function scoped(table, column, target, id = column, branchColumn = 'branch_id') {
  fk(table, `${column},${branchColumn}`, target, `${id},branch_id`, column);
}
function unique(table, columns, filter = '') {
  indexes.push(`CREATE UNIQUE INDEX UX_${table}_${columns.replaceAll(',', '_')} ON dbo.${table} (${columns})${filter ? ` WHERE ${filter}` : ''};`);
}
function index(table, columns) {
  indexes.push(`CREATE INDEX IX_${table}_${columns.replaceAll(',', '_')} ON dbo.${table} (${columns});`);
}
function check(table, name, expression) {
  checks.push(`ALTER TABLE dbo.${table} ADD CONSTRAINT CK_${table}_${name} CHECK (${expression});`);
}
function json(table, column) { check(table, column, `${column} IS NULL OR ISJSON(${column})=1`); }
const created = 'created_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()';
const active = 'is_active bit NOT NULL DEFAULT 1';

table('branches', 'branch_id', `
code varchar(30) NOT NULL
name nvarchar(150) NOT NULL
address nvarchar(500) NULL
phone varchar(30) NULL
timezone varchar(80) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh'
${active}
${created}`);
unique('branches', 'code');
table('departments', 'department_id', `
code varchar(30) NOT NULL
name nvarchar(150) NOT NULL
manager_user_id bigint NULL
description nvarchar(2000) NULL
${active}`, { branch: true });
unique('departments', 'branch_id,code');
fk('departments', 'manager_user_id', 'users', 'user_id');
table('locations', 'location_id', `
parent_location_id bigint NULL
kind varchar(10) NOT NULL
code varchar(30) NOT NULL
name nvarchar(150) NOT NULL
floor_number int NULL
${active}`, { branch: true });
unique('locations', 'branch_id,code');
scoped('locations', 'parent_location_id', 'locations', 'location_id');
check('locations', 'kind', "kind IN ('FLOOR','AREA')");
check('locations', 'parent', 'parent_location_id IS NULL OR parent_location_id<>location_id');
table('room_types', 'room_type_id', `
code varchar(30) NOT NULL
name nvarchar(150) NOT NULL
capacity int NOT NULL
description nvarchar(2000) NULL
${active}`, { branch: true });
unique('room_types', 'branch_id,code');
check('room_types', 'capacity', 'capacity>0');
table('rooms', 'room_id', `
location_id bigint NOT NULL
room_type_id bigint NOT NULL
department_id bigint NULL
code varchar(30) NOT NULL
operational_status varchar(30) NOT NULL DEFAULT 'DIRTY'
turnover_no int NOT NULL DEFAULT 0
current_cleaning_work_id bigint NULL
${active}
${created}`, { branch: true });
unique('rooms', 'branch_id,code');
scoped('rooms', 'location_id', 'locations');
scoped('rooms', 'room_type_id', 'room_types');
scoped('rooms', 'department_id', 'departments');
scoped('rooms', 'current_cleaning_work_id', 'work_items', 'work_id');
check('rooms', 'status', "operational_status IN ('AVAILABLE','OCCUPIED','DIRTY','CLEANING','INSPECTION','READY','OUT_OF_SERVICE')");
check('rooms', 'turnover', 'turnover_no>=0');

table('users', 'user_id', `
employee_code varchar(30) NOT NULL
username nvarchar(100) NOT NULL
email nvarchar(254) NULL
full_name nvarchar(200) NOT NULL
phone varchar(30) NULL
password_hash nvarchar(500) NULL
security_stamp uniqueidentifier NOT NULL DEFAULT NEWID()
failed_login_count int NOT NULL DEFAULT 0
locked_until datetime2(7) NULL
temporary_password_expires_at datetime2(7) NULL
manager_user_id bigint NULL
employment_status varchar(30) NOT NULL
account_status varchar(30) NOT NULL
last_login_at datetime2(7) NULL
deleted_at datetime2(7) NULL
${created}`);
unique('users', 'username'); unique('users', 'employee_code'); unique('users', 'email', 'email IS NOT NULL');
fk('users', 'manager_user_id', 'users', 'user_id');
check('users', 'manager', 'manager_user_id IS NULL OR manager_user_id<>user_id');
check('users', 'attempts', 'failed_login_count>=0');
table('memberships', 'membership_id', `
user_id bigint NOT NULL
department_id bigint NULL
kind varchar(24) NOT NULL
access_level varchar(30) NULL
is_primary bit NOT NULL DEFAULT 0
valid_from datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
valid_to datetime2(7) NULL
recorded_by bigint NULL
provenance nvarchar(500) NULL`, { branch: true });
fk('memberships', 'user_id', 'users', 'user_id'); fk('memberships', 'recorded_by', 'users', 'user_id');
scoped('memberships', 'department_id', 'departments');
check('memberships', 'kind', "(kind='BRANCH_ACCESS' AND department_id IS NULL) OR (kind IN ('EMPLOYMENT','DEPARTMENT_AUTHORITY') AND department_id IS NOT NULL)");
check('memberships', 'primary', "is_primary=0 OR kind='BRANCH_ACCESS'");
check('memberships', 'dates', 'valid_to IS NULL OR valid_to>=valid_from');
unique('memberships', 'user_id,branch_id,kind,department_id', 'valid_to IS NULL');
unique('memberships', 'user_id', 'is_primary=1 AND valid_to IS NULL');
table('roles', 'role_id', `
code varchar(60) NOT NULL
name nvarchar(150) NOT NULL
description nvarchar(1000) NULL
${active}`);
unique('roles', 'code');
table('permissions', 'permission_id', `
code varchar(100) NOT NULL
module_code varchar(30) NOT NULL
description nvarchar(1000) NULL
${active}`);
unique('permissions', 'code');
table('role_permissions', 'grant_id', `
role_id bigint NOT NULL
permission_id bigint NOT NULL
data_scope varchar(20) NOT NULL`);
fk('role_permissions', 'role_id', 'roles', 'role_id'); fk('role_permissions', 'permission_id', 'permissions', 'permission_id');
unique('role_permissions', 'role_id,permission_id');
check('role_permissions', 'scope', "data_scope IN ('OWN','DEPARTMENT','BRANCH','ALL')");
table('user_roles', 'user_role_id', `
user_id bigint NOT NULL
role_id bigint NOT NULL
branch_id bigint NULL
assigned_by bigint NULL
assigned_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
expires_at datetime2(7) NULL
revoked_at datetime2(7) NULL`);
fk('user_roles', 'user_id', 'users', 'user_id'); fk('user_roles', 'role_id', 'roles', 'role_id');
fk('user_roles', 'branch_id', 'branches', 'branch_id'); fk('user_roles', 'assigned_by', 'users', 'user_id');
unique('user_roles', 'user_id,role_id,branch_id', 'revoked_at IS NULL');
check('user_roles', 'dates', '(expires_at IS NULL OR expires_at>assigned_at) AND (revoked_at IS NULL OR revoked_at>=assigned_at)');
table('user_sessions', 'session_id', `
user_id bigint NOT NULL
token_hash binary(32) NOT NULL
security_stamp uniqueidentifier NOT NULL
expires_at datetime2(7) NOT NULL
revoked_at datetime2(7) NULL
${created}`);
fk('user_sessions', 'user_id', 'users', 'user_id'); unique('user_sessions', 'token_hash');
check('user_sessions', 'dates', 'expires_at>created_at AND (revoked_at IS NULL OR revoked_at>=created_at)');

table('reference_values', 'reference_id', `
kind varchar(40) NOT NULL
code varchar(60) NOT NULL
name nvarchar(150) NOT NULL
parent_id bigint NULL
base_unit_id bigint NULL
conversion_factor decimal(19,8) NULL
settings_json nvarchar(max) NULL
sort_order int NOT NULL DEFAULT 0
${active}`);
unique('reference_values', 'kind,code'); unique('reference_values', 'reference_id,kind');
fk('reference_values', 'parent_id', 'reference_values', 'reference_id');
fk('reference_values', 'base_unit_id', 'reference_values', 'reference_id');
check('reference_values', 'conversion', '(base_unit_id IS NULL AND conversion_factor IS NULL) OR (base_unit_id IS NOT NULL AND conversion_factor IS NOT NULL AND conversion_factor>0 AND base_unit_id<>reference_id)');
json('reference_values', 'settings_json');
function reference(table, column, kind) {
  const def = tables.find(t => t.name === table);
  const kindColumn = `${column.replace(/_id$/, '')}_kind`;
  def.columns.push(`${kindColumn} varchar(40) NOT NULL DEFAULT '${kind}'`);
  check(table, kindColumn, `${kindColumn}='${kind}'`);
  fk(table, `${column},${kindColumn}`, 'reference_values', 'reference_id,kind', column);
}
table('assets', 'asset_id', `
asset_code varchar(60) NOT NULL
name nvarchar(200) NOT NULL
asset_type_id bigint NULL
location_id bigint NULL
room_id bigint NULL
department_id bigint NULL
custodian_user_id bigint NULL
serial_number nvarchar(100) NULL
condition_code varchar(40) NOT NULL
purchased_on date NULL
warranty_until date NULL
retired_at datetime2(7) NULL
retirement_reason nvarchar(2000) NULL
${active}
${created}`, { branch: true });
unique('assets', 'asset_code'); reference('assets', 'asset_type_id', 'ASSET_TYPE');
scoped('assets', 'location_id', 'locations'); scoped('assets', 'room_id', 'rooms'); scoped('assets', 'department_id', 'departments');
fk('assets', 'custodian_user_id', 'users', 'user_id');
check('assets', 'location', 'location_id IS NULL OR room_id IS NULL');
check('assets', 'retired', 'retired_at IS NULL OR (is_active=0 AND retirement_reason IS NOT NULL)');

table('checklist_templates', 'checklist_template_id', `
code varchar(60) NOT NULL
version_no int NOT NULL
name nvarchar(200) NOT NULL
kind varchar(20) NOT NULL
published_at datetime2(7) NULL
created_by bigint NOT NULL
${active}
${created}`, { branch: true });
unique('checklist_templates', 'branch_id,code,version_no');
check('checklist_templates', 'version', 'version_no>0');
check('checklist_templates', 'kind', "kind IN ('CLEANING','INSPECTION','MAINTENANCE','OPERATIONAL')");
fk('checklist_templates', 'created_by', 'users', 'user_id');
table('checklist_items', 'checklist_item_id', `
checklist_template_id bigint NOT NULL
line_no int NOT NULL
description nvarchar(2000) NOT NULL
is_required bit NOT NULL DEFAULT 1
weight decimal(9,3) NOT NULL DEFAULT 1`, { version: false });
fk('checklist_items', 'checklist_template_id', 'checklist_templates', 'checklist_template_id');
unique('checklist_items', 'checklist_template_id,line_no'); unique('checklist_items', 'checklist_item_id,checklist_template_id');
check('checklist_items', 'values', 'line_no>0 AND weight>=0');
table('work_templates', 'work_template_id', `
code varchar(60) NOT NULL
kind varchar(24) NOT NULL
title nvarchar(200) NOT NULL
instruction nvarchar(max) NULL
department_id bigint NULL
asset_id bigint NULL
room_id bigint NULL
location_id bigint NULL
room_type_id bigint NULL
checklist_template_id bigint NULL
frequency varchar(30) NULL
frequency_interval int NULL
schedule_json nvarchar(max) NULL
next_due_at datetime2(7) NULL
created_by bigint NOT NULL
${active}`, { branch: true });
unique('work_templates', 'branch_id,code');
check('work_templates', 'kind', "kind IN ('CLEANING','REWORK','INSPECTION','MAINTENANCE','OPERATIONAL','INTERNAL_REQUEST')");
check('work_templates', 'interval', 'frequency_interval IS NULL OR frequency_interval>0'); json('work_templates', 'schedule_json');
for (const [col, target] of [['department_id','departments'],['asset_id','assets'],['room_id','rooms'],['location_id','locations'],['room_type_id','room_types'],['checklist_template_id','checklist_templates']]) scoped('work_templates', col, target);
fk('work_templates', 'created_by', 'users', 'user_id');
table('work_items', 'work_id', `
work_no varchar(60) NOT NULL
kind varchar(24) NOT NULL
status varchar(30) NOT NULL
title nvarchar(250) NOT NULL
description nvarchar(max) NULL
department_id bigint NULL
requester_department_id bigint NULL
room_id bigint NULL
location_id bigint NULL
asset_id bigint NULL
category_id bigint NULL
priority_id bigint NULL
severity_id bigint NULL
work_template_id bigint NULL
checklist_template_id bigint NULL
parent_work_id bigint NULL
turnover_no int NULL
occurrence_at datetime2(7) NULL
progress_percent decimal(5,2) NOT NULL DEFAULT 0
planned_start_at datetime2(7) NULL
due_at datetime2(7) NULL
response_deadline_at datetime2(7) NULL
actual_start_at datetime2(7) NULL
completed_at datetime2(7) NULL
closed_at datetime2(7) NULL
verified_by bigint NULL
verified_at datetime2(7) NULL
result varchar(20) NULL
completion_note nvarchar(max) NULL
created_by bigint NOT NULL
${created}`, { branch: true });
unique('work_items', 'branch_id,work_no'); unique('work_items', 'work_id,branch_id,room_id,turnover_no');
unique('work_items', 'work_id,checklist_template_id');
unique('work_items', 'work_template_id,occurrence_at', 'work_template_id IS NOT NULL AND occurrence_at IS NOT NULL');
for (const [col, target, id] of [['department_id','departments'],['requester_department_id','departments','department_id'],['room_id','rooms'],['location_id','locations'],['asset_id','assets'],['work_template_id','work_templates'],['checklist_template_id','checklist_templates'],['parent_work_id','work_items','work_id']]) scoped('work_items', col, target, id || col);
fk('work_items', 'created_by', 'users', 'user_id');
fk('work_items', 'verified_by', 'users', 'user_id');
check('work_items', 'verification_pair', '(verified_by IS NULL AND verified_at IS NULL) OR (verified_by IS NOT NULL AND verified_at IS NOT NULL AND verified_at>=created_at)');
check('work_items', 'review_required', "NOT ((kind='INSPECTION' AND status IN ('PASSED','FAILED')) OR (kind IN ('INCIDENT','MAINTENANCE','OPERATIONAL') AND status='CLOSED')) OR (verified_by IS NOT NULL AND verified_at IS NOT NULL)");
check('work_items', 'parent_kind', "parent_work_id IS NULL OR kind IN ('INSPECTION','REWORK','MAINTENANCE')");
reference('work_items', 'category_id', 'WORK_CATEGORY'); reference('work_items', 'priority_id', 'PRIORITY'); reference('work_items', 'severity_id', 'SEVERITY');
check('work_items', 'progress', 'progress_percent BETWEEN 0 AND 100');
check('work_items', 'parent', 'parent_work_id IS NULL OR parent_work_id<>work_id');
check('work_items', 'room_cycle', "kind NOT IN ('CLEANING','INSPECTION','REWORK') OR (room_id IS NOT NULL AND turnover_no IS NOT NULL AND turnover_no>=0)");
check('work_items', 'dates', '(completed_at IS NULL OR actual_start_at IS NULL OR completed_at>=actual_start_at) AND (closed_at IS NULL OR closed_at>=created_at)');
check('work_items', 'kind_status', `
(kind='CLEANING' AND status IN ('PENDING_ASSIGNMENT','ASSIGNED','IN_PROGRESS','ON_HOLD','PENDING_INSPECTION','REWORK_REQUIRED','COMPLETED','CANCELLED')) OR
(kind='REWORK' AND status IN ('PENDING_ACCEPTANCE','ACCEPTED','IN_PROGRESS','ON_HOLD','PENDING_REINSPECTION','COMPLETED','FAILED','CANCELLED')) OR
(kind='INSPECTION' AND status IN ('ASSIGNED','INSPECTING','PENDING_CONCLUSION','PASSED','FAILED','CANCELLED')) OR
(kind='INCIDENT' AND status IN ('OPEN','IN_PROGRESS','RESOLVED','CLOSED','CANCELLED')) OR
(kind='MAINTENANCE' AND status IN ('PENDING','IN_PROGRESS','ON_HOLD','COMPLETED','CLOSED','CANCELLED')) OR
(kind='OPERATIONAL' AND status IN ('PENDING_ASSIGNMENT','ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION','CLOSED','CANCELLED')) OR
(kind='INTERNAL_REQUEST' AND status IN ('DRAFT','SUBMITTED','PENDING_ACCEPTANCE','ACCEPTED','IN_PROGRESS','COMPLETED','CLOSED','REJECTED','CANCELLED'))`);
check('work_items', 'inspection_result', "kind<>'INSPECTION' OR (status='PASSED' AND result IS NOT NULL AND result='PASS') OR (status='FAILED' AND result IS NOT NULL AND result='FAIL') OR (status NOT IN ('PASSED','FAILED') AND result IS NULL)");
fk('rooms', 'current_cleaning_work_id,branch_id,room_id,turnover_no', 'work_items', 'work_id,branch_id,room_id,turnover_no', 'current_cycle');
index('work_items', 'branch_id,kind,status,due_at'); index('work_items', 'room_id,turnover_no,kind');
table('work_links', 'work_link_id', `
source_work_id bigint NOT NULL
target_work_id bigint NOT NULL
relation varchar(30) NOT NULL
created_by bigint NOT NULL
${created}`, { branch: true, version: false });
scoped('work_links', 'source_work_id', 'work_items', 'work_id'); scoped('work_links', 'target_work_id', 'work_items', 'work_id');
fk('work_links', 'created_by', 'users', 'user_id'); unique('work_links', 'source_work_id,target_work_id,relation');
check('work_links', 'self', 'source_work_id<>target_work_id');
check('work_links', 'relation', "relation IN ('SOURCE','DEPENDS_ON','REWORK_OF','REINSPECTION_OF','RELATED','BLOCKS')");
table('work_assignments', 'assignment_id', `
work_id bigint NOT NULL
assignee_user_id bigint NOT NULL
assigned_by bigint NOT NULL
assignment_status varchar(20) NOT NULL DEFAULT 'ACTIVE'
assigned_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
accepted_at datetime2(7) NULL
ended_at datetime2(7) NULL
reason nvarchar(2000) NULL`, { branch: true });
scoped('work_assignments', 'work_id', 'work_items');
fk('work_assignments', 'assignee_user_id', 'users', 'user_id'); fk('work_assignments', 'assigned_by', 'users', 'user_id');
unique('work_assignments', 'work_id', "assignment_status='ACTIVE'");
check('work_assignments', 'status', "(assignment_status='ACTIVE' AND ended_at IS NULL) OR (assignment_status IN ('COMPLETED','REASSIGNED','CANCELLED') AND ended_at IS NOT NULL AND ended_at>=assigned_at)");
table('work_events', 'work_event_id', `
work_id bigint NOT NULL
event_type varchar(60) NOT NULL
recipient_user_id bigint NULL
related_event_id bigint NULL
actor_user_id bigint NOT NULL
operation_key uniqueidentifier NOT NULL
from_status varchar(30) NULL
to_status varchar(30) NULL
progress_percent decimal(5,2) NULL
reason nvarchar(max) NULL
snapshot_json nvarchar(max) NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { branch: true, version: false });
scoped('work_events', 'work_id', 'work_items'); fk('work_events', 'actor_user_id', 'users', 'user_id');
unique('work_events', 'work_id,actor_user_id,operation_key'); index('work_events', 'work_id,occurred_at');
fk('work_events', 'recipient_user_id', 'users', 'user_id');
unique('work_events', 'work_event_id,work_id,branch_id');
fk('work_events', 'related_event_id,work_id,branch_id', 'work_events', 'work_event_id,work_id,branch_id', 'related_event');
check('work_events', 'not_self', 'related_event_id IS NULL OR related_event_id<>work_event_id');
json('work_events', 'snapshot_json'); check('work_events', 'progress', 'progress_percent IS NULL OR progress_percent BETWEEN 0 AND 100');
table('work_checklist_items', 'work_checklist_item_id', `
work_id bigint NOT NULL
checklist_template_id bigint NOT NULL
checklist_item_id bigint NOT NULL
description_snapshot nvarchar(2000) NOT NULL
required_snapshot bit NOT NULL
weight_snapshot decimal(9,3) NOT NULL
outcome varchar(10) NULL
note nvarchar(2000) NULL
checked_by bigint NULL
checked_at datetime2(7) NULL`, { branch: true });
scoped('work_checklist_items', 'work_id', 'work_items');
fk('work_checklist_items', 'work_id,checklist_template_id', 'work_items', 'work_id,checklist_template_id', 'work_template');
fk('work_checklist_items', 'checklist_item_id,checklist_template_id', 'checklist_items', 'checklist_item_id,checklist_template_id', 'item_template');
fk('work_checklist_items', 'checked_by', 'users', 'user_id');
unique('work_checklist_items', 'work_id,checklist_item_id'); unique('work_checklist_items', 'work_checklist_item_id,work_id');
check('work_checklist_items', 'outcome', "(outcome IS NULL AND checked_by IS NULL AND checked_at IS NULL) OR (outcome IS NOT NULL AND outcome IN ('PASS','FAIL','NA') AND checked_by IS NOT NULL AND checked_at IS NOT NULL)");
check('work_checklist_items', 'weight', 'weight_snapshot>=0');
table('inspection_findings', 'finding_id', `
inspection_work_id bigint NOT NULL
work_checklist_item_id bigint NULL
rework_work_id bigint NULL
severity_id bigint NULL
description nvarchar(2000) NOT NULL
status varchar(20) NOT NULL DEFAULT 'OPEN'
assigned_to bigint NULL
resolved_at datetime2(7) NULL
resolution_note nvarchar(2000) NULL`, { branch: true });
scoped('inspection_findings', 'inspection_work_id', 'work_items', 'work_id'); scoped('inspection_findings', 'rework_work_id', 'work_items', 'work_id');
fk('inspection_findings', 'work_checklist_item_id,inspection_work_id', 'work_checklist_items', 'work_checklist_item_id,work_id', 'result');
reference('inspection_findings', 'severity_id', 'SEVERITY'); fk('inspection_findings', 'assigned_to', 'users', 'user_id');
check('inspection_findings', 'status', "status IN ('OPEN','IN_PROGRESS','RESOLVED','CANCELLED')");
check('inspection_findings', 'resolution', "status<>'RESOLVED' OR (resolved_at IS NOT NULL AND resolution_note IS NOT NULL)");
table('room_blocks', 'block_id', `
room_id bigint NOT NULL
source_work_id bigint NULL
reason nvarchar(2000) NOT NULL
blocked_by bigint NOT NULL
blocked_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
expected_release_at datetime2(7) NULL
released_by bigint NULL
released_at datetime2(7) NULL`, { branch: true });
scoped('room_blocks', 'room_id', 'rooms'); scoped('room_blocks', 'source_work_id', 'work_items', 'work_id');
fk('room_blocks', 'blocked_by', 'users', 'user_id'); fk('room_blocks', 'released_by', 'users', 'user_id');
unique('room_blocks', 'room_id', 'released_at IS NULL');
check('room_blocks', 'release', '(released_at IS NULL AND released_by IS NULL) OR (released_at IS NOT NULL AND released_by IS NOT NULL AND released_at>=blocked_at)');
table('room_events', 'room_event_id', `
room_id bigint NOT NULL
source_work_id bigint NULL
turnover_no int NOT NULL
event_type varchar(40) NOT NULL
from_status varchar(30) NULL
to_status varchar(30) NULL
actor_user_id bigint NOT NULL
operation_key uniqueidentifier NOT NULL
reason nvarchar(2000) NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { branch: true, version: false });
scoped('room_events', 'room_id', 'rooms'); scoped('room_events', 'source_work_id', 'work_items', 'work_id');
fk('room_events', 'actor_user_id', 'users', 'user_id'); unique('room_events', 'room_id,actor_user_id,operation_key'); index('room_events', 'room_id,occurred_at');
check('room_events', 'cycle', 'turnover_no>=0');

table('warehouses', 'warehouse_id', `
code varchar(30) NOT NULL
name nvarchar(150) NOT NULL
location_id bigint NULL
department_id bigint NULL
${active}`, { branch: true });
unique('warehouses', 'branch_id,code'); scoped('warehouses', 'location_id', 'locations'); scoped('warehouses', 'department_id', 'departments');
table('inventory_items', 'item_id', `
code varchar(60) NOT NULL
name nvarchar(200) NOT NULL
category_id bigint NULL
base_unit_id bigint NOT NULL
description nvarchar(2000) NULL
${active}`);
unique('inventory_items', 'code'); reference('inventory_items', 'category_id', 'ITEM_CATEGORY'); reference('inventory_items', 'base_unit_id', 'UOM');
table('inventory_policies', 'inventory_policy_id', `
warehouse_id bigint NOT NULL
item_id bigint NOT NULL
minimum_qty decimal(19,6) NOT NULL DEFAULT 0
target_qty decimal(19,6) NOT NULL DEFAULT 0
low_stock_episode uniqueidentifier NULL
alerted_at datetime2(7) NULL`, { branch: true });
scoped('inventory_policies', 'warehouse_id', 'warehouses'); fk('inventory_policies', 'item_id', 'inventory_items', 'item_id');
unique('inventory_policies', 'warehouse_id,item_id'); check('inventory_policies', 'quantities', 'minimum_qty>=0 AND target_qty>=minimum_qty');
table('inventory_documents', 'document_id', `
document_no varchar(60) NOT NULL
kind varchar(30) NOT NULL
status varchar(30) NOT NULL
department_id bigint NULL
source_warehouse_id bigint NULL
destination_warehouse_id bigint NULL
destination_branch_id bigint NULL
source_document_id bigint NULL
source_work_id bigint NULL
receiving_room_id bigint NULL
supplier_name nvarchar(200) NULL
expected_on date NULL
reason nvarchar(2000) NULL
created_by bigint NOT NULL
approved_by bigint NULL
approved_at datetime2(7) NULL
posted_by bigint NULL
posted_at datetime2(7) NULL
closed_at datetime2(7) NULL
${created}`, { branch: true });
unique('inventory_documents', 'branch_id,document_no');
scoped('inventory_documents', 'department_id', 'departments'); scoped('inventory_documents', 'source_warehouse_id', 'warehouses', 'warehouse_id');
scoped('inventory_documents', 'destination_warehouse_id', 'warehouses', 'warehouse_id', 'destination_branch_id');
fk('inventory_documents', 'destination_branch_id', 'branches', 'branch_id');
// Cross-branch receipts may reference a transfer dispatched by another branch.
fk('inventory_documents', 'source_document_id', 'inventory_documents', 'document_id');
scoped('inventory_documents', 'source_work_id', 'work_items', 'work_id'); scoped('inventory_documents', 'receiving_room_id', 'rooms', 'room_id');
for (const col of ['created_by','approved_by','posted_by']) fk('inventory_documents', col, 'users', 'user_id');
check('inventory_documents', 'destination', '(destination_warehouse_id IS NULL AND destination_branch_id IS NULL) OR (destination_warehouse_id IS NOT NULL AND destination_branch_id IS NOT NULL)');
check('inventory_documents', 'source', 'source_document_id IS NULL OR source_document_id<>document_id');
check('inventory_documents', 'kind', "kind IN ('MATERIAL_REQUEST','REPLENISHMENT','RECEIPT','ISSUE','RETURN','TRANSFER','TRANSFER_RECEIPT','TRANSFER_REQUEST','STOCKTAKE','ADJUSTMENT','REVERSAL')");
check('inventory_documents', 'warehouses', 'source_warehouse_id IS NULL OR destination_warehouse_id IS NULL OR source_warehouse_id<>destination_warehouse_id');
check('inventory_documents', 'posting', '(posted_at IS NULL AND posted_by IS NULL) OR (posted_at IS NOT NULL AND posted_by IS NOT NULL AND posted_at>=created_at)');
check('inventory_documents', 'approval', '(approved_at IS NULL AND approved_by IS NULL) OR (approved_at IS NOT NULL AND approved_by IS NOT NULL AND approved_by<>created_by)');
index('inventory_documents', 'branch_id,kind,status');
table('inventory_document_lines', 'line_id', `
document_id bigint NOT NULL
line_no int NOT NULL
item_id bigint NOT NULL
unit_id bigint NOT NULL
conversion_factor decimal(19,8) NOT NULL
requested_qty decimal(19,6) NULL
approved_qty decimal(19,6) NULL
actual_qty decimal(19,6) NOT NULL DEFAULT 0
base_qty AS CONVERT(decimal(19,6),actual_qty*conversion_factor) PERSISTED
counted_qty decimal(19,6) NULL
expected_qty decimal(19,6) NULL
damaged_qty decimal(19,6) NOT NULL DEFAULT 0
missing_qty decimal(19,6) NOT NULL DEFAULT 0
unit_cost decimal(19,4) NULL
source_line_id bigint NULL
note nvarchar(2000) NULL`, { branch: true });
scoped('inventory_document_lines', 'document_id', 'inventory_documents'); fk('inventory_document_lines', 'item_id', 'inventory_items', 'item_id');
reference('inventory_document_lines', 'unit_id', 'UOM');
unique('inventory_document_lines', 'document_id,line_no'); unique('inventory_document_lines', 'line_id,item_id'); unique('inventory_document_lines', 'line_id,branch_id,item_id');
fk('inventory_document_lines', 'source_line_id,item_id', 'inventory_document_lines', 'line_id,item_id', 'source_item');
check('inventory_document_lines', 'source', 'source_line_id IS NULL OR source_line_id<>line_id');
check('inventory_document_lines', 'quantity', 'line_no>0 AND conversion_factor>0 AND actual_qty>=0 AND damaged_qty>=0 AND missing_qty>=0 AND (requested_qty IS NULL OR requested_qty>=0) AND (approved_qty IS NULL OR approved_qty>=0) AND (counted_qty IS NULL OR counted_qty>=0) AND (expected_qty IS NULL OR expected_qty>=0) AND (unit_cost IS NULL OR unit_cost>=0)');
table('stock_movements', 'movement_id', `
line_id bigint NOT NULL
warehouse_id bigint NOT NULL
item_id bigint NOT NULL
signed_base_qty decimal(19,6) NOT NULL
unit_cost decimal(19,4) NULL
reversal_of_movement_id bigint NULL
created_by bigint NOT NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { branch: true, version: false });
scoped('stock_movements', 'warehouse_id', 'warehouses');
fk('stock_movements', 'line_id,branch_id,item_id', 'inventory_document_lines', 'line_id,branch_id,item_id', 'line_item');
fk('stock_movements', 'created_by', 'users', 'user_id'); fk('stock_movements', 'reversal_of_movement_id', 'stock_movements', 'movement_id');
unique('stock_movements', 'line_id,warehouse_id'); unique('stock_movements', 'reversal_of_movement_id', 'reversal_of_movement_id IS NOT NULL');
check('stock_movements', 'quantity', 'signed_base_qty<>0 AND (unit_cost IS NULL OR unit_cost>=0)');
check('stock_movements', 'self', 'reversal_of_movement_id IS NULL OR reversal_of_movement_id<>movement_id');
index('stock_movements', 'warehouse_id,item_id,occurred_at');
table('document_events', 'document_event_id', `
document_id bigint NOT NULL
line_id bigint NULL
event_type varchar(60) NOT NULL
revision_no int NOT NULL DEFAULT 1
actor_user_id bigint NOT NULL
operation_key uniqueidentifier NOT NULL
quantity_snapshot decimal(19,6) NULL
reason nvarchar(2000) NULL
snapshot_json nvarchar(max) NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { branch: true, version: false });
scoped('document_events', 'document_id', 'inventory_documents');
unique('inventory_document_lines', 'line_id,document_id');
fk('document_events', 'line_id,document_id', 'inventory_document_lines', 'line_id,document_id', 'line_document');
fk('document_events', 'actor_user_id', 'users', 'user_id');
unique('document_events', 'document_id,actor_user_id,operation_key,line_id'); json('document_events', 'snapshot_json');
check('document_events', 'revision', 'revision_no>0');

// FE-12 Level 2: time belongs to work, never to a mandatory shift/workday.
table('work_intervals', 'interval_id', `
work_id bigint NOT NULL
assignment_id bigint NOT NULL
user_id bigint NOT NULL
opened_by bigint NOT NULL
opened_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
closed_by bigint NULL
closed_at datetime2(7) NULL
close_reason varchar(20) NULL
effective_started_at datetime2(7) NOT NULL
effective_ended_at datetime2(7) NULL`, { branch: true });
unique('work_assignments', 'assignment_id,work_id,branch_id,assignee_user_id');
fk('work_intervals', 'assignment_id,work_id,branch_id,user_id', 'work_assignments', 'assignment_id,work_id,branch_id,assignee_user_id', 'assignment_identity');
scoped('work_intervals', 'work_id', 'work_items');
fk('work_intervals', 'user_id', 'users', 'user_id');
fk('work_intervals', 'opened_by', 'users', 'user_id');
fk('work_intervals', 'closed_by', 'users', 'user_id');
unique('work_intervals', 'user_id', 'closed_at IS NULL');
index('work_intervals', 'work_id,user_id,effective_started_at');
check('work_intervals', 'dates', '(closed_at IS NULL AND closed_by IS NULL AND effective_ended_at IS NULL AND close_reason IS NULL) OR (closed_at IS NOT NULL AND closed_by IS NOT NULL AND effective_ended_at IS NOT NULL AND close_reason IS NOT NULL AND closed_at>=opened_at AND effective_ended_at>=effective_started_at)');
check('work_intervals', 'reason', "close_reason IS NULL OR close_reason IN ('PAUSE','COMPLETE','REASSIGN','CANCEL')");

table('asset_events', 'asset_event_id', `
asset_id bigint NOT NULL
event_type varchar(30) NOT NULL
source_work_id bigint NULL
from_branch_id bigint NULL
from_location_id bigint NULL
to_location_id bigint NULL
from_room_id bigint NULL
to_room_id bigint NULL
from_user_id bigint NULL
to_user_id bigint NULL
old_condition varchar(40) NULL
new_condition varchar(40) NULL
handover_status varchar(30) NULL
accepted_at datetime2(7) NULL
actor_user_id bigint NOT NULL
operation_key uniqueidentifier NOT NULL
note nvarchar(2000) NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { branch: true, version: false });
// Historic branch is retained when the current asset moves to another branch.
fk('asset_events', 'asset_id', 'assets', 'asset_id'); fk('asset_events', 'from_branch_id', 'branches', 'branch_id');
scoped('asset_events', 'source_work_id', 'work_items', 'work_id');
scoped('asset_events', 'from_location_id', 'locations', 'location_id', 'from_branch_id'); scoped('asset_events', 'to_location_id', 'locations', 'location_id');
scoped('asset_events', 'from_room_id', 'rooms', 'room_id', 'from_branch_id'); scoped('asset_events', 'to_room_id', 'rooms', 'room_id');
for (const col of ['from_user_id','to_user_id','actor_user_id']) fk('asset_events', col, 'users', 'user_id');
unique('asset_events', 'asset_id,actor_user_id,operation_key');
table('attachments', 'attachment_id', `
work_id bigint NULL
finding_id bigint NULL
document_id bigint NULL
asset_id bigint NULL
storage_key nvarchar(450) NOT NULL
original_name nvarchar(255) NOT NULL
mime_type varchar(150) NOT NULL
byte_length bigint NOT NULL
sha256 binary(32) NOT NULL
uploaded_by bigint NOT NULL
invalidated_at datetime2(7) NULL
invalidation_reason nvarchar(2000) NULL
${created}`, { branch: true });
for (const [col, target] of [['work_id','work_items'],['finding_id','inspection_findings'],['document_id','inventory_documents'],['asset_id','assets']]) scoped('attachments', col, target);
fk('attachments', 'uploaded_by', 'users', 'user_id'); unique('attachments', 'storage_key');
check('attachments', 'owner', '(CASE WHEN work_id IS NULL THEN 0 ELSE 1 END + CASE WHEN finding_id IS NULL THEN 0 ELSE 1 END + CASE WHEN document_id IS NULL THEN 0 ELSE 1 END + CASE WHEN asset_id IS NULL THEN 0 ELSE 1 END)=1');
check('attachments', 'size', 'byte_length>=0');
check('attachments', 'invalidation', '(invalidated_at IS NULL AND invalidation_reason IS NULL) OR (invalidated_at IS NOT NULL AND invalidation_reason IS NOT NULL)');
table('audit_logs', 'audit_id', `
branch_id bigint NULL
actor_user_id bigint NULL
operation_key uniqueidentifier NOT NULL
entity_type varchar(80) NOT NULL
entity_key nvarchar(150) NOT NULL
action varchar(80) NOT NULL
before_json nvarchar(max) NULL
after_json nvarchar(max) NULL
occurred_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()`, { version: false });
fk('audit_logs', 'branch_id', 'branches', 'branch_id'); fk('audit_logs', 'actor_user_id', 'users', 'user_id');
json('audit_logs', 'before_json'); json('audit_logs', 'after_json'); index('audit_logs', 'branch_id,occurred_at');
table('notifications', 'notification_id', `
recipient_user_id bigint NOT NULL
work_id bigint NULL
document_id bigint NULL
room_id bigint NULL
event_key uniqueidentifier NOT NULL
notification_type varchar(60) NOT NULL
title nvarchar(250) NOT NULL
message nvarchar(max) NOT NULL
requires_acknowledgment bit NOT NULL DEFAULT 0
available_at datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
lease_id uniqueidentifier NULL
lease_until datetime2(7) NULL
attempt_count int NOT NULL DEFAULT 0
delivered_at datetime2(7) NULL
read_at datetime2(7) NULL
acknowledged_at datetime2(7) NULL
expires_at datetime2(7) NULL
last_error nvarchar(2000) NULL
${created}`, { branch: true });
fk('notifications', 'recipient_user_id', 'users', 'user_id');
scoped('notifications', 'work_id', 'work_items'); scoped('notifications', 'document_id', 'inventory_documents'); scoped('notifications', 'room_id', 'rooms');
unique('notifications', 'recipient_user_id,event_key');
index('notifications', 'delivered_at,available_at'); index('notifications', 'recipient_user_id,created_at');
check('notifications', 'attempts', 'attempt_count>=0');
check('notifications', 'lease', '(lease_id IS NULL AND lease_until IS NULL) OR (lease_id IS NOT NULL AND lease_until IS NOT NULL)');
check('notifications', 'ack', 'acknowledged_at IS NULL OR (requires_acknowledgment=1 AND delivered_at IS NOT NULL AND acknowledged_at>=delivered_at)');

if (tables.length !== 37) throw new Error(`Expected 37 tables, got ${tables.length}`);
const ddl = `/* HotelOps Compact v2 — Level 2. Generated by Generate-Schema.mjs.
   SQL Server 2022+. New independent database only. No data migration or role grants.
   Run through Install-Compact.ps1; schema and supporting objects are one transaction.
   This is NOT compatible with the current HotelOpsDbContext. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database name required.',1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51001,'Target must be empty.',1;
GO
${tables.map(t => `CREATE TABLE dbo.${t.name} (\n    ${t.columns.join(',\n    ')}\n);`).join('\nGO\n')}
GO
${indexes.join('\n')}
GO
${foreignKeys.join('\n')}
GO
${checks.join('\n')}
GO
EXEC sys.sp_addextendedproperty @name=N'HotelOpsCompactSchema',@value=N'2.0.0';
GO
`;
writeFileSync(fileURLToPath(new URL('./01_Schema.sql', import.meta.url)), ddl);
writeFileSync(fileURLToPath(new URL('./table-manifest.json', import.meta.url)), JSON.stringify(tables.map(t => ({ name: t.name, primary_key: t.id, columns: t.columns.length })), null, 2) + '\n');
console.log(`Generated ${tables.length} tables, ${foreignKeys.length} foreign keys, ${checks.length} checks.`);
