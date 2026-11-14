SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
    AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
    AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51970,'Exact owned disposable target required',1;

IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1') THROW 51992,'Amendment already exists',1;
BEGIN TRANSACTION;
GO
-- Explicit opt-in amendment; Run-Housekeeping.ps1 owns the guarded transaction and package marker.
SET XACT_ABORT ON;
IF @@TRANCOUNT=0 OR DB_NAME() NOT LIKE N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
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

GO
EXEC sys.sp_addextendedproperty @name=N'HotelOps.Housekeeping.EvidenceRevision.V1',@value=N'314DBD336CED88E2DCFDCF6762F8126BFA95D349247ACADA7294F83F19B1F7AD';
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.EvidenceRevision.V1'
 AND LEN(CONVERT(nvarchar(128),value))=64)
 OR (SELECT COUNT(*) FROM sys.triggers WHERE
 (object_id=OBJECT_ID(N'dbo.TR_hk_defect_files_v1') AND parent_id=OBJECT_ID(N'dbo.inspection_defect_attachments')) OR
 (object_id=OBJECT_ID(N'dbo.TR_hk_result_files_v1') AND parent_id=OBJECT_ID(N'dbo.inspection_result_attachments')) OR
 (object_id=OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1') AND parent_id=OBJECT_ID(N'dbo.attachments')))<>3
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE object_id IN(OBJECT_ID(N'dbo.TR_hk_defect_files_v1'),OBJECT_ID(N'dbo.TR_hk_result_files_v1'),OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1'))
 AND (is_disabled=1 OR is_not_for_replication=1))
 OR (SELECT COUNT(*) FROM sys.trigger_events WHERE object_id IN(OBJECT_ID(N'dbo.TR_hk_defect_files_v1'),OBJECT_ID(N'dbo.TR_hk_result_files_v1'),OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1'))
 AND type_desc IN('INSERT','UPDATE','DELETE'))<>9
 THROW 51730,'Evidence revision amendment missing or incompatible.',1;
-- Exact package hash and all three OBJECT_DEFINITION values are verified by the runner.

COMMIT;
