-- Approved FE-10 additive contract; Database First, no migrations or legacy rewrites.
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
 THROW 51000, 'Replenishment patch requires HotelOpsDB.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:Replenishment:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51001, 'Schema lock unavailable.', 1;
 IF OBJECT_ID('dbo.material_replenishments','U') IS NOT NULL OR OBJECT_ID('dbo.material_replenishment_lines','U') IS NOT NULL OR OBJECT_ID('dbo.material_replenishment_receipt_lines','U') IS NOT NULL
  THROW 51002, 'FE-10 tables already exist; compare installed metadata instead of overwriting.', 1;
 CREATE TABLE dbo.material_replenishments(
  replenishment_id bigint IDENTITY CONSTRAINT PK_material_replenishments PRIMARY KEY,
  replenishment_no varchar(50) NOT NULL CONSTRAINT UQ_replenishment_no UNIQUE,
  warehouse_id bigint NOT NULL CONSTRAINT FK_replenishments_warehouse REFERENCES dbo.warehouses(warehouse_id),
  requested_by bigint NOT NULL CONSTRAINT FK_replenishments_requester REFERENCES dbo.users(user_id),
  supplier_name nvarchar(200) NULL, expected_on date NULL,
  status varchar(30) NOT NULL,
  closed_at datetime2(7) NULL,
  closed_by bigint NULL CONSTRAINT FK_replenishments_closer REFERENCES dbo.users(user_id),
  close_reason nvarchar(max) NULL,
  created_at datetime2(7) NOT NULL, updated_at datetime2(7) NULL,
  row_version rowversion NOT NULL,
  CONSTRAINT UQ_replenishments_warehouse UNIQUE(replenishment_id,warehouse_id),
  CONSTRAINT CK_replenishments_status CHECK(status IN('REQUESTED','PARTIALLY_RECEIVED','FULLY_RECEIVED','CLOSED')),
  CONSTRAINT CK_replenishments_closure CHECK(
    (status='CLOSED' AND closed_at IS NOT NULL AND closed_by IS NOT NULL AND LEN(LTRIM(RTRIM(close_reason)))>=10 AND close_reason IS NOT NULL)
    OR (status<>'CLOSED' AND closed_at IS NULL AND closed_by IS NULL AND close_reason IS NULL))
 );
 CREATE TABLE dbo.material_replenishment_lines(
  replenishment_line_id bigint IDENTITY CONSTRAINT PK_material_replenishment_lines PRIMARY KEY,
  replenishment_id bigint NOT NULL,
  warehouse_id bigint NOT NULL,
  line_no int NOT NULL,
  item_id bigint NOT NULL CONSTRAINT FK_replenishment_lines_item REFERENCES dbo.inventory_items(item_id),
  base_uom_id bigint NOT NULL CONSTRAINT FK_replenishment_lines_uom REFERENCES dbo.units_of_measure(uom_id),
  requested_base_qty decimal(18,3) NOT NULL,
  received_base_qty decimal(18,3) NOT NULL CONSTRAINT DF_replenishment_lines_received DEFAULT(0),
  source_type varchar(30) NOT NULL,
  source_threshold_id bigint NULL CONSTRAINT FK_replenishment_lines_threshold REFERENCES dbo.stock_thresholds(threshold_id),
  source_material_request_line_id bigint NULL CONSTRAINT FK_replenishment_lines_request_line REFERENCES dbo.material_request_lines(line_id),
  is_open_coverage bit NOT NULL,
  note nvarchar(max) NULL,
  CONSTRAINT FK_replenishment_lines_owner FOREIGN KEY(replenishment_id,warehouse_id) REFERENCES dbo.material_replenishments(replenishment_id,warehouse_id),
  CONSTRAINT UQ_replenishment_lines_number UNIQUE(replenishment_id,line_no),
  CONSTRAINT CK_replenishment_lines_quantities CHECK(line_no>0 AND requested_base_qty>0 AND received_base_qty>=0 AND received_base_qty<=requested_base_qty),
  CONSTRAINT CK_replenishment_lines_source CHECK(
   (source_type='LOW_STOCK' AND source_threshold_id IS NOT NULL AND source_material_request_line_id IS NULL)
   OR (source_type='MATERIAL_REQUEST_SHORTAGE' AND source_material_request_line_id IS NOT NULL AND source_threshold_id IS NULL))
 );
 CREATE UNIQUE INDEX UX_replenishment_active_coverage ON dbo.material_replenishment_lines(warehouse_id,item_id) WHERE is_open_coverage=1;
 CREATE INDEX IX_replenishment_lines_threshold ON dbo.material_replenishment_lines(source_threshold_id);
 CREATE INDEX IX_replenishment_lines_request_line ON dbo.material_replenishment_lines(source_material_request_line_id);
 CREATE TABLE dbo.material_replenishment_receipt_lines(
  replenishment_receipt_line_id bigint IDENTITY CONSTRAINT PK_material_replenishment_receipt_lines PRIMARY KEY,
  replenishment_line_id bigint NOT NULL CONSTRAINT FK_replenishment_receipts_owner REFERENCES dbo.material_replenishment_lines(replenishment_line_id),
  stock_document_line_id bigint NOT NULL CONSTRAINT FK_replenishment_receipts_stock_line REFERENCES dbo.stock_document_lines(line_id),
  credited_base_qty decimal(18,3) NOT NULL,
  linked_at datetime2(7) NOT NULL,
  linked_by bigint NOT NULL CONSTRAINT FK_replenishment_receipts_actor REFERENCES dbo.users(user_id),
  CONSTRAINT UQ_replenishment_receipt_stock_line UNIQUE(stock_document_line_id),
  CONSTRAINT CK_replenishment_receipts_credit CHECK(credited_base_qty>0)
 );
 CREATE INDEX IX_replenishment_receipts_owner ON dbo.material_replenishment_receipt_lines(replenishment_line_id);
 CREATE INDEX IX_replenishments_tracking ON dbo.material_replenishments(warehouse_id,status,expected_on,replenishment_id);
 CREATE INDEX IX_material_replenishments_closed_by ON dbo.material_replenishments(closed_by);
 CREATE INDEX IX_material_replenishments_requested_by ON dbo.material_replenishments(requested_by);
 CREATE INDEX IX_material_replenishment_lines_base_uom_id ON dbo.material_replenishment_lines(base_uom_id);
 CREATE INDEX IX_material_replenishment_lines_item_id ON dbo.material_replenishment_lines(item_id);
 CREATE INDEX IX_material_replenishment_lines_replenishment_id_warehouse_id ON dbo.material_replenishment_lines(replenishment_id,warehouse_id);
 CREATE INDEX IX_material_replenishment_receipt_lines_linked_by ON dbo.material_replenishment_receipt_lines(linked_by);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
