-- Approved additive SLA storage. No default durations, backfill, grants or public writer.
-- Preview/installation only on an identity-verified disposable database.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51845,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51845,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.incidents','U') IS NULL OR COL_LENGTH('dbo.incidents','discovered_at') IS NULL
 THROW 51845,'Incident source schema required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentSla:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51845,'Migration lock unavailable.',1;
IF COL_LENGTH('dbo.incidents','response_deadline_at') IS NULL
 ALTER TABLE dbo.incidents ADD response_deadline_at datetime2(7) NULL;
IF COL_LENGTH('dbo.incidents','resolution_deadline_at') IS NULL
 ALTER TABLE dbo.incidents ADD resolution_deadline_at datetime2(7) NULL;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
 AND name IN('response_deadline_at','resolution_deadline_at') AND system_type_id=42 AND scale=7 AND is_nullable=1)<>2
 THROW 51845,'Incident SLA columns conflict with approved storage.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incidents') AND name='CK_incidents_sla_discovery_v1')
 EXEC(N'ALTER TABLE dbo.incidents WITH CHECK ADD CONSTRAINT CK_incidents_sla_discovery_v1 CHECK
  ((response_deadline_at IS NULL OR response_deadline_at>=discovered_at)
   AND (resolution_deadline_at IS NULL OR resolution_deadline_at>=discovered_at))');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incidents')
 AND name='CK_incidents_sla_discovery_v1' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51845,'Trusted SLA validation required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentSla.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentSla.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentSla.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51845,'Conflicting SLA capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
