-- Additive completion receipts; ApplyChanges=0 rolls back installation. Preserve receipts on source rollback.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51846,'Verified development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51846,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveCompletion:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51849,'Completion schema lock unavailable.',1;
IF OBJECT_ID('dbo.preventive_occurrence_details','U') IS NULL THROW 51849,'Occurrence schema required.',1;
IF OBJECT_ID('dbo.preventive_occurrence_completions','U') IS NULL
CREATE TABLE dbo.preventive_occurrence_completions(
 run_id bigint NOT NULL CONSTRAINT PK_pm_completions PRIMARY KEY REFERENCES dbo.preventive_occurrence_details(run_id),
 interval_id bigint NOT NULL CONSTRAINT UQ_pm_completion_interval UNIQUE REFERENCES dbo.work_intervals(interval_id),
 audit_id bigint NOT NULL CONSTRAINT UQ_pm_completion_audit UNIQUE REFERENCES dbo.audit_logs(audit_id),
 actor_id bigint NOT NULL REFERENCES dbo.users(user_id),
 completed_at datetime2(7) NOT NULL,
 results_json nvarchar(max) NOT NULL CHECK(ISJSON(results_json)=1),
 note nvarchar(4000) NULL,
 next_due_at datetime2(7) NOT NULL
);
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_occurrence_completions')
 AND name IN('run_id','interval_id','audit_id','actor_id','completed_at','results_json','note','next_due_at'))<>8
 THROW 51849,'Completion schema mismatch.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_completions')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>4
 THROW 51849,'Completion integrity constraints required.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_completion_immutable ON dbo.preventive_occurrence_completions AFTER UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51849,''Completion receipts are immutable.'',1; END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveCompletion.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.PreventiveCompletion.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveCompletion.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51849,'Completion version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;