-- UC022 room lifecycle writer coordination. No business rows or grants rewritten.
-- SQLCMD: ExpectedServer, ExpectedDatabase, ExpectedDatabaseGuid, ApplyChanges (0 rollback / 1 commit).
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF @@TRANCOUNT<>0 THROW 51988,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51988,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51988,'ApplyChanges must be 0 or 1.',1;
IF ISNULL(COL_LENGTH('dbo.room_status_history','effective_from'),0)<>40 OR ISNULL(COL_LENGTH('dbo.room_status_history','effective_to'),0)<>40
 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL OR COL_LENGTH('dbo.room_inspections','workflow_status') IS NULL
 OR COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NULL
 THROW 51988,'Housekeeping and readiness history prerequisites required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomLifecycle:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51988,'Migration lock unavailable.',1;
 -- Extend the current trusted check rather than replacing previously approved event values.
 DECLARE @check nvarchar(max)=(SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.room_status_history')
   AND name='CK_room_status_history_trigger_type' AND is_disabled=0 AND is_not_trusted=0);
 IF @check IS NULL THROW 51988,'Trusted history trigger-type constraint required.',1;
 IF CHARINDEX('ROOM_LIFECYCLE',@check)=0
 BEGIN
  ALTER TABLE dbo.room_status_history DROP CONSTRAINT CK_room_status_history_trigger_type;
  EXEC(N'ALTER TABLE dbo.room_status_history WITH CHECK ADD CONSTRAINT CK_room_status_history_trigger_type CHECK(('+@check+N') OR trigger_type=''ROOM_LIFECYCLE'');');
 END;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_cleaning_v1 ON dbo.cleaning_tasks AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id WHERE r.is_active=0 AND (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.cleaning_task_id=i.cleaning_task_id AND d.room_id=i.room_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''COMPLETED'',''CANCELLED''))) THROW 51988,''Room is inactive; new work or placement is not permitted.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_reworks_v1 ON dbo.cleaning_reworks AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id WHERE r.is_active=0 AND (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.rework_id=i.rework_id AND d.cleaning_task_id=i.cleaning_task_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''COMPLETED'',''FAILED'',''CANCELLED''))) THROW 51988,''Room is inactive; new work or placement is not permitted.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_inspections_v1 ON dbo.room_inspections AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id WHERE r.is_active=0 AND (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.inspection_id=i.inspection_id AND d.room_id=i.room_id) OR i.workflow_status IS NULL OR i.workflow_status NOT IN(''PASSED'',''FAILED'',''CANCELLED''))) THROW 51988,''Room is inactive; new work or placement is not permitted.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_blocks_v1 ON dbo.room_blocks AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id WHERE r.is_active=0 AND (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.block_id=i.block_id AND d.room_id=i.room_id) OR i.released_at IS NULL OR i.released_at>SYSUTCDATETIME())) THROW 51988,''Room is inactive; new work or placement is not permitted.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_assets_v1 ON dbo.assets AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.current_room_id WHERE r.is_active=0) THROW 51988,''Room is inactive; new work or placement is not permitted.'',1;
 END;');
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_lifecycle_intervals_v1 ON dbo.work_intervals AFTER INSERT,UPDATE AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i
    LEFT JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON i.work_item_kind=''CLEANING'' AND t.cleaning_task_id=i.work_item_id
    LEFT JOIN dbo.cleaning_reworks w WITH(HOLDLOCK) ON i.work_item_kind=''REWORK'' AND w.rework_id=i.work_item_id
    LEFT JOIN dbo.cleaning_tasks rt WITH(HOLDLOCK) ON rt.cleaning_task_id=w.cleaning_task_id
    JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=COALESCE(t.room_id,rt.room_id)
    WHERE i.closed_at IS NULL AND r.is_active=0)
    THROW 51988,''Cannot run work in an inactive room.'',1;
 END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RoomLifecycle.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.RoomLifecycle.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RoomLifecycle.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51988,'Conflicting capability marker.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- ApplyChanges=0 rolls back all DDL. Preserve history/new event codes after activation;
-- rollback admission by disabling the lifecycle writer, not by dropping integrity guards.
