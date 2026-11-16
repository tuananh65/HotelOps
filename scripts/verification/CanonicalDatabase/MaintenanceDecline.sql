-- UC054 immutable Decline receipt. Disposable only.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51847,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51847,'ApplyChanges must be 0 or 1.',1;

-- No grants or data seeds. Receipt FK intentionally prevents audit purge until archive design supports it.
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceDecline:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51847,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.maintenance_decline_events','U') IS NULL
BEGIN
 CREATE TABLE dbo.maintenance_decline_events(
  audit_id bigint NOT NULL CONSTRAINT PK_maintenance_decline PRIMARY KEY,
  maintenance_work_id bigint NOT NULL,
  incident_id bigint NOT NULL,
  previous_assignee_id bigint NOT NULL,
  actor_id bigint NOT NULL,
  assignment_version binary(8) NOT NULL,
  reason nvarchar(2000) NOT NULL,
  occurred_at datetime2(7) NOT NULL,
  CONSTRAINT UQ_maintenance_decline_assignment UNIQUE(maintenance_work_id,assignment_version),
  CONSTRAINT FK_maintenance_decline_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT FK_maintenance_decline_work FOREIGN KEY(maintenance_work_id) REFERENCES dbo.maintenance_works(maintenance_work_id),
  CONSTRAINT FK_maintenance_decline_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
  CONSTRAINT FK_maintenance_decline_actor FOREIGN KEY(actor_id) REFERENCES dbo.users(user_id),
  CONSTRAINT CK_maintenance_decline_actor CHECK(actor_id=previous_assignee_id),
  CONSTRAINT CK_maintenance_decline_reason CHECK(LEN(LTRIM(RTRIM(reason)))>=10));
 EXEC(N'CREATE TRIGGER dbo.TR_maintenance_decline_immutable ON dbo.maintenance_decline_events AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51847,''Decline receipts are immutable; archive policy required for retention.'',1; END');
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_decline_events') AND is_nullable=0 AND
 ((name IN('audit_id','maintenance_work_id','incident_id','previous_assignee_id','actor_id') AND system_type_id=127)
 OR (name='assignment_version' AND system_type_id=173 AND max_length=8)
 OR (name='reason' AND system_type_id=231 AND max_length=4000)
 OR (name='occurred_at' AND system_type_id=42 AND scale=7)))<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
 WHERE f.parent_object_id=OBJECT_ID('dbo.maintenance_decline_events') AND f.is_disabled=0 AND f.is_not_trusted=0
 AND f.delete_referential_action=0 AND f.update_referential_action=0 AND
 ((COL_NAME(c.parent_object_id,c.parent_column_id)='audit_id' AND c.referenced_object_id=OBJECT_ID('dbo.audit_logs') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='audit_id')
 OR (COL_NAME(c.parent_object_id,c.parent_column_id)='maintenance_work_id' AND c.referenced_object_id=OBJECT_ID('dbo.maintenance_works') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='maintenance_work_id')
 OR (COL_NAME(c.parent_object_id,c.parent_column_id)='incident_id' AND c.referenced_object_id=OBJECT_ID('dbo.incidents') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='incident_id')
 OR (COL_NAME(c.parent_object_id,c.parent_column_id)='actor_id' AND c.referenced_object_id=OBJECT_ID('dbo.users') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='user_id')))<>4
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_decline_events') AND is_disabled=0 AND is_not_trusted=0
 AND name IN('CK_maintenance_decline_actor','CK_maintenance_decline_reason'))<>2
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_decline_events') AND name='UQ_maintenance_decline_assignment' AND is_unique=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_decline_immutable') AND is_disabled=0)
 THROW 51847,'Decline storage mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceDecline.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaintenanceDecline.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceDecline.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51847,'Conflicting decline capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
