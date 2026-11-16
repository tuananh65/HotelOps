-- UC057/059 additive storage. No role/grant seeds. Disposable only; preview rolls back.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51845,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51845,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentEscalation:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51845,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.incident_escalations','U') IS NULL
BEGIN
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_events') AND name='UQ_incident_event_source')
ALTER TABLE dbo.incident_events ADD CONSTRAINT UQ_incident_event_source UNIQUE(event_id,incident_id);
CREATE TABLE dbo.incident_escalations(
 escalation_id bigint IDENTITY NOT NULL CONSTRAINT PK_incident_escalations PRIMARY KEY,
 incident_id bigint NOT NULL,
 opened_event_id bigint NOT NULL,
 state varchar(10) NOT NULL,
 opened_at datetime2(7) NOT NULL,
 reason nvarchar(4000) NOT NULL,
 requested_support nvarchar(4000) NOT NULL,
 closed_at datetime2(7) NULL,
 closed_event_id bigint NULL,
 CONSTRAINT FK_incident_escalation_source FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
 CONSTRAINT FK_incident_escalation_open FOREIGN KEY(opened_event_id,incident_id) REFERENCES dbo.incident_events(event_id,incident_id),
 CONSTRAINT FK_incident_escalation_close FOREIGN KEY(closed_event_id,incident_id) REFERENCES dbo.incident_events(event_id,incident_id),
 CONSTRAINT CK_incident_escalation_reason CHECK(LEN(LTRIM(RTRIM(reason)))>0),
 CONSTRAINT CK_incident_escalation_support CHECK(LEN(LTRIM(RTRIM(requested_support)))>0),
 CONSTRAINT CK_incident_escalation_state CHECK(
  (state='OPEN' AND closed_at IS NULL AND closed_event_id IS NULL) OR
  (state='CLOSED' AND closed_at IS NOT NULL AND closed_at>=opened_at AND closed_event_id IS NOT NULL AND closed_event_id<>opened_event_id))
);
CREATE UNIQUE INDEX UX_incident_escalation_open ON dbo.incident_escalations(incident_id) WHERE state='OPEN';
CREATE UNIQUE INDEX UX_incident_escalation_event ON dbo.incident_escalations(opened_event_id);
END;
IF COL_LENGTH('dbo.incident_escalations','reason')<>8000 OR COL_LENGTH('dbo.incident_escalations','requested_support')<>8000
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_escalations')
  AND name IN('FK_incident_escalation_source','FK_incident_escalation_open','FK_incident_escalation_close') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_escalations')
  AND name IN('CK_incident_escalation_reason','CK_incident_escalation_support','CK_incident_escalation_state') AND is_disabled=0 AND is_not_trusted=0)<>3
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_escalations') AND name='UX_incident_escalation_open' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''OPEN'')')
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_escalations') AND name='UX_incident_escalation_event' AND is_unique=1 AND is_disabled=0)
 THROW 51845,'Escalation schema mismatch; preserve existing source.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentEscalation.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentEscalation.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentEscalation.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51845,'Conflicting escalation capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
