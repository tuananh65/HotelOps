-- Additive UC061/062 storage; rollback installation with ApplyChanges=0. Retain committed provenance on source rollback.
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
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveOccurrence:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51847,'Occurrence schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveSchedule.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51847,'Install schedule metadata first.',1;
IF OBJECT_ID('dbo.preventive_occurrence_details','U') IS NULL
BEGIN
 CREATE TABLE dbo.preventive_occurrence_details(
  run_id bigint NOT NULL CONSTRAINT PK_preventive_occurrence_details PRIMARY KEY,
  branch_id bigint NOT NULL,
  definition_json nvarchar(max) NOT NULL,
  assignee_id bigint NULL,
  workflow_status varchar(30) NOT NULL,
  generated_at datetime2(7) NOT NULL,
  generation_audit_id bigint NOT NULL,
  row_version rowversion NOT NULL,
  CONSTRAINT FK_pm_occurrence_run FOREIGN KEY(run_id) REFERENCES dbo.preventive_maintenance_runs(run_id),
  CONSTRAINT FK_pm_occurrence_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT FK_pm_occurrence_assignee FOREIGN KEY(assignee_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_pm_occurrence_audit FOREIGN KEY(generation_audit_id) REFERENCES dbo.audit_logs(audit_id),
  CONSTRAINT CK_pm_occurrence_definition CHECK(ISJSON(definition_json)=1),
  CONSTRAINT CK_pm_occurrence_state CHECK(workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','IN_PROGRESS','COMPLETED','CANCELLED')),
  CONSTRAINT CK_pm_occurrence_assignee CHECK((workflow_status='PENDING_ASSIGNMENT' AND assignee_id IS NULL)
    OR (workflow_status IN('ASSIGNED','IN_PROGRESS','COMPLETED') AND assignee_id IS NOT NULL) OR workflow_status='CANCELLED')
 );
 CREATE INDEX IX_pm_occurrence_branch ON dbo.preventive_occurrence_details(branch_id,run_id);
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_occurrence_details') AND name IN
 ('run_id','branch_id','definition_json','assignee_id','workflow_status','generated_at','generation_audit_id','row_version'))<>8
 THROW 51847,'Occurrence schema mismatch; preserve existing data.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_details')
 AND name IN('FK_pm_occurrence_run','FK_pm_occurrence_branch','FK_pm_occurrence_assignee','FK_pm_occurrence_audit')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>4
 THROW 51847,'Occurrence integrity constraints unavailable.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_occurrence_provenance ON dbo.preventive_occurrence_details AFTER UPDATE,DELETE AS
 BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.run_id=d.run_id WHERE i.run_id IS NULL
 OR i.branch_id<>d.branch_id OR CONVERT(varbinary(max),i.definition_json)<>CONVERT(varbinary(max),d.definition_json)
 OR i.generated_at<>d.generated_at OR i.generation_audit_id<>d.generation_audit_id)
 THROW 51847,''Occurrence generation provenance is immutable.'',1;
 END');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveOccurrence.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.PreventiveOccurrence.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveOccurrence.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51847,'Occurrence version mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;