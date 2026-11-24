-- TEST ONLY. Apply after p0-role-migration.sql and a fresh verified backup.
-- Preserve historical warehouses and all ledger rows. No guessed warehouse or stock transfer.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestP0%' THROW 51960,'P0 isolated TEST database required.',1;
DECLARE @operation uniqueidentifier=NEWID(),@now datetime2(7)=SYSUTCDATETIME(),@lock int;
BEGIN TRY
 BEGIN TRANSACTION;
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps.P0.StorePolicy',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
 IF @lock<0 THROW 51961,'Another store policy migration is running.',1;
 -- Conservatively treat ANY warehouse FK reference as retained operational/history data.
 -- Only completely unused extra rows may be deactivated automatically.
 CREATE TABLE #used(warehouse_id bigint NOT NULL);
 DECLARE @table nvarchar(517),@column sysname,@sql nvarchar(max);
 DECLARE refs CURSOR LOCAL FAST_FORWARD FOR
 SELECT QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+'.'+QUOTENAME(OBJECT_NAME(f.parent_object_id)),c.name
 FROM sys.foreign_key_columns f JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=f.parent_column_id
 JOIN sys.columns target ON target.object_id=f.referenced_object_id AND target.column_id=f.referenced_column_id
 WHERE f.referenced_object_id=OBJECT_ID('dbo.warehouses') AND target.name='warehouse_id';
 OPEN refs;FETCH NEXT FROM refs INTO @table,@column;
 WHILE @@FETCH_STATUS=0
 BEGIN
  SET @sql=N'INSERT #used SELECT DISTINCT '+QUOTENAME(@column)+N' FROM '+@table+N' WHERE '+QUOTENAME(@column)+N' IS NOT NULL';
  EXEC sys.sp_executesql @sql;
  FETCH NEXT FROM refs INTO @table,@column;
 END;
 CLOSE refs;DEALLOCATE refs;
 SELECT w.branch_id,COUNT(*) active_count,SUM(CASE WHEN u.warehouse_id IS NULL THEN 0 ELSE 1 END) referenced_count
 INTO #branches FROM dbo.warehouses w LEFT JOIN (SELECT DISTINCT warehouse_id FROM #used)u ON u.warehouse_id=w.warehouse_id
 WHERE w.is_active=1 GROUP BY w.branch_id;
 IF EXISTS(SELECT 1 FROM #branches WHERE active_count>1 AND referenced_count<>1)
 BEGIN
  SELECT w.branch_id,w.warehouse_id,w.code,CASE WHEN EXISTS(SELECT 1 FROM #used u WHERE u.warehouse_id=w.warehouse_id) THEN 1 ELSE 0 END has_references
  FROM dbo.warehouses w JOIN #branches b ON b.branch_id=w.branch_id WHERE w.is_active=1 AND b.active_count>1 AND b.referenced_count<>1;
  THROW 51962,'Multiple warehouses require an explicit retained-warehouse decision and reconciliation; no stock/history was changed.',1;
 END;
 DECLARE @before nvarchar(max)=(SELECT w.* FROM dbo.warehouses w JOIN #branches b ON b.branch_id=w.branch_id WHERE b.active_count>1 FOR JSON PATH);
 UPDATE w SET is_active=0 FROM dbo.warehouses w JOIN #branches b ON b.branch_id=w.branch_id
 WHERE w.is_active=1 AND b.active_count>1 AND b.referenced_count=1 AND NOT EXISTS(SELECT 1 FROM #used u WHERE u.warehouse_id=w.warehouse_id);
 -- At most one active warehouse; no fake warehouse is inserted for branches not yet configured.
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.warehouses') AND name='UX_warehouses_one_active_branch')
  CREATE UNIQUE INDEX UX_warehouses_one_active_branch ON dbo.warehouses(branch_id) WHERE is_active=1;
 DECLARE @role bigint=(SELECT role_id FROM dbo.roles WHERE code='STOREKEEPER' AND is_active=1),
  @permission bigint=(SELECT permission_id FROM dbo.permissions WHERE code='MR_APPROVE_L1' AND is_active=1);
 IF @role IS NULL OR @permission IS NULL THROW 51963,'Active storekeeper role and existing MR_APPROVE_L1 permission are required.',1;
 -- Revoke obsolete manager approval paths without deleting permission definitions/history.
 DELETE grant_row FROM dbo.role_permissions grant_row JOIN dbo.roles r ON r.role_id=grant_row.role_id JOIN dbo.permissions p ON p.permission_id=grant_row.permission_id
 WHERE r.code IN ('OPERATION_MANAGER','DEPT_MANAGER') AND p.code IN ('MR_APPROVE_L1','MR_APPROVE_FALLBACK');
 UPDATE dbo.role_permissions SET data_scope='BRANCH' WHERE role_id=@role AND permission_id=@permission;
 IF NOT EXISTS(SELECT 1 FROM dbo.role_permissions WHERE role_id=@role AND permission_id=@permission)
  INSERT dbo.role_permissions(role_id,permission_id,data_scope) VALUES(@role,@permission,'BRANCH');
 -- One storekeeper can approve somebody else's in-hotel request and physically issue it.
 -- The creator/approver CHECK constraint is deliberately unchanged.
 DECLARE @trigger nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_material_issue_approval'));
 IF @trigger IS NULL THROW 51964,'Material issue approval trigger is missing.',1;
 IF CHARINDEX('P0_STOREKEEPER_APPROVAL',@trigger)=0
 BEGIN
  IF CHARINDEX('request.approved_by=m.created_by',@trigger)=0 THROW 51965,'Unknown material approval trigger definition; inspect before adapting.',1;
  SET @trigger=REPLACE(@trigger,'request.approved_by=m.created_by',
   '(request.approved_by=m.created_by AND NOT EXISTS(/* P0_STOREKEEPER_APPROVAL */ SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.users u ON u.user_id=ur.user_id JOIN dbo.role_permissions rp ON rp.role_id=r.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE ur.user_id=m.created_by AND ur.branch_id=issue.branch_id AND r.code=''STOREKEEPER'' AND r.is_active=1 AND u.account_status=''ACTIVE'' AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND p.code=''MR_APPROVE_L1'' AND p.is_active=1 AND rp.data_scope=''BRANCH'' AND EXISTS(SELECT 1 FROM dbo.memberships member WHERE member.user_id=ur.user_id AND member.branch_id=ur.branch_id AND member.kind=''BRANCH_ACCESS'' AND member.valid_from<=SYSUTCDATETIME() AND (member.valid_to IS NULL OR member.valid_to>SYSUTCDATETIME()))))');
  SET @trigger=N'ALTER '+SUBSTRING(@trigger,CHARINDEX('TRIGGER',@trigger),LEN(@trigger));
  EXEC sys.sp_executesql @trigger;
 END;
 INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at)
 VALUES(@operation,'P0_STORE_POLICY','261124-v1','APPLIED',@before,
  (SELECT 'STOREKEEPER' approver,'BRANCH' scope,'One active warehouse per branch' warehouse_policy FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@now);
 COMMIT;
 SELECT 'APPLIED' result;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
