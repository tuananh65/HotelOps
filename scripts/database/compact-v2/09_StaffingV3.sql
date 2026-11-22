-- Approved Staffing V3 only. Runner supplies verified manifest, fresh versions and batch ID.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV3202611221630') THROW 51600,'Unexpected target database.',1;
IF @Mode NOT IN ('PREFLIGHT','APPLY','FAIL_BEFORE_COMMIT') THROW 51600,'Unexpected operation.',1;
IF @BatchId IS NULL OR @BatchId='00000000-0000-0000-0000-000000000000' THROW 51600,'A nonempty batch identifier is required.',1;
IF @Mode='FAIL_BEFORE_COMMIT' AND DB_NAME()='HotelOpsCompactDB' THROW 51600,'Fault injection is fixture-only.',1;
IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='StaffingV3' AND action='APPLY') BEGIN
 SELECT 'ALREADY_APPLIED' AS result; RETURN;
END;
SELECT * INTO #target FROM OPENJSON(@Targets) WITH(
 user_id bigint,employee_code varchar(30),username nvarchar(100),full_name nvarchar(200),email nvarchar(254),
 role_code varchar(60),branch_code varchar(30),department_code varchar(30),
 old_employee_code varchar(30),old_username nvarchar(100),old_full_name nvarchar(200),old_email nvarchar(254),old_role_id bigint);
IF (SELECT COUNT(*) FROM #target)<>54 OR (SELECT COUNT(*) FROM #target WHERE user_id IS NULL)<>39 OR (SELECT COUNT(DISTINCT user_id) FROM #target)<>15 THROW 51601,'Manifest cardinality mismatch.',1;
IF EXISTS(SELECT employee_code FROM #target GROUP BY employee_code HAVING COUNT(*)>1) OR EXISTS(SELECT username FROM #target GROUP BY username HAVING COUNT(*)>1) OR EXISTS(SELECT email FROM #target GROUP BY email HAVING COUNT(*)>1) THROW 51602,'Duplicate target identifiers.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@now datetime2(7)=SYSUTCDATETIME();
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps-StaffingV3',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51603,'Migration lock unavailable.',1;
 -- Exclusive table locks prevent concurrent logins/assignments from being overwritten.
 SELECT * INTO #oldUsers FROM users WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #oldRoles FROM user_roles WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #oldMembers FROM memberships WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #oldSessions FROM user_sessions WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #oldGrants FROM role_permissions WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #roleDefinitions FROM roles WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #permissionDefinitions FROM permissions WITH(TABLOCKX,HOLDLOCK);
 IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>160 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 51604,'Schema or foreign-key baseline mismatch.',1;
 IF (SELECT COUNT(*) FROM #oldUsers)<>15 OR (SELECT COUNT(*) FROM #oldRoles)<>15 OR (SELECT COUNT(*) FROM #oldMembers)<>32 THROW 51605,'User/access baseline changed; refresh review.',1;
 IF (SELECT COUNT(*) FROM roles)<>12 OR (SELECT COUNT(*) FROM roles WHERE is_active=1)<>10 OR (SELECT COUNT(*) FROM permissions)<>67 OR (SELECT COUNT(*) FROM role_permissions)<>178 THROW 51606,'Permission baseline changed.',1;
 IF EXISTS(SELECT role_id,code,is_active FROM roles EXCEPT SELECT role_id,code,is_active FROM OPENJSON(@ExpectedRoles) WITH(role_id bigint,code varchar(60),is_active bit)) THROW 51606,'Role definitions differ from approved mapping.',1;
 IF EXISTS(SELECT r.role_id,r.code,p.code,p.is_active,r.is_active,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id
 EXCEPT SELECT role_id,role_code,permission_code,permission_active,role_active,data_scope FROM OPENJSON(@ExpectedGrants) WITH(role_id bigint,role_code varchar(60),permission_code varchar(100),permission_active bit,role_active bit,data_scope varchar(30))) THROW 51606,'Grant definitions differ from dry-run.',1;
 IF EXISTS(SELECT 1 FROM #target t LEFT JOIN #oldUsers u ON u.user_id=t.user_id WHERE t.user_id IS NOT NULL AND (u.user_id IS NULL OR u.employee_code<>t.old_employee_code OR u.username<>t.old_username OR u.full_name<>t.old_full_name OR ISNULL(u.email,N'')<>ISNULL(t.old_email,N''))) THROW 51607,'Existing identity differs from approved before-values.',1;
 IF EXISTS(SELECT 1 FROM #oldUsers u LEFT JOIN OPENJSON(@ExpectedUsers) WITH(user_id bigint,row_version varchar(18)) e ON e.user_id=u.user_id WHERE e.user_id IS NULL OR u.row_version<>CONVERT(binary(8),e.row_version,1)) THROW 51608,'User concurrency mismatch.',1;
 IF EXISTS(SELECT 1 FROM #oldRoles u LEFT JOIN OPENJSON(@ExpectedUserRoles) WITH(user_role_id bigint,row_version varchar(18)) e ON e.user_role_id=u.user_role_id WHERE e.user_role_id IS NULL OR u.row_version<>CONVERT(binary(8),e.row_version,1)) THROW 51608,'User role concurrency mismatch.',1;
 IF EXISTS(SELECT 1 FROM #oldMembers u LEFT JOIN OPENJSON(@ExpectedMemberships) WITH(membership_id bigint,row_version varchar(18)) e ON e.membership_id=u.membership_id WHERE e.membership_id IS NULL OR u.row_version<>CONVERT(binary(8),e.row_version,1)) THROW 51608,'Membership concurrency mismatch.',1;
 IF EXISTS(SELECT 1 FROM #target t JOIN users u ON (u.username=t.username OR u.employee_code=t.employee_code OR u.email=t.email) WHERE t.user_id IS NULL OR t.user_id<>u.user_id) THROW 51609,'Target identifier already exists.',1;
 SELECT t.*,r.role_id,b.branch_id,d.department_id INTO #mapped FROM #target t LEFT JOIN roles r ON r.code=t.role_code LEFT JOIN branches b ON b.code=t.branch_code LEFT JOIN departments d ON d.branch_id=b.branch_id AND d.code=t.department_code;
 IF (SELECT COUNT(*) FROM #mapped)<>54 OR EXISTS(SELECT 1 FROM #mapped m LEFT JOIN roles r ON r.role_id=m.role_id WHERE r.role_id IS NULL OR r.is_active=0) THROW 51610,'Invalid target role.',1;
 IF EXISTS(SELECT 1 FROM #mapped m LEFT JOIN branches b ON b.branch_id=m.branch_id LEFT JOIN departments d ON d.department_id=m.department_id WHERE m.user_id IS NULL AND (b.is_active<>1 OR b.branch_id IS NULL OR d.is_active<>1 OR d.department_id IS NULL OR d.branch_id<>b.branch_id)) THROW 51610,'Invalid new branch/department.',1;
 IF EXISTS(SELECT 1 FROM #mapped WHERE user_id IS NULL AND NOT ((role_code IN ('HK_STAFF','HOUSEKEEPING_SUPERVISOR') AND department_code='HK') OR (role_code IN ('MAINTENANCE_STAFF','MAINTENANCE_LEAD') AND department_code='ENG') OR(role_code='RECEPTION' AND department_code='FO'))) THROW 51610,'New role/department mismatch.',1;
 IF EXISTS(SELECT 1 FROM #target t JOIN user_roles ur ON ur.user_id=t.user_id WHERE ur.role_id<>t.old_role_id OR ur.revoked_at IS NOT NULL OR (ur.expires_at IS NOT NULL AND ur.expires_at<=@now)) THROW 51611,'Source assignment changed.',1;
 IF NOT EXISTS(SELECT 1 FROM users u JOIN user_roles ur ON ur.user_id=u.user_id JOIN roles r ON r.role_id=ur.role_id WHERE u.user_id=@ActorId AND u.user_id=424 AND r.code='SYS_ADMIN' AND r.is_active=1 AND ur.branch_id IS NULL AND ur.revoked_at IS NULL AND ur.assigned_at<=@now AND ur.expires_at IS NULL AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.password_hash IS NOT NULL AND (u.locked_until IS NULL OR u.locked_until<=@now) AND (u.temporary_password_expires_at IS NULL OR u.temporary_password_expires_at>@now)) THROW 51612,'Administrator not recoverably active.',1;
 IF NOT EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=438 AND r.code='OWNER' AND ur.branch_id IS NULL AND ur.revoked_at IS NULL) THROW 51613,'Owner global assignment drift.',1;
 IF EXISTS(SELECT 1 FROM work_assignments WHERE assignee_user_id IN(425,432,433) AND assignment_status='ACTIVE') OR EXISTS(SELECT 1 FROM work_intervals WHERE user_id IN(425,432,433) AND closed_at IS NULL) OR EXISTS(SELECT 1 FROM workday_assignments WHERE user_id IN(425,432,433) AND status IN('PLANNED','WORKING')) THROW 51614,'Changing role has active responsibilities.',1;
 -- Capture every unaffected table as a canonical server-side hash; no confidential rows leave SQL.
 CREATE TABLE #protected(table_name sysname PRIMARY KEY,before_hash varbinary(32));
 DECLARE @table sysname,@order nvarchar(max),@sql nvarchar(max),@hash varbinary(32);
 DECLARE tables CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.tables WHERE is_ms_shipped=0 AND name NOT IN('users','user_roles','memberships','audit_logs');
 OPEN tables; FETCH NEXT FROM tables INTO @table;
 WHILE @@FETCH_STATUS=0 BEGIN
  SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.'+@table) AND i.is_primary_key=1;
  IF @order IS NULL THROW 51615,'Protected table has no deterministic primary key.',1;
  SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',(SELECT * FROM dbo.'+QUOTENAME(@table)+N' WITH(TABLOCK,HOLDLOCK) ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES))';
  EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@hash OUTPUT;
  INSERT #protected VALUES(@table,@hash); FETCH NEXT FROM tables INTO @table;
 END; CLOSE tables; DEALLOCATE tables;
 DECLARE @oldAuditHash varbinary(32)=HASHBYTES('SHA2_256',(SELECT * FROM audit_logs WITH(TABLOCK,HOLDLOCK) ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES));
 DECLARE @before nvarchar(max)=(SELECT
  (SELECT user_id,employee_code,username,full_name,email,account_status,employment_status,phone,manager_user_id,CONVERT(varchar(18),CONVERT(varbinary(8),row_version),1) AS row_version FROM #oldUsers ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS users,
  (SELECT * FROM #oldRoles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS user_roles,
  (SELECT * FROM #oldMembers ORDER BY membership_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS memberships
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 IF @Mode='PREFLIGHT' BEGIN ROLLBACK; SELECT 'PREFLIGHT_PASS' AS result,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS sql_instance,DB_NAME() AS database_name,40 AS tables,15 AS existing_users,39 AS new_users,@before AS before_json; RETURN; END;
 UPDATE u SET employee_code=t.employee_code,username=t.username,full_name=t.full_name,email=t.email FROM users u JOIN #target t ON t.user_id=u.user_id;
 IF @@ROWCOUNT<>15 THROW 51616,'Expected fifteen identity updates.',1;
 INSERT users(employee_code,username,full_name,email,password_hash,account_status,employment_status,created_at)
 SELECT employee_code,username,full_name,email,NULL,'INACTIVE','ACTIVE',@now FROM #mapped WHERE user_id IS NULL;
 IF @@ROWCOUNT<>39 THROW 51617,'Expected thirty-nine inactive users.',1;
 SELECT u.user_id,m.employee_code,m.role_id,m.branch_id,m.department_id INTO #newUsers FROM #mapped m JOIN users u ON u.employee_code=m.employee_code WHERE m.user_id IS NULL;
 INSERT memberships(user_id,branch_id,kind,is_primary,recorded_by,provenance,valid_from)
 SELECT user_id,branch_id,'BRANCH_ACCESS',1,@ActorId,CONCAT('StaffingV3:',CONVERT(varchar(36),@BatchId)),@now FROM #newUsers;
 IF @@ROWCOUNT<>39 THROW 51618,'Expected thirty-nine branch memberships.',1;
 INSERT memberships(user_id,branch_id,department_id,kind,is_primary,recorded_by,provenance,valid_from)
 SELECT user_id,branch_id,department_id,'EMPLOYMENT',0,@ActorId,CONCAT('StaffingV3:',CONVERT(varchar(36),@BatchId)),@now FROM #newUsers;
 IF @@ROWCOUNT<>39 THROW 51618,'Expected thirty-nine employment memberships.',1;
 UPDATE user_roles SET revoked_at=@now WHERE user_id IN(425,432,433) AND revoked_at IS NULL;
 IF @@ROWCOUNT<>3 THROW 51619,'Expected three old assignment revocations.',1;
 INSERT user_roles(user_id,role_id,branch_id,assigned_at,assigned_by)
 SELECT user_id,role_id,branch_id,@now,@ActorId FROM #newUsers UNION ALL SELECT user_id,role_id,branch_id,@now,@ActorId FROM #mapped WHERE user_id IN(425,432,433);
 IF @@ROWCOUNT<>42 THROW 51620,'Expected forty-two new role assignments.',1;
 IF (SELECT COUNT(*) FROM users)<>54 OR (SELECT COUNT(*) FROM user_roles)<>57 OR (SELECT COUNT(*) FROM memberships)<>110 OR (SELECT COUNT(*) FROM users WHERE account_status='INACTIVE')<>39 THROW 51621,'Post-change counts invalid.',1;
 IF EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.revoked_at IS NULL AND r.is_active=0) THROW 51622,'Inactive role still assigned.',1;
 IF EXISTS(SELECT 1 FROM #newUsers n JOIN users u ON u.user_id=n.user_id WHERE u.password_hash IS NOT NULL OR u.account_status<>'INACTIVE') THROW 51622,'New user unexpectedly provisioned.',1;
 IF EXISTS(SELECT user_id,password_hash,security_stamp,account_status,employment_status,phone,manager_user_id,failed_login_count,locked_until,temporary_password_expires_at,last_login_at,deleted_at,created_at FROM #oldUsers EXCEPT SELECT user_id,password_hash,security_stamp,account_status,employment_status,phone,manager_user_id,failed_login_count,locked_until,temporary_password_expires_at,last_login_at,deleted_at,created_at FROM users) THROW 51623,'Protected old account state changed.',1;
 IF EXISTS(SELECT * FROM #oldMembers EXCEPT SELECT * FROM memberships) THROW 51624,'Existing membership changed.',1;
 IF EXISTS(SELECT * FROM #oldRoles WHERE user_id NOT IN(425,432,433) EXCEPT SELECT * FROM user_roles) THROW 51624,'Unapproved assignment changed.',1;
 IF EXISTS(SELECT user_role_id,user_id,role_id,branch_id,assigned_at,assigned_by,expires_at FROM #oldRoles EXCEPT SELECT user_role_id,user_id,role_id,branch_id,assigned_at,assigned_by,expires_at FROM user_roles) THROW 51624,'Historical assignment identity changed.',1;
 IF EXISTS(SELECT * FROM #oldSessions EXCEPT SELECT * FROM user_sessions) OR EXISTS(SELECT * FROM user_sessions EXCEPT SELECT * FROM #oldSessions) THROW 51625,'Session changed.',1;
 IF EXISTS(SELECT * FROM #oldGrants EXCEPT SELECT * FROM role_permissions) OR EXISTS(SELECT * FROM #roleDefinitions EXCEPT SELECT * FROM roles) OR EXISTS(SELECT * FROM #permissionDefinitions EXCEPT SELECT * FROM permissions) THROW 51625,'Permission definitions changed.',1;
 IF EXISTS(SELECT 1 FROM users u JOIN #oldUsers o ON o.user_id=u.user_id WHERE u.row_version=o.row_version) THROW 51626,'Updated users must receive fresh rowversions.',1;
 DECLARE protected_tables CURSOR LOCAL FAST_FORWARD FOR SELECT table_name FROM #protected;
 OPEN protected_tables; FETCH NEXT FROM protected_tables INTO @table;
 WHILE @@FETCH_STATUS=0 BEGIN
  SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.'+@table) AND i.is_primary_key=1;
  SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',(SELECT * FROM dbo.'+QUOTENAME(@table)+N' ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES))';
  EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@hash OUTPUT;
  IF @hash<>(SELECT before_hash FROM #protected WHERE table_name=@table) THROW 51627,'Unrelated business table changed.',1;
  FETCH NEXT FROM protected_tables INTO @table;
 END; CLOSE protected_tables; DEALLOCATE protected_tables;
 IF @oldAuditHash<>HASHBYTES('SHA2_256',(SELECT * FROM audit_logs ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES)) THROW 51627,'Existing audit history changed.',1;
 DECLARE @after nvarchar(max)=(SELECT CONVERT(varchar(36),@BatchId) AS batch_id,'54 users;39 INACTIVE;57 role assignments;110 memberships;40 tables;no protected data drift' AS validation,
  (SELECT user_id,employee_code,username,full_name,email,account_status,employment_status,CONVERT(varchar(18),CONVERT(varbinary(8),row_version),1) AS row_version FROM users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS users,
  (SELECT * FROM user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS user_roles,
  (SELECT * FROM memberships ORDER BY membership_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS memberships,
  (SELECT table_name,CONVERT(varchar(66),before_hash,1) AS protected_sha256 FROM #protected ORDER BY table_name FOR JSON PATH) AS protected_tables,
  'Existing DEPARTMENT_AUTHORITY retained pending workflow decision; Owner remains global ALL' AS retained_exceptions
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at)
 VALUES(@ActorId,@BatchId,'StaffingV3','STAFFING-V3-20261122','APPLY',@before,@after,@now);
 IF @Mode='FAIL_BEFORE_COMMIT' THROW 51699,'Intentional fixture-only rollback verification.',1;
 COMMIT;
 SELECT 'APPLIED' AS result,DB_NAME() AS database_name,CONVERT(varchar(36),@BatchId) AS batch_id,15 AS existing_users_updated,39 AS new_users_created,54 AS total_users,42 AS new_role_assignments,3 AS revoked_role_assignments,39 AS branch_memberships_created,39 AS department_memberships_created,15 AS active_accounts,39 AS inactive_accounts,0 AS role_permission_changes;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
