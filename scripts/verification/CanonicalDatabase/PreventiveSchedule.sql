-- UC061 additive metadata. Historical rows retain unknown title/checklist; no inferred backfill or grants.
-- ApplyChanges=0 rolls back installation. After use, retain metadata/history during source rollback.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveSchedule:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51846,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.preventive_maintenance_schedules','U') IS NULL THROW 51846,'Schedule schema required.',1;
IF COL_LENGTH('dbo.preventive_maintenance_schedules','title') IS NULL
 ALTER TABLE dbo.preventive_maintenance_schedules ADD title nvarchar(200) NULL;
IF COL_LENGTH('dbo.preventive_maintenance_schedules','check_items_json') IS NULL
 ALTER TABLE dbo.preventive_maintenance_schedules ADD check_items_json nvarchar(max) NULL;
IF COL_LENGTH('dbo.preventive_maintenance_schedules','default_assignee_id') IS NULL
 ALTER TABLE dbo.preventive_maintenance_schedules ADD default_assignee_id bigint NULL;
IF COL_LENGTH('dbo.preventive_maintenance_schedules','row_version') IS NULL
 ALTER TABLE dbo.preventive_maintenance_schedules ADD row_version rowversion NOT NULL;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules')
 AND ((name='title' AND system_type_id=231 AND max_length=400 AND is_nullable=1)
 OR (name='check_items_json' AND system_type_id=231 AND max_length=-1 AND is_nullable=1)
 OR (name='default_assignee_id' AND system_type_id=127 AND is_nullable=1)
 OR (name='row_version' AND system_type_id=189 AND is_nullable=0)))<>4
 THROW 51846,'Schedule metadata mismatch; preserve existing data.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_preventive_default_assignee'
 AND parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules'))
 EXEC(N'ALTER TABLE dbo.preventive_maintenance_schedules WITH CHECK ADD CONSTRAINT FK_preventive_default_assignee
 FOREIGN KEY(default_assignee_id) REFERENCES dbo.users(user_id)');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_preventive_definition'
 AND parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules'))
 EXEC(N'ALTER TABLE dbo.preventive_maintenance_schedules WITH CHECK ADD CONSTRAINT CK_preventive_definition CHECK(
 title IS NULL OR (LEN(LTRIM(RTRIM(title)))>0 AND instruction IS NOT NULL AND LEN(LTRIM(RTRIM(instruction)))>0
 AND check_items_json IS NOT NULL AND ISJSON(check_items_json)=1
 AND frequency IN(''DAYS'',''WEEKS'',''MONTHS'') AND frequency_interval>0
 AND (CASE WHEN asset_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN room_id IS NOT NULL THEN 1 ELSE 0 END
 + CASE WHEN area_id IS NOT NULL THEN 1 ELSE 0 END)=1))');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules') AND name='UX_pm_asset_title')
 EXEC(N'CREATE UNIQUE INDEX UX_pm_asset_title ON dbo.preventive_maintenance_schedules(asset_id,title)
 WHERE is_active=1 AND title IS NOT NULL AND asset_id IS NOT NULL');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules') AND name='UX_pm_room_title')
 EXEC(N'CREATE UNIQUE INDEX UX_pm_room_title ON dbo.preventive_maintenance_schedules(room_id,title)
 WHERE is_active=1 AND title IS NOT NULL AND room_id IS NOT NULL');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules') AND name='UX_pm_area_title')
 EXEC(N'CREATE UNIQUE INDEX UX_pm_area_title ON dbo.preventive_maintenance_schedules(area_id,title)
 WHERE is_active=1 AND title IS NOT NULL AND area_id IS NOT NULL');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_preventive_default_assignee'
 AND parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules') AND is_disabled=0 AND is_not_trusted=0
 AND delete_referential_action=0 AND update_referential_action=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_preventive_definition'
 AND parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules') AND is_disabled=0 AND is_not_trusted=0)
 THROW 51846,'Schedule constraints unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveSchedule.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.PreventiveSchedule.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveSchedule.V1'
 AND CONVERT(nvarchar(10),value)='1') THROW 51846,'Schedule version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;