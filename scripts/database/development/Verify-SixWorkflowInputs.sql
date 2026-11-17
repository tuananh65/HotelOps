-- Read-only assertions for the initial W6 fixture. Run before exercising its workflows.
SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsDB' THROW 51995,'Verify the intended local HotelOpsDB.',1;
DECLARE @tag varchar(30)='W6_20261118';
IF (SELECT COUNT(*) FROM dbo.rooms WHERE room_number LIKE 'W6-1811-%')<>4
 OR (SELECT COUNT(*) FROM dbo.cleaning_tasks WHERE task_no LIKE @tag+'%')<>3
 OR (SELECT COUNT(*) FROM dbo.incidents WHERE incident_no=@tag+'-INC')<>1
 OR (SELECT COUNT(*) FROM dbo.material_requests WHERE request_no LIKE @tag+'-MR%')<>3
 OR (SELECT COUNT(*) FROM dbo.work_shifts WHERE note LIKE @tag+'%')<>6
 THROW 51995,'Fixture root inventory differs.',1;
IF EXISTS(SELECT 1 FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id
 WHERE t.task_no LIKE @tag+'%' AND (t.status<>'PENDING' OR t.workflow_status<>'PENDING_ASSIGNMENT'
 OR NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state s WHERE s.room_id=r.room_id AND s.cleaning_task_id=t.cleaning_task_id)
 OR NOT EXISTS(SELECT 1 FROM dbo.room_readiness_events e WHERE e.room_id=r.room_id AND e.cleaning_task_id=t.cleaning_task_id AND e.readiness='PENDING_CLEANING')))
 THROW 51995,'Initial cleaning state invalid or already exercised.',1;
IF EXISTS(SELECT 1 FROM dbo.material_requests r WHERE r.request_no LIKE @tag+'-MR%' AND
 (r.status<>'DRAFT' OR r.submitted_at IS NOT NULL OR r.completed_at IS NOT NULL
 OR NOT EXISTS(SELECT 1 FROM dbo.material_request_lines l WHERE l.request_id=r.request_id AND l.requested_qty>0 AND l.approved_qty IS NULL AND l.fulfilled_qty=0)
 OR EXISTS(SELECT 1 FROM dbo.material_request_approval_cycles a WHERE a.request_id=r.request_id)))
 THROW 51995,'Draft contains executed approval/fulfillment.',1;
IF EXISTS(SELECT 1 FROM dbo.employee_department_memberships m JOIN dbo.users u ON u.user_id=m.user_id
 WHERE m.provenance LIKE @tag+'%' AND (m.branch_id<>u.primary_branch_id OR m.department_id<>u.department_id
 OR NOT EXISTS(SELECT 1 FROM dbo.user_branch_access a WHERE a.user_id=u.user_id AND a.branch_id=m.branch_id AND a.revoked_at IS NULL)))
 THROW 51995,'Fixture membership extends original branch/department scope.',1;
IF (SELECT COUNT(*) FROM dbo.shift_assignments a JOIN dbo.work_shifts s ON s.shift_id=a.shift_id WHERE s.note LIKE @tag+'%')<>12
 THROW 51995,'Expected 12 fixture assignments.',1;
IF EXISTS(SELECT 1 FROM dbo.workdays d JOIN dbo.work_shifts s ON s.shift_id=(SELECT shift_id FROM dbo.shift_assignments WHERE shift_assignment_id=d.shift_assignment_id) WHERE s.note LIKE @tag+'%')
 THROW 51995,'Fixture workday already started.',1;
IF EXISTS(SELECT 1 FROM dbo.shift_assignments a JOIN dbo.work_shifts s ON s.shift_id=a.shift_id
 JOIN dbo.shift_assignments b ON b.user_id=a.user_id AND b.shift_assignment_id<>a.shift_assignment_id AND b.assignment_status='ASSIGNED'
 JOIN dbo.work_shifts t ON t.shift_id=b.shift_id WHERE s.note LIKE @tag+'%' AND s.status='PLANNED' AND t.status='PLANNED'
 AND s.scheduled_start_at<t.scheduled_end_at AND t.scheduled_start_at<s.scheduled_end_at)
 THROW 51995,'Overlapping staff assignments.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51995,'Disabled/untrusted constraints.',1;
SELECT 'PASS: initial fixture inventory, states, scope, shifts and constraints' AS result;
SELECT r.room_id,r.room_number,t.cleaning_task_id,t.task_no,t.workflow_status
FROM dbo.rooms r LEFT JOIN dbo.cleaning_tasks t ON t.room_id=r.room_id WHERE r.room_number LIKE 'W6-1811-%' ORDER BY r.room_number;
SELECT 'EXPECTED_BLOCKER' AS result,'MR_VIEW missing from current permission catalog' AS detail WHERE NOT EXISTS(SELECT 1 FROM dbo.permissions WHERE permission_code='MR_VIEW');
SELECT 'EXPECTED_BLOCKER' AS result,'INCIDENT_CATEGORY has no active entries' AS detail WHERE NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='INCIDENT_CATEGORY' AND is_active=1);
