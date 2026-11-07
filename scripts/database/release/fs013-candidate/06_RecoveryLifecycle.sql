-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f130001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS013.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS013')
 THROW 51899,'FS013 isolated target/owner required.',1;
-- Empty-owned recovery only. Never drops populated lifecycle evidence.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningLifecycle.V1' AND CONVERT(nvarchar(100),value)='FS013-V1') THROW 51880,'Unknown lifecycle package.',1;
IF EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state) OR EXISTS(SELECT 1 FROM dbo.room_readiness_events) THROW 51880,'Populated lifecycle requires forward recovery.',1;
DROP TABLE dbo.room_readiness_events;
DROP TABLE dbo.room_cleaning_cycle_state;
DROP INDEX UX_cleaning_tasks_open_room_v1 ON dbo.cleaning_tasks;
DROP INDEX UX_cleaning_task_room_v1 ON dbo.cleaning_tasks;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.CleaningLifecycle.V1';
COMMIT;
