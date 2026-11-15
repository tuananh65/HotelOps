SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51864,'Exact owned disposable database required.',1;

-- Decision 67: no new table or legacy status. The existing audit stores the intent.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.task_progress_versions','workflow_status'),-1)<>20
 OR ISNULL(COL_LENGTH('dbo.operational_task_progress','description'),0)<>-1
 OR ISNULL(COL_LENGTH('dbo.audit_logs','new_values'),0)<>-1
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51867,'Verified current FE11 workflow capability required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskDecline.V1')
BEGIN
 ALTER TABLE dbo.task_progress_versions DROP CONSTRAINT CK_task_progress_state_entry_v1;
 ALTER TABLE dbo.task_progress_versions WITH CHECK ADD CONSTRAINT CK_task_progress_state_entry_v1
  CHECK(workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','ACCEPTED','IN_PROGRESS','PAUSED','PENDING_VERIFICATION','CLOSED'));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskDecline.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskDecline.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%PENDING_ASSIGNMENT%')
 THROW 51867,'Conflicting decline capability.',1;
COMMIT;
SELECT 'TASK_DECLINE_INSTALLED' AS result;
