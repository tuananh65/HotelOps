SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51864,'Exact owned disposable database required.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.task_progress_versions WITH(UPDLOCK,HOLDLOCK) WHERE workflow_status NOT IN('IN_PROGRESS','PAUSED'))
 THROW 51864,'Entry workflows remain; no data conversion or loss allowed.',1;
IF OBJECT_ID('dbo.CK_task_progress_state_entry_v1','C') IS NOT NULL
BEGIN
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_v1 CHECK(workflow_status IN('IN_PROGRESS','PAUSED'));
END;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskEntry.V1')
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskEntry.V1';
COMMIT;
SELECT 'TASK_ENTRY_ROLLED_BACK' AS result;
