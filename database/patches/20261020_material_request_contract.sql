-- Approved FE-09 additive contract. Database First; never run baseline seed scripts.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'HotelOpsDB'
    THROW 51000, 'Material Request patch requires HotelOpsDB.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @r int;
 EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:MaterialRequest:Schema', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
 IF @r<0 THROW 51001, 'Schema lock unavailable.', 1;
 IF COL_LENGTH('dbo.material_requests','row_version') IS NULL
 BEGIN
  ALTER TABLE dbo.material_requests ADD row_version rowversion NOT NULL,
   receiving_room_id bigint NULL CONSTRAINT FK_material_requests_receiving_room REFERENCES dbo.rooms(room_id),
   receiving_area_id bigint NULL CONSTRAINT FK_material_requests_receiving_area REFERENCES dbo.areas(area_id),
   incident_id bigint NULL CONSTRAINT FK_material_requests_incident REFERENCES dbo.incidents(incident_id),
   remainder_closed_at datetime2(7) NULL,
   remainder_closed_by bigint NULL CONSTRAINT FK_material_requests_remainder_closed_by REFERENCES dbo.users(user_id),
   remainder_close_reason nvarchar(max) NULL;
  EXEC(N'ALTER TABLE dbo.material_requests WITH CHECK ADD CONSTRAINT CK_material_requests_receiving_location CHECK(receiving_room_id IS NULL OR receiving_area_id IS NULL)');
 END;
 IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.material_requests') AND name='CK_material_requests_status')
  ALTER TABLE dbo.material_requests DROP CONSTRAINT CK_material_requests_status;
 ALTER TABLE dbo.material_requests WITH CHECK ADD CONSTRAINT CK_material_requests_status CHECK(status IN('DRAFT','PENDING_APPROVAL','NEEDS_INFORMATION','APPROVED','PARTIALLY_ISSUED','FULLY_ISSUED','REMAINDER_CLOSED','REJECTED','CANCELLED','SUBMITTED','RETURNED','PARTIALLY_FULFILLED','COMPLETED'));
 IF COL_LENGTH('dbo.material_request_lines','is_active') IS NULL
  ALTER TABLE dbo.material_request_lines ADD is_active bit NOT NULL CONSTRAINT DF_material_request_lines_active DEFAULT(1) WITH VALUES;
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_material_request_lines_quantities')
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.material_request_lines WHERE requested_qty<=0 OR fulfilled_qty<0 OR approved_qty<0 OR approved_qty>requested_qty OR fulfilled_qty>COALESCE(approved_qty,0))
   THROW 51002, 'Legacy request quantities require review.', 1;
  ALTER TABLE dbo.material_request_lines WITH CHECK ADD CONSTRAINT CK_material_request_lines_quantities CHECK(requested_qty>0 AND fulfilled_qty>=0 AND (approved_qty IS NULL OR (approved_qty>=0 AND approved_qty<=requested_qty)) AND fulfilled_qty<=COALESCE(approved_qty,0));
 END;
 IF OBJECT_ID('dbo.material_request_approval_cycles','U') IS NULL
 CREATE TABLE dbo.material_request_approval_cycles(
  approval_cycle_id bigint IDENTITY PRIMARY KEY,
  request_id bigint NOT NULL CONSTRAINT FK_mr_cycles_request REFERENCES dbo.material_requests(request_id),
  revision_no int NOT NULL,
  submitted_by bigint NOT NULL CONSTRAINT FK_mr_cycles_submitter REFERENCES dbo.users(user_id),
  assigned_approver_id bigint NOT NULL CONSTRAINT FK_mr_cycles_approver REFERENCES dbo.users(user_id),
  status varchar(30) NOT NULL,
  submitted_at datetime2(7) NOT NULL,
  resolved_at datetime2(7) NULL,
  CONSTRAINT UQ_mr_cycles_revision UNIQUE(request_id,revision_no),
  CONSTRAINT CK_mr_cycles_status CHECK(status IN('PENDING','NEEDS_INFORMATION','APPROVED','REJECTED','SUPERSEDED')),
  CONSTRAINT CK_mr_cycles_revision CHECK(revision_no>0),
  CONSTRAINT CK_mr_cycles_segregation CHECK(submitted_by<>assigned_approver_id)
 );
 IF OBJECT_ID('dbo.material_request_approval_line_snapshots','U') IS NULL
 CREATE TABLE dbo.material_request_approval_line_snapshots(
  snapshot_id bigint IDENTITY PRIMARY KEY,
  approval_cycle_id bigint NOT NULL CONSTRAINT FK_mr_snapshots_cycle REFERENCES dbo.material_request_approval_cycles(approval_cycle_id),
  request_line_id bigint NOT NULL CONSTRAINT FK_mr_snapshots_line REFERENCES dbo.material_request_lines(line_id),
  line_no int NOT NULL,
  item_id bigint NOT NULL CONSTRAINT FK_mr_snapshots_item REFERENCES dbo.inventory_items(item_id),
  uom_id bigint NOT NULL CONSTRAINT FK_mr_snapshots_uom REFERENCES dbo.units_of_measure(uom_id),
  requested_qty decimal(18,3) NOT NULL,
  approved_qty decimal(18,3) NULL,
  note nvarchar(max) NULL,
  CONSTRAINT UQ_mr_snapshots_line UNIQUE(approval_cycle_id,request_line_id),
  CONSTRAINT CK_mr_snapshots_qty CHECK(requested_qty>0 AND (approved_qty IS NULL OR (approved_qty>=0 AND approved_qty<=requested_qty)))
 );
 IF COL_LENGTH('dbo.material_request_approvals','approval_cycle_id') IS NULL
  ALTER TABLE dbo.material_request_approvals ADD approval_cycle_id bigint NULL CONSTRAINT FK_mr_approvals_cycle REFERENCES dbo.material_request_approval_cycles(approval_cycle_id);
 IF COL_LENGTH('dbo.material_fulfillment_lines','stock_document_line_id') IS NULL
  ALTER TABLE dbo.material_fulfillment_lines ADD stock_document_line_id bigint NULL CONSTRAINT FK_mr_fulfillment_issue_line REFERENCES dbo.stock_document_lines(line_id);
 IF EXISTS(SELECT stock_document_id FROM dbo.material_fulfillments WHERE stock_document_id IS NOT NULL GROUP BY stock_document_id HAVING COUNT(*)>1)
  THROW 51003, 'Legacy duplicate fulfillment document.', 1;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_fulfillments') AND name='UX_mr_fulfillment_document')
  CREATE UNIQUE INDEX UX_mr_fulfillment_document ON dbo.material_fulfillments(stock_document_id) WHERE stock_document_id IS NOT NULL;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_fulfillment_lines') AND name='UX_mr_fulfillment_issue_line')
  EXEC(N'CREATE UNIQUE INDEX UX_mr_fulfillment_issue_line ON dbo.material_fulfillment_lines(stock_document_line_id) WHERE stock_document_line_id IS NOT NULL');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_request_approvals') AND name='UX_mr_decision_cycle')
  EXEC(N'CREATE UNIQUE INDEX UX_mr_decision_cycle ON dbo.material_request_approvals(approval_cycle_id) WHERE approval_cycle_id IS NOT NULL');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_fulfillments') AND name='IX_mr_fulfillments_request')
  CREATE INDEX IX_mr_fulfillments_request ON dbo.material_fulfillments(request_id,fulfillment_id);
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_fulfillment_lines') AND name='IX_mr_fulfillment_request_line')
  CREATE INDEX IX_mr_fulfillment_request_line ON dbo.material_fulfillment_lines(request_line_id);
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_request_approvals') AND name='IX_mr_approval_timeline')
  CREATE INDEX IX_mr_approval_timeline ON dbo.material_request_approvals(request_id,decided_at,approval_id);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
