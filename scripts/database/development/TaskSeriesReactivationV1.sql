-- UC102: preserve immutable definitions/occurrences; permit audited explicit reactivation.
-- ApplyChanges=0 rolls back. No grants, historical timestamp backfill or occurrence updates.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
  AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
  AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51871,'Verified authorized development database required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51871,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:TaskSeries:Reactivation:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51871,'Migration lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.operational_task_series','row_version'),-1)<>8
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_task_series_definition_v1' AND is_disabled=0)
 THROW 51871,'Installed immutable task series prerequisite required.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_series_definition_v1 ON dbo.operational_task_series AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.series_id=d.series_id WHERE i.series_id IS NULL
  OR i.branch_id<>d.branch_id OR i.department_id<>d.department_id OR i.created_by<>d.created_by
  OR i.default_assignee_id<>d.default_assignee_id OR i.rule_code<>d.rule_code OR i.weekday_mask<>d.weekday_mask
  OR ISNULL(i.month_day,0)<>ISNULL(d.month_day,0) OR i.first_due_at<>d.first_due_at
  OR i.end_date<>d.end_date OR (i.end_date IS NULL AND d.end_date IS NOT NULL) OR (i.end_date IS NOT NULL AND d.end_date IS NULL)
  OR i.template_json COLLATE Latin1_General_100_BIN2<>d.template_json COLLATE Latin1_General_100_BIN2
  OR i.creation_key<>d.creation_key OR i.creation_hash<>d.creation_hash OR i.created_at<>d.created_at
  OR (d.is_active=0 AND (i.is_active<>1 OR i.next_due_at IS NULL OR i.next_due_at<=SYSUTCDATETIME()
   OR NOT EXISTS(SELECT 1 FROM dbo.audit_logs a
      CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N''{}'' END AS payload) j
      WHERE a.entity_type=''operational_task_series'' AND a.entity_id=CONVERT(nvarchar(30),i.series_id)
        AND a.branch_id=i.branch_id AND a.actor_user_id=i.created_by AND a.action=''UPDATE''
        AND a.occurred_at>=d.stopped_at AND a.occurred_at<=SYSUTCDATETIME()
        AND JSON_VALUE(j.payload,''$.operation'')=''REACTIVATE_SERIES''
        AND JSON_VALUE(j.payload,''$.previousVersion'')=CONVERT(varchar(16),d.row_version,2)
        AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,''$.reason''))))>0
        AND TRY_CONVERT(datetime2(7),JSON_VALUE(j.payload,''$.nextDueAt''))=i.next_due_at)))
  OR i.next_due_at<d.next_due_at OR (d.next_due_at IS NULL AND i.next_due_at IS NOT NULL))
  THROW 51871,''Series definition is immutable; reactivation requires a matching audit and future cursor.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.Reactivation.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskSeries.Reactivation.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
