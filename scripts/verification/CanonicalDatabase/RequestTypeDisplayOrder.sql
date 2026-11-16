-- Additive catalog display order. Existing types retain name/ID tie-breaking at order zero.
-- ApplyChanges=0 rolls back installation. Source rollback retains this additive column.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RequestTypeDisplayOrder:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51846,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.internal_request_types','U') IS NULL THROW 51846,'Request type schema unavailable.',1;
IF COL_LENGTH('dbo.internal_request_types','sort_order') IS NULL
 ALTER TABLE dbo.internal_request_types ADD sort_order int NOT NULL CONSTRAINT DF_internal_request_types_sort_order DEFAULT(0) WITH VALUES;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_request_types')
 AND name='sort_order' AND system_type_id=56 AND is_nullable=0)
 THROW 51846,'Display order schema mismatch; preserve existing data.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RequestTypeDisplayOrder.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.RequestTypeDisplayOrder.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RequestTypeDisplayOrder.V1'
 AND CONVERT(nvarchar(10),value)='1') THROW 51846,'Display order version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;