-- Local development fixtures only. Execute via the reviewed transaction runner.
-- @ExpectedDatabase, @ExpectedGuid are parameters; caller owns commit/rollback.
-- Initial inputs, NOT evidence of completed workflows or production actions.
SET NOCOUNT ON; SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
IF @@TRANCOUNT<>1 OR DB_NAME()<>@ExpectedDatabase
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=@ExpectedGuid)
 THROW 51994,'Verified local target and caller transaction required.',1;
DECLARE @tag varchar(30)='W6_20261118',@now datetime2(7)=SYSUTCDATETIME();
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource='HotelOps:W6_20261118:Fixtures',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51994,'Fixture lock unavailable.',1;
IF EXISTS(SELECT 1 FROM dbo.audit_logs WHERE request_id=@tag AND entity_type='WORKFLOW_FIXTURE')
BEGIN SELECT 'ALREADY_SEEDED' AS result; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.room_types WHERE type_code=@tag)
 OR EXISTS(SELECT 1 FROM dbo.rooms WHERE room_number LIKE 'W6-1811-%')
 OR EXISTS(SELECT 1 FROM dbo.cleaning_tasks WHERE task_no LIKE @tag+'%')
 OR EXISTS(SELECT 1 FROM dbo.material_requests WHERE request_no LIKE @tag+'%')
 OR EXISTS(SELECT 1 FROM dbo.incidents WHERE incident_no LIKE @tag+'%')
 THROW 51994,'Fixture prefix already used without completion receipt; no overwrite.',1;
DECLARE @branch bigint=(SELECT branch_id FROM dbo.branches WHERE branch_code='DVBV' AND is_active=1);
DECLARE @floor bigint=(SELECT floor_id FROM dbo.floors WHERE branch_id=@branch AND floor_code='F01' AND is_active=1);
DECLARE @supervisor bigint=(SELECT user_id FROM dbo.users WHERE username='mai.tran' AND primary_branch_id=@branch AND account_status='ACTIVE' AND employment_status='ACTIVE' AND deleted_at IS NULL);
DECLARE @cleaner bigint=(SELECT user_id FROM dbo.users WHERE username='lan.dang' AND primary_branch_id=@branch AND account_status='ACTIVE' AND employment_status='ACTIVE' AND deleted_at IS NULL);
DECLARE @lead bigint=(SELECT user_id FROM dbo.users WHERE username='minh.le' AND primary_branch_id=@branch AND account_status='ACTIVE' AND employment_status='ACTIVE' AND deleted_at IS NULL);
DECLARE @technician bigint=(SELECT user_id FROM dbo.users WHERE username='thinh.ngo' AND primary_branch_id=@branch AND account_status='ACTIVE' AND employment_status='ACTIVE' AND deleted_at IS NULL);
DECLARE @planner bigint=(SELECT user_id FROM dbo.users WHERE username='ha.pham' AND primary_branch_id=@branch AND account_status='ACTIVE' AND employment_status='ACTIVE' AND deleted_at IS NULL);
DECLARE @checklist bigint=(SELECT checklist_id FROM dbo.inspection_checklists WHERE checklist_code='CL-GEN' AND room_type_id IS NULL AND is_active=1);
DECLARE @version bigint=(SELECT TOP(1) version_id FROM dbo.inspection_checklist_versions WHERE checklist_id=@checklist AND status='PUBLISHED' AND effective_at<=@now ORDER BY version_no DESC);
DECLARE @priority bigint=(SELECT reference_id FROM dbo.reference_values WHERE group_code='PRIORITY' AND value_code='NORMAL' AND is_active=1);
DECLARE @cleanType bigint=(SELECT reference_id FROM dbo.reference_values WHERE group_code='CLEANING_TYPE' AND value_code='CHECKOUT' AND is_active=1);
DECLARE @warehouse bigint=(SELECT warehouse_id FROM dbo.warehouses WHERE branch_id=@branch AND warehouse_code='WH-HK' AND is_active=1);
DECLARE @item bigint=(SELECT item_id FROM dbo.inventory_items WHERE item_code='AMN-SOAP' AND is_active=1);
DECLARE @uom bigint=(SELECT base_uom_id FROM dbo.inventory_items WHERE item_id=@item);
DECLARE @template bigint=(SELECT shift_template_id FROM dbo.shift_templates WHERE branch_id=@branch AND shift_code='MORNING' AND is_active=1 AND crosses_midnight=0);
IF @branch IS NULL OR @floor IS NULL OR @supervisor IS NULL OR @cleaner IS NULL OR @lead IS NULL OR @technician IS NULL OR @planner IS NULL
 OR @version IS NULL OR @priority IS NULL OR @cleanType IS NULL OR @warehouse IS NULL OR @item IS NULL OR @uom IS NULL OR @template IS NULL
 THROW 51994,'Required existing catalog/user missing; do not invent or grant.',1;
DECLARE @created TABLE(entity_type nvarchar(100),entity_id nvarchar(100),scenario nvarchar(250));
INSERT dbo.room_types(branch_id,type_code,type_name,description,standard_capacity,is_active,standard_cleaning_minutes,default_checklist_id)
VALUES(@branch,@tag,N'W6 — Phòng thực hành',N'W6_20261118: dữ liệu test riêng; dùng checklist CL-GEN hiện có.',2,1,30,@checklist);
DECLARE @type bigint=SCOPE_IDENTITY();
INSERT @created VALUES('room_types',CONVERT(nvarchar(100),@type),N'Loại phòng test, thời lượng thực hành 30 phút');
INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order)
SELECT @type,criterion,is_required,sort_order FROM dbo.inspection_checklist_items WHERE version_id=@version;
IF @@ROWCOUNT=0 THROW 51994,'Published checklist has no criteria.',1;
DECLARE @rooms TABLE(room_id bigint,room_number nvarchar(15));
INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,description,is_active,created_at)
OUTPUT inserted.room_id,inserted.room_number INTO @rooms
SELECT @branch,@floor,@type,v.number,'DIRTY',@tag+N': '+v.purpose,1,@now FROM (VALUES
 (N'W6-1811-A',N'Tạo/phân công vệ sinh'),(N'W6-1811-P',N'Thực hành vệ sinh rồi kiểm tra đạt'),
 (N'W6-1811-F',N'Thực hành kiểm tra không đạt rồi làm lại'),(N'W6-1811-M',N'Báo sự cố và bảo trì'))v(number,purpose);
INSERT @created SELECT 'rooms',CONVERT(nvarchar(100),room_id),room_number FROM @rooms;
DECLARE @tasks TABLE(task_id bigint,room_id bigint,task_no varchar(50));
INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,priority_id,instruction,due_at,status,created_by,created_at,workflow_status)
OUTPUT inserted.cleaning_task_id,inserted.room_id,inserted.task_no INTO @tasks
SELECT @tag+'-'+RIGHT(room_number,1),room_id,@cleanType,@priority,
 @tag+N': fixture ban đầu. Phân công và thực hiện thật trước khi kiểm tra; chưa có kết quả đạt/không đạt.',DATEADD(day,3,@now),'PENDING',@supervisor,@now,'PENDING_ASSIGNMENT'
FROM @rooms WHERE room_number<>N'W6-1811-M';
INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) SELECT room_id,task_id FROM @tasks;
INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at)
SELECT room_id,task_id,'PENDING_CLEANING',0,@supervisor,@now FROM @tasks;
INSERT @created SELECT 'cleaning_tasks',CONVERT(nvarchar(100),task_id),task_no FROM @tasks;
-- Reported incident only. Classification/SLA/assignment must be performed through the application.
DECLARE @maintenanceRoom bigint=(SELECT room_id FROM @rooms WHERE room_number=N'W6-1811-M');
INSERT dbo.incidents(incident_no,branch_id,room_id,incident_title,description,priority_id,status,reported_by,discovered_at,reported_at)
VALUES(@tag+'-INC',@branch,@maintenanceRoom,N'W6 — Kiểm tra đèn phòng thực hành',
 N'W6_20261118: tình huống mô phỏng đèn không sáng. Chờ phân loại/tiếp nhận; chưa gán SLA hoặc xác nhận sửa xong.',@priority,'OPEN',@cleaner,@now,@now);
DECLARE @incident bigint=SCOPE_IDENTITY();
INSERT dbo.incident_events(incident_id,event_type,to_status,note,actor_id,occurred_at)
VALUES(@incident,'CREATED','OPEN',N'W6_20261118: dữ liệu thực hành do công cụ chuẩn bị; không phải báo cáo thực địa.',@cleaner,@now);
INSERT @created VALUES('incidents',CONVERT(nvarchar(100),@incident),N'OPEN; chờ phân loại, tiếp nhận rồi phân công bảo trì');
-- Drafts only: no fictitious approval, fulfillment, warehouse movement or stock balance.
DECLARE @requests TABLE(request_id bigint,request_no varchar(50));
INSERT dbo.material_requests(request_no,branch_id,department_id,requester_id,source_warehouse_id,purpose,needed_at,priority_id,status,created_at,receiving_room_id)
OUTPUT inserted.request_id,inserted.request_no INTO @requests
SELECT @tag+'-MR'+v.code,@branch,u.department_id,@cleaner,@warehouse,@tag+N': '+v.purpose,DATEADD(day,2,@now),@priority,'DRAFT',@now,(SELECT room_id FROM @rooms WHERE room_number=N'W6-1811-A')
FROM dbo.users u CROSS JOIN(VALUES('1',N'Thực hành chỉnh sửa và gửi duyệt'),('2',N'Thực hành duyệt/cấp số lượng nhỏ'),('3',N'Thực hành cấp một phần khi tồn không đủ'))v(code,purpose)
WHERE u.user_id=@cleaner AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=u.department_id AND d.branch_id=@branch AND d.is_active=1);
IF @@ROWCOUNT<>3 THROW 51994,'Requester department mismatch.',1;
INSERT dbo.material_request_lines(request_id,line_no,item_id,uom_id,requested_qty,fulfilled_qty,note,is_active)
SELECT request_id,1,@item,@uom,CASE RIGHT(request_no,1) WHEN '3' THEN 5000 ELSE 5 END,0,@tag+N': chưa duyệt/chưa cấp',1 FROM @requests;
INSERT @created SELECT 'material_requests',CONVERT(nvarchar(100),request_id),request_no FROM @requests;
-- Membership mirrors each user's existing primary branch + department only.
-- No role, permission, branch-access or user record is changed.
DECLARE @staff TABLE(user_id bigint,department_id bigint);
INSERT @staff SELECT u.user_id,u.department_id FROM dbo.users u JOIN dbo.departments d ON d.department_id=u.department_id AND d.branch_id=u.primary_branch_id AND d.is_active=1
WHERE u.user_id IN(@supervisor,@cleaner,@lead,@technician) AND u.primary_branch_id=@branch
 AND EXISTS(SELECT 1 FROM dbo.user_branch_access a WHERE a.user_id=u.user_id AND a.branch_id=@branch AND a.revoked_at IS NULL AND a.granted_at<=@now);
IF (SELECT COUNT(*) FROM @staff)<>4 THROW 51994,'Existing staff branch/department scope unavailable.',1;
IF EXISTS(SELECT 1 FROM @staff s JOIN dbo.employee_department_memberships m ON m.user_id=s.user_id AND m.branch_id=@branch AND m.department_id=s.department_id WHERE m.revoked_at IS NOT NULL OR m.granted_at>@now)
 THROW 51994,'Existing revoked/future membership must not be reactivated.',1;
INSERT dbo.employee_department_memberships(user_id,branch_id,department_id,granted_at,recorded_by,provenance)
SELECT s.user_id,@branch,s.department_id,@now,@planner,@tag+': mirrored existing users.primary_branch_id/department_id and active branch access'
FROM @staff s WHERE NOT EXISTS(SELECT 1 FROM dbo.employee_department_memberships m WHERE m.user_id=s.user_id AND m.branch_id=@branch AND m.department_id=s.department_id);
DECLARE @dates TABLE(day date);
INSERT @dates SELECT DATEADD(day,n,CONVERT(date,DATEADD(hour,7,@now))) FROM(VALUES(0),(1),(2))v(n);
DECLARE @shifts TABLE(shift_id bigint,department_id bigint,shift_date date);
-- All timestamps persisted as UTC, derived from the actual active template's local hours.
IF EXISTS(SELECT 1 FROM @staff s JOIN dbo.shift_assignments a ON a.user_id=s.user_id AND a.assignment_status='ASSIGNED' JOIN dbo.work_shifts w ON w.shift_id=a.shift_id JOIN @dates d ON d.day=w.shift_date WHERE w.status='PLANNED')
 THROW 51994,'Existing planned assignments on fixture dates require overlap review.',1;
INSERT dbo.work_shifts(branch_id,department_id,shift_template_id,shift_date,scheduled_start_at,scheduled_end_at,required_staff_count,status,note,created_by,created_at)
OUTPUT inserted.shift_id,inserted.department_id,inserted.shift_date INTO @shifts
SELECT @branch,s.department_id,@template,d.day,
 DATEADD(hour,-7,DATEADD(second,DATEDIFF(second,CONVERT(time,'00:00'),t.start_time),CONVERT(datetime2(7),d.day))),
 DATEADD(hour,-7,DATEADD(second,DATEDIFF(second,CONVERT(time,'00:00'),t.end_time),CONVERT(datetime2(7),d.day))),
 COUNT(*),'PLANNED',@tag+N': ca thực hành theo mẫu MORNING; chưa bắt đầu workday',@planner,@now
FROM @staff s CROSS JOIN @dates d JOIN dbo.shift_templates t ON t.shift_template_id=@template
GROUP BY s.department_id,d.day,t.start_time,t.end_time;
INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at)
SELECT w.shift_id,s.user_id,'ASSIGNED',@planner,@now FROM @shifts w JOIN @staff s ON s.department_id=w.department_id;
INSERT @created SELECT 'work_shifts',CONVERT(nvarchar(100),shift_id),CONVERT(nvarchar(30),shift_date)+N' MORNING' FROM @shifts;
-- Honest operator-provisioning audit: NULL actor means no impersonated HTTP user.
INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,changed_fields,request_id,user_agent,occurred_at)
SELECT @branch,NULL,entity_type,entity_id,'CREATE',
 (SELECT @tag fixture,scenario scenario,N'LOCAL_TEST_PROVISIONING' source FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'["fixture"]',@tag,N'Authorized local fixture preparation; not workflow acceptance',@now FROM @created;
INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,changed_fields,request_id,user_agent,occurred_at)
VALUES(@branch,NULL,'WORKFLOW_FIXTURE',@tag,'CREATE',
 (SELECT @tag fixture,(SELECT * FROM @created FOR JSON PATH) records FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'["fixture"]',@tag,N'Authorized local fixture preparation',@now);
SELECT * FROM @created ORDER BY entity_type,entity_id;
