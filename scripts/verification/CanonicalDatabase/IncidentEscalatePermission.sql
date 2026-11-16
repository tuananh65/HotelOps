-- UC057 independent permission catalog only. Never creates role grants.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%' OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000') THROW 51849,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51849,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentEscalatePermission:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51882,'Catalog lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='INCIDENT_ESCALATE')
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 VALUES('INCIDENT_ESCALATE',N'Escalate incident','INCIDENT','incident','escalate',N'UC057 Maintenance Lead creates escalation; independent INCIDENT_VIEW and branch scope required. No automatic grants.');
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WHERE permission_code='INCIDENT_ESCALATE' AND module_code='INCIDENT' AND resource_code='incident' AND action_code='escalate')
 THROW 51882,'Conflicting INCIDENT_ESCALATE catalog contract.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentEscalatePermission.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentEscalatePermission.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentEscalatePermission.V1' AND CONVERT(nvarchar(20),value)='1')
 THROW 51882,'Conflicting catalog capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;