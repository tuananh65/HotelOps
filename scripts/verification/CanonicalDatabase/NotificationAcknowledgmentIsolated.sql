SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51864,'Exact owned disposable database required.',1;
IF COL_LENGTH('dbo.notification_recipients','read_at') IS NULL OR COL_LENGTH('dbo.notifications','notification_type')<>60
 THROW 51881,'Notification baseline schema incompatible.',1;
IF EXISTS(SELECT 1 FROM dbo.notifications) OR EXISTS(SELECT 1 FROM dbo.notification_recipients)
 THROW 51881,'Empty owned notification fixture required before schema preparation.',1;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.notifications','requires_acknowledgment') IS NULL
 ALTER TABLE dbo.notifications ADD requires_acknowledgment bit NOT NULL CONSTRAINT DF_notification_requires_ack DEFAULT(0);
IF COL_LENGTH('dbo.notification_recipients','acknowledged_at') IS NULL
 ALTER TABLE dbo.notification_recipients ADD acknowledged_at datetime2(7) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.notifications') AND name='requires_acknowledgment' AND system_type_id=104 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.notification_recipients') AND name='acknowledged_at' AND system_type_id=42 AND is_nullable=1)
 THROW 51881,'Conflicting acknowledgment column definition.',1;
IF OBJECT_ID('dbo.CK_notification_ack_read','C') IS NULL
 EXEC(N'ALTER TABLE dbo.notification_recipients WITH CHECK ADD CONSTRAINT CK_notification_ack_read CHECK(acknowledged_at IS NULL OR (read_at IS NOT NULL AND acknowledged_at>=read_at AND delivery_status=''DELIVERED'' AND delivery_channel=''IN_APP''))');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_notification_ack_read' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51881,'Acknowledgment constraint must be trusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.NotificationAcknowledgment.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.NotificationAcknowledgment.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.NotificationAcknowledgment.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51881,'Conflicting acknowledgment capability.',1;
COMMIT;
