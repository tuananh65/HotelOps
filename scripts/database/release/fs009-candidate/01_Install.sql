-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_230000_f090001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS009.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS009')
 THROW 51899,'FS009 isolated target/owner required.',1;
-- FS009 additive V1. Execute only on an explicitly verified isolated development database.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.room_cleaning_cycle_state') IS NULL
BEGIN
  IF EXISTS(SELECT room_id FROM dbo.cleaning_tasks WHERE status NOT IN('COMPLETED','CANCELLED') GROUP BY room_id HAVING COUNT(*)>1)
    THROW 51880,'Existing duplicate open cleaning tasks require explicit reconciliation.',1;
  CREATE UNIQUE INDEX UX_cleaning_tasks_open_room_v1 ON dbo.cleaning_tasks(room_id) WHERE status <> 'COMPLETED' AND status <> 'CANCELLED';
  CREATE UNIQUE INDEX UX_cleaning_task_room_v1 ON dbo.cleaning_tasks(cleaning_task_id,room_id);
  CREATE TABLE dbo.room_cleaning_cycle_state(
    room_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.rooms(room_id),
    cleaning_task_id bigint NOT NULL,
    row_version rowversion NOT NULL,
    CONSTRAINT FK_room_cycle_task_v1 FOREIGN KEY(cleaning_task_id,room_id) REFERENCES dbo.cleaning_tasks(cleaning_task_id,room_id));
  CREATE TABLE dbo.room_readiness_events(
    event_id bigint IDENTITY PRIMARY KEY,
    room_id bigint NOT NULL,
    cleaning_task_id bigint NOT NULL,
    readiness varchar(40) NOT NULL CHECK(readiness='PENDING_CLEANING'),
    has_effective_block bit NOT NULL,
    actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
    occurred_at datetime2(7) NOT NULL,
    CONSTRAINT FK_readiness_task_room_v1 FOREIGN KEY(cleaning_task_id,room_id) REFERENCES dbo.cleaning_tasks(cleaning_task_id,room_id));
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningLifecycle.V1',@value=N'FS009-V1';
END;
IF OBJECT_ID('dbo.room_readiness_events') IS NULL OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.cleaning_tasks') AND name='UX_cleaning_tasks_open_room_v1' AND is_unique=1)
  THROW 51881,'Partial lifecycle schema is not supported.',1;
COMMIT;
