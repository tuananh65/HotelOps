-- UC139: extend the existing occurrence state; retain run, assignment and interval history.
-- Caller must own the Serializable transaction and verify the development identity.
-- Roll back the transaction on failure. Once used, retain hold receipts on source rollback;
-- do not drop history or coerce held occurrences back into running work.
SET XACT_ABORT ON;
IF @@TRANCOUNT=0 THROW 51970,'Owning migration transaction required.',1;
IF DB_NAME()<>N'HotelOpsDB' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51970,'Confirmed development database identity required.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveHold:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51970,'Migration lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveOccurrence.V1'
 AND CONVERT(nvarchar(10),value)='1') THROW 51970,'Occurrence foundation required.',1;
IF OBJECT_ID('dbo.preventive_occurrence_holds','U') IS NULL
BEGIN
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveHold.V1')
  THROW 51970,'Incomplete hold installation; preserve existing data.',1;
 IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_details')
  AND is_disabled=0 AND is_not_trusted=0 AND
  ((name='CK_pm_occurrence_state' AND definition=N'([workflow_status]=''CANCELLED'' OR [workflow_status]=''COMPLETED'' OR [workflow_status]=''IN_PROGRESS'' OR [workflow_status]=''ASSIGNED'' OR [workflow_status]=''PENDING_ASSIGNMENT'')')
  OR (name='CK_pm_occurrence_assignee' AND definition=N'([workflow_status]=''PENDING_ASSIGNMENT'' AND [assignee_id] IS NULL OR ([workflow_status]=''COMPLETED'' OR [workflow_status]=''IN_PROGRESS'' OR [workflow_status]=''ASSIGNED'') AND [assignee_id] IS NOT NULL OR [workflow_status]=''CANCELLED'')')))<>2
  THROW 51970,'Unexpected occurrence constraints; do not overwrite.',1;
 ALTER TABLE dbo.preventive_occurrence_details DROP CONSTRAINT CK_pm_occurrence_state,CK_pm_occurrence_assignee;
 ALTER TABLE dbo.preventive_occurrence_details WITH CHECK ADD
  CONSTRAINT CK_pm_occurrence_state CHECK(workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','IN_PROGRESS','ON_HOLD','COMPLETED','CANCELLED')),
  CONSTRAINT CK_pm_occurrence_assignee CHECK((workflow_status='PENDING_ASSIGNMENT' AND assignee_id IS NULL)
   OR (workflow_status IN('ASSIGNED','IN_PROGRESS','ON_HOLD','COMPLETED') AND assignee_id IS NOT NULL) OR workflow_status='CANCELLED');
 CREATE TABLE dbo.preventive_occurrence_holds(
  interval_id bigint NOT NULL CONSTRAINT PK_pm_occurrence_holds PRIMARY KEY,
  run_id bigint NOT NULL,
  actor_id bigint NOT NULL,
  held_at datetime2(7) NOT NULL,
  audit_id bigint NOT NULL CONSTRAINT UQ_pm_hold_audit UNIQUE,
  reason_code varchar(30) NOT NULL CONSTRAINT CK_pm_hold_reason CHECK(reason_code='END_OF_SHIFT'),
  CONSTRAINT FK_pm_hold_interval FOREIGN KEY(interval_id) REFERENCES dbo.work_intervals(interval_id),
  CONSTRAINT FK_pm_hold_run FOREIGN KEY(run_id) REFERENCES dbo.preventive_maintenance_runs(run_id),
  CONSTRAINT FK_pm_hold_actor FOREIGN KEY(actor_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_pm_hold_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id)
 );
 CREATE INDEX IX_pm_hold_run ON dbo.preventive_occurrence_holds(run_id,interval_id);
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_occurrence_holds')
 AND name IN('interval_id','run_id','actor_id','held_at','audit_id','reason_code'))<>6
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_holds')
 AND name IN('FK_pm_hold_interval','FK_pm_hold_run','FK_pm_hold_actor','FK_pm_hold_audit')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>4
 THROW 51970,'Hold schema incompatible.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_hold_immutable ON dbo.preventive_occurrence_holds AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51970,''Hold receipts are immutable.'',1;
 IF EXISTS(SELECT 1 FROM inserted h
 LEFT JOIN dbo.work_intervals i ON i.interval_id=h.interval_id
 LEFT JOIN dbo.preventive_occurrence_details d ON d.run_id=h.run_id
 LEFT JOIN dbo.audit_logs a ON a.audit_id=h.audit_id
 WHERE i.interval_id IS NULL OR d.run_id IS NULL OR a.audit_id IS NULL
 OR i.work_item_kind<>''OCCURRENCE'' OR i.work_item_id<>h.run_id OR i.user_id<>h.actor_id
 OR i.branch_id<>d.branch_id OR i.closed_at IS NULL OR i.closed_at<>h.held_at OR i.closed_at<i.opened_at
 OR d.workflow_status<>''ON_HOLD'' OR d.assignee_id IS NULL OR d.assignee_id<>h.actor_id
 OR a.actor_user_id<>h.actor_id OR a.branch_id IS NULL OR a.branch_id<>d.branch_id
 OR a.entity_type<>''preventive_maintenance_runs'' OR a.entity_id<>CONVERT(nvarchar(30),h.run_id)
 OR a.occurred_at<>h.held_at)
 THROW 51970,''Hold receipt provenance mismatch.'',1;
END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveHold.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.PreventiveHold.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveHold.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51970,'Unexpected hold version.',1;
