SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51864,'Exact owned disposable database required.',1;
IF EXISTS(SELECT 1 FROM dbo.notifications) OR EXISTS(SELECT 1 FROM dbo.notification_recipients)
 THROW 51881,'Empty owned notification fixture required before rollback.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.NotificationAcknowledgment.V1')
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.NotificationAcknowledgment.V1';
IF OBJECT_ID('dbo.CK_notification_ack_read','C') IS NOT NULL ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_notification_ack_read;
IF COL_LENGTH('dbo.notification_recipients','acknowledged_at') IS NOT NULL ALTER TABLE dbo.notification_recipients DROP COLUMN acknowledged_at;
IF OBJECT_ID('dbo.DF_notification_requires_ack','D') IS NOT NULL ALTER TABLE dbo.notifications DROP CONSTRAINT DF_notification_requires_ack;
IF COL_LENGTH('dbo.notifications','requires_acknowledgment') IS NOT NULL ALTER TABLE dbo.notifications DROP COLUMN requires_acknowledgment;
COMMIT;
