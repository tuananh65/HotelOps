-- Synthetic rollback smoke test only; not business workflow acceptance.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
    AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
    AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51991,'Exact owned disposable target required',1;
IF EXISTS(SELECT 1 FROM dbo.branches) OR EXISTS(SELECT 1 FROM dbo.users)
 THROW 51991,'Empty synthetic trial required',1;
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT dbo.branches(branch_code,branch_name) VALUES('D3-142A-ROLLBACK',N'Synthetic rollback probe');
 IF (SELECT COUNT(*) FROM dbo.branches WHERE branch_code='D3-142A-ROLLBACK')<>1
  THROW 51991,'Synthetic insert was not observed',1;
 ROLLBACK TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.branches) OR EXISTS(SELECT 1 FROM dbo.users)
  THROW 51991,'Rollback cleanup failed',1;
 SELECT 'PASS_SYNTHETIC_INSERT_ROLLED_BACK' AS Result,DB_NAME() AS DatabaseIdentity;
 SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID();
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
