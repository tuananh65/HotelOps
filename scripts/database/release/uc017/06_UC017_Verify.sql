-- Read-only postflight, executed inside the deployment entry point's outer transaction.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)=N'branch-v1')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment' AND CONVERT(nvarchar(128),value)=N'Task69-v1')
 THROW 51068,'UC017 deployment version mismatch.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.room_types') AND is_nullable=1 AND is_computed=0
 AND ((name IN(N'branch_id',N'default_checklist_id') AND system_type_id=127 AND max_length=8)
 OR (name=N'standard_cleaning_minutes' AND system_type_id=56 AND max_length=4)))<>3
 THROW 51068,'UC017 column metadata mismatch.',1;
IF (SELECT COUNT(*) FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.room_types') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=1
 AND ((i.name=N'UX_room_types_branch_code'
 AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,N'[',N''),N']',N''),N' ',N''),N'(',N''),N')',N'')=N'branch_idISNOTNULL'
 AND INDEX_COL(N'dbo.room_types',i.index_id,1)=N'branch_id' AND INDEX_COL(N'dbo.room_types',i.index_id,2)=N'type_code' AND INDEX_COL(N'dbo.room_types',i.index_id,3) IS NULL)
 OR (i.name=N'UX_room_types_legacy_code'
 AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,N'[',N''),N']',N''),N' ',N''),N'(',N''),N')',N'')=N'branch_idISNULL'
 AND INDEX_COL(N'dbo.room_types',i.index_id,1)=N'type_code' AND INDEX_COL(N'dbo.room_types',i.index_id,2) IS NULL)))<>2
 THROW 51068,'UC017 index keys/filter mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.room_types') AND i.name=N'IX_room_types_default_checklist_v1'
 AND i.is_unique=0 AND i.is_disabled=0 AND i.has_filter=0 AND INDEX_COL(N'dbo.room_types',i.index_id,1)=N'default_checklist_id'
 AND INDEX_COL(N'dbo.room_types',i.index_id,2) IS NULL)
 THROW 51068,'UC017 checklist supporting index mismatch.',1;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.room_types') AND is_unique=1 AND has_filter=0 AND is_primary_key=0)
 THROW 51068,'UC017 obsolete global unique index remains.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
 WHERE f.parent_object_id=OBJECT_ID(N'dbo.room_types') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
 AND ((f.name=N'FK_room_types_branch_v1' AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'branch_id'
 AND c.referenced_object_id=OBJECT_ID(N'dbo.branches') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'branch_id')
 OR (f.name=N'FK_room_types_default_checklist_v1' AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'default_checklist_id'
 AND c.referenced_object_id=OBJECT_ID(N'dbo.inspection_checklists') AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'checklist_id')))<>2
 THROW 51068,'UC017 FK metadata mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name=N'CK_room_types_uc017_shape' AND is_disabled=0 AND is_not_trusted=0)
 OR (SELECT COUNT(*) FROM sys.triggers WHERE parent_id IN(OBJECT_ID(N'dbo.rooms'),OBJECT_ID(N'dbo.room_types'))
 AND name IN(N'TR_rooms_uc017_type_branch',N'TR_room_types_uc017_branch_immutable') AND is_disabled=0)<>2
 THROW 51068,'UC017 CHECK/trigger unavailable.',1;
DECLARE @shape nvarchar(max)=(SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name=N'CK_room_types_uc017_shape');
SET @shape=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@shape,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''),CHAR(13),N''),CHAR(10),N'');
IF @shape IS NULL OR @shape<>N'branch_idISNULLANDstandard_cleaning_minutesISNULLANDdefault_checklist_idISNULLORbranch_idISNOTNULLANDstandard_cleaning_minutesISNOTNULLANDstandard_cleaning_minutes>0ANDdefault_checklist_idISNOTNULL'
 THROW 51068,'UC017 CHECK definition mismatch.',1;
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
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_room_types_uc017_branch_immutable')) IS NULL
 OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_room_types_uc017_branch_immutable')) COLLATE Latin1_General_100_BIN2<>@typeTrigger COLLATE Latin1_General_100_BIN2
 OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_rooms_uc017_type_branch')) IS NULL
 OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_rooms_uc017_type_branch')) COLLATE Latin1_General_100_BIN2<>@roomTrigger COLLATE Latin1_General_100_BIN2
 THROW 51068,'UC017 trigger definition mismatch; no silent acceptance.',1;
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.room_types WHERE (branch_id IS NULL AND (standard_cleaning_minutes IS NOT NULL OR default_checklist_id IS NOT NULL))
 OR (branch_id IS NOT NULL AND (standard_cleaning_minutes IS NULL OR standard_cleaning_minutes<=0 OR default_checklist_id IS NULL)))
 OR EXISTS(SELECT 1 FROM dbo.rooms r JOIN dbo.room_types t ON t.room_type_id=r.room_type_id WHERE t.branch_id IS NOT NULL AND t.branch_id<>r.branch_id)
 THROW 51068,''UC017 invalid scoped data; no automatic repair.'',1;');
