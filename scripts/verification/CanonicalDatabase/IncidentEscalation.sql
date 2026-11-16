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
IF OBJECT_ID('dbo.incident_escalations') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentEscalation.V1')
 THROW 51845,'Existing escalation storage requires metadata review; no overwrite.',1;
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
EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentEscalation.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
