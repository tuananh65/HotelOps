-- Development-only exact-target interlock. Shared/production deployment is not enabled.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS012')
 THROW 51899,'FS012 isolated target/owner required.',1;
-- Read-only lifecycle contract verification.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningLifecycle.V1' AND CONVERT(nvarchar(100),value)='FS012-V1') THROW 51880,'Lifecycle marker missing.',1;
IF OBJECT_ID('dbo.room_cleaning_cycle_state') IS NULL OR OBJECT_ID('dbo.room_readiness_events') IS NULL THROW 51880,'Lifecycle tables missing.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_room_cycle_task_v1','FK_readiness_task_room_v1') AND is_disabled=0 AND is_not_trusted=0)<>2 THROW 51880,'Lifecycle lineage FKs not trusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.cleaning_tasks') AND name='UX_cleaning_tasks_open_room_v1' AND is_unique=1 AND has_filter=1 AND is_disabled=0) THROW 51880,'Open task uniqueness missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.room_cleaning_cycle_state') AND name='row_version' AND system_type_id=189 AND max_length=8) THROW 51880,'Cycle rowversion missing.',1;
SELECT 'LIFECYCLE_V1_VERIFIED' AS result;
