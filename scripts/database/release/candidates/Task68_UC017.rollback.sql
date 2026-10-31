-- Candidate only. Refuses to erase any scoped/new operational data.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 THROW 51068,'UC017 rollback requires an approved disposable trial.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 51068,'UC017 schema serialization unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)=N'branch-v1')
  THROW 51068,'UC017 rollback marker missing; no automatic schema guess.',1;
 DECLARE @rows bigint;SELECT @rows=COUNT_BIG(*) FROM dbo.room_types WITH(TABLOCKX,HOLDLOCK);
 EXEC(N'IF EXISTS(SELECT 1 FROM dbo.room_types WHERE branch_id IS NOT NULL OR standard_cleaning_minutes IS NOT NULL OR default_checklist_id IS NOT NULL)
 THROW 51068,''UC017 rollback blocked: scoped data exists; no data will be deleted.'',1;');
 DECLARE @old sysname=(SELECT CONVERT(sysname,value) FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.PreviousUnique');
 IF @old IS NULL THROW 51068,'UC017 original unique constraint identity missing.',1;
 DROP INDEX UX_room_types_branch_code ON dbo.room_types;
 DROP INDEX UX_room_types_legacy_code ON dbo.room_types;
 ALTER TABLE dbo.room_types DROP CONSTRAINT CK_room_types_uc017_shape,FK_room_types_branch_v1,FK_room_types_default_checklist_v1;
 ALTER TABLE dbo.room_types DROP COLUMN branch_id,standard_cleaning_minutes,default_checklist_id;
 DECLARE @restore nvarchar(max)=N'ALTER TABLE dbo.room_types ADD CONSTRAINT '+QUOTENAME(@old)+N' UNIQUE(type_code);';
 EXEC sys.sp_executesql @restore;
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.UC017.Contract',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'room_types';
 EXEC sys.sp_dropextendedproperty @name=N'HotelOps.UC017.PreviousUnique',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'room_types';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
