/* Display-name change only. Back up and VERIFYONLY before applying.
   Stable role code/ID, grants, assignments and historical events are preserved. */
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51991,'Compact database required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.roles WHERE code='MAINTENANCE_SUPERVISOR')
 THROW 51991,'Maintenance role not found.',1;
BEGIN TRANSACTION;
DECLARE @roleId bigint,@before nvarchar(max),@after nvarchar(max);
SELECT @roleId=role_id FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE code='MAINTENANCE_SUPERVISOR';
IF EXISTS(SELECT 1 FROM dbo.roles WHERE role_id=@roleId AND
 (name<>N'Trưởng đội bảo trì' OR ISNULL(description,N'')<>N'Maintenance Lead'))
BEGIN
 SELECT @before=(SELECT role_id,code,name,description FROM dbo.roles WHERE role_id=@roleId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.roles SET name=N'Trưởng đội bảo trì',description=N'Maintenance Lead' WHERE role_id=@roleId;
 SELECT @after=(SELECT role_id,code,name,description FROM dbo.roles WHERE role_id=@roleId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at)
 VALUES(NEWID(),'Role',CONVERT(nvarchar(30),@roleId),'DISPLAY_NAME_CHANGED',@before,@after,SYSUTCDATETIME());
END;
COMMIT;
