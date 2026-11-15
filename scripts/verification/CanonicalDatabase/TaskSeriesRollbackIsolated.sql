SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51871,'Exact owned disposable database required.',1;
IF EXISTS(SELECT 1 FROM dbo.operational_tasks) OR EXISTS(SELECT 1 FROM dbo.audit_logs)
 THROW 51871,'Only empty source rollback is allowed; drop populated clone instead.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.operational_task_series') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.operational_task_series) OR EXISTS(SELECT 1 FROM dbo.task_series_occurrences)
  THROW 51871,'Series data remains; drop owned clone instead.',1;
 DROP TABLE dbo.task_series_occurrences;
 DROP TABLE dbo.operational_task_series;
END;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1')
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskSeries.V1';
COMMIT;
SELECT 'TASK_SERIES_REMOVED' AS result;
