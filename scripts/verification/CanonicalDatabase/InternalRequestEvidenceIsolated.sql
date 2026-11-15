SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51896,'Exact owned disposable database required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestEvidence.V1')
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
   AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0
   AND definition=N'([entity_type]=''TASK'' OR [entity_type]=''REQUEST'' OR [entity_type]=''STOCK_DOCUMENT'' OR [entity_type]=''ASSET'' OR [entity_type]=''MAINTENANCE'' OR [entity_type]=''INCIDENT'' OR [entity_type]=''INSPECTION'' OR [entity_type]=''CLEANING_TASK'' OR [entity_type]=''ROOM'')')
  THROW 51898,'Attachment discriminator baseline differs; preserve and review.',1;
 ALTER TABLE dbo.attachments DROP CONSTRAINT CK_attachments_entity_type;
 ALTER TABLE dbo.attachments WITH CHECK ADD CONSTRAINT CK_attachments_entity_type
  CHECK(entity_type IN('TASK','REQUEST','STOCK_DOCUMENT','ASSET','MAINTENANCE','INCIDENT','INSPECTION','CLEANING_TASK','ROOM','INTERNAL_REQUEST_PROGRESS'));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestEvidence.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestEvidence.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
   AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''INTERNAL_REQUEST_PROGRESS''%')
 THROW 51898,'Attachment discriminator capability incompatible.',1;
COMMIT;
