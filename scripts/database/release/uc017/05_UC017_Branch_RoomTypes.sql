-- UC017 explicit deployment package. Disposable canonical trials only; never run at API startup.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF (DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh' AND DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Upgrade' AND DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]AuthUpgrade')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 THROW 51068,'UC017 candidate requires an approved disposable trial.',1;
IF OBJECT_ID(N'dbo.room_types',N'U') IS NULL OR COL_LENGTH(N'dbo.room_types',N'room_type_id')<>8 OR COL_LENGTH(N'dbo.room_types',N'type_code')<>30
 OR COL_LENGTH(N'dbo.room_types',N'type_name')<>300 OR COL_LENGTH(N'dbo.room_types',N'standard_capacity')<>4
 OR OBJECT_ID(N'dbo.branches',N'U') IS NULL OR OBJECT_ID(N'dbo.inspection_checklists',N'U') IS NULL
 THROW 51068,'UC017 legacy prerequisites do not match.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 51068,'UC017 schema serialization unavailable.',1;
 DECLARE @rows bigint; SELECT @rows=COUNT_BIG(*) FROM dbo.room_types WITH(TABLOCKX,HOLDLOCK);
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)<>N'branch-v1')
  THROW 51068,'UC017 unknown contract marker; no automatic overwrite.',1;
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)=N'branch-v1')
 BEGIN
  IF COL_LENGTH(N'dbo.room_types',N'branch_id')<>8 OR COL_LENGTH(N'dbo.room_types',N'standard_cleaning_minutes')<>4
   OR COL_LENGTH(N'dbo.room_types',N'default_checklist_id')<>8
   OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.room_types') AND name=N'UX_room_types_branch_code' AND is_unique=1 AND is_disabled=0)
   OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.room_types') AND name=N'UX_room_types_legacy_code' AND is_unique=1 AND is_disabled=0)
   OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name IN(N'FK_room_types_branch_v1',N'FK_room_types_default_checklist_v1') AND is_disabled=0 AND is_not_trusted=0)<>2
   OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name=N'CK_room_types_uc017_shape' AND is_disabled=0 AND is_not_trusted=0)
   THROW 51068,'UC017 existing marker has incompatible schema; no automatic repair.',1;
 END ELSE BEGIN
 IF COL_LENGTH(N'dbo.room_types',N'branch_id') IS NOT NULL OR COL_LENGTH(N'dbo.room_types',N'standard_cleaning_minutes') IS NOT NULL
  OR COL_LENGTH(N'dbo.room_types',N'default_checklist_id') IS NOT NULL
  THROW 51068,'UC017 partial schema requires explicit review.',1;
 DECLARE @unique sysname;
 SELECT @unique=kc.name FROM sys.key_constraints kc
 JOIN sys.index_columns ic ON ic.object_id=kc.parent_object_id AND ic.index_id=kc.unique_index_id AND ic.key_ordinal=1
 JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE kc.parent_object_id=OBJECT_ID(N'dbo.room_types') AND kc.type='UQ' AND c.name=N'type_code'
  AND NOT EXISTS(SELECT 1 FROM sys.index_columns more WHERE more.object_id=kc.parent_object_id AND more.index_id=kc.unique_index_id AND more.key_ordinal>1);
 IF @unique IS NULL THROW 51068,'UC017 original type-code constraint not found.',1;
 IF EXISTS(SELECT UPPER(LTRIM(RTRIM(type_code))) FROM dbo.room_types GROUP BY UPPER(LTRIM(RTRIM(type_code))) HAVING COUNT(*)>1)
  THROW 51068,'UC017 legacy normalized codes collide; preserve data and review.',1;
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC017.PreviousUnique',@value=@unique,@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'room_types';
 DECLARE @drop nvarchar(max)=N'ALTER TABLE dbo.room_types DROP CONSTRAINT '+QUOTENAME(@unique);
 EXEC sys.sp_executesql @drop;
 ALTER TABLE dbo.room_types ADD branch_id bigint NULL,standard_cleaning_minutes int NULL,default_checklist_id bigint NULL;
 -- Dynamic batch compiles only after new columns exist. Existing rows remain unmodified NULLs.
 EXEC(N'ALTER TABLE dbo.room_types WITH CHECK ADD CONSTRAINT FK_room_types_branch_v1 FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id);
 ALTER TABLE dbo.room_types WITH CHECK ADD CONSTRAINT FK_room_types_default_checklist_v1 FOREIGN KEY(default_checklist_id) REFERENCES dbo.inspection_checklists(checklist_id);
 ALTER TABLE dbo.room_types WITH CHECK ADD CONSTRAINT CK_room_types_uc017_shape CHECK(
  (branch_id IS NULL AND standard_cleaning_minutes IS NULL AND default_checklist_id IS NULL)
  OR (branch_id IS NOT NULL AND standard_cleaning_minutes IS NOT NULL AND standard_cleaning_minutes>0 AND default_checklist_id IS NOT NULL));
 CREATE UNIQUE INDEX UX_room_types_branch_code ON dbo.room_types(branch_id,type_code) WHERE branch_id IS NOT NULL;
 CREATE UNIQUE INDEX UX_room_types_legacy_code ON dbo.room_types(type_code) WHERE branch_id IS NULL;');
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC017.Contract',@value=N'branch-v1',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'room_types';
 END; -- legacy upgrade branch
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.room_types') AND name=N'IX_room_types_default_checklist_v1')
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment')
   THROW 51068,'UC017 supporting index missing from installed package; no automatic repair.',1;
  EXEC(N'CREATE INDEX IX_room_types_default_checklist_v1 ON dbo.room_types(default_checklist_id);');
 END;
 -- Branch ownership is immutable in V1, including global legacy records.
 -- This needs no Rooms read after taking a type lock.
 DECLARE @typeTrigger nvarchar(max)=N'CREATE TRIGGER dbo.TR_room_types_uc017_branch_immutable ON dbo.room_types AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.room_type_id=i.room_type_id WHERE ISNULL(i.branch_id,0)<>ISNULL(d.branch_id,0))
  THROW 51069,''UC017 branch ownership is immutable; use a separately reviewed migration.'',1;
END';
 DECLARE @roomTrigger nvarchar(max)=N'CREATE TRIGGER dbo.TR_rooms_uc017_type_branch ON dbo.rooms AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.room_types t WITH(HOLDLOCK) ON t.room_type_id=i.room_type_id WHERE t.branch_id IS NOT NULL AND t.branch_id<>i.branch_id)
  THROW 51069,''UC017 RoomType belongs to another branch.'',1;
END';
 IF OBJECT_ID(N'dbo.TR_room_types_uc017_branch_immutable',N'TR') IS NULL EXEC sys.sp_executesql @typeTrigger;
 ELSE IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_room_types_uc017_branch_immutable')) COLLATE Latin1_General_100_BIN2<>@typeTrigger COLLATE Latin1_General_100_BIN2
  THROW 51068,'UC017 type trigger definition differs; no overwrite.',1;
 IF OBJECT_ID(N'dbo.TR_rooms_uc017_type_branch',N'TR') IS NULL EXEC sys.sp_executesql @roomTrigger;
 ELSE IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_rooms_uc017_type_branch')) COLLATE Latin1_General_100_BIN2<>@roomTrigger COLLATE Latin1_General_100_BIN2
  THROW 51068,'UC017 Room trigger definition differs; no overwrite.',1;
 EXEC(N'IF EXISTS(SELECT 1 FROM dbo.rooms r JOIN dbo.room_types t ON t.room_type_id=r.room_type_id WHERE t.branch_id IS NOT NULL AND t.branch_id<>r.branch_id)
 THROW 51068,''UC017 existing cross-branch assignments require review; no conversion.'',1;');
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC017.Deployment',@value=N'Task69-v1',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'room_types';
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment' AND CONVERT(nvarchar(128),value)=N'Task69-v1')
  THROW 51068,'UC017 unknown deployment version.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
