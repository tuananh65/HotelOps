-- Explicit opt-in amendment; Run-Housekeeping.ps1 owns the guarded transaction and package marker.
SET XACT_ABORT ON;
IF @@TRANCOUNT=0 OR DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.V1')
 THROW 51730,'Guarded caller-owned Housekeeping trial transaction required.',1;
GO
CREATE OR ALTER TRIGGER dbo.TR_hk_defect_files_v1 ON dbo.inspection_defect_attachments AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_defects d ON d.defect_id=i.defect_id JOIN dbo.attachments a ON a.attachment_id=i.attachment_id
  WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>d.inspection_id OR a.deleted_at IS NOT NULL)
  THROW 51731,'Attachment must belong to this inspection.',1;
 ;WITH targets AS (SELECT defect_id FROM inserted UNION SELECT defect_id FROM deleted),
 parents AS (SELECT DISTINCT d.inspection_id FROM targets t JOIN dbo.inspection_defects d ON d.defect_id=t.defect_id)
 UPDATE r SET summary_note=summary_note FROM dbo.room_inspections r JOIN parents p ON p.inspection_id=r.inspection_id
 WHERE r.workflow_status IS NOT NULL;
END
GO
CREATE OR ALTER TRIGGER dbo.TR_hk_result_files_v1 ON dbo.inspection_result_attachments AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inspection_results r ON r.result_id=i.result_id JOIN dbo.attachments a ON a.attachment_id=i.attachment_id
  WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>r.inspection_id OR a.deleted_at IS NOT NULL)
  THROW 51731,'Attachment must belong to this inspection.',1;
 ;WITH targets AS (SELECT result_id FROM inserted UNION SELECT result_id FROM deleted),
 parents AS (SELECT DISTINCT x.inspection_id FROM targets t JOIN dbo.inspection_results x ON x.result_id=t.result_id)
 UPDATE r SET summary_note=summary_note FROM dbo.room_inspections r JOIN parents p ON p.inspection_id=r.inspection_id
 WHERE r.workflow_status IS NOT NULL;
END
GO
CREATE OR ALTER TRIGGER dbo.TR_hk_attachment_revision_v1 ON dbo.attachments AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 -- Updating attachment binding must preserve the existing bridge ownership invariant.
 -- Soft deletion is allowed here; new links to deleted attachments remain forbidden by link triggers.
 IF EXISTS(SELECT 1 FROM inserted a JOIN dbo.inspection_defect_attachments l ON l.attachment_id=a.attachment_id
  JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>d.inspection_id)
 OR EXISTS(SELECT 1 FROM inserted a JOIN dbo.inspection_result_attachments l ON l.attachment_id=a.attachment_id
  JOIN dbo.inspection_results r ON r.result_id=l.result_id WHERE a.entity_type<>'INSPECTION' OR a.entity_id<>r.inspection_id)
  THROW 51731,'Attachment binding conflicts with its inspection evidence links.',1;
 ;WITH changed AS (SELECT attachment_id FROM inserted UNION SELECT attachment_id FROM deleted),
 parents AS (
  SELECT entity_id AS inspection_id FROM inserted WHERE entity_type='INSPECTION'
  UNION SELECT entity_id FROM deleted WHERE entity_type='INSPECTION'
  UNION SELECT d.inspection_id FROM changed c JOIN dbo.inspection_defect_attachments l ON l.attachment_id=c.attachment_id
   JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id
  UNION SELECT r.inspection_id FROM changed c JOIN dbo.inspection_result_attachments l ON l.attachment_id=c.attachment_id
   JOIN dbo.inspection_results r ON r.result_id=l.result_id)
 UPDATE r SET summary_note=summary_note FROM dbo.room_inspections r JOIN parents p ON p.inspection_id=r.inspection_id
 WHERE r.workflow_status IS NOT NULL;
END
