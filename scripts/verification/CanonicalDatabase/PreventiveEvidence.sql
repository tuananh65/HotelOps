-- Additive source value; ApplyChanges=0 rolls back installation. Retain the value after attachments exist.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveEvidence:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51851,'Evidence schema lock unavailable.',1;
DECLARE @definition nvarchar(max);
SELECT @definition=definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
 AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0;
IF @definition IS NULL THROW 51851,'Trusted attachment source constraint required.',1;
IF @definition NOT LIKE '%''OCCURRENCE''%'
BEGIN
 -- Preserve the entire existing source predicate; only add the new distinct occurrence source.
 ALTER TABLE dbo.attachments DROP CONSTRAINT CK_attachments_entity_type;
 EXEC(N'ALTER TABLE dbo.attachments WITH CHECK ADD CONSTRAINT CK_attachments_entity_type CHECK(('+@definition+N') OR entity_type=''OCCURRENCE'')');
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveEvidence.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.PreventiveEvidence.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveEvidence.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51851,'Evidence version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;