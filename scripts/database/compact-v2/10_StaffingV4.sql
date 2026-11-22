-- Parameters are supplied in memory by Invoke-StaffingV4.ps1; never embed credentials here.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS' OR DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV4202611221659') THROW 51700,'Unexpected server or database.',1;
IF @Mode NOT IN ('PREFLIGHT','APPLY','FAIL_BEFORE_COMMIT','STALE_SNAPSHOT') THROW 51700,'Invalid mode.',1;
IF @BatchId IS NULL OR @BatchId='00000000-0000-0000-0000-000000000000' THROW 51700,'Nonempty batch identifier required.',1;
IF @Mode IN ('FAIL_BEFORE_COMMIT','STALE_SNAPSHOT') AND DB_NAME()='HotelOpsCompactDB' THROW 51700,'Fault injection is fixture-only.',1;
IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='StaffingV4' AND action='C_ACTIVATE') BEGIN SELECT 'ALREADY_APPLIED' result; RETURN; END;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @now datetime2(7)=SYSUTCDATETIME(),@lock int;
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps-StaffingV4',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51701,'Batch lock unavailable.',1;
 -- Capture and lock every table. Hashes remain server-side or in process memory, never in reports.
 CREATE TABLE #before(table_name sysname PRIMARY KEY,digest varbinary(32));
 DECLARE @table sysname,@order nvarchar(max),@sql nvarchar(max),@digest varbinary(32);
 DECLARE baseline CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name;
 OPEN baseline; FETCH NEXT FROM baseline INTO @table;
 WHILE @@FETCH_STATUS=0 BEGIN
  SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.'+@table) AND i.is_primary_key=1;
  IF @order IS NULL THROW 51701,'Missing primary key.',1;
  SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',(SELECT * FROM dbo.'+QUOTENAME(@table)+N' WITH(TABLOCKX,HOLDLOCK) ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES))';
  EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@digest OUTPUT;
  INSERT #before VALUES(@table,@digest); FETCH NEXT FROM baseline INTO @table;
 END; CLOSE baseline; DEALLOCATE baseline;
 IF (SELECT COUNT(*) FROM #before)<>40 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>160 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 51702,'Schema baseline drift.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@Snapshot))<>40 OR EXISTS(SELECT 1 FROM #before b LEFT JOIN OPENJSON(@Snapshot) WITH(table_name sysname,digest varchar(66)) s ON s.table_name=b.table_name WHERE s.table_name IS NULL OR b.digest<>CONVERT(varbinary(32),s.digest,1)) THROW 51703,'Concurrent database change since snapshot; retry after review.',1;
 IF (SELECT COUNT(*) FROM users)<>54 OR (SELECT COUNT(*) FROM users WHERE user_id BETWEEN 424 AND 477)<>54 OR (SELECT COUNT(*) FROM users WHERE account_status='ACTIVE')<>15 OR (SELECT COUNT(*) FROM users WHERE account_status='INACTIVE' AND user_id BETWEEN 439 AND 477 AND password_hash IS NULL)<>39 THROW 51704,'User baseline drift.',1;
 IF EXISTS(SELECT 1 FROM users WHERE employment_status<>'ACTIVE' OR deleted_at IS NOT NULL) THROW 51704,'Invalid employment state.',1;
 IF (SELECT COUNT(*) FROM user_roles)<>57 OR (SELECT COUNT(*) FROM memberships)<>110 OR (SELECT COUNT(*) FROM roles WHERE is_active=1)<>10 OR (SELECT COUNT(*) FROM permissions)<>67 OR (SELECT COUNT(*) FROM role_permissions)<>178 THROW 51705,'Access baseline drift.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@Roster))<>54 OR EXISTS(SELECT user_id,employee_code,username,full_name,email FROM users EXCEPT SELECT user_id,employee_code,username,full_name,email FROM OPENJSON(@Roster) WITH(user_id bigint,employee_code varchar(30),username nvarchar(100),full_name nvarchar(200),email nvarchar(254))) THROW 51706,'Approved V3 identity drift.',1;
 IF EXISTS(SELECT u.user_id,r.code,ur.branch_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id AND ur.revoked_at IS NULL JOIN roles r ON r.role_id=ur.role_id EXCEPT SELECT user_id,role_code,branch_id FROM OPENJSON(@Roster) WITH(user_id bigint,role_code varchar(60),branch_id bigint)) THROW 51706,'Approved V3 role assignment drift.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@Grants))<>178 OR EXISTS(SELECT r.role_id,r.code,p.code,p.is_active,r.is_active,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id EXCEPT SELECT role_id,role_code,permission_code,permission_active,role_active,data_scope FROM OPENJSON(@Grants) WITH(role_id bigint,role_code varchar(60),permission_code varchar(100),permission_active bit,role_active bit,data_scope varchar(30))) THROW 51706,'Approved permission mapping drift.',1;
 IF EXISTS(SELECT 1 FROM users u WHERE (SELECT COUNT(*) FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=u.user_id AND ur.revoked_at IS NULL AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND r.is_active=1)<>1) THROW 51707,'Missing active role.',1;
 IF NOT EXISTS(SELECT 1 FROM users u JOIN user_roles ur ON ur.user_id=u.user_id JOIN roles r ON r.role_id=ur.role_id WHERE u.user_id=424 AND u.username='NamNH1' AND u.account_status='ACTIVE' AND u.password_hash IS NOT NULL AND r.code='SYS_ADMIN' AND ur.branch_id IS NULL AND ur.revoked_at IS NULL) THROW 51707,'Administrator recovery baseline invalid.',1;
 IF NOT EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=438 AND r.code='OWNER' AND ur.branch_id IS NULL AND ur.revoked_at IS NULL) OR (SELECT COUNT(*) FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id WHERE r.code='OWNER' AND rp.data_scope='ALL')<>12 THROW 51707,'Owner scope drift.',1;
 IF NOT EXISTS(SELECT 1 FROM roles r JOIN role_permissions rp ON rp.role_id=r.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='DEPT_MANAGER' AND r.is_active=1 AND p.code='SHIFT_MANAGE' AND p.is_active=1 AND rp.data_scope='BRANCH') THROW 51707,'Department Head grant missing.',1;
 SELECT * INTO #new FROM OPENJSON(@NewUsers) WITH(employee_code varchar(30),username nvarchar(100),full_name nvarchar(200),email nvarchar(254),role_code varchar(60),department_code varchar(30));
 IF (SELECT COUNT(*) FROM #new)<>2 OR NOT EXISTS(SELECT 1 FROM #new WHERE employee_code='DH002' AND role_code='DEPT_MANAGER' AND department_code='HK') OR NOT EXISTS(SELECT 1 FROM #new WHERE employee_code='EMP050' AND role_code='STOREKEEPER' AND department_code='STORE') THROW 51708,'New identities outside approved scope.',1;
 IF EXISTS(SELECT 1 FROM #new n JOIN users u ON n.employee_code=u.employee_code OR n.username=u.username OR n.email=u.email) OR (SELECT COUNT(DISTINCT username) FROM #new)<>2 OR (SELECT COUNT(DISTINCT email) FROM #new)<>2 THROW 51708,'Duplicate new identity.',1;
 SELECT n.*,b.branch_id,d.department_id,r.role_id INTO #mapped FROM #new n JOIN branches b ON b.code='DVCT' AND b.is_active=1 JOIN departments d ON d.branch_id=b.branch_id AND d.code=n.department_code AND d.is_active=1 JOIN roles r ON r.code=n.role_code AND r.is_active=1;
 IF (SELECT COUNT(*) FROM #mapped)<>2 THROW 51708,'Invalid active branch/department/role mapping.',1;
 IF EXISTS(SELECT 1 FROM user_roles ur WHERE ur.revoked_at IS NULL AND ur.branch_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM memberships m WHERE m.user_id=ur.user_id AND m.branch_id=ur.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=@now AND (m.valid_to IS NULL OR m.valid_to>@now))) THROW 51708,'Existing role/membership scope mismatch.',1;
 IF @Mode='PREFLIGHT' BEGIN ROLLBACK;SELECT 'PREFLIGHT_PASS' result,54 users,15 active_users,39 inactive_users,40 tables;RETURN;END;
 SELECT * INTO #oldUsers FROM users;
 SELECT * INTO #oldRoles FROM user_roles;
 SELECT * INTO #oldMembers FROM memberships;
 SELECT * INTO #oldSessions FROM user_sessions;
 DECLARE @auditMax bigint=(SELECT ISNULL(MAX(audit_id),0) FROM audit_logs);
 -- A: two personnel records and exactly the existing branch-scoped roles/memberships.
 INSERT users(employee_code,username,full_name,email,account_status,employment_status) SELECT employee_code,username,full_name,email,'INACTIVE','ACTIVE' FROM #mapped;
 IF @@ROWCOUNT<>2 THROW 51710,'Expected two new personnel records.',1;
 SELECT u.user_id,m.* INTO #created FROM #mapped m JOIN users u ON u.employee_code=m.employee_code;
 INSERT user_roles(user_id,role_id,branch_id,assigned_at,assigned_by) SELECT user_id,role_id,branch_id,@now,424 FROM #created;
 IF @@ROWCOUNT<>2 THROW 51710,'Expected two new role assignments.',1;
 INSERT memberships(user_id,branch_id,department_id,kind,is_primary,recorded_by,provenance,valid_from)
 SELECT user_id,branch_id,NULL,'BRANCH_ACCESS',1,424,CONCAT('StaffingV4:',@BatchId),@now FROM #created UNION ALL SELECT user_id,branch_id,department_id,'EMPLOYMENT',0,424,CONCAT('StaffingV4:',@BatchId),@now FROM #created;
 IF @@ROWCOUNT<>4 THROW 51710,'Expected four memberships.',1;
 DECLARE @newJson nvarchar(max)=(SELECT user_id,employee_code,username,full_name,email,role_code,branch_id,department_id FROM #created FOR JSON PATH);
 INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,after_json,occurred_at) VALUES(424,@BatchId,'StaffingV4','STAFFING-V4-20261122','A_CREATE',@newJson,@now);
 -- B: application-generated, independently salted Identity hashes supplied only as parameters.
 SELECT * INTO #credentials FROM OPENJSON(@Credentials) WITH(username nvarchar(100),password_hash nvarchar(500));
 IF (SELECT COUNT(*) FROM #credentials)<>56 OR (SELECT COUNT(DISTINCT username) FROM #credentials)<>56 OR (SELECT COUNT(DISTINCT password_hash) FROM #credentials)<>56 OR EXISTS(SELECT 1 FROM #credentials WHERE LEN(password_hash)<80) THROW 51711,'Invalid credential batch.',1;
 UPDATE u SET password_hash=c.password_hash,security_stamp=NEWID(),temporary_password_expires_at=NULL,failed_login_count=0,locked_until=NULL FROM users u JOIN #credentials c ON c.username=u.username;
 IF @@ROWCOUNT<>56 THROW 51711,'Expected exactly56 credential updates.',1;
 UPDATE user_sessions SET revoked_at=@now WHERE revoked_at IS NULL;
 DECLARE @revoked int=@@ROWCOUNT;
 DECLARE @credentialAudit nvarchar(max)=(SELECT 15 existing_passwords_reset,39 v3_passwords_set,2 new_passwords_set,56 rotated_security_stamps,@revoked revoked_sessions,CAST(0 AS bit) force_password_change FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,after_json,occurred_at) VALUES(424,@BatchId,'StaffingV4','STAFFING-V4-20261122','B_CREDENTIALS',@credentialAudit,SYSUTCDATETIME());
 -- C: activation only after all credential and access prerequisites are available.
 UPDATE users SET account_status='ACTIVE' WHERE account_status='INACTIVE' AND password_hash IS NOT NULL;
 IF @@ROWCOUNT<>41 THROW 51712,'Expected41 activations.',1;
 IF (SELECT COUNT(*) FROM users)<>56 OR (SELECT COUNT(*) FROM users WHERE account_status='ACTIVE' AND employment_status='ACTIVE' AND password_hash IS NOT NULL AND temporary_password_expires_at IS NULL AND locked_until IS NULL AND failed_login_count=0)<>56 OR (SELECT COUNT(*) FROM user_roles)<>59 OR (SELECT COUNT(*) FROM memberships)<>114 THROW 51713,'Post-change counts invalid.',1;
 IF EXISTS(SELECT user_id,employee_code,username,full_name,email,phone,manager_user_id,employment_status,last_login_at,deleted_at,created_at FROM #oldUsers EXCEPT SELECT user_id,employee_code,username,full_name,email,phone,manager_user_id,employment_status,last_login_at,deleted_at,created_at FROM users) THROW 51714,'Protected user identity/history changed.',1;
 IF EXISTS(SELECT * FROM #oldRoles EXCEPT SELECT * FROM user_roles) OR EXISTS(SELECT * FROM #oldMembers EXCEPT SELECT * FROM memberships) THROW 51714,'Existing access assignment changed.',1;
 IF EXISTS(SELECT 1 FROM users u JOIN #oldUsers o ON o.user_id=u.user_id WHERE u.security_stamp=o.security_stamp OR u.row_version=o.row_version) THROW 51714,'Stamp or rowversion failed to rotate.',1;
 IF EXISTS(SELECT session_id,user_id,token_hash,security_stamp,expires_at,created_at FROM #oldSessions EXCEPT SELECT session_id,user_id,token_hash,security_stamp,expires_at,created_at FROM user_sessions) OR (SELECT COUNT(*) FROM #oldSessions)<>(SELECT COUNT(*) FROM user_sessions) OR EXISTS(SELECT 1 FROM user_sessions WHERE revoked_at IS NULL) OR EXISTS(SELECT 1 FROM #oldSessions o JOIN user_sessions s ON s.session_id=o.session_id WHERE o.revoked_at IS NOT NULL AND s.revoked_at<>o.revoked_at) THROW 51714,'Session history/revocation invariant failed.',1;
 -- Verify all35 unrelated tables, including permission definitions and operational history.
 DECLARE protected CURSOR LOCAL FAST_FORWARD FOR SELECT table_name FROM #before WHERE table_name NOT IN('users','user_roles','memberships','user_sessions','audit_logs');
 OPEN protected; FETCH NEXT FROM protected INTO @table;
 WHILE @@FETCH_STATUS=0 BEGIN
  SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.'+@table) AND i.is_primary_key=1;
  SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',(SELECT * FROM dbo.'+QUOTENAME(@table)+N' ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES))';
  EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@digest OUTPUT;
  IF @digest<>(SELECT digest FROM #before WHERE table_name=@table) THROW 51714,'Unrelated table changed.',1;
  FETCH NEXT FROM protected INTO @table;
 END; CLOSE protected; DEALLOCATE protected;
 IF HASHBYTES('SHA2_256',(SELECT * FROM audit_logs WHERE audit_id<=@auditMax ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES))<>(SELECT digest FROM #before WHERE table_name='audit_logs') THROW 51714,'Existing audit history changed.',1;
 INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,after_json,occurred_at) VALUES(424,@BatchId,'StaffingV4','STAFFING-V4-20261122','C_ACTIVATE',N'{"v3_activated":39,"new_activated":2,"total_users":56,"active_users":56,"inactive_users":0,"unchanged_business_tables":35}',SYSUTCDATETIME());
 IF @Mode='FAIL_BEFORE_COMMIT' THROW 51799,'Intentional fixture rollback.',1;
 COMMIT;
 SELECT 'APPLIED' result,DB_NAME() database_name,CONVERT(varchar(36),@BatchId) batch_id,@now started_at,SYSUTCDATETIME() completed_at,56 credential_updates,41 activations,@revoked revoked_sessions,56 users,56 active_users,0 inactive_users,@newJson new_users;
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
