SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
    AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
    AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51970,'Exact owned disposable target required',1;

IF EXISTS(SELECT 1 FROM dbo.users) OR EXISTS(SELECT 1 FROM dbo.user_sessions) OR EXISTS(SELECT 1 FROM dbo.audit_logs WHERE audit_id NOT IN(1,3) OR entity_type<>'users' OR entity_id<>'1' OR actor_user_id IS NOT NULL) THROW 51991,'Unexpected fixture state',1;
BEGIN TRANSACTION;
DELETE FROM dbo.audit_logs WHERE audit_id IN(1,3) AND entity_type='users' AND entity_id='1' AND actor_user_id IS NULL;
COMMIT;
SELECT COUNT(*) RemainingAuditRows FROM dbo.audit_logs;
