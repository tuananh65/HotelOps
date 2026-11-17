-- UC082/090: independent request/approval catalog and immutable approval evidence.
-- Additive only; no historical approval inference and no automatic role/user grants.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF @@TRANCOUNT<>0 THROW 51990,'Standalone installer required.',1;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51990,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51990,'ApplyChanges must be 0 or 1.',1;
IF ISNULL(COL_LENGTH('dbo.stock_documents','row_version'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.stock_documents','approved_by'),0)<>8
 OR OBJECT_ID('dbo.stock_return_line_details','U') IS NULL
 THROW 51990,'Existing stock document and return lineage schema required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:StockReturnApproval:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51990,'Installation lock unavailable.',1;
 DECLARE @catalog table(code varchar(100),name nvarchar(150),action varchar(40));
 INSERT @catalog VALUES
 ('STOCK_RETURN_REQUEST',N'Request return of received materials','request'),
 ('STOCK_RETURN_APPROVE',N'Independently approve stock return','approve');
 IF EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) JOIN @catalog c ON p.permission_code=c.code
   WHERE p.module_code<>'INVENTORY' OR p.resource_code<>'stock_return' OR p.action_code<>c.action)
  THROW 51990,'Conflicting return permission semantics; preserve existing catalog.',1;
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 SELECT c.code,c.name,'INVENTORY','stock_return',c.action,
   N'UC082/090: current session and branch/data scope; requester cannot approve own request. Approval never posts stock or grants inventory access.'
 FROM @catalog c WHERE NOT EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) WHERE p.permission_code=c.code);
 IF EXISTS(SELECT c.code FROM @catalog c LEFT JOIN dbo.permissions p ON p.permission_code=c.code GROUP BY c.code HAVING COUNT(p.permission_id)<>1)
  THROW 51990,'Unique permission codes required.',1;
 IF OBJECT_ID('dbo.stock_return_approvals','U') IS NULL
 BEGIN
  CREATE TABLE dbo.stock_return_approvals(
   document_id bigint NOT NULL CONSTRAINT PK_stock_return_approvals PRIMARY KEY,
   approved_by bigint NOT NULL,approved_at datetime2(7) NOT NULL,audit_id bigint NOT NULL,
   approved_document_version binary(8) NOT NULL,snapshot_json nvarchar(max) NOT NULL,
   CONSTRAINT FK_stock_return_approval_document FOREIGN KEY(document_id) REFERENCES dbo.stock_documents(document_id),
   CONSTRAINT FK_stock_return_approval_user FOREIGN KEY(approved_by) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_stock_return_approval_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
   CONSTRAINT CK_stock_return_approval_json CHECK(ISJSON(snapshot_json)=1),
   CONSTRAINT UQ_stock_return_approval_audit UNIQUE(audit_id));
 END;
 IF ISNULL(COL_LENGTH('dbo.stock_return_approvals','document_id'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.stock_return_approvals','approved_document_version'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.stock_return_approvals','snapshot_json'),0)<>-1
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.stock_return_approvals')
      AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
  THROW 51990,'Incompatible approval schema or integrity constraints.',1;
 EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_stock_return_approvals_immutable ON dbo.stock_return_approvals AFTER UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51990,''Stock return approval evidence is immutable.'',1; END;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.StockReturnApproval.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.StockReturnApproval.V1',@value=N'1';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.StockReturnApproval.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51990,'Conflicting approval capability version.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- ApplyChanges=0 rolls back the installer. After use, preserve receipts/audit;
-- application rollback must disable posting, never delete approval evidence.
