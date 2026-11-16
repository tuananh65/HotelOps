-- UC139 approved: workflow ON_HOLD, physical IN_PROGRESS. Disposable preview defaults to rollback.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51842,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51842,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.work_intervals','U') IS NULL OR ISNULL(COL_LENGTH('dbo.cleaning_reworks','row_version'),-1)<>8
 OR COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NULL
 THROW 51842,'Rework lifecycle prerequisites missing.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:ReworkEndOfShift:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51842,'Migration lock unavailable.',1;
DECLARE @original nvarchar(max)=(SELECT definition FROM sys.check_constraints
 WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0);
IF @original IS NULL THROW 51842,'Trusted existing workflow constraint required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReworkEndOfShift.V1')
BEGIN
 IF @original LIKE '%ON_HOLD%' THROW 51842,'Unversioned ON_HOLD contract requires review.',1;
 -- Preserve every existing transition (including independently installed ACCEPTED/FAILED).
 -- Only the explicitly approved workflow/physical pair is added.
 DECLARE @saved nvarchar(3500)=CONVERT(nvarchar(3500),@original);
 IF DATALENGTH(@original)>7000 THROW 51842,'Constraint too long to preserve rollback definition.',1;
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReworkEndOfShift.PreviousCheck',@value=@saved;
 DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1;
 ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK ('+@original+
 N' OR (workflow_status=''ON_HOLD'' AND status=''IN_PROGRESS''));';
 EXEC sys.sp_executesql @ddl;
END;
IF OBJECT_ID('dbo.rework_end_of_shift_events','U') IS NULL
 CREATE TABLE dbo.rework_end_of_shift_events(
 interval_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.work_intervals(interval_id),
 rework_id bigint NOT NULL REFERENCES dbo.cleaning_reworks(rework_id),
 hold_revision binary(8) NOT NULL,
 reason_code varchar(20) NOT NULL CHECK(reason_code='END_OF_SHIFT'),
 paused_at datetime2(7) NOT NULL);
IF ISNULL(COL_LENGTH('dbo.rework_end_of_shift_events','hold_revision'),-1)<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.rework_end_of_shift_events') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks')
   AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%ON_HOLD%')
 THROW 51842,'Rework pause schema mismatch.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_rework_end_of_shift_immutable ON dbo.rework_end_of_shift_events
 AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51842,''Rework pause receipts are immutable.'',1; END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.ReworkEndOfShift.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReworkEndOfShift.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
