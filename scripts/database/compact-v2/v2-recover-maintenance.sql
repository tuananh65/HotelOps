SET XACT_ABORT ON; SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='V2_ROLE_RECOVERY' AND entity_key='261125-v1' AND action='APPLIED') BEGIN COMMIT; SELECT 'ALREADY_APPLIED' result; RETURN; END;
 DECLARE @snapshot nvarchar(max),@cutover datetime2,@now datetime2=SYSUTCDATETIME(),@operation uniqueidentifier=NEWID();
 SELECT @snapshot=before_json,@cutover=occurred_at FROM audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED';
 IF @snapshot IS NULL THROW 51990,'Recovery provenance missing.',1;
 SELECT old.user_id,old.branch_id,old.assigned_by,old.expires_at,old.role_id,old.user_role_id INTO #recover
 FROM OPENJSON(@snapshot,'$.assignments') WITH(user_role_id bigint,user_id bigint,branch_id bigint,role_id bigint,assigned_by bigint,expires_at datetime2,revoked_at datetime2) old
 JOIN roles r ON r.role_id=old.role_id AND r.code='MAINTENANCE_STAFF'
 JOIN user_roles current_old ON current_old.user_role_id=old.user_role_id AND current_old.revoked_at=@cutover
 WHERE old.revoked_at IS NULL AND (old.expires_at IS NULL OR old.expires_at>@now);
 IF NOT EXISTS(SELECT 1 FROM #recover) THROW 51990,'No exact P0 staff conversion evidence.',1;
 IF EXISTS(SELECT 1 FROM work_assignments a JOIN work_items w ON w.work_id=a.work_id JOIN #recover r ON r.user_id=a.assignee_user_id AND r.branch_id=w.branch_id
 WHERE a.assignment_status='ACTIVE' AND w.kind IN('CLEANING','REWORK') AND w.status NOT IN('CLOSED','CANCELLED')) THROW 51990,'Affected staff have active housekeeping work; resolve before recovery.',1;
 IF EXISTS(SELECT 1 FROM staff_supply_allocations a JOIN #recover r ON r.user_id=a.user_id AND r.branch_id=a.branch_id WHERE a.status<>'CLOSED') THROW 51990,'Open supply allocation requires reconciliation.',1;
 -- Revoke only HK roles inserted by the evidenced migration; genuine pre-existing HK roles survive.
 UPDATE ur SET revoked_at=@now FROM user_roles ur JOIN roles role ON role.role_id=ur.role_id AND role.code='HK_STAFF'
 JOIN #recover r ON r.user_id=ur.user_id AND r.branch_id=ur.branch_id
 WHERE ur.revoked_at IS NULL AND ur.assigned_at=@cutover
 AND NOT EXISTS(SELECT 1 FROM OPENJSON(@snapshot,'$.assignments') WITH(user_role_id bigint) old WHERE old.user_role_id=ur.user_role_id);
 UPDATE roles SET is_active=1 WHERE code='MAINTENANCE_STAFF';
 UPDATE roles SET description=N'Điều phối đội bảo trì nội bộ, nghiệm thu kỹ thuật và dịch vụ ngoài khi cần' WHERE code='MAINTENANCE_SUPERVISOR';
 INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at,expires_at)
 SELECT r.user_id,r.role_id,r.branch_id,r.assigned_by,@now,r.expires_at FROM #recover r
 WHERE NOT EXISTS(SELECT 1 FROM user_roles u WHERE u.user_id=r.user_id AND u.role_id=r.role_id AND u.branch_id=r.branch_id AND u.revoked_at IS NULL);
 SELECT m.*,old.department_id old_department_id,old.is_primary old_primary INTO #employment
 FROM memberships m JOIN #recover r ON r.user_id=m.user_id AND r.branch_id=m.branch_id
 JOIN OPENJSON(@snapshot,'$.memberships') WITH(membership_id bigint,department_id bigint,is_primary bit) old
 ON m.provenance=N'P0 261124: ENG to HK; source membership '+CONVERT(nvarchar(30),old.membership_id)
 WHERE m.kind='EMPLOYMENT' AND m.valid_from=@cutover AND (m.valid_to IS NULL OR m.valid_to>@now);
 IF (SELECT COUNT(*) FROM #employment)<>(SELECT COUNT(*) FROM #recover) THROW 51990,'Employment recovery is ambiguous.',1;
 UPDATE m SET valid_to=@now FROM memberships m JOIN #employment e ON e.membership_id=m.membership_id;
 INSERT memberships(branch_id,user_id,department_id,kind,is_primary,valid_from,valid_to,recorded_by,provenance)
 SELECT branch_id,user_id,old_department_id,'EMPLOYMENT',old_primary,@now,valid_to,recorded_by,N'V2 recovery: source P0 membership '+CONVERT(nvarchar(30),membership_id) FROM #employment;
 -- Historical/started schedules stay unchanged. Restore only exact snapshot assignments still planned.
 UPDATE shift SET is_active=1 FROM workday_shifts shift JOIN roles role ON role.role_id=shift.role_id AND role.code='MAINTENANCE_STAFF'
 WHERE EXISTS(SELECT 1 FROM OPENJSON(@snapshot,'$.shifts') WITH(shift_id bigint,is_active bit) old WHERE old.shift_id=shift.shift_id AND old.is_active=1);
 UPDATE current_schedule SET shift_id=old.shift_id FROM workday_assignments current_schedule
 JOIN OPENJSON(@snapshot,'$.schedules') WITH(assignment_id bigint,shift_id bigint,user_id bigint) old ON old.assignment_id=current_schedule.assignment_id AND old.user_id=current_schedule.user_id
 JOIN #recover r ON r.user_id=current_schedule.user_id
 JOIN workday_shifts s ON s.shift_id=old.shift_id AND s.branch_id=r.branch_id
 JOIN roles role ON role.role_id=s.role_id AND role.code='MAINTENANCE_STAFF'
 WHERE current_schedule.status='PLANNED' AND DATEADD(minute,s.start_minute,CONVERT(datetime2,current_schedule.work_date))>DATEADD(hour,7,@now);
 DECLARE @grants TABLE(code varchar(100),scope varchar(20));
 INSERT @grants VALUES('MAINTENANCE_VIEW','OWN'),('MAINTENANCE_EXECUTE','OWN'),('ASSET_VIEW','BRANCH'),('ATTENDANCE_RECORD','OWN'),('IR_VIEW','OWN'),('IR_CREATE','OWN'),('IR_PROCESS','OWN');
 IF EXISTS(SELECT 1 FROM @grants g WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=g.code AND p.is_active=1)) THROW 51990,'Required permission code missing.',1;
 DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id WHERE r.code='MAINTENANCE_STAFF';
 INSERT role_permissions(role_id,permission_id,data_scope) SELECT r.role_id,p.permission_id,g.scope FROM @grants g JOIN permissions p ON p.code=g.code CROSS JOIN roles r WHERE r.code='MAINTENANCE_STAFF';
 INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p WHERE r.code='MAINTENANCE_SUPERVISOR' AND p.code='MAINTENANCE_ASSIGN'
 AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
 UPDATE s SET revoked_at=@now FROM user_sessions s JOIN user_roles ur ON ur.user_id=s.user_id JOIN roles r ON r.role_id=ur.role_id
 WHERE s.revoked_at IS NULL AND ur.revoked_at IS NULL AND r.code='MAINTENANCE_SUPERVISOR';
 UPDATE s SET revoked_at=@now FROM user_sessions s WHERE s.revoked_at IS NULL AND EXISTS(SELECT 1 FROM #recover r WHERE r.user_id=s.user_id);
 DECLARE @after nvarchar(max)=(SELECT user_id,branch_id,user_role_id source_user_role_id FROM #recover FOR JSON PATH);
 INSERT audit_logs(operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at) VALUES(@operation,'V2_ROLE_RECOVERY','261125-v1','APPLIED',N'{"source":"P0_ROLE_MIGRATION/261124-v1"}',@after,@now);
 COMMIT; SELECT 'APPLIED' result,(SELECT COUNT(*) FROM #recover) recovered_users;
END TRY
BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
