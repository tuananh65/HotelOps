-- Approved 2026-11-17: audit purge disabled; receipts preserved. Archive is in-place
-- classification: original audit IDs/payloads/FKs remain authoritative and unchanged.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
 AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51872,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51872,'ApplyChanges must be 0 or 1.',1;


BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:AuditPreservation:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51875,'Migration lock unavailable.',1;
-- Historical disposable reinspection writer V1 finalizes an audit with UPDATE.
-- Never activate append-only storage over that incompatible writer.
IF EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_reassign_reinspection')
 AND definition LIKE '%UPDATE dbo.audit_logs%')
 THROW 51875,'Legacy reinspection audit writer requires an append-only contract upgrade before preservation installation.',1;
IF OBJECT_ID('dbo.audit_archive_state','U') IS NULL
BEGIN
 CREATE TABLE dbo.audit_archive_state(
  audit_id bigint NOT NULL CONSTRAINT PK_audit_archive_state PRIMARY KEY,
  archived_at datetime2(7) NOT NULL,retention_until datetime2(7) NOT NULL,
  archived_by nvarchar(50) NOT NULL,reason nvarchar(500) NOT NULL,
  archive_status varchar(30) NOT NULL CONSTRAINT CK_audit_archive_status CHECK(archive_status='ARCHIVED_IN_PLACE'),
  policy_version varchar(50) NOT NULL,
  legal_hold bit NOT NULL CONSTRAINT DF_audit_archive_hold DEFAULT(0),legal_hold_until datetime2(7) NULL,
  purge_eligibility varchar(30) NOT NULL CONSTRAINT CK_audit_archive_purge CHECK(purge_eligibility='PURGE_BLOCKED'),
  purge_block_reason varchar(100) NOT NULL CONSTRAINT CK_audit_archive_purge_reason CHECK(purge_block_reason='AUDIT_LOG_PURGE_DISABLED'),
  row_version rowversion NOT NULL,
  CONSTRAINT FK_audit_archive_source FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_audit_archive_retention CHECK(retention_until>=DATEADD(day,365,archived_at)),
  CONSTRAINT CK_audit_archive_hold CHECK(legal_hold=1 OR legal_hold_until IS NULL));
 CREATE INDEX IX_audit_archive_retention ON dbo.audit_archive_state(retention_until,audit_id);
END;
IF ISNULL(COL_LENGTH('dbo.audit_archive_state','row_version'),0)<>8
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_audit_archive_source' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
 THROW 51875,'Archive schema mismatch; preserve and review.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_audit_append_only_v1 ON dbo.audit_logs AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51875,''Audit updates and purge are disabled by preservation policy.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_audit_no_credentials_v1 ON dbo.audit_logs AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @queue TABLE(id int IDENTITY PRIMARY KEY,payload nvarchar(max),depth int);
 INSERT @queue(payload,depth) SELECT old_values,0 FROM inserted WHERE ISJSON(old_values)=1
 UNION ALL SELECT new_values,0 FROM inserted WHERE ISJSON(new_values)=1;
 DECLARE @id int,@payload nvarchar(max),@depth int;
 WHILE EXISTS(SELECT 1 FROM @queue)
 BEGIN
  SELECT TOP(1) @id=id,@payload=payload,@depth=depth FROM @queue ORDER BY id;
  DELETE @queue WHERE id=@id;
  IF @depth>32 THROW 51875,''Audit payload exceeds inspected nesting depth.'',1;
  IF EXISTS(SELECT 1 FROM OPENJSON(@payload) j WHERE j.type<>0 AND j.value<>N''''
    AND LOWER(REPLACE(REPLACE(j.[key],N''_'',N''''),N''-'',N'''')) IN
    (N''password'',N''passwordhash'',N''newpassword'',N''oldpassword'',N''temporarypassword'',N''confirmpassword'',
     N''token'',N''accesstoken'',N''refreshtoken'',N''resettoken'',N''csrftoken'',N''apikey'',N''clientsecret'',N''authorization'',N''cookie''))
   THROW 51875,''Credentials must not be recorded in audit payloads.'',1;
  INSERT @queue(payload,depth) SELECT j.value,@depth+1 FROM OPENJSON(@payload) j WHERE j.type IN(4,5);
 END;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_audit_archive_immutable_v1 ON dbo.audit_archive_state AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51875,''Archive evidence is immutable; no hold release endpoint is authorized.'',1;
END;');
EXEC(N'CREATE OR ALTER PROCEDURE dbo.job06_archive_audit_v1 @batch_size int=500
WITH EXECUTE AS OWNER AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @batch_size<1 OR @batch_size>1000 THROW 51875,''Invalid archive batch size.'',1;
 BEGIN TRANSACTION;
 DECLARE @lease int,@now datetime2(7)=SYSUTCDATETIME();
 EXEC @lease=sys.sp_getapplock @Resource=N''HotelOps:AuditArchive:V1'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=0;
 IF @lease<0 BEGIN ROLLBACK; SELECT CAST(0 AS int) AS Value; RETURN; END;
 INSERT dbo.audit_archive_state(audit_id,archived_at,retention_until,archived_by,reason,archive_status,policy_version,purge_eligibility,purge_block_reason)
 SELECT TOP(@batch_size) a.audit_id,@now,DATEADD(day,365,@now),N''SYSTEM:JOB06'',N''Approved evidence retention; original payload and receipt links retained'',
 ''ARCHIVED_IN_PLACE'',''DEV3_20261117'', ''PURGE_BLOCKED'',''AUDIT_LOG_PURGE_DISABLED''
 FROM dbo.audit_logs a WITH(UPDLOCK,HOLDLOCK)
 WHERE a.occurred_at<DATEADD(day,-365,@now)
 AND NOT EXISTS(SELECT 1 FROM dbo.audit_archive_state s WITH(UPDLOCK,HOLDLOCK) WHERE s.audit_id=a.audit_id)
 ORDER BY a.occurred_at,a.audit_id;
 DECLARE @count int=@@ROWCOUNT;
 COMMIT;
 SELECT @count AS Value;
END;');
-- No EXECUTE grants or background activation; deployment identity configures the job explicitly.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.AuditPreservation.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.AuditPreservation.V1',@value=N'DEV3_20261117';
IF N'$(ApplyChanges)'='1' COMMIT; ELSE ROLLBACK;
