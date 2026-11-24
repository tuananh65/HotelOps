-- P0 TEST-only role cutover. Run AFTER verified backup/restore, before starting the P0 API.
-- No production target is accepted. One transaction; no disabled constraints/triggers.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestP0%' THROW 51901,'P0 isolated TEST database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(varchar(30),value) IN ('2.1.0','3.0.0'))
 THROW 51902,'Expected Compact baseline 2.1.0 or P0 3.0.0.',1;
DECLARE @now datetime2(7)=SYSUTCDATETIME(),@operation uniqueidentifier=NEWID(),@before nvarchar(max),@after nvarchar(max),@lock int;
BEGIN TRY
 BEGIN TRANSACTION;
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps.P0.RoleCutover',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
 IF @lock<0 THROW 51903,'Another role migration is running.',1;
 IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED')
 BEGIN
  COMMIT;
  SELECT 'ALREADY_APPLIED' result;
  RETURN;
 END;
 DECLARE @map TABLE(old_code varchar(60) PRIMARY KEY,new_code varchar(60));
 INSERT @map VALUES('DEPT_MANAGER','OPERATION_MANAGER'),('MAINTENANCE_LEAD','MAINTENANCE_SUPERVISOR'),('MAINTENANCE_STAFF','HK_STAFF');
 DECLARE @issues TABLE(category varchar(60),entity_id bigint NULL,detail nvarchar(1000));
 -- Keep future assignments, expired assignments and immutable past history distinct.
 SELECT ur.*,m.old_code,m.new_code INTO #affected FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id JOIN @map m ON m.old_code=r.code
 WHERE ur.revoked_at IS NULL;
 INSERT @issues SELECT 'GLOBAL_LEGACY_ROLE',user_role_id,N'Assign a concrete branch before migration.' FROM #affected WHERE branch_id IS NULL;
 INSERT @issues SELECT 'UNKNOWN_ACTIVE_ROLE',role_id,code FROM roles WHERE is_active=1 AND code NOT IN
 ('SYS_ADMIN','OWNER','OPERATION_MANAGER','HOUSEKEEPING_SUPERVISOR','HK_STAFF','MAINTENANCE_SUPERVISOR','RECEPTION','STOREKEEPER','DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF');
 INSERT @issues SELECT 'ACTIVE_OLD_SHIFT',a.assignment_id,N'End the active legacy-role shift through the normal attendance workflow first.'
 FROM workday_assignments a JOIN workday_shifts s ON s.shift_id=a.shift_id JOIN roles r ON r.role_id=s.role_id JOIN @map m ON m.old_code=r.code WHERE a.status='WORKING';
 INSERT @issues SELECT 'HK_DEPARTMENT_MISSING',a.user_role_id,N'Active HK employment department is required in the same branch.' FROM #affected a
 WHERE a.old_code='MAINTENANCE_STAFF' AND (a.expires_at IS NULL OR a.expires_at>@now)
 AND NOT EXISTS(SELECT 1 FROM departments d WHERE d.branch_id=a.branch_id AND d.code='HK' AND d.is_active=1);
 INSERT @issues SELECT 'OTHER_TECHNICAL_ROLE',ur.user_role_id,N'Another role would retain technical authority for a migrated maintenance staff member; review explicitly.'
 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id JOIN role_permissions rp ON rp.role_id=r.role_id JOIN permissions p ON p.permission_id=rp.permission_id
 WHERE ur.revoked_at IS NULL AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND r.is_active=1
 AND r.code NOT IN ('MAINTENANCE_STAFF','MAINTENANCE_LEAD','DEPT_MANAGER','OPERATION_MANAGER','HK_STAFF')
 AND p.code IN ('MAINTENANCE_ASSIGN','MAINTENANCE_EXECUTE','MAINTENANCE_REVIEW','INCIDENT_MANAGE')
 AND EXISTS(SELECT 1 FROM #affected x WHERE x.user_id=ur.user_id AND x.old_code='MAINTENANCE_STAFF');
 -- Reassign only unstarted maintenance work with exactly one eligible branch supervisor.
 SELECT DISTINCT ur.user_id,ur.branch_id INTO #supervisors FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id JOIN users u ON u.user_id=ur.user_id
 WHERE r.code IN ('MAINTENANCE_LEAD','MAINTENANCE_SUPERVISOR') AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now
 AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
 AND EXISTS(SELECT 1 FROM memberships mb WHERE mb.user_id=ur.user_id AND mb.branch_id=ur.branch_id AND mb.kind='BRANCH_ACCESS' AND mb.valid_from<=@now AND (mb.valid_to IS NULL OR mb.valid_to>@now));
 SELECT w.work_id,w.branch_id,a.assignment_id,a.assignee_user_id,w.status,w.actual_start_at,
 (SELECT COUNT(*) FROM #supervisors s WHERE s.branch_id=w.branch_id) supervisor_count,
 (SELECT MIN(s.user_id) FROM #supervisors s WHERE s.branch_id=w.branch_id) supervisor_id
 INTO #maintenance FROM work_items w LEFT JOIN work_assignments a ON a.work_id=w.work_id AND a.assignment_status='ACTIVE'
 WHERE w.kind='MAINTENANCE' AND w.status NOT IN ('CLOSED','COMPLETED','CANCELLED');
 INSERT @issues SELECT 'OPEN_MAINTENANCE_REVIEW',m.work_id,N'Cannot safely choose responsibility: retain current work and explicitly resolve supervisor/started-work status before cutover.'
 FROM #maintenance m WHERE (m.supervisor_count<>1 AND NOT EXISTS(SELECT 1 FROM #supervisors s WHERE s.user_id=m.assignee_user_id AND s.branch_id=m.branch_id))
 OR (m.assignee_user_id<>m.supervisor_id AND (m.actual_start_at IS NOT NULL OR EXISTS(SELECT 1 FROM work_intervals i WHERE i.assignment_id=m.assignment_id)))
 OR (m.assignment_id IS NULL AND m.status NOT IN ('OPEN','ASSIGNED'));
 IF EXISTS(SELECT 1 FROM @issues) BEGIN SELECT * FROM @issues ORDER BY category,entity_id; THROW 51904,'Unsafe role cutover. Resolve returned rows; no changes applied.',1; END;
 SELECT @before=(SELECT JSON_QUERY((SELECT * FROM roles FOR JSON PATH)) roles,
 JSON_QUERY((SELECT * FROM role_permissions FOR JSON PATH)) grants,
 JSON_QUERY((SELECT * FROM user_roles FOR JSON PATH)) assignments,
 JSON_QUERY((SELECT * FROM memberships FOR JSON PATH)) memberships,
 JSON_QUERY((SELECT * FROM workday_shifts FOR JSON PATH)) shifts,
 JSON_QUERY((SELECT * FROM workday_assignments FOR JSON PATH)) schedules,
 JSON_QUERY((SELECT * FROM work_assignments WHERE work_id IN (SELECT work_id FROM #maintenance) FOR JSON PATH)) maintenance_assignments FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 IF NOT EXISTS(SELECT 1 FROM roles WHERE code='MAINTENANCE_SUPERVISOR')
 INSERT roles(code,name,description,is_active) VALUES('MAINTENANCE_SUPERVISOR',N'Giám sát bảo trì',N'Tiếp nhận và giám sát khắc phục bởi dịch vụ bên ngoài',1);
 UPDATE roles SET is_active=1 WHERE code='MAINTENANCE_SUPERVISOR';
 -- Expired target rows must be retired before the filtered unique key can accept a new assignment.
 UPDATE ur SET revoked_at=@now FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id
 WHERE ur.revoked_at IS NULL AND ur.expires_at<=@now AND EXISTS(SELECT 1 FROM #affected a WHERE a.user_id=ur.user_id AND a.branch_id=ur.branch_id AND a.new_code=r.code);
 INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at,expires_at)
 SELECT a.user_id,r.role_id,a.branch_id,a.assigned_by,CASE WHEN a.assigned_at>@now THEN a.assigned_at ELSE @now END,a.expires_at
 FROM #affected a JOIN roles r ON r.code=a.new_code
 WHERE (a.expires_at IS NULL OR a.expires_at>@now) AND NOT EXISTS(SELECT 1 FROM user_roles u WHERE u.user_id=a.user_id AND u.role_id=r.role_id AND u.branch_id=a.branch_id AND u.revoked_at IS NULL);
 -- Existing target assignment must cover the old role's effective period; do not silently shorten access.
 IF EXISTS(SELECT 1 FROM #affected a JOIN roles r ON r.code=a.new_code JOIN user_roles u ON u.user_id=a.user_id AND u.role_id=r.role_id AND u.branch_id=a.branch_id AND u.revoked_at IS NULL
 WHERE (a.expires_at IS NULL OR a.expires_at>@now) AND (u.assigned_at>CASE WHEN a.assigned_at>@now THEN a.assigned_at ELSE @now END OR (u.expires_at IS NOT NULL AND (a.expires_at IS NULL OR u.expires_at<a.expires_at))))
 THROW 51905,'Existing target role does not cover source effective dates; review assignments.',1;
 UPDATE ur SET revoked_at=CASE WHEN ur.assigned_at>@now THEN ur.assigned_at ELSE @now END FROM user_roles ur JOIN #affected a ON a.user_role_id=ur.user_role_id;
 UPDATE roles SET is_active=0 WHERE code IN ('DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF');
 -- Split employment at cutover; never mutate historical department identity.
 SELECT DISTINCT mb.*,d.department_id target_department_id INTO #employment FROM memberships mb JOIN departments old ON old.department_id=mb.department_id
 JOIN departments d ON d.branch_id=mb.branch_id AND d.code='HK' AND d.is_active=1
 WHERE mb.kind='EMPLOYMENT' AND old.code='ENG' AND (mb.valid_to IS NULL OR mb.valid_to>@now)
 AND EXISTS(SELECT 1 FROM #affected a WHERE a.user_id=mb.user_id AND a.branch_id=mb.branch_id AND a.old_code='MAINTENANCE_STAFF' AND (a.expires_at IS NULL OR a.expires_at>@now));
 IF EXISTS(SELECT 1 FROM #employment e JOIN memberships mb ON mb.user_id=e.user_id AND mb.branch_id=e.branch_id AND mb.kind='EMPLOYMENT' AND mb.department_id=e.target_department_id
 WHERE (mb.valid_to IS NULL OR mb.valid_to>@now) AND NOT (mb.valid_from<=CASE WHEN e.valid_from>@now THEN e.valid_from ELSE @now END AND (mb.valid_to IS NULL OR (e.valid_to IS NOT NULL AND mb.valid_to>=e.valid_to))))
 THROW 51906,'Overlapping HK employment periods require explicit reconciliation.',1;
 UPDATE mb SET valid_to=CASE WHEN mb.valid_from>@now THEN mb.valid_from ELSE @now END FROM memberships mb JOIN #employment e ON e.membership_id=mb.membership_id;
 INSERT memberships(branch_id,user_id,department_id,kind,is_primary,valid_from,valid_to,recorded_by,provenance)
 SELECT e.branch_id,e.user_id,e.target_department_id,'EMPLOYMENT',0,CASE WHEN e.valid_from>@now THEN e.valid_from ELSE @now END,e.valid_to,e.recorded_by,N'P0 261124: ENG to HK; source membership '+CONVERT(nvarchar(30),e.membership_id)
 FROM #employment e WHERE NOT EXISTS(SELECT 1 FROM memberships mb WHERE mb.user_id=e.user_id AND mb.branch_id=e.branch_id AND mb.department_id=e.target_department_id AND mb.kind='EMPLOYMENT'
 AND mb.valid_from<=CASE WHEN e.valid_from>@now THEN e.valid_from ELSE @now END AND (mb.valid_to IS NULL OR (e.valid_to IS NOT NULL AND mb.valid_to>=e.valid_to)));
 -- Future assignments use new/reused templates. Past assignments keep their original FK.
 SELECT s.shift_id old_shift_id,s.branch_id,CASE WHEN m.old_code='MAINTENANCE_STAFF' THEN hk.department_id ELSE s.department_id END department_id,
 r.role_id,s.start_minute,s.end_minute,s.capacity,CASE WHEN m.old_code='MAINTENANCE_STAFF' THEN N'Buồng phòng' WHEN m.old_code='MAINTENANCE_LEAD' THEN N'Giám sát bảo trì' ELSE N'Quản lý vận hành' END name
 INTO #shift_map FROM workday_shifts s JOIN roles old ON old.role_id=s.role_id JOIN @map m ON m.old_code=old.code JOIN roles r ON r.code=m.new_code
 LEFT JOIN departments hk ON hk.branch_id=s.branch_id AND hk.code='HK' AND hk.is_active=1 WHERE s.is_active=1;
 IF EXISTS(SELECT 1 FROM #shift_map WHERE department_id IS NULL) THROW 51907,'Missing target shift department.',1;
 INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity,is_active)
 SELECT m.branch_id,m.department_id,m.role_id,m.name,m.start_minute,m.end_minute,MAX(m.capacity),1 FROM #shift_map m
 WHERE NOT EXISTS(SELECT 1 FROM workday_shifts s WHERE s.branch_id=m.branch_id AND s.department_id=m.department_id AND s.role_id=m.role_id AND s.start_minute=m.start_minute AND s.end_minute=m.end_minute)
 GROUP BY m.branch_id,m.department_id,m.role_id,m.name,m.start_minute,m.end_minute;
 SELECT a.assignment_id,a.user_id,a.work_date,s.shift_id new_shift_id,a.created_by INTO #future FROM workday_assignments a JOIN #shift_map m ON m.old_shift_id=a.shift_id
 JOIN workday_shifts s ON s.branch_id=m.branch_id AND s.department_id=m.department_id AND s.role_id=m.role_id AND s.start_minute=m.start_minute AND s.end_minute=m.end_minute
 WHERE a.status='PLANNED' AND DATEADD(minute,m.start_minute,CONVERT(datetime2,a.work_date))>DATEADD(hour,7,@now);
 IF EXISTS(SELECT 1 FROM #future f JOIN workday_shifts s ON s.shift_id=f.new_shift_id WHERE s.is_active=0)
 THROW 51908,'Target shift template is inactive; explicit review required.',1;
 IF EXISTS(SELECT 1 FROM #future f JOIN workday_assignments a ON a.shift_id=f.new_shift_id AND a.work_date=f.work_date AND a.user_id=f.user_id AND a.status<>'CANCELLED')
 THROW 51909,'Duplicate target future shift; reconcile explicitly before migration.',1;
 IF EXISTS(SELECT 1 FROM #future f JOIN workday_shifts s ON s.shift_id=f.new_shift_id GROUP BY f.new_shift_id,f.work_date,s.capacity
 HAVING COUNT(*)+(SELECT COUNT(*) FROM workday_assignments a WHERE a.shift_id=f.new_shift_id AND a.work_date=f.work_date AND a.status<>'CANCELLED')>s.capacity)
 THROW 51910,'Target future shift capacity exceeded; reconcile explicitly.',1;
 UPDATE a SET shift_id=f.new_shift_id FROM workday_assignments a JOIN #future f ON f.assignment_id=a.assignment_id;
 UPDATE s SET is_active=0 FROM workday_shifts s JOIN #shift_map m ON m.old_shift_id=s.shift_id;
 -- Safe unstarted maintenance responsibility change, with immutable event and assignment history.
 UPDATE a SET assignment_status='REASSIGNED',ended_at=@now,reason=N'P0: chuyển trách nhiệm cho giám sát bảo trì' FROM work_assignments a JOIN #maintenance m ON m.assignment_id=a.assignment_id
 WHERE m.supervisor_count=1 AND a.assignee_user_id<>m.supervisor_id;
 INSERT work_assignments(branch_id,work_id,assignee_user_id,assigned_by,assigned_at,reason)
 SELECT m.branch_id,m.work_id,m.supervisor_id,m.supervisor_id,@now,N'P0: chuyển trách nhiệm cho giám sát bảo trì' FROM #maintenance m
 WHERE m.supervisor_count=1 AND (m.assignee_user_id IS NULL OR m.assignee_user_id<>m.supervisor_id);
 INSERT work_events(branch_id,work_id,event_type,actor_user_id,operation_key,from_status,to_status,reason)
 SELECT m.branch_id,m.work_id,'ASSIGN',m.supervisor_id,NEWID(),m.status,m.status,N'P0 migration: quản lý khắc phục bởi giám sát bảo trì; không đổi loại công việc' FROM #maintenance m
 WHERE m.supervisor_count=1 AND (m.assignee_user_id IS NULL OR m.assignee_user_id<>m.supervisor_id);
 -- EXPLICIT TARGET GRANTS inserted below. Never union old roles.
 DECLARE @grants TABLE(role_code varchar(60),permission_code varchar(100),scope varchar(20),PRIMARY KEY(role_code,permission_code));
 /* TARGET_GRANTS */
 INSERT @grants VALUES
 ('SYS_ADMIN','AUDIT_VIEW','ALL'),
 ('SYS_ADMIN','MASTER_DATA_MANAGE','ALL'),
 ('SYS_ADMIN','ROLE_MANAGE','ALL'),
 ('SYS_ADMIN','USER_MANAGE','ALL'),
 ('SYS_ADMIN','USER_VIEW','ALL'),
 ('OWNER','ASSET_VIEW','ALL'),
 ('OWNER','CLEANING_VIEW','ALL'),
 ('OWNER','DASHBOARD_VIEW','ALL'),
 ('OWNER','INCIDENT_VIEW','ALL'),
 ('OWNER','INSPECTION_VIEW','ALL'),
 ('OWNER','INVENTORY_VIEW','ALL'),
 ('OWNER','IR_VIEW','ALL'),
 ('OWNER','MAINTENANCE_VIEW','ALL'),
 ('OWNER','MR_VIEW','ALL'),
 ('OWNER','REPORT_VIEW','ALL'),
 ('OWNER','ROOM_VIEW','ALL'),
 ('OWNER','TASK_VIEW','ALL'),
 ('OWNER','REPLENISHMENT_VIEW','ALL'),
 ('OWNER','STAFF_SUPPLY_VIEW','ALL'),
 ('OPERATION_MANAGER','ASSET_MANAGE','BRANCH'),
 ('OPERATION_MANAGER','ASSET_RETIRE','BRANCH'),
 ('OPERATION_MANAGER','ASSET_TRANSFER','BRANCH'),
 ('OPERATION_MANAGER','ASSET_VIEW','BRANCH'),
 ('OPERATION_MANAGER','AUDIT_VIEW','BRANCH'),
 ('OPERATION_MANAGER','ATTENDANCE_RECORD','OWN'),
 ('OPERATION_MANAGER','CLEANING_VIEW','BRANCH'),
 ('OPERATION_MANAGER','DASHBOARD_VIEW','BRANCH'),
 ('OPERATION_MANAGER','INCIDENT_VIEW','BRANCH'),
 ('OPERATION_MANAGER','INSPECTION_VIEW','BRANCH'),
 ('OPERATION_MANAGER','INVENTORY_VIEW','BRANCH'),
 ('OPERATION_MANAGER','IR_VIEW','BRANCH'),
 ('OPERATION_MANAGER','MAINTENANCE_VIEW','BRANCH'),
 ('OPERATION_MANAGER','MR_VIEW','BRANCH'),
 ('OPERATION_MANAGER','REPORT_VIEW','BRANCH'),
 ('OPERATION_MANAGER','ROOM_BLOCK_MANAGE','BRANCH'),
 ('OPERATION_MANAGER','ROOM_VIEW','BRANCH'),
 ('OPERATION_MANAGER','SHIFT_MANAGE','BRANCH'),
 ('OPERATION_MANAGER','TASK_MANAGE','BRANCH'),
 ('OPERATION_MANAGER','TASK_EXECUTE','BRANCH'),
 ('OPERATION_MANAGER','TASK_VIEW','BRANCH'),
 ('OPERATION_MANAGER','USER_VIEW','BRANCH'),
 ('OPERATION_MANAGER','ROOM_SUPPLY_CONFIG','BRANCH'),
 ('OPERATION_MANAGER','STAFF_SUPPLY_VIEW','BRANCH'),
 ('OPERATION_MANAGER','REPLENISHMENT_VIEW','BRANCH'),
 ('OPERATION_MANAGER','REPLENISHMENT_APPROVE','BRANCH'),
 ('OPERATION_MANAGER','TRANSFER_REQUEST_CREATE','BRANCH'),
 ('OPERATION_MANAGER','TRANSFER_REQUEST_APPROVE','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','ATTENDANCE_RECORD','OWN'),
 ('HOUSEKEEPING_SUPERVISOR','CLEANING_ASSIGN','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','CLEANING_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','DASHBOARD_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','DEFECT_RESOLVE','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','INCIDENT_CREATE','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','INCIDENT_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','INSPECTION_PERFORM','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','INSPECTION_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','ROOM_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','IR_CREATE','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','IR_PROCESS','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','IR_VIEW','BRANCH'),
 ('HOUSEKEEPING_SUPERVISOR','STAFF_SUPPLY_VIEW','BRANCH'),
 ('HK_STAFF','ATTENDANCE_RECORD','OWN'),
 ('HK_STAFF','CLEANING_EXECUTE','BRANCH'),
 ('HK_STAFF','CLEANING_VIEW','BRANCH'),
 ('HK_STAFF','IR_CREATE','BRANCH'),
 ('HK_STAFF','IR_PROCESS','BRANCH'),
 ('HK_STAFF','IR_VIEW','BRANCH'),
 ('HK_STAFF','MR_CREATE','BRANCH'),
 ('HK_STAFF','MR_VIEW','BRANCH'),
 ('HK_STAFF','ROOM_VIEW','BRANCH'),
 ('HK_STAFF','TASK_EXECUTE','BRANCH'),
 ('HK_STAFF','TASK_VIEW','BRANCH'),
 ('HK_STAFF','STAFF_SUPPLY_VIEW','OWN'),
 ('MAINTENANCE_SUPERVISOR','ASSET_VIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','ATTENDANCE_RECORD','OWN'),
 ('MAINTENANCE_SUPERVISOR','DASHBOARD_VIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','INCIDENT_MANAGE','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','INCIDENT_VIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','MAINTENANCE_EXECUTE','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','MAINTENANCE_REVIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','MAINTENANCE_VIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','ROOM_VIEW','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','IR_CREATE','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','IR_PROCESS','BRANCH'),
 ('MAINTENANCE_SUPERVISOR','IR_VIEW','BRANCH'),
 ('RECEPTION','ATTENDANCE_RECORD','OWN'),
 ('RECEPTION','IR_CREATE','BRANCH'),
 ('RECEPTION','IR_VIEW','BRANCH'),
 ('RECEPTION','ROOM_CHECKIN','BRANCH'),
 ('RECEPTION','ROOM_CHECKOUT','BRANCH'),
 ('RECEPTION','ROOM_OCCUPANCY_MOVE','BRANCH'),
 ('RECEPTION','ROOM_VIEW','BRANCH'),
 ('STOREKEEPER','ATTENDANCE_RECORD','OWN'),
 ('STOREKEEPER','INVENTORY_VIEW','BRANCH'),
 ('STOREKEEPER','ITEM_MASTER_MANAGE','BRANCH'),
 ('STOREKEEPER','MR_APPROVE_L1','BRANCH'),
 ('STOREKEEPER','MR_FULFILL','BRANCH'),
 ('STOREKEEPER','MR_VIEW','BRANCH'),
 ('STOREKEEPER','REPLENISHMENT_CLOSE','BRANCH'),
 ('STOREKEEPER','REPLENISHMENT_CREATE','BRANCH'),
 ('STOREKEEPER','REPLENISHMENT_RECEIVE','BRANCH'),
 ('STOREKEEPER','REPLENISHMENT_VIEW','BRANCH'),
 ('STOREKEEPER','STOCK_ADJUST_POST','BRANCH'),
 ('STOREKEEPER','STOCK_ISSUE_POST','BRANCH'),
 ('STOREKEEPER','STOCK_RECEIPT_POST','BRANCH'),
 ('STOREKEEPER','STOCK_RETURN_POST','BRANCH'),
 ('STOREKEEPER','STOCK_REVERSE','BRANCH'),
 ('STOREKEEPER','STOCK_TRANSFER_POST','BRANCH'),
 ('STOREKEEPER','STOCKTAKE_PERFORM','BRANCH'),
 ('STOREKEEPER','IR_CREATE','BRANCH'),
 ('STOREKEEPER','IR_PROCESS','BRANCH'),
 ('STOREKEEPER','IR_VIEW','BRANCH'),
 ('STOREKEEPER','STAFF_SUPPLY_VIEW','BRANCH');
 /* END_TARGET_GRANTS */
 INSERT permissions(code,module_code,description,is_active)
 SELECT v.code,'P0',v.description,1 FROM (VALUES
 ('ROOM_SUPPLY_CONFIG',N'Cấu hình cấp đồ fill và định mức theo chi nhánh'),('STAFF_SUPPLY_VIEW',N'Xem tồn đồ fill của nhân viên theo phạm vi'),
 ('REPLENISHMENT_APPROVE',N'Xử lý yêu cầu bổ sung kho gửi quản lý vận hành'),('TRANSFER_REQUEST_CREATE',N'Tạo đề nghị điều chuyển liên chi nhánh'),
 ('TRANSFER_REQUEST_APPROVE',N'Duyệt đề nghị điều chuyển trong phạm vi được giao'))v(code,description)
 WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=v.code);
 IF EXISTS(SELECT 1 FROM @grants g LEFT JOIN permissions p ON p.code=g.permission_code WHERE p.permission_id IS NULL OR p.is_active=0)
 THROW 51911,'Target permission missing/inactive. Review catalog before cutover.',1;
 DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id WHERE r.code IN (SELECT DISTINCT role_code FROM @grants);
 INSERT role_permissions(role_id,permission_id,data_scope) SELECT r.role_id,p.permission_id,g.scope FROM @grants g JOIN roles r ON r.code=g.role_code JOIN permissions p ON p.code=g.permission_code;
 -- All sessions with changed effective grants are revoked, including pre-existing OM/HK accounts.
 UPDATE s SET revoked_at=@now FROM user_sessions s WHERE s.revoked_at IS NULL AND
 (EXISTS(SELECT 1 FROM #affected a WHERE a.user_id=s.user_id) OR EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id JOIN @grants g ON g.role_code=r.code WHERE ur.user_id=s.user_id AND ur.revoked_at IS NULL));
 SELECT @after=(SELECT JSON_QUERY((SELECT code,is_active FROM roles FOR JSON PATH)) roles,
 JSON_QUERY((SELECT * FROM @grants FOR JSON PATH)) grants,JSON_QUERY((SELECT * FROM #future FOR JSON PATH)) future_schedule_changes FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT audit_logs(operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at)
 VALUES(@operation,'P0_ROLE_MIGRATION','261124-v1','APPLIED',@before,@after,@now);
 IF EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE r.code IN ('DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF') AND ur.revoked_at IS NULL)
 THROW 51912,'Legacy active assignment remained; rollback.',1;
 COMMIT;
 SELECT 'APPLIED' result,@operation operation_key;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
