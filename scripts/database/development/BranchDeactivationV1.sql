-- UC012 branch writer coordination. Additive triggers; no business data or grants changed.
-- SQLCMD variables: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF @@TRANCOUNT<>0 THROW 51987,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51987,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51987,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.branches','U') IS NULL OR COL_LENGTH('dbo.branches','branch_id')<>8
 THROW 51987,'Expected branches schema required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.branches')
 AND name='branch_code' AND collation_name LIKE N'%[_]CI[_]%')
 THROW 51987,'Case-insensitive branch code schema required; do not rewrite historical codes automatically.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
 WHERE i.object_id=OBJECT_ID('dbo.branches') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
 AND k.key_ordinal=1 AND COL_NAME(k.object_id,k.column_id)='branch_code'
 AND NOT EXISTS(SELECT 1 FROM sys.index_columns extra WHERE extra.object_id=i.object_id AND extra.index_id=i.index_id AND extra.key_ordinal>1))
 THROW 51987,'Existing unique branch-code index required.',1;
IF OBJECT_ID('dbo.preventive_occurrence_details','U') IS NULL
 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL
 OR COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.LocationGuards.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51987,'Location and lifecycle prerequisites required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:BranchDeactivation:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51987,'Migration lock unavailable.',1;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_internal_requests_v1 ON dbo.internal_requests AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.internal_request_id=i.internal_request_id AND d.branch_id=i.branch_id) OR i.status NOT IN(''CLOSED'',''REJECTED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_material_requests_v1 ON dbo.material_requests AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.request_id=i.request_id AND d.branch_id=i.branch_id) OR i.status NOT IN(''FULLY_ISSUED'',''REMAINDER_CLOSED'',''REJECTED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_material_replenishments_v1 ON dbo.material_replenishments AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.warehouses w WITH(HOLDLOCK) ON w.warehouse_id=i.warehouse_id WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.replenishment_id=i.replenishment_id AND d.warehouse_id=i.warehouse_id) OR i.status<>''CLOSED'') AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=w.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_stock_movements_v1 ON dbo.stock_movements AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.warehouses w WITH(HOLDLOCK) ON w.warehouse_id=i.warehouse_id WHERE NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=w.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_work_intervals_v1 ON dbo.work_intervals AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE (i.closed_at IS NULL OR NOT EXISTS(SELECT 1 FROM deleted d WHERE d.interval_id=i.interval_id AND d.branch_id=i.branch_id)) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_cleaning_tasks_v1 ON dbo.cleaning_tasks AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.cleaning_task_id=i.cleaning_task_id AND d.room_id=i.room_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''COMPLETED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=r.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_cleaning_reworks_v1 ON dbo.cleaning_reworks AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.rework_id=i.rework_id AND d.cleaning_task_id=i.cleaning_task_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''COMPLETED'',''FAILED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=r.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_room_inspections_v1 ON dbo.room_inspections AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.inspection_id=i.inspection_id AND d.room_id=i.room_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''PASSED'',''FAILED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=r.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_occurrence_details_v1 ON dbo.preventive_occurrence_details AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.run_id=i.run_id AND d.branch_id=i.branch_id) OR i.workflow_status NOT IN(''COMPLETED'',''CANCELLED'')) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_warehouses_v1 ON dbo.warehouses AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE NOT EXISTS(SELECT 1 FROM deleted d WHERE d.warehouse_id=i.warehouse_id AND d.branch_id=i.branch_id) AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1)) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_branch_maintenance_works_v1 ON dbo.maintenance_works AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.departments dept WITH(HOLDLOCK) ON dept.department_id=i.department_id WHERE i.status NOT IN(''CLOSED'',''CANCELLED'') AND (i.department_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=dept.branch_id AND b.is_active=1) OR i.department_id IS NULL AND i.incident_id IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.preventive_maintenance_runs r WITH(HOLDLOCK) JOIN dbo.preventive_occurrence_details d WITH(HOLDLOCK) ON d.run_id=r.run_id JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=d.branch_id AND b.is_active=1 WHERE r.maintenance_work_id=i.maintenance_work_id))) THROW 51987,''Branch is inactive or branch provenance is unresolved.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.BranchDeactivation.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.BranchDeactivation.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.BranchDeactivation.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51987,'Conflicting capability marker.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- ApplyChanges=0 is an atomic DDL rollback trial. After activation, preserve guards/history;
-- disable deactivation admission instead of removing protections from live writers.
