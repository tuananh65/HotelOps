-- UC060: immutable cycle boundary and prior SLA. Additive; no grants or business-row updates.
-- ApplyChanges=0 rolls back installation. After use, retain receipts during source rollback.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentReopenCycles:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51846,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.incident_reopen_cycles','U') IS NULL
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_events') AND name='UQ_incident_event_source')
  ALTER TABLE dbo.incident_events ADD CONSTRAINT UQ_incident_event_source UNIQUE(event_id,incident_id);
 IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_works') AND name='UQ_maintenance_incident_source')
  ALTER TABLE dbo.maintenance_works ADD CONSTRAINT UQ_maintenance_incident_source UNIQUE(maintenance_work_id,incident_id);
 CREATE TABLE dbo.incident_reopen_cycles(
  reopen_event_id bigint NOT NULL CONSTRAINT PK_incident_reopen_cycles PRIMARY KEY,
  incident_id bigint NOT NULL,
  previous_work_id bigint NOT NULL CONSTRAINT UQ_incident_reopen_work UNIQUE,
  closure_event_id bigint NOT NULL CONSTRAINT UQ_incident_reopen_closure UNIQUE,
  closure_audit_id bigint NOT NULL CONSTRAINT UQ_incident_reopen_closure_audit UNIQUE,
  reopen_audit_id bigint NOT NULL CONSTRAINT UQ_incident_reopen_audit UNIQUE,
  recurrence_discovered_at datetime2(7) NOT NULL,
  previous_closed_at datetime2(7) NOT NULL,
  reopened_at datetime2(7) NOT NULL,
  previous_response_deadline_at datetime2(7) NOT NULL,
  previous_resolution_deadline_at datetime2(7) NOT NULL,
  CONSTRAINT FK_incident_reopen_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
  CONSTRAINT FK_incident_reopen_event FOREIGN KEY(reopen_event_id,incident_id) REFERENCES dbo.incident_events(event_id,incident_id),
  CONSTRAINT FK_incident_reopen_closure FOREIGN KEY(closure_event_id,incident_id) REFERENCES dbo.incident_events(event_id,incident_id),
  CONSTRAINT FK_incident_reopen_work FOREIGN KEY(previous_work_id,incident_id) REFERENCES dbo.maintenance_works(maintenance_work_id,incident_id),
  CONSTRAINT FK_incident_reopen_closure_audit FOREIGN KEY(closure_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT FK_incident_reopen_audit FOREIGN KEY(reopen_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_incident_reopen_time CHECK(previous_closed_at<=recurrence_discovered_at AND recurrence_discovered_at<=reopened_at),
  CONSTRAINT CK_incident_reopen_identity CHECK(reopen_event_id<>closure_event_id AND reopen_audit_id<>closure_audit_id)
 );
 CREATE INDEX IX_incident_reopen_current ON dbo.incident_reopen_cycles(incident_id,reopen_event_id DESC);
 EXEC(N'CREATE TRIGGER dbo.TR_incident_reopen_cycles_immutable ON dbo.incident_reopen_cycles
 AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51846,''Incident cycle receipts are immutable.'',1; END;');
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_reopen_cycles') AND is_nullable=0
 AND ((name IN('reopen_event_id','incident_id','previous_work_id','closure_event_id','closure_audit_id','reopen_audit_id') AND system_type_id=127)
 OR (name IN('recurrence_discovered_at','previous_closed_at','reopened_at','previous_response_deadline_at','previous_resolution_deadline_at') AND system_type_id=42 AND scale=7)))<>11
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_reopen_cycles')
 AND name IN('FK_incident_reopen_incident','FK_incident_reopen_event','FK_incident_reopen_closure','FK_incident_reopen_work','FK_incident_reopen_closure_audit','FK_incident_reopen_audit')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>6
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_reopen_cycles')
 AND name IN('CK_incident_reopen_time','CK_incident_reopen_identity') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_reopen_cycles')
 AND name IN('PK_incident_reopen_cycles','UQ_incident_reopen_work','UQ_incident_reopen_closure','UQ_incident_reopen_closure_audit','UQ_incident_reopen_audit') AND is_unique=1 AND is_disabled=0)<>5
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.incident_reopen_cycles') AND name='TR_incident_reopen_cycles_immutable' AND is_disabled=0)
 THROW 51846,'Cycle storage mismatch; preserve existing data.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentReopenCycles.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentReopenCycles.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentReopenCycles.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51846,'Conflicting cycle capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
