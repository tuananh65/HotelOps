-- Approved UC051/057 catalog only. No role_permissions or user_roles writes.
-- Run with sqlcmd -b -v ApplyChanges=1 on the approved development identity.
SET XACT_ABORT ON;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51984,'Explicit ApplyChanges required.',1;
IF @@TRANCOUNT<>0 THROW 51984,'Standalone installation required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
  AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51984,'Approved development identity required.',1;
IF ISNULL(COL_LENGTH('dbo.permissions','permission_code'),0)<>100
 OR ISNULL(COL_LENGTH('dbo.permissions','action_code'),0)<>40
 OR ISNULL(COLUMNPROPERTY(OBJECT_ID('dbo.permissions'),'permission_id','IsIdentity'),0)<>1
 THROW 51984,'Permission baseline incompatible.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentEvidencePermission:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51984,'Evidence catalog lock unavailable.',1;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentEscalatePermission:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51984,'Escalation catalog lock unavailable.',1;
 DECLARE @catalog table(code varchar(50) PRIMARY KEY,label nvarchar(150),action varchar(20));
 INSERT @catalog VALUES
 ('INCIDENT_EVIDENCE_ADD',N'Add incident evidence','add_evidence'),
 ('INCIDENT_ESCALATE',N'Escalate incident','escalate');
 IF EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) JOIN @catalog c ON c.code=p.permission_code
  WHERE p.module_code<>'INCIDENT' OR p.resource_code<>'incident' OR p.action_code<>c.action)
  THROW 51984,'Conflicting permission semantics; preserve and review.',1;
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 SELECT c.code,c.label,'INCIDENT','incident',c.action,
  N'Independent permission; current allowed actor, INCIDENT_VIEW and branch/data scope required. No automatic grants.'
 FROM @catalog c WHERE NOT EXISTS(SELECT 1 FROM dbo.permissions p WITH(UPDLOCK,HOLDLOCK) WHERE p.permission_code=c.code);
 IF EXISTS(SELECT c.code FROM @catalog c LEFT JOIN dbo.permissions p ON p.permission_code=c.code
  GROUP BY c.code HAVING COUNT(p.permission_id)<>1)
  THROW 51984,'Permission uniqueness required.',1;
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
  AND name IN('HotelOps.IncidentEvidencePermission.V1','HotelOps.IncidentEscalatePermission.V1')
  AND CONVERT(nvarchar(10),value)<>'1') THROW 51984,'Conflicting catalog capability.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentEvidencePermission.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentEvidencePermission.V1',@value=N'1';
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentEscalatePermission.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentEscalatePermission.V1',@value=N'1';
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
