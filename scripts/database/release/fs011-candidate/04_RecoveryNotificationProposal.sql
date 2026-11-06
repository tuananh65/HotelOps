-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f110001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS011.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS011')
 THROW 51899,'FS011 isolated target/owner required.',1;
-- Recovery proposal refuses to drop populated durable operation/inbox evidence.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.notification_operation_reservations) OR EXISTS(SELECT 1 FROM dbo.notification_publications) OR EXISTS(SELECT 1 FROM dbo.notification_delivery_intents)
 THROW 51893,'Populated notification proposal requires reviewed forward recovery; evidence retained.',1;
DROP TABLE dbo.notification_delivery_intents;
DROP TABLE dbo.notification_publications;
DROP TABLE dbo.notification_operation_reservations;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FS011.NotificationProposal';
COMMIT;
