-- Additive classification flag. NULL preserves unknown historical impact; no grants or data backfill.
-- ApplyChanges=0 rolls back installation. Source rollback retains this column and its history.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentClassification:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51846,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.incidents','U') IS NULL THROW 51846,'Incident schema unavailable.',1;
IF COL_LENGTH('dbo.incidents','affects_room_readiness') IS NULL
 ALTER TABLE dbo.incidents ADD affects_room_readiness bit NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
 AND name='affects_room_readiness' AND system_type_id=104 AND is_nullable=1)
 THROW 51846,'Classification schema mismatch; preserve existing data.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentClassification.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentClassification.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentClassification.V1'
 AND CONVERT(nvarchar(10),value)='1') THROW 51846,'Classification version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;