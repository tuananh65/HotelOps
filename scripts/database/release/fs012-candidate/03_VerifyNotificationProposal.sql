-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS012')
 THROW 51899,'FS012 isolated target/owner required.',1;
-- Verification only; no caller may infer A01/A02 acceptance from table existence.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS012.NotificationProposal') THROW 51892,'Missing proposal marker.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE name IN('notification_operation_reservations','notification_publications','notification_delivery_intents'))<>3 THROW 51892,'Incomplete proposal.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_delivery_inbox_v1' AND is_disabled=0 AND is_not_trusted=0) THROW 51892,'Inbox FK not trusted.',1;
SELECT 'VERIFIED_PROPOSAL_ONLY' AS result;
