-- Additive cross-branch transfer provenance. No historical backfill or grants.
-- Requires AssetCustodyV1 and AssetVersionV1. Exact confirmed development identity only.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF @@TRANCOUNT<>0 THROW 51986,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51986,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51986,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.asset_handover_versions','U') IS NULL OR COL_LENGTH('dbo.assets','row_version')<>8
 THROW 51986,'Install custody and asset version dependencies first.',1;
IF OBJECT_ID('dbo.LocationEligibleV1','FN') IS NULL OR OBJECT_ID('dbo.incident_affected_rooms','U') IS NULL
 THROW 51986,'Install location guards before enabling cross-branch custody.',1;
IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_asset_handover_versions_immutable') AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handovers')
   AND name='UX_asset_handover_pending_v1' AND is_unique=1 AND is_disabled=0 AND has_filter=1)
 THROW 51986,'Immutable custody versions and unique pending handover integrity required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AssetTransfer:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51986,'Migration lock unavailable.',1;
 IF OBJECT_ID('dbo.asset_transfer_requests','U') IS NULL
 BEGIN
  CREATE TABLE dbo.asset_transfer_requests(
   handover_id bigint NOT NULL CONSTRAINT PK_asset_transfer_requests PRIMARY KEY,
   source_branch_id bigint NOT NULL,destination_branch_id bigint NOT NULL,
   source_room_id bigint NULL,source_area_id bigint NULL,target_room_id bigint NULL,target_area_id bigint NULL,
   reason nvarchar(4000) NOT NULL,confirmation_note nvarchar(4000) NULL,
   CONSTRAINT FK_asset_transfer_handover FOREIGN KEY(handover_id) REFERENCES dbo.asset_handover_versions(handover_id),
   CONSTRAINT FK_asset_transfer_source_branch FOREIGN KEY(source_branch_id) REFERENCES dbo.branches(branch_id),
   CONSTRAINT FK_asset_transfer_destination_branch FOREIGN KEY(destination_branch_id) REFERENCES dbo.branches(branch_id),
   CONSTRAINT FK_asset_transfer_source_room FOREIGN KEY(source_room_id) REFERENCES dbo.rooms(room_id),
   CONSTRAINT FK_asset_transfer_source_area FOREIGN KEY(source_area_id) REFERENCES dbo.areas(area_id),
   CONSTRAINT FK_asset_transfer_target_room FOREIGN KEY(target_room_id) REFERENCES dbo.rooms(room_id),
   CONSTRAINT FK_asset_transfer_target_area FOREIGN KEY(target_area_id) REFERENCES dbo.areas(area_id),
   CONSTRAINT CK_asset_transfer_distinct_branch CHECK(source_branch_id<>destination_branch_id),
   CONSTRAINT CK_asset_transfer_locations CHECK((CASE WHEN source_room_id IS NULL THEN 0 ELSE 1 END+CASE WHEN source_area_id IS NULL THEN 0 ELSE 1 END)=1
    AND (CASE WHEN target_room_id IS NULL THEN 0 ELSE 1 END+CASE WHEN target_area_id IS NULL THEN 0 ELSE 1 END)=1),
   CONSTRAINT CK_asset_transfer_reason CHECK(LEN(LTRIM(RTRIM(reason)))>=10));
 END;
 IF OBJECT_ID('dbo.asset_work_source_scopes','U') IS NULL
 BEGIN
  CREATE TABLE dbo.asset_work_source_scopes(
   scope_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_asset_work_source_scopes PRIMARY KEY,
   asset_id bigint NOT NULL,branch_id bigint NOT NULL,incident_id bigint NULL,run_id bigint NULL,
   handover_id bigint NOT NULL,audit_id bigint NOT NULL,
   asset_code varchar(50) NOT NULL,asset_name nvarchar(200) NOT NULL,captured_at datetime2(7) NOT NULL,
   CONSTRAINT FK_asset_work_scope_asset FOREIGN KEY(asset_id) REFERENCES dbo.assets(asset_id),
   CONSTRAINT FK_asset_work_scope_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
   CONSTRAINT FK_asset_work_scope_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
   CONSTRAINT FK_asset_work_scope_run FOREIGN KEY(run_id) REFERENCES dbo.preventive_maintenance_runs(run_id),
   CONSTRAINT FK_asset_work_scope_handover FOREIGN KEY(handover_id) REFERENCES dbo.asset_transfer_requests(handover_id),
   CONSTRAINT FK_asset_work_scope_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
   CONSTRAINT CK_asset_work_scope_source CHECK((CASE WHEN incident_id IS NULL THEN 0 ELSE 1 END+CASE WHEN run_id IS NULL THEN 0 ELSE 1 END)=1));
  CREATE UNIQUE INDEX UX_asset_work_scope_incident ON dbo.asset_work_source_scopes(incident_id) WHERE incident_id IS NOT NULL;
  CREATE UNIQUE INDEX UX_asset_work_scope_run ON dbo.asset_work_source_scopes(run_id) WHERE run_id IS NOT NULL;
 END;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_transfer_requests_immutable ON dbo.asset_transfer_requests INSTEAD OF UPDATE,DELETE AS
 BEGIN THROW 51986,''Transfer request provenance is immutable.'',1; END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_work_source_scopes_immutable ON dbo.asset_work_source_scopes INSTEAD OF UPDATE,DELETE AS
 BEGIN THROW 51986,''Original work scope is immutable.'',1; END;');
 IF OBJECT_ID('dbo.asset_schedule_source_scopes','U') IS NULL
 BEGIN
  CREATE TABLE dbo.asset_schedule_source_scopes(
   schedule_id bigint NOT NULL CONSTRAINT PK_asset_schedule_source_scopes PRIMARY KEY,
   asset_id bigint NOT NULL,branch_id bigint NOT NULL,handover_id bigint NOT NULL,audit_id bigint NOT NULL,captured_at datetime2(7) NOT NULL,
   CONSTRAINT FK_asset_schedule_scope_schedule FOREIGN KEY(schedule_id) REFERENCES dbo.preventive_maintenance_schedules(schedule_id),
   CONSTRAINT FK_asset_schedule_scope_asset FOREIGN KEY(asset_id) REFERENCES dbo.assets(asset_id),
   CONSTRAINT FK_asset_schedule_scope_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
   CONSTRAINT FK_asset_schedule_scope_handover FOREIGN KEY(handover_id) REFERENCES dbo.asset_transfer_requests(handover_id),
   CONSTRAINT FK_asset_schedule_scope_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id));
 END;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_schedule_source_scopes_immutable ON dbo.asset_schedule_source_scopes INSTEAD OF UPDATE,DELETE AS
 BEGIN THROW 51986,''Original schedule scope is immutable.'',1; END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_schedule_original_target ON dbo.preventive_maintenance_schedules AFTER UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.asset_schedule_source_scopes s WITH(HOLDLOCK) ON s.schedule_id=i.schedule_id
    WHERE i.asset_id IS NULL OR i.asset_id<>s.asset_id OR i.room_id IS NOT NULL OR i.area_id IS NOT NULL)
   THROW 51986,''Historical schedule target is immutable.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_incident_original_asset_scope ON dbo.incidents AFTER UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.asset_work_source_scopes s WITH(HOLDLOCK) ON s.incident_id=i.incident_id
   WHERE i.branch_id<>s.branch_id OR i.asset_id IS NULL OR i.asset_id<>s.asset_id)
   THROW 51986,''Historical incident branch and asset reference are immutable.'',1;
 END;');
 EXEC(N'CREATE OR ALTER FUNCTION dbo.AssetWorkLocationEligibleV1(@branch bigint,@room bigint,@area bigint,@asset bigint,@incident bigint,@run bigint)
 RETURNS bit AS
 BEGIN
  IF @asset IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.asset_work_source_scopes p WITH(HOLDLOCK)
    WHERE p.asset_id=@asset AND p.branch_id=@branch
      AND (@incident IS NOT NULL AND p.incident_id=@incident OR @run IS NOT NULL AND p.run_id=@run))
   RETURN dbo.LocationEligibleV1(@branch,@room,@area,NULL);
  RETURN dbo.LocationEligibleV1(@branch,@room,@area,@asset);
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_incidents_v1 ON dbo.incidents AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i WHERE i.status NOT IN(''CLOSED'',''CANCELLED'')
    AND (dbo.AssetWorkLocationEligibleV1(i.branch_id,i.room_id,i.area_id,i.asset_id,i.incident_id,NULL)=0
      OR EXISTS(SELECT 1 FROM dbo.incident_affected_rooms m WITH(HOLDLOCK) WHERE m.incident_id=i.incident_id
        AND dbo.LocationEligibleV1(i.branch_id,m.room_id,NULL,NULL)=0)))
   THROW 51986,''Work locations must remain consistent with the original branch.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_preventive_maintenance_runs_v1 ON dbo.preventive_maintenance_runs AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i
    JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=i.schedule_id
    LEFT JOIN dbo.asset_work_source_scopes p WITH(HOLDLOCK) ON p.run_id=i.run_id
    LEFT JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id
    LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=s.room_id
    LEFT JOIN dbo.areas l WITH(HOLDLOCK) ON l.area_id=s.area_id
    LEFT JOIN dbo.maintenance_works w WITH(HOLDLOCK) ON w.maintenance_work_id=i.maintenance_work_id
    WHERE (i.maintenance_work_id IS NULL AND i.performed_at IS NULL OR w.status NOT IN(''CLOSED'',''CANCELLED''))
      AND dbo.AssetWorkLocationEligibleV1(COALESCE(p.branch_id,a.branch_id,r.branch_id,l.branch_id),s.room_id,s.area_id,s.asset_id,NULL,i.run_id)=0)
   THROW 51986,''Occurrence locations must remain consistent with the original branch.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_location_maintenance_works_v1 ON dbo.maintenance_works AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.incidents n WITH(HOLDLOCK) ON n.incident_id=i.incident_id
    WHERE i.status NOT IN(''CLOSED'',''CANCELLED'') AND
     (i.incident_id IS NOT NULL AND dbo.AssetWorkLocationEligibleV1(n.branch_id,n.room_id,n.area_id,n.asset_id,n.incident_id,NULL)=0
      OR EXISTS(SELECT 1 FROM dbo.preventive_maintenance_runs pr WITH(HOLDLOCK)
        JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=pr.schedule_id
        LEFT JOIN dbo.asset_work_source_scopes p WITH(HOLDLOCK) ON p.run_id=pr.run_id
        LEFT JOIN dbo.assets a WITH(HOLDLOCK) ON a.asset_id=s.asset_id
        LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=s.room_id
        LEFT JOIN dbo.areas l WITH(HOLDLOCK) ON l.area_id=s.area_id
        WHERE pr.maintenance_work_id=i.maintenance_work_id
          AND dbo.AssetWorkLocationEligibleV1(COALESCE(p.branch_id,a.branch_id,r.branch_id,l.branch_id),s.room_id,s.area_id,s.asset_id,NULL,pr.run_id)=0)))
   THROW 51986,''Maintenance locations must remain consistent with the original branch.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_asset_run_original_target ON dbo.preventive_maintenance_runs AFTER UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.run_id=i.run_id
    JOIN dbo.asset_work_source_scopes p WITH(HOLDLOCK) ON p.run_id=i.run_id
    JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=i.schedule_id
    WHERE i.schedule_id<>d.schedule_id OR s.asset_id IS NULL OR s.asset_id<>p.asset_id)
   THROW 51986,''Historical occurrence target is immutable.'',1;
 END;');
 IF ISNULL(COL_LENGTH('dbo.asset_transfer_requests','handover_id'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.asset_transfer_requests','reason'),0)<>8000
  OR ISNULL(COL_LENGTH('dbo.asset_work_source_scopes','scope_id'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.asset_work_source_scopes','asset_code'),0)<>50
  OR ISNULL(COL_LENGTH('dbo.asset_work_source_scopes','asset_name'),0)<>400
  OR ISNULL(COL_LENGTH('dbo.asset_schedule_source_scopes','schedule_id'),0)<>8
  THROW 51986,'Incompatible transfer provenance schema.',1;
 IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_transfer_requests')
      AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>7
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_work_source_scopes')
      AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>6
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_schedule_source_scopes')
      AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>5
  OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_work_source_scopes')
      AND name IN('UX_asset_work_scope_incident','UX_asset_work_scope_run') AND is_unique=1 AND is_disabled=0 AND has_filter=1)<>2
  OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.asset_transfer_requests')
      AND name IN('CK_asset_transfer_distinct_branch','CK_asset_transfer_locations','CK_asset_transfer_reason') AND is_disabled=0 AND is_not_trusted=0)<>3
  THROW 51986,'Trusted transfer provenance constraints required.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetTransfer.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.AssetTransfer.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AssetTransfer.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51986,'Conflicting capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- ApplyChanges=0 reverses DDL. After acceptance, retain all provenance and disable
-- the transfer writer for application rollback; never move assets/history back implicitly.
