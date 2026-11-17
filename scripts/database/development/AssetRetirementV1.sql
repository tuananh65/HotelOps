-- UC074 additive writer coordination. No records, grants or historical states are rewritten.
-- SQLCMD: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF @@TRANCOUNT<>0 THROW 51985,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51985,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51985,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.preventive_occurrence_details','U') IS NULL OR OBJECT_ID('dbo.work_intervals','U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.LocationGuards.V1' AND CONVERT(nvarchar(10),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetVersion.V1' AND CONVERT(nvarchar(10),value)='1')
 OR (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND name IN
 ('TR_location_incidents_v1','TR_location_preventive_maintenance_schedules_v1','TR_location_preventive_maintenance_runs_v1','TR_location_maintenance_works_v1'))<>4
 THROW 51985,'Installed location, occurrence, work interval and asset version capabilities required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AssetRetirement:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51985,'Migration lock unavailable.',1;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_assets_retirement_dependencies_v1 ON dbo.assets AFTER UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted a WHERE a.lifecycle_status=''RETIRED'' AND (
    EXISTS(SELECT 1 FROM dbo.incidents n WITH(HOLDLOCK) WHERE n.asset_id=a.asset_id AND n.status NOT IN(''CLOSED'',''CANCELLED''))
    OR EXISTS(SELECT 1 FROM dbo.maintenance_works w WITH(HOLDLOCK) JOIN dbo.incidents n WITH(HOLDLOCK) ON n.incident_id=w.incident_id
      WHERE n.asset_id=a.asset_id AND w.status NOT IN(''CLOSED'',''CANCELLED''))
    OR EXISTS(SELECT 1 FROM dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) WHERE s.asset_id=a.asset_id AND s.is_active=1)
    OR EXISTS(SELECT 1 FROM dbo.asset_handovers h WITH(HOLDLOCK) WHERE h.asset_id=a.asset_id AND h.handover_status=''PENDING'')
    OR EXISTS(SELECT 1 FROM dbo.preventive_maintenance_runs r WITH(HOLDLOCK)
      JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=r.schedule_id
      LEFT JOIN dbo.preventive_occurrence_details d WITH(HOLDLOCK) ON d.run_id=r.run_id
      WHERE s.asset_id=a.asset_id AND (d.run_id IS NULL OR d.workflow_status NOT IN(''COMPLETED'',''CANCELLED'')))
    OR EXISTS(SELECT 1 FROM dbo.work_intervals wi WITH(HOLDLOCK) WHERE wi.closed_at IS NULL AND (
      wi.work_item_kind=''MAINTENANCE'' AND EXISTS(SELECT 1 FROM dbo.maintenance_works w WITH(HOLDLOCK)
        JOIN dbo.incidents n WITH(HOLDLOCK) ON n.incident_id=w.incident_id WHERE w.maintenance_work_id=wi.work_item_id AND n.asset_id=a.asset_id)
      OR wi.work_item_kind=''OCCURRENCE'' AND EXISTS(SELECT 1 FROM dbo.preventive_maintenance_runs r WITH(HOLDLOCK)
        JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=r.schedule_id WHERE r.run_id=wi.work_item_id AND s.asset_id=a.asset_id)))
  )) THROW 51985,''Asset retirement blocked by open dependencies.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_handovers_retirement_v1 ON dbo.asset_handovers AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted h JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=h.asset_id
    WHERE h.handover_status=''PENDING'' AND a.lifecycle_status=''RETIRED'')
   THROW 51985,''A retired asset cannot acquire a pending handover.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_occurrence_details_retirement_v1 ON dbo.preventive_occurrence_details AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted d JOIN dbo.preventive_maintenance_runs r WITH(HOLDLOCK) ON r.run_id=d.run_id
    JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=r.schedule_id
    JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id
    WHERE d.workflow_status NOT IN(''COMPLETED'',''CANCELLED'') AND a.lifecycle_status=''RETIRED'')
   THROW 51985,''A retired asset cannot acquire an open occurrence.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetRetirement.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.AssetRetirement.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetRetirement.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51985,'Conflicting capability marker.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- Rollback trial uses ApplyChanges=0. After activation, preserve guards and disable
-- retirement admission if rollback is needed; do not drop protections/history on a live database.
