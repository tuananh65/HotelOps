-- UC139: additive immutable reason receipt. Existing Cleaning ON_HOLD remains authoritative.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51841,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51841,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.work_intervals','U') IS NULL OR COL_LENGTH('dbo.cleaning_tasks','row_version')<>8
 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL
 THROW 51841,'Cleaning lifecycle prerequisites missing.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:CleaningEndOfShift:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51841,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.cleaning_end_of_shift_events','U') IS NULL
 CREATE TABLE dbo.cleaning_end_of_shift_events(
 interval_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.work_intervals(interval_id),
 cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 hold_revision binary(8) NOT NULL,
 reason_code varchar(20) NOT NULL CHECK(reason_code='END_OF_SHIFT'),
 paused_at datetime2(7) NOT NULL);
IF ISNULL(COL_LENGTH('dbo.cleaning_end_of_shift_events','hold_revision'),-1)<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.cleaning_end_of_shift_events') AND is_disabled=0 AND is_not_trusted=0)<>2
 THROW 51841,'Pause receipt schema mismatch.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_cleaning_end_of_shift_immutable ON dbo.cleaning_end_of_shift_events
 AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51841,''Cleaning pause receipts are immutable.'',1; END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.CleaningEndOfShift.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningEndOfShift.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
