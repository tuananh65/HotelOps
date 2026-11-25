SET NOCOUNT ON;
IF DB_NAME() <> 'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
DECLARE @before nvarchar(max),@after nvarchar(max);
SELECT @before=before_json,@after=after_json FROM audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED';
IF @before IS NULL THROW 51990,'P0 provenance missing; no inferred recovery.',1;
SELECT a.user_id,a.branch_id,a.user_role_id,a.assigned_at,a.revoked_at,r.code
FROM OPENJSON(@before,'$.assignments') WITH(user_role_id bigint,user_id bigint,role_id bigint,branch_id bigint,assigned_at datetime2,revoked_at datetime2) a
JOIN roles r ON r.role_id=a.role_id WHERE r.code='MAINTENANCE_STAFF' AND a.revoked_at IS NULL;
SELECT m.* FROM memberships m WHERE m.provenance LIKE 'P0 261124: ENG to HK;%';
SELECT s.assignment_id,s.user_id,s.shift_id old_shift_id,a.shift_id current_shift_id,a.status,a.work_date
FROM OPENJSON(@before,'$.schedules') WITH(assignment_id bigint,user_id bigint,shift_id bigint) s
JOIN workday_assignments a ON a.assignment_id=s.assignment_id
WHERE s.shift_id<>a.shift_id AND EXISTS(SELECT 1 FROM OPENJSON(@before,'$.assignments') WITH(user_id bigint,role_id bigint,revoked_at datetime2) ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=s.user_id AND r.code='MAINTENANCE_STAFF' AND ur.revoked_at IS NULL);
SELECT w.work_id,w.kind,w.status,w.branch_id,a.assignee_user_id FROM work_items w JOIN work_assignments a ON a.work_id=w.work_id AND a.assignment_status='ACTIVE'
WHERE w.status NOT IN('CLOSED','CANCELLED') AND EXISTS(SELECT 1 FROM memberships m WHERE m.user_id=a.assignee_user_id AND m.provenance LIKE 'P0 261124: ENG to HK;%');
