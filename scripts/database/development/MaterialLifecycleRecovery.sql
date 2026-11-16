-- Compatibility batch for the existing Database First material lifecycle mappings.
-- Exact disposable recovery database only. Default rollback; set @Apply=1 to install.
-- Does not create historical cycles, infer issue links, relabel legacy statuses,
-- change permissions, or enable any production mutation endpoint.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @Apply bit = 0;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51900,'Exact verified disposable recovery database required.',1;

BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaterialLifecycle:Install',
   @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51900,'Cannot lock material lifecycle installation.',1;
 IF OBJECT_ID('dbo.material_requests','U') IS NULL
  OR OBJECT_ID('dbo.material_request_lines','U') IS NULL
  OR OBJECT_ID('dbo.material_request_approvals','U') IS NULL
  OR OBJECT_ID('dbo.material_fulfillment_lines','U') IS NULL
  OR OBJECT_ID('dbo.stock_document_lines','U') IS NULL
  THROW 51900,'Existing material and stock schema required.',1;

 DECLARE @columns TABLE(table_name sysname,column_name sysname,type_id int,bytes smallint,nullable bit);
 INSERT @columns VALUES
 ('material_requests','row_version',189,8,0),
 ('material_requests','receiving_room_id',127,8,1),
 ('material_requests','receiving_area_id',127,8,1),
 ('material_requests','incident_id',127,8,1),
 ('material_requests','remainder_closed_at',42,8,1),
 ('material_requests','remainder_closed_by',127,8,1),
 ('material_requests','remainder_close_reason',231,-1,1),
 ('material_request_lines','is_active',104,1,0),
 ('material_request_approvals','approval_cycle_id',127,8,1),
 ('material_fulfillment_lines','stock_document_line_id',127,8,1),
 ('material_request_approval_cycles','approval_cycle_id',127,8,0),
 ('material_request_approval_cycles','request_id',127,8,0),
 ('material_request_approval_cycles','revision_no',56,4,0),
 ('material_request_approval_cycles','submitted_by',127,8,0),
 ('material_request_approval_cycles','assigned_approver_id',127,8,0),
 ('material_request_approval_cycles','status',167,30,0),
 ('material_request_approval_cycles','submitted_at',42,8,0),
 ('material_request_approval_cycles','resolved_at',42,8,1),
 ('material_request_approval_line_snapshots','snapshot_id',127,8,0),
 ('material_request_approval_line_snapshots','approval_cycle_id',127,8,0),
 ('material_request_approval_line_snapshots','request_line_id',127,8,0),
 ('material_request_approval_line_snapshots','line_no',56,4,0),
 ('material_request_approval_line_snapshots','item_id',127,8,0),
 ('material_request_approval_line_snapshots','uom_id',127,8,0),
 ('material_request_approval_line_snapshots','requested_qty',106,9,0),
 ('material_request_approval_line_snapshots','approved_qty',106,9,1),
 ('material_request_approval_line_snapshots','note',231,-1,1);

 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaterialLifecycle.V1')
 BEGIN
  IF EXISTS(SELECT 1 FROM @columns e JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.table_name) AND c.name=e.column_name)
   OR OBJECT_ID('dbo.material_request_approval_cycles') IS NOT NULL
   OR OBJECT_ID('dbo.material_request_approval_line_snapshots') IS NOT NULL
   THROW 51900,'Unexpected partial lifecycle schema; preserve and review.',1;
  IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.material_requests')
    AND name='CK_material_requests_status' AND is_disabled=0 AND is_not_trusted=0
    AND definition=N'([status]=''CANCELLED'' OR [status]=''REJECTED'' OR [status]=''COMPLETED'' OR [status]=''PARTIALLY_FULFILLED'' OR [status]=''APPROVED'' OR [status]=''RETURNED'' OR [status]=''SUBMITTED'' OR [status]=''DRAFT'')')
   THROW 51900,'Legacy status constraint differs from inspected baseline; preserve and review.',1;

  EXEC(N'ALTER TABLE dbo.material_requests ADD row_version rowversion NOT NULL,
    receiving_room_id bigint NULL,receiving_area_id bigint NULL,incident_id bigint NULL,
    remainder_closed_at datetime2(7) NULL,remainder_closed_by bigint NULL,remainder_close_reason nvarchar(max) NULL;
   ALTER TABLE dbo.material_request_lines ADD is_active bit NOT NULL
    CONSTRAINT DF_material_request_lines_active DEFAULT(1) WITH VALUES;
   ALTER TABLE dbo.material_request_approvals ADD approval_cycle_id bigint NULL;
   ALTER TABLE dbo.material_fulfillment_lines ADD stock_document_line_id bigint NULL;');

  CREATE TABLE dbo.material_request_approval_cycles(
   approval_cycle_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_mr_approval_cycles PRIMARY KEY,
   request_id bigint NOT NULL,revision_no int NOT NULL,submitted_by bigint NOT NULL,
   assigned_approver_id bigint NOT NULL,status varchar(30) NOT NULL,
   submitted_at datetime2(7) NOT NULL,resolved_at datetime2(7) NULL,
   CONSTRAINT UQ_mr_cycles_revision UNIQUE(request_id,revision_no),
   CONSTRAINT CK_mr_cycles_revision CHECK(revision_no>0),
   CONSTRAINT CK_mr_cycles_status CHECK(status IN('PENDING','APPROVED','REJECTED','NEEDS_INFORMATION','SUPERSEDED')),
   CONSTRAINT FK_mr_cycles_request FOREIGN KEY(request_id) REFERENCES dbo.material_requests(request_id),
   CONSTRAINT FK_mr_cycles_submitter FOREIGN KEY(submitted_by) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_mr_cycles_approver FOREIGN KEY(assigned_approver_id) REFERENCES dbo.users(user_id));
  CREATE TABLE dbo.material_request_approval_line_snapshots(
   snapshot_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_mr_approval_snapshots PRIMARY KEY,
   approval_cycle_id bigint NOT NULL,request_line_id bigint NOT NULL,line_no int NOT NULL,
   item_id bigint NOT NULL,uom_id bigint NOT NULL,requested_qty decimal(18,3) NOT NULL,
   approved_qty decimal(18,3) NULL,note nvarchar(max) NULL,
   CONSTRAINT UQ_mr_snapshots_line UNIQUE(approval_cycle_id,request_line_id),
   CONSTRAINT CK_mr_snapshot_quantity CHECK(line_no>0 AND requested_qty>0
     AND (approved_qty IS NULL OR approved_qty>=0 AND approved_qty<=requested_qty)),
   CONSTRAINT FK_mr_snapshots_cycle FOREIGN KEY(approval_cycle_id) REFERENCES dbo.material_request_approval_cycles(approval_cycle_id),
   CONSTRAINT FK_mr_snapshots_line FOREIGN KEY(request_line_id) REFERENCES dbo.material_request_lines(line_id),
   CONSTRAINT FK_mr_snapshots_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
   CONSTRAINT FK_mr_snapshots_uom FOREIGN KEY(uom_id) REFERENCES dbo.units_of_measure(uom_id));

  EXEC(N'ALTER TABLE dbo.material_requests WITH CHECK ADD
    CONSTRAINT FK_material_requests_receiving_room FOREIGN KEY(receiving_room_id) REFERENCES dbo.rooms(room_id),
    CONSTRAINT FK_material_requests_receiving_area FOREIGN KEY(receiving_area_id) REFERENCES dbo.areas(area_id),
    CONSTRAINT FK_material_requests_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
    CONSTRAINT FK_material_requests_remainder_closed_by FOREIGN KEY(remainder_closed_by) REFERENCES dbo.users(user_id);
   ALTER TABLE dbo.material_request_approvals WITH CHECK ADD CONSTRAINT FK_mr_approvals_cycle
    FOREIGN KEY(approval_cycle_id) REFERENCES dbo.material_request_approval_cycles(approval_cycle_id);
   ALTER TABLE dbo.material_fulfillment_lines WITH CHECK ADD CONSTRAINT FK_mr_fulfillment_issue_line
    FOREIGN KEY(stock_document_line_id) REFERENCES dbo.stock_document_lines(line_id);
   CREATE UNIQUE INDEX UX_mr_decision_cycle ON dbo.material_request_approvals(approval_cycle_id) WHERE approval_cycle_id IS NOT NULL;
   CREATE INDEX IX_mr_approval_timeline ON dbo.material_request_approvals(request_id,decided_at,approval_id);
   CREATE UNIQUE INDEX UX_mr_fulfillment_issue_line ON dbo.material_fulfillment_lines(stock_document_line_id) WHERE stock_document_line_id IS NOT NULL;
   CREATE INDEX IX_mr_fulfillment_request_line ON dbo.material_fulfillment_lines(request_line_id);');

  -- Replace atomically under schema locks; retain every existing allowed value.
  -- Legacy COMPLETED/PARTIALLY_FULFILLED are NOT evidence of FULLY_ISSUED.
  ALTER TABLE dbo.material_requests DROP CONSTRAINT CK_material_requests_status;
  ALTER TABLE dbo.material_requests WITH CHECK ADD CONSTRAINT CK_material_requests_status
   CHECK(status IN('DRAFT','SUBMITTED','RETURNED','APPROVED','PARTIALLY_FULFILLED','COMPLETED','REJECTED','CANCELLED',
     'PENDING_APPROVAL','NEEDS_INFORMATION','PARTIALLY_ISSUED','FULLY_ISSUED','REMAINDER_CLOSED'));
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaterialLifecycle.V1',@value=N'1';
 END;

 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaterialLifecycle.V1' AND CONVERT(nvarchar(20),value)=N'1')
  OR EXISTS(SELECT 1 FROM @columns e WHERE NOT EXISTS(SELECT 1 FROM sys.columns c
    WHERE c.object_id=OBJECT_ID(N'dbo.'+e.table_name) AND c.name=e.column_name
      AND c.system_type_id=e.type_id AND c.max_length=e.bytes AND c.is_nullable=e.nullable
      AND (e.type_id<>42 OR c.scale=7) AND (e.type_id<>106 OR c.precision=18 AND c.scale=3)))
  OR ISNULL(COLUMNPROPERTY(OBJECT_ID('dbo.material_request_approval_cycles'),'approval_cycle_id','IsIdentity'),0)<>1
  OR ISNULL(COLUMNPROPERTY(OBJECT_ID('dbo.material_request_approval_line_snapshots'),'snapshot_id','IsIdentity'),0)<>1
  THROW 51900,'Lifecycle column/identity schema incompatible.',1;

 DECLARE @keys TABLE(name sysname,source_table sysname,source_column sysname,target_table sysname,target_column sysname);
 INSERT @keys VALUES
 ('FK_material_requests_receiving_room','material_requests','receiving_room_id','rooms','room_id'),
 ('FK_material_requests_receiving_area','material_requests','receiving_area_id','areas','area_id'),
 ('FK_material_requests_incident','material_requests','incident_id','incidents','incident_id'),
 ('FK_material_requests_remainder_closed_by','material_requests','remainder_closed_by','users','user_id'),
 ('FK_mr_approvals_cycle','material_request_approvals','approval_cycle_id','material_request_approval_cycles','approval_cycle_id'),
 ('FK_mr_fulfillment_issue_line','material_fulfillment_lines','stock_document_line_id','stock_document_lines','line_id'),
 ('FK_mr_cycles_request','material_request_approval_cycles','request_id','material_requests','request_id'),
 ('FK_mr_cycles_submitter','material_request_approval_cycles','submitted_by','users','user_id'),
 ('FK_mr_cycles_approver','material_request_approval_cycles','assigned_approver_id','users','user_id'),
 ('FK_mr_snapshots_cycle','material_request_approval_line_snapshots','approval_cycle_id','material_request_approval_cycles','approval_cycle_id'),
 ('FK_mr_snapshots_line','material_request_approval_line_snapshots','request_line_id','material_request_lines','line_id'),
 ('FK_mr_snapshots_item','material_request_approval_line_snapshots','item_id','inventory_items','item_id'),
 ('FK_mr_snapshots_uom','material_request_approval_line_snapshots','uom_id','units_of_measure','uom_id');
 IF EXISTS(SELECT 1 FROM @keys e WHERE NOT EXISTS(SELECT 1 FROM sys.foreign_keys f
   JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
   WHERE f.name=e.name AND f.parent_object_id=OBJECT_ID(N'dbo.'+e.source_table)
    AND f.referenced_object_id=OBJECT_ID(N'dbo.'+e.target_table)
    AND COL_NAME(c.parent_object_id,c.parent_column_id)=e.source_column
    AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=e.target_column
    AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
    AND (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)=1))
  THROW 51900,'Lifecycle FK must be exact, trusted and restrictive.',1;
 DECLARE @indexes TABLE(name sysname,table_name sysname,columns nvarchar(200),filter nvarchar(200));
 INSERT @indexes VALUES
 ('UQ_mr_cycles_revision','material_request_approval_cycles','request_id,revision_no',NULL),
 ('UQ_mr_snapshots_line','material_request_approval_line_snapshots','approval_cycle_id,request_line_id',NULL),
 ('UX_mr_decision_cycle','material_request_approvals','approval_cycle_id','([approval_cycle_id] IS NOT NULL)'),
 ('UX_mr_fulfillment_issue_line','material_fulfillment_lines','stock_document_line_id','([stock_document_line_id] IS NOT NULL)');
 IF EXISTS(SELECT 1 FROM @indexes e WHERE NOT EXISTS(SELECT 1 FROM sys.indexes i
   WHERE i.object_id=OBJECT_ID(N'dbo.'+e.table_name) AND i.name=e.name AND i.is_unique=1 AND i.is_disabled=0
    AND ISNULL(i.filter_definition,N'')=ISNULL(e.filter,N'')
    AND (SELECT STRING_AGG(CONVERT(nvarchar(max),COL_NAME(c.object_id,c.column_id)),N',') WITHIN GROUP(ORDER BY c.key_ordinal)
      FROM sys.index_columns c WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal>0)=e.columns))
  OR (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_material_requests_status','CK_mr_cycles_revision','CK_mr_cycles_status','CK_mr_snapshot_quantity')
    AND is_disabled=0 AND is_not_trusted=0)<>4
  THROW 51900,'Lifecycle constraint/index capability incompatible.',1;
 IF @Apply=1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
 SELECT @Apply Applied,N'MaterialLifecycle.V1' Capability;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
