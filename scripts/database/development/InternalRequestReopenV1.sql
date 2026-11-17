-- UC121: additive receipt and independent permission; no grants or physical status changes.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51962,'Installer transaction required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01' OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51962,'Confirmed development identity required.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:InternalRequestReopen:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51962,'Installation lock unavailable.',1;
IF OBJECT_ID('dbo.internal_request_closures','U') IS NULL OR ISNULL(COL_LENGTH('dbo.internal_requests','row_version'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.permissions','permission_code'),0)<>100 THROW 51962,'Prerequisite schema unavailable.',1;
IF OBJECT_ID('dbo.internal_request_reopens','U') IS NOT NULL
 AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.InternalRequestReopen.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51962,'Unversioned reopen storage exists; preserve and review.',1;
IF OBJECT_ID('dbo.internal_request_reopens','U') IS NULL
BEGIN
 CREATE TABLE dbo.internal_request_reopens(
  event_id bigint NOT NULL CONSTRAINT PK_internal_request_reopens PRIMARY KEY,
  closure_event_id bigint NOT NULL CONSTRAINT UQ_ir_reopen_closure UNIQUE,
  assignment_id bigint NOT NULL CONSTRAINT UQ_ir_reopen_assignment UNIQUE,
  audit_id bigint NOT NULL CONSTRAINT UQ_ir_reopen_audit UNIQUE,
  receiving_department_id bigint NOT NULL,
  window_days int NOT NULL CONSTRAINT CK_ir_reopen_window CHECK(window_days>0),
  CONSTRAINT FK_ir_reopen_event FOREIGN KEY(event_id) REFERENCES dbo.internal_request_events(event_id),
  CONSTRAINT FK_ir_reopen_closure FOREIGN KEY(closure_event_id) REFERENCES dbo.internal_request_closures(event_id),
  CONSTRAINT FK_ir_reopen_assignment FOREIGN KEY(assignment_id) REFERENCES dbo.internal_request_assignments(assignment_id),
  CONSTRAINT FK_ir_reopen_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT FK_ir_reopen_department FOREIGN KEY(receiving_department_id) REFERENCES dbo.departments(department_id));
 EXEC(N'CREATE TRIGGER dbo.TR_ir_reopen_immutable ON dbo.internal_request_reopens INSTEAD OF UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51962,''Reopen receipts are immutable.'',1; END');
END;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_reopens') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>5
 OR ISNULL(COL_LENGTH('dbo.internal_request_reopens','window_days'),0)<>4
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.internal_request_reopens') AND name='TR_ir_reopen_immutable' AND is_disabled=0)
 THROW 51962,'Reopen receipt schema incompatible.',1;
IF EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='IR_REOPEN'
 AND (module_code<>'INTERNAL_REQUEST' OR resource_code<>'internal_request' OR action_code<>'reopen'))
 THROW 51962,'Existing permission semantics conflict.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='IR_REOPEN')
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 VALUES('IR_REOPEN',N'Reopen internal request','INTERNAL_REQUEST','internal_request','reopen',
 N'GBR-153 requester or explicitly mapped requesting Department Head, current IR_VIEW, branch scope and reopen window. No automatic grants.');
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.InternalRequestReopen.V1' AND CONVERT(nvarchar(20),value)<>'1')
 THROW 51962,'Conflicting reopen version.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.InternalRequestReopen.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestReopen.V1',@value=1;
-- Installer rolls back failed installation. After successful use retain receipts and audit FKs;
-- reverting the app must not drop history or cascade-delete receipts.
