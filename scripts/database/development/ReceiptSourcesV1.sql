-- SQLCMD: caller must supply the verified development identity.
-- Additive receipt source metadata; preserve existing documents and history.
-- Rollback application rollout without dropping populated storage.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=CONVERT(uniqueidentifier,'$(ExpectedDatabaseGuid)'))
 THROW 51980,'Confirmed development database identity required.',1;
IF OBJECT_ID('dbo.material_replenishments','U') IS NULL
 THROW 51981,'Install reviewed database/patches/20261021_replenishment_contract.sql on the confirmed development database first.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51981,'Receipt source installation lock unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReceiptSources.V1')
 BEGIN
  IF OBJECT_ID('dbo.stock_receipt_sources') IS NOT NULL THROW 51981,'Unversioned receipt source storage requires review.',1;
  CREATE TABLE dbo.stock_receipt_sources(
   document_id bigint NOT NULL CONSTRAINT PK_stock_receipt_sources PRIMARY KEY,
   source_label nvarchar(200) NOT NULL,
   reference_number nvarchar(100) NULL,
   replenishment_id bigint NULL,
   CONSTRAINT FK_receipt_source_document FOREIGN KEY(document_id) REFERENCES dbo.stock_documents(document_id),
   CONSTRAINT FK_receipt_source_replenishment FOREIGN KEY(replenishment_id) REFERENCES dbo.material_replenishments(replenishment_id),
   CONSTRAINT CK_receipt_source_label CHECK(LEN(LTRIM(RTRIM(source_label)))>0)
  );
  CREATE INDEX IX_receipt_source_replenishment ON dbo.stock_receipt_sources(replenishment_id,document_id);
  EXEC(N'CREATE TRIGGER dbo.TR_receipt_source_guard ON dbo.stock_receipt_sources AFTER INSERT,UPDATE,DELETE AS
  BEGIN
   SET NOCOUNT ON;
   IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.document_id=d.document_id WHERE i.document_id IS NULL)
    THROW 51981,''Receipt source history cannot be deleted.'',1;
   IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.stock_documents d WITH(UPDLOCK,HOLDLOCK) ON d.document_id=i.document_id
     LEFT JOIN dbo.material_replenishments r WITH(HOLDLOCK) ON r.replenishment_id=i.replenishment_id
     WHERE d.document_type<>''RECEIPT'' OR d.status<>''DRAFT'' OR d.destination_warehouse_id IS NULL OR d.source_warehouse_id IS NOT NULL
      OR (i.replenishment_id IS NOT NULL AND (r.replenishment_id IS NULL OR r.warehouse_id<>d.destination_warehouse_id OR r.status NOT IN(''REQUESTED'',''PARTIALLY_RECEIVED''))))
    THROW 51981,''Only a matching Draft receipt source may change.'',1;
  END');
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReceiptSources.V1',@value=1;
 END;
 IF COL_LENGTH('dbo.stock_receipt_sources','source_label')<>400
  OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.stock_receipt_sources') AND name='TR_receipt_source_guard' AND is_disabled=0)
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.stock_receipt_sources') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>2
  THROW 51981,'Receipt source schema compatibility failed.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
