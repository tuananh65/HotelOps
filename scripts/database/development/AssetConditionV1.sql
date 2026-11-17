-- Approved LOST lifecycle uses the existing varchar column; no historical remapping.
-- Development only. ApplyChanges=0 rolls back DDL; 1 commits. No grants or business rows.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 51985,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51985,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51985,'ApplyChanges must be 0 or 1.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.assets') AND name='lifecycle_status' AND system_type_id=167 AND max_length=40 AND is_nullable=0)
 THROW 51985,'Expected asset lifecycle storage required.',1;
-- Do not silently replace an installation-specific business constraint.
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.assets') AND definition LIKE '%lifecycle_status%')
 THROW 51985,'Review installation-specific lifecycle CHECK before enabling LOST; no constraint replaced.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AssetCondition:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51985,'Migration lock unavailable.',1;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_incidents_asset_available_v1 ON dbo.incidents AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.incident_id=i.incident_id
   JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=i.asset_id
   WHERE (d.incident_id IS NULL OR ISNULL(d.asset_id,0)<>i.asset_id)
    AND a.lifecycle_status NOT IN(''IN_USE'',''UNDER_REPAIR''))
   THROW 51985,''New incident asset must be available.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_schedule_asset_available_v1 ON dbo.preventive_maintenance_schedules AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=i.asset_id
   WHERE i.is_active=1 AND a.lifecycle_status NOT IN(''IN_USE'',''UNDER_REPAIR''))
   THROW 51985,''Active maintenance schedule asset must be available.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_run_asset_available_v1 ON dbo.preventive_maintenance_runs AFTER INSERT AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=i.schedule_id
   JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id
   WHERE a.lifecycle_status NOT IN(''IN_USE'',''UNDER_REPAIR''))
   THROW 51985,''New maintenance occurrence asset must be available.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetCondition.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.AssetCondition.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetCondition.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51985,'Conflicting capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- Post-deployment rollback: disable the condition writer; preserve LOST events and audits.
