SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh' AND DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Upgrade' AND DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]AuthUpgrade'
 THROW 51720,'Disposable canonical trial required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 THROW 51720,'Verified personnel trial required.',1;
IF COL_LENGTH(N'dbo.room_types',N'branch_id') IS NULL THROW 51720,'UC017 deployment required.',1;
IF COL_LENGTH(N'dbo.inspection_checklists',N'branch_id') IS NOT NULL OR COL_LENGTH(N'dbo.inspection_checklists',N'row_version') IS NOT NULL OR COL_LENGTH(N'dbo.inspection_checklist_items',N'criterion_group') IS NOT NULL OR OBJECT_ID(N'dbo.inspection_checklist_room_types') IS NOT NULL
 THROW 51720,'Existing partial checklist schema requires review.',1;
ALTER TABLE dbo.inspection_checklists ADD branch_id bigint NULL;
ALTER TABLE dbo.inspection_checklists ADD row_version rowversion NOT NULL;
ALTER TABLE dbo.inspection_checklist_items ADD criterion_group nvarchar(100) NULL;
GO
ALTER TABLE dbo.inspection_checklists WITH CHECK ADD CONSTRAINT FK_checklists_branch_v1 FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id);
CREATE INDEX IX_checklists_branch_v1 ON dbo.inspection_checklists(branch_id,checklist_id);
CREATE TABLE dbo.inspection_checklist_room_types(
 checklist_id bigint NOT NULL,room_type_id bigint NOT NULL,
 CONSTRAINT PK_checklist_room_types_v1 PRIMARY KEY(checklist_id,room_type_id),
 CONSTRAINT FK_checklist_types_checklist_v1 FOREIGN KEY(checklist_id) REFERENCES dbo.inspection_checklists(checklist_id),
 CONSTRAINT FK_checklist_types_type_v1 FOREIGN KEY(room_type_id) REFERENCES dbo.room_types(room_type_id));
CREATE INDEX IX_checklist_types_reverse_v1 ON dbo.inspection_checklist_room_types(room_type_id,checklist_id);
GO
CREATE TRIGGER dbo.TR_checklists_scope_v1 ON dbo.inspection_checklists AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.checklist_id=d.checklist_id WHERE ISNULL(i.branch_id,0)<>ISNULL(d.branch_id,0))
  THROW 51721,'Checklist ownership is immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted WHERE branch_id IS NOT NULL AND room_type_id IS NOT NULL)
  THROW 51721,'Scoped checklist uses applicability bridge.',1;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.checklist_id=d.checklist_id WHERE d.branch_id IS NOT NULL AND i.checklist_id IS NULL)
  THROW 51721,'Scoped checklist deletion is not supported.',1;
END
GO
CREATE TRIGGER dbo.TR_checklist_types_scope_v1 ON dbo.inspection_checklist_room_types AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_checklists c WITH(UPDLOCK,HOLDLOCK) ON c.checklist_id=i.checklist_id
  JOIN dbo.room_types t WITH(HOLDLOCK) ON t.room_type_id=i.room_type_id WHERE c.branch_id IS NULL OR t.branch_id IS NULL OR c.branch_id<>t.branch_id)
  THROW 51721,'Checklist/RoomType branch mismatch.',1;
 IF EXISTS(SELECT 1 FROM (SELECT checklist_id FROM inserted UNION SELECT checklist_id FROM deleted) a
  JOIN dbo.inspection_checklist_versions v WITH(HOLDLOCK) ON v.checklist_id=a.checklist_id WHERE v.status<>N'DRAFT')
  THROW 51721,'Published applicability is immutable.',1;
END
GO
CREATE TRIGGER dbo.TR_checklist_items_immutable_v1 ON dbo.inspection_checklist_items AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT version_id FROM inserted UNION SELECT version_id FROM deleted) a
  JOIN dbo.inspection_checklist_versions v WITH(UPDLOCK,HOLDLOCK) ON v.version_id=a.version_id
  JOIN dbo.inspection_checklists c ON c.checklist_id=v.checklist_id
  WHERE c.branch_id IS NOT NULL AND (v.status<>N'DRAFT' OR EXISTS(SELECT 1 FROM dbo.room_inspections r WITH(HOLDLOCK) WHERE r.version_id=v.version_id)))
  THROW 51721,'Published/used checklist criteria are immutable.',1;
END
GO
CREATE TRIGGER dbo.TR_checklist_versions_v1 ON dbo.inspection_checklist_versions AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.version_id=d.version_id
  JOIN dbo.inspection_checklists oldc ON oldc.checklist_id=d.checklist_id JOIN dbo.inspection_checklists newc ON newc.checklist_id=i.checklist_id
  WHERE (oldc.branch_id IS NOT NULL OR newc.branch_id IS NOT NULL) AND i.checklist_id<>d.checklist_id)
  THROW 51721,'Version ownership is immutable.',1;
 IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.inspection_checklists c ON c.checklist_id=d.checklist_id
  LEFT JOIN inserted i ON i.version_id=d.version_id WHERE c.branch_id IS NOT NULL AND (i.version_id IS NULL OR
   i.checklist_id<>d.checklist_id OR i.version_no<>d.version_no OR
   (d.status<>N'DRAFT' AND (NOT(d.status=N'PUBLISHED' AND i.status=N'RETIRED')
    OR ISNULL(i.effective_at,'19000101')<>ISNULL(d.effective_at,'19000101') OR ISNULL(i.published_by,0)<>ISNULL(d.published_by,0))) OR
   (EXISTS(SELECT 1 FROM dbo.room_inspections r WITH(HOLDLOCK) WHERE r.version_id=d.version_id) AND d.status=N'DRAFT')))
  THROW 51721,'Published/used version is immutable except retirement.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_checklists c WITH(UPDLOCK,HOLDLOCK) ON c.checklist_id=i.checklist_id
  WHERE c.branch_id IS NOT NULL AND (NOT EXISTS(SELECT 1 FROM deleted d WHERE d.version_id=i.version_id) AND i.status<>N'DRAFT'
   OR i.status=N'PUBLISHED' AND (c.is_active=0 OR i.effective_at IS NULL OR i.effective_at>SYSUTCDATETIME()
    OR NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_items x WITH(HOLDLOCK) WHERE x.version_id=i.version_id AND x.is_active=1)
    OR EXISTS(SELECT 1 FROM dbo.inspection_checklist_items x WITH(HOLDLOCK) WHERE x.version_id=i.version_id AND x.is_active=1 AND LEN(LTRIM(RTRIM(x.criterion)))=0)
    OR EXISTS(SELECT 1 FROM dbo.inspection_checklist_versions v WITH(HOLDLOCK) WHERE v.checklist_id=i.checklist_id AND v.status=N'PUBLISHED' AND v.version_id<>i.version_id))))
  THROW 51721,'Invalid checklist publication.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_checklists c ON c.checklist_id=i.checklist_id
  WHERE c.branch_id IS NOT NULL AND i.status=N'DRAFT' AND EXISTS(SELECT 1 FROM dbo.inspection_checklist_versions v WITH(HOLDLOCK)
   WHERE v.checklist_id=i.checklist_id AND v.status=N'DRAFT' AND v.version_id<>i.version_id))
  THROW 51721,'Only one Draft per checklist.',1;
END
GO
CREATE TRIGGER dbo.TR_room_types_checklist_v1 ON dbo.room_types AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.room_type_id=i.room_type_id
  JOIN dbo.inspection_checklists c WITH(HOLDLOCK) ON c.checklist_id=i.default_checklist_id
  WHERE c.branch_id IS NOT NULL AND (d.room_type_id IS NULL OR ISNULL(d.default_checklist_id,0)<>ISNULL(i.default_checklist_id,0))
   AND (c.branch_id<>i.branch_id OR i.branch_id IS NULL OR c.is_active=0
    OR NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_room_types a WITH(HOLDLOCK) WHERE a.checklist_id=c.checklist_id AND a.room_type_id=i.room_type_id)
    OR NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_versions v WITH(HOLDLOCK) WHERE v.checklist_id=c.checklist_id AND v.status=N'PUBLISHED'
     AND v.effective_at<=SYSUTCDATETIME() AND EXISTS(SELECT 1 FROM dbo.inspection_checklist_items x WITH(HOLDLOCK) WHERE x.version_id=v.version_id AND x.is_active=1))))
  THROW 51721,'RoomType default checklist must be applicable and Published.',1;
END
GO
CREATE TRIGGER dbo.TR_inspection_checklist_selection_v1 ON dbo.room_inspections AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.inspection_id=i.inspection_id
  JOIN dbo.inspection_checklist_versions v WITH(HOLDLOCK) ON v.version_id=i.version_id
  JOIN dbo.inspection_checklists c WITH(HOLDLOCK) ON c.checklist_id=v.checklist_id
  JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id
  WHERE c.branch_id IS NOT NULL AND (d.inspection_id IS NULL OR d.version_id<>i.version_id OR d.room_id<>i.room_id)
   AND (c.branch_id<>r.branch_id OR c.is_active=0 OR v.status<>N'PUBLISHED' OR v.effective_at IS NULL OR v.effective_at>SYSUTCDATETIME()
    OR NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_room_types a WITH(HOLDLOCK) WHERE a.checklist_id=c.checklist_id AND a.room_type_id=r.room_type_id)
    OR NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_items x WITH(HOLDLOCK) WHERE x.version_id=v.version_id AND x.is_active=1)))
  THROW 51721,'Inspection requires an applicable effective Published checklist.',1;
END
