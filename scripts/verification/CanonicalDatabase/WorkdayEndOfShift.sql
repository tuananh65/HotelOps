-- UC139, version 1. SQLCMD variables must come from a verified disposable identity receipt.
-- Preview: ApplyChanges=0 rolls the entire migration back. No production grants or data updates.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME() <> N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
     AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
     AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51839,'Verified disposable database identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51839,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.task_progress_versions','U') IS NULL
 OR OBJECT_ID('dbo.operational_task_progress','U') IS NULL
 OR OBJECT_ID('dbo.work_intervals','U') IS NULL
 OR ISNULL(COL_LENGTH('dbo.reference_values','value_code'),-1)<>60
 OR (OBJECT_ID('dbo.task_pause_events','U') IS NOT NULL AND
 (ISNULL(COL_LENGTH('dbo.task_pause_events','reason_code'),-1)<>60
 OR ISNULL(COL_LENGTH('dbo.task_pause_events','note'),-1)<>2000
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints
     WHERE parent_object_id=OBJECT_ID('dbo.task_pause_events') AND name='CK_task_pause_code'
         AND is_disabled=0 AND is_not_trusted=0)))
 THROW 51839,'Trusted task pause schema required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:WorkdayEndOfShift:v1',
 @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51839,'Migration lock unavailable.',1;
-- Same approved UC110 receipt shape when recovering a database without the optional store.
IF OBJECT_ID('dbo.task_pause_events','U') IS NULL
 CREATE TABLE dbo.task_pause_events(
 progress_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.operational_task_progress(progress_id),
 reason_id bigint NOT NULL REFERENCES dbo.reference_values(reference_id),
 reason_code varchar(60) NOT NULL,
 reason_label nvarchar(150) NOT NULL,
 note nvarchar(1000) NULL,
 interval_id bigint NOT NULL UNIQUE REFERENCES dbo.work_intervals(interval_id),
 CONSTRAINT CK_task_pause_code CHECK(reason_code IN('WAITING_MATERIAL','WAITING_EQUIPMENT',
     'WAITING_APPROVAL','ACCESS_BLOCKED','SAFETY_ISSUE','OTHER','END_OF_SHIFT')),
 CONSTRAINT CK_task_pause_note CHECK(reason_code<>'OTHER' OR (note IS NOT NULL AND LEN(LTRIM(RTRIM(note)))>0)));
IF EXISTS(SELECT 1 FROM dbo.reference_values WITH(UPDLOCK,HOLDLOCK)
 WHERE group_code='TASK_PAUSE_REASON' AND value_code='END_OF_SHIFT' AND display_name<>N'End of Shift')
 THROW 51839,'Existing reference meaning differs; do not overwrite history.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WITH(UPDLOCK,HOLDLOCK)
 WHERE group_code='TASK_PAUSE_REASON' AND value_code='END_OF_SHIFT')
 INSERT dbo.reference_values(group_code,value_code,display_name,sort_order,is_active)
 VALUES('TASK_PAUSE_REASON','END_OF_SHIFT',N'End of Shift',7,1);
-- Preserve any previous deactivation. Extend only the dedicated event check; never disable FK/trigger.
ALTER TABLE dbo.task_pause_events DROP CONSTRAINT CK_task_pause_code;
ALTER TABLE dbo.task_pause_events WITH CHECK ADD CONSTRAINT CK_task_pause_code
 CHECK(reason_code IN('WAITING_MATERIAL','WAITING_EQUIPMENT','WAITING_APPROVAL',
     'ACCESS_BLOCKED','SAFETY_ISSUE','OTHER','END_OF_SHIFT'));
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkdayEndOfShift.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.WorkdayEndOfShift.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
-- After business use, do not remove the reference or narrow the check: receipts retain its meaning.
