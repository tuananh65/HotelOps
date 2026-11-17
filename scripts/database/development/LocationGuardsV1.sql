-- UC016 location writer coordination. Additive guards; no business rows or grants changed.
-- ApplyChanges=0 rolls back DDL; 1 commits on the exact authorized development identity.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
 AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51872,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51872,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.incident_affected_rooms','U') IS NULL THROW 51872,'Affected room capability required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:LocationGuards:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51872,'Migration lock unavailable.',1;
EXEC(N'CREATE OR ALTER FUNCTION dbo.LocationEligibleV1(@branch bigint,@room bigint,@area bigint,@asset bigint)
RETURNS bit AS
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.branches WITH(HOLDLOCK) WHERE branch_id=@branch AND is_active=1) RETURN 0;
 IF @asset IS NOT NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.assets WITH(HOLDLOCK) WHERE asset_id=@asset AND branch_id=@branch AND lifecycle_status<>''RETIRED'') RETURN 0;
  DECLARE @ar bigint,@aa bigint;
  SELECT @ar=current_room_id,@aa=current_area_id FROM dbo.assets WITH(HOLDLOCK) WHERE asset_id=@asset;
  IF dbo.LocationEligibleV1(@branch,@ar,@aa,NULL)=0 RETURN 0;
 END;
 IF @room IS NOT NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.rooms r WITH(HOLDLOCK) JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=r.floor_id
    WHERE r.room_id=@room AND r.branch_id=@branch AND r.is_active=1 AND f.branch_id=@branch AND f.is_active=1
      AND (@area IS NULL OR r.area_id=@area)) RETURN 0;
  DECLARE @ra bigint,@rf bigint;
  SELECT @ra=area_id,@rf=floor_id FROM dbo.rooms WITH(HOLDLOCK) WHERE room_id=@room;
  IF @ra IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.areas WITH(HOLDLOCK)
    WHERE area_id=@ra AND branch_id=@branch AND is_active=1 AND (floor_id IS NULL OR floor_id=@rf)) RETURN 0;
 END;
 IF @area IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.areas a WITH(HOLDLOCK)
   LEFT JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=a.floor_id
   WHERE a.area_id=@area AND a.branch_id=@branch AND a.is_active=1
     AND (a.floor_id IS NULL OR f.branch_id=@branch AND f.is_active=1)) RETURN 0;
 RETURN 1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_areas_v1 ON dbo.areas AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=i.floor_id WHERE i.is_active=1 AND (NOT EXISTS(SELECT 1 FROM dbo.branches b WITH(HOLDLOCK) WHERE b.branch_id=i.branch_id AND b.is_active=1) OR i.floor_id IS NOT NULL AND (f.floor_id IS NULL OR f.branch_id<>i.branch_id OR f.is_active=0))) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_rooms_v1 ON dbo.rooms AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.is_active=1 AND dbo.LocationEligibleV1(i.branch_id,i.room_id,i.area_id,NULL)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_assets_v1 ON dbo.assets AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.lifecycle_status<>''RETIRED'' AND dbo.LocationEligibleV1(i.branch_id,i.current_room_id,i.current_area_id,NULL)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_operational_tasks_v1 ON dbo.operational_tasks AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.status NOT IN(''COMPLETED'',''CANCELLED'') AND dbo.LocationEligibleV1(i.branch_id,i.room_id,i.area_id,NULL)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_incidents_v1 ON dbo.incidents AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.status NOT IN(''CLOSED'',''CANCELLED'') AND (dbo.LocationEligibleV1(i.branch_id,i.room_id,i.area_id,i.asset_id)=0 OR EXISTS(SELECT 1 FROM dbo.incident_affected_rooms m WITH(HOLDLOCK) WHERE m.incident_id=i.incident_id AND dbo.LocationEligibleV1(i.branch_id,m.room_id,NULL,NULL)=0))) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_incident_affected_rooms_v1 ON dbo.incident_affected_rooms AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.incidents n WITH(HOLDLOCK) ON n.incident_id=i.incident_id WHERE n.status NOT IN(''CLOSED'',''CANCELLED'') AND dbo.LocationEligibleV1(n.branch_id,i.room_id,NULL,NULL)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_preventive_maintenance_schedules_v1 ON dbo.preventive_maintenance_schedules AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted s LEFT JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=s.room_id LEFT JOIN dbo.areas l WITH(HOLDLOCK) ON l.area_id=s.area_id WHERE s.is_active=1 AND dbo.LocationEligibleV1(COALESCE(a.branch_id,r.branch_id,l.branch_id),s.room_id,s.area_id,s.asset_id)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_preventive_maintenance_runs_v1 ON dbo.preventive_maintenance_runs AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=i.schedule_id LEFT JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=s.room_id LEFT JOIN dbo.areas l WITH(HOLDLOCK) ON l.area_id=s.area_id LEFT JOIN dbo.maintenance_works w WITH(HOLDLOCK) ON w.maintenance_work_id=i.maintenance_work_id WHERE (i.maintenance_work_id IS NULL AND i.performed_at IS NULL OR w.status NOT IN(''CLOSED'',''CANCELLED'')) AND dbo.LocationEligibleV1(COALESCE(a.branch_id,r.branch_id,l.branch_id),s.room_id,s.area_id,s.asset_id)=0) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_maintenance_works_v1 ON dbo.maintenance_works AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.incidents n WITH(HOLDLOCK) ON n.incident_id=i.incident_id WHERE i.status NOT IN(''CLOSED'',''CANCELLED'') AND (i.incident_id IS NOT NULL AND dbo.LocationEligibleV1(n.branch_id,n.room_id,n.area_id,n.asset_id)=0 OR EXISTS(SELECT 1 FROM dbo.preventive_maintenance_runs pr WITH(HOLDLOCK) JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=pr.schedule_id LEFT JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=s.room_id LEFT JOIN dbo.areas l WITH(HOLDLOCK) ON l.area_id=s.area_id WHERE pr.maintenance_work_id=i.maintenance_work_id AND dbo.LocationEligibleV1(COALESCE(a.branch_id,r.branch_id,l.branch_id),s.room_id,s.area_id,s.asset_id)=0))) THROW 51872,''Active work/location must reference active consistent parents.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.LocationGuards.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.LocationGuards.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
