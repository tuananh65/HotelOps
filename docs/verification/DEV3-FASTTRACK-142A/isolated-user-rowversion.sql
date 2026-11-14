-- Task26 rowversion contract, retargeted to the verified sprint-owned disposable database.
-- Source: scripts/verification/UserProfileUpdate/rowversion-forward.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
    AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
    AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51970,'Exact owned disposable target required',1;
IF OBJECT_ID(N'dbo.users',N'U') IS NULL OR EXISTS(SELECT 1 FROM dbo.users)
 THROW 51971,'Empty existing users table required',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users') AND system_type_id=189)
  OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserProfile.RowVersion')
  THROW 51972,'Patch already present or partial; explicit review required',1;
 ALTER TABLE dbo.users ADD row_version rowversion NOT NULL;
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserProfile.RowVersion',@value=N'Task26';
 COMMIT TRANSACTION;
 SELECT 'USER_ROWVERSION_INSTALLED' AS Result;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
