-- Requires IncidentSla.sql first. No SLA target values or role grants are seeded.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51846,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51846,'ApplyChanges must be 0 or 1.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentSla.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51846,'Incident SLA storage must be installed first.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentTriage:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51846,'Migration lock unavailable.',1;
IF COL_LENGTH('dbo.incidents','row_version') IS NULL
BEGIN
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents') AND system_type_id=189)
  THROW 51846,'Existing Incident concurrency column requires review.',1;
 ALTER TABLE dbo.incidents ADD row_version rowversion NOT NULL;
END;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 THROW 51846,'Incident concurrency schema conflict.',1;
IF OBJECT_ID('dbo.incident_priority_sla_targets','U') IS NULL
 CREATE TABLE dbo.incident_priority_sla_targets(
  priority_id bigint NOT NULL CONSTRAINT PK_incident_priority_sla_targets PRIMARY KEY,
  response_minutes int NOT NULL,
  resolution_minutes int NOT NULL,
  is_active bit NOT NULL CONSTRAINT DF_incident_sla_active DEFAULT(0),
  row_version rowversion NOT NULL,
  CONSTRAINT FK_incident_sla_priority FOREIGN KEY(priority_id) REFERENCES dbo.reference_values(reference_id),
  CONSTRAINT CK_incident_sla_minutes CHECK(response_minutes>0 AND resolution_minutes>0));
IF ISNULL(COL_LENGTH('dbo.incident_priority_sla_targets','response_minutes'),-1)<>4
 OR ISNULL(COL_LENGTH('dbo.incident_priority_sla_targets','resolution_minutes'),-1)<>4
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets')
  AND name IN('response_minutes','resolution_minutes') AND system_type_id=56 AND is_nullable=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets') AND name='priority_id' AND system_type_id=127 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets') AND name='is_active' AND system_type_id=104 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
  WHERE f.parent_object_id=OBJECT_ID('dbo.incident_priority_sla_targets') AND f.referenced_object_id=OBJECT_ID('dbo.reference_values')
  AND f.name='FK_incident_sla_priority' AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0
  AND COL_NAME(c.parent_object_id,c.parent_column_id)='priority_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='reference_id')
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_priority_sla_targets')
  AND name='CK_incident_sla_minutes' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51846,'SLA target schema conflict.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentTriage.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentTriage.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentTriage.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51846,'Conflicting Triage capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
