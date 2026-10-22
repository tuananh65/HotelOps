-- Approved Maintenance Material Usage V1; additive Database First contract.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET NOCOUNT ON;
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'HotelOpsDB'
 THROW 51000, 'Material usage patch requires HotelOpsDB.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceMaterialUsage:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51001, 'Schema lock unavailable.', 1;
 IF OBJECT_ID('dbo.maintenance_material_usages','U') IS NOT NULL
  THROW 51002, 'Usage table already exists; compare metadata instead of overwriting.', 1;
 ALTER TABLE dbo.maintenance_work_logs ADD CONSTRAINT UQ_maintenance_logs_work UNIQUE(log_id,maintenance_work_id);
 CREATE TABLE dbo.maintenance_material_usages(
  usage_id bigint IDENTITY CONSTRAINT PK_maintenance_material_usages PRIMARY KEY,
  maintenance_work_id bigint NOT NULL CONSTRAINT FK_material_usages_work REFERENCES dbo.maintenance_works(maintenance_work_id),
  maintenance_work_log_id bigint NOT NULL,
  stock_document_line_id bigint NOT NULL CONSTRAINT FK_material_usages_issue_line REFERENCES dbo.stock_document_lines(line_id),
  item_id bigint NOT NULL CONSTRAINT FK_material_usages_item REFERENCES dbo.inventory_items(item_id),
  base_uom_id bigint NOT NULL CONSTRAINT FK_material_usages_base_uom REFERENCES dbo.units_of_measure(uom_id),
  uom_id bigint NOT NULL CONSTRAINT FK_material_usages_uom REFERENCES dbo.units_of_measure(uom_id),
  used_qty decimal(18,3) NOT NULL,
  conversion_factor decimal(18,6) NOT NULL,
  used_base_qty decimal(18,3) NOT NULL,
  CONSTRAINT FK_material_usages_log_work FOREIGN KEY(maintenance_work_log_id,maintenance_work_id) REFERENCES dbo.maintenance_work_logs(log_id,maintenance_work_id),
  CONSTRAINT UQ_material_usages_log_issue UNIQUE(maintenance_work_log_id,stock_document_line_id),
  CONSTRAINT CK_material_usages_quantity CHECK(used_qty>0 AND conversion_factor>0 AND used_base_qty>0 AND used_base_qty=used_qty*conversion_factor),
  CONSTRAINT CK_material_usages_base_factor CHECK(uom_id<>base_uom_id OR conversion_factor=1)
 );
 CREATE INDEX IX_material_usages_work_history ON dbo.maintenance_material_usages(maintenance_work_id,usage_id);
 CREATE INDEX IX_material_usages_issue_capacity ON dbo.maintenance_material_usages(stock_document_line_id,usage_id) INCLUDE(used_base_qty);
 CREATE INDEX IX_material_usages_log_work ON dbo.maintenance_material_usages(maintenance_work_log_id,maintenance_work_id);
 CREATE INDEX IX_material_usages_item ON dbo.maintenance_material_usages(item_id);
 CREATE INDEX IX_material_usages_base_uom ON dbo.maintenance_material_usages(base_uom_id);
 CREATE INDEX IX_material_usages_uom ON dbo.maintenance_material_usages(uom_id);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
