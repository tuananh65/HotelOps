-- Approved rework workflow package. Execute only through the pinned transactional installer.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database')) IS NULL
 OR DB_NAME()<>CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database'))
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'ReworkWriter.DatabaseGuid')))
 THROW 51999,'Pinned database transaction required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'ReworkWriter.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 THROW 51998,'Actual committed source required',1;
IF OBJECT_ID('dbo.fs016_rework_source_proofs') IS NULL OR OBJECT_ID('dbo.fe04_rework_authority_issuers') IS NULL
 OR OBJECT_ID('dbo.notification_operation_reservations') IS NULL
 THROW 51998,'Committed FE05 authority and operation ledger required',1;
IF COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NOT NULL
 OR COL_LENGTH('dbo.cleaning_rework_defects','remediated_by') IS NOT NULL OR COL_LENGTH('dbo.cleaning_rework_defects','remediated_at') IS NOT NULL
 OR OBJECT_ID('dbo.fs016_rework_evidence') IS NOT NULL OR OBJECT_ID('dbo.fe04_commit_rework_submission') IS NOT NULL
 OR DATABASE_PRINCIPAL_ID('hotelops_rework_submission_writer') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkSubmission.V1')
 THROW 51998,'Existing submission storage must not be overwritten',1;
ALTER TABLE dbo.cleaning_rework_defects ADD remediation_note nvarchar(500) NULL,remediated_by bigint NULL,remediated_at datetime2(7) NULL;
EXEC(N'ALTER TABLE dbo.cleaning_rework_defects WITH CHECK ADD CONSTRAINT FK_rework_remediator_v1 FOREIGN KEY(remediated_by) REFERENCES dbo.users(user_id);
 ALTER TABLE dbo.cleaning_rework_defects WITH CHECK ADD CONSTRAINT CK_rework_remediation_v1 CHECK(
 (remediation_note IS NULL AND remediated_by IS NULL AND remediated_at IS NULL) OR
 (remediation_note IS NOT NULL AND LEN(LTRIM(RTRIM(remediation_note)))>0 AND remediated_by IS NOT NULL AND remediated_at IS NOT NULL));');
CREATE TABLE dbo.fs016_rework_evidence(rework_id bigint NOT NULL,defect_id bigint NOT NULL,attachment_id bigint NOT NULL,
 CONSTRAINT PK_rework_evidence_v1 PRIMARY KEY(rework_id,defect_id,attachment_id),
 CONSTRAINT FK_rework_evidence_source_v1 FOREIGN KEY(rework_id,defect_id) REFERENCES dbo.cleaning_rework_defects(rework_id,defect_id),
 CONSTRAINT FK_rework_evidence_file_v1 FOREIGN KEY(attachment_id) REFERENCES dbo.attachments(attachment_id));
EXEC(N'CREATE TRIGGER dbo.TR_rework_remediation_revision_v1 ON dbo.cleaning_rework_defects AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 UPDATE w SET note=note FROM dbo.cleaning_reworks w JOIN
 (SELECT rework_id FROM inserted UNION SELECT rework_id FROM deleted) x ON x.rework_id=w.rework_id;
 END');
EXEC(N'CREATE TRIGGER dbo.TR_rework_evidence_revision_v1 ON dbo.fs016_rework_evidence AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted e JOIN dbo.attachments a ON a.attachment_id=e.attachment_id
  JOIN dbo.cleaning_reworks w ON w.rework_id=e.rework_id
  WHERE a.entity_type<>''CLEANING_TASK'' OR a.entity_id<>w.cleaning_task_id OR a.deleted_at IS NOT NULL)
  THROW 51998,''Remediation evidence source mismatch'',1;
 UPDATE w SET note=note FROM dbo.cleaning_reworks w JOIN
 (SELECT rework_id FROM inserted UNION SELECT rework_id FROM deleted) x ON x.rework_id=w.rework_id;
 END');
EXEC(N'CREATE PROCEDURE dbo.fe04_commit_rework_submission
 @actor bigint,@branch bigint,@rework bigint,@operation uniqueidentifier,
 @fingerprint varbinary(max),@source_digest varbinary(max),@remediation_digest varbinary(max),@entity nvarchar(max)
WITH EXECUTE AS OWNER AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
   WHERE x.issuer=''FE04.UC036'' AND x.version=1 AND x.is_active=1 AND x.writer_object_id=@@PROCID AND m.execute_as_principal_id=-2
     AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51998,''Registered submission writer required'',1;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51998,''Caller serializable transaction required'',1;
 IF @operation IS NULL OR @actor<=0 OR @branch<=0 OR @rework<=0 OR ISNULL(DATALENGTH(@fingerprint),0)<>32
  OR ISNULL(DATALENGTH(@source_digest),0)<>32 OR ISNULL(DATALENGTH(@remediation_digest),0)<>32 OR ISNULL(ISJSON(@entity),0)<>1
  THROW 51998,''Submission receipt parameters invalid'',1;
 DECLARE @inspection bigint,@task bigint,@room bigint,@revision binary(8),@authority uniqueidentifier,@interval bigint,@closed datetime2(7);
 SELECT @inspection=w.inspection_id,@task=w.cleaning_task_id,@room=r.room_id,@revision=w.row_version,@authority=p.operation_key
 FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.room_inspections i WITH(HOLDLOCK) ON i.inspection_id=w.inspection_id AND i.cleaning_task_id=w.cleaning_task_id
 JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id AND t.room_id=i.room_id
 JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
 JOIN dbo.fs016_rework_source_proofs p WITH(HOLDLOCK) ON p.rework_id=w.rework_id AND p.inspection_id=i.inspection_id
   AND p.inspection_revision=i.row_version AND p.cleaning_task_id=t.cleaning_task_id AND p.room_id=r.room_id AND p.branch_id=r.branch_id
 WHERE w.rework_id=@rework AND w.assignee_user_id=@actor AND w.workflow_status=''PENDING_REINSPECTION'' AND w.status=''IN_PROGRESS'' AND w.completed_at IS NULL
   AND i.workflow_status=''FAILED'' AND i.status=''COMPLETED'' AND i.result=''FAIL'' AND t.workflow_status=''REWORK_REQUIRED''
   AND p.origin=''FE05_COMMITTED_V1'' AND p.source_digest=@source_digest AND p.writer_transaction_id<>CURRENT_TRANSACTION_ID()
   AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WHERE newer.cleaning_task_id=t.cleaning_task_id AND newer.inspection_id>i.inspection_id);
 IF @inspection IS NULL OR (SELECT COUNT(*) FROM dbo.cleaning_reworks WHERE inspection_id=@inspection AND status NOT IN(''COMPLETED'',''CANCELLED''))<>1
  THROW 51998,''Current committed rework source required'',1;
 IF (SELECT COUNT(*) FROM dbo.cleaning_rework_defects WHERE rework_id=@rework) NOT BETWEEN 1 AND 200
  OR EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects WHERE rework_id=@rework
    AND (remediation_note IS NULL OR LEN(LTRIM(RTRIM(remediation_note)))=0 OR remediated_by IS NULL OR remediated_by<>@actor OR remediated_at IS NULL))
  THROW 51998,''Every defect requires remediation attribution'',1;
 SELECT @interval=interval_id,@closed=closed_at FROM dbo.work_intervals WITH(HOLDLOCK)
 WHERE interval_id=TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.intervalId'')) AND user_id=@actor AND branch_id=@branch
   AND work_item_kind=''REWORK'' AND work_item_id=@rework AND closed_at IS NOT NULL AND closed_by=@actor;
 IF @interval IS NULL OR EXISTS(SELECT 1 FROM dbo.work_intervals WHERE user_id=@actor AND closed_at IS NULL)
  OR EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects WHERE rework_id=@rework AND remediated_at<>@closed)
  THROW 51998,''Closed owned work interval required'',1;
 IF ISNULL(JSON_VALUE(@entity,''$.contract''),'''')<>''FE04_REWORK_SUBMISSION_V1''
  OR ISNULL(JSON_VALUE(@entity,''$.state''),'''')<>''PENDING_REINSPECTION'' OR ISNULL(JSON_VALUE(@entity,''$.action''),'''')<>''complete''
  OR ISNULL(JSON_VALUE(@entity,''$.notification''),'''')<>''PENDING_DELIVERY''
  OR ISNULL(JSON_VALUE(@entity,''$.readiness''),'''') NOT IN(''PENDING_INSPECTION'',''PENDING_CLEANING'',''BLOCKED'')
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.id'')),0)<>@rework
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.branchId'')),0)<>@branch
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.assigneeId'')),0)<>@actor
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.cleaningTaskId'')),0)<>@task
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.inspectionId'')),0)<>@inspection
  OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.operationId'')) IS NULL OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.operationId''))<>@operation
  OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.publicationId'')) IS NULL OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.publicationId''))<>@operation
  OR ISNULL(JSON_VALUE(@entity,''$.sourceETag''),'''')<>CHAR(34)+CONVERT(varchar(16),@revision,2)+CHAR(34)
  OR TRY_CONVERT(datetime2(7),JSON_VALUE(@entity,''$.occurredAtUtc''),127) IS NULL
  OR TRY_CONVERT(datetime2(7),JSON_VALUE(@entity,''$.occurredAtUtc''),127)<>@closed
  THROW 51998,''Submission receipt does not match persisted results'',1;
 DECLARE @writer bigint=CURRENT_TRANSACTION_ID(),@receipt nvarchar(max);
 SET @receipt=(SELECT ''FE04_REWORK_SUBMISSION_V1'' AS contract,''FE04.UC036'' AS issuer,1 AS issuerVersion,
  @actor AS actorUserId,@branch AS branchId,@rework AS reworkId,@operation AS operationId,@writer AS writerTransactionId,
  @authority AS authorityOperationId,CONVERT(varchar(64),@source_digest,2) AS sourceDigest,
  CONVERT(varchar(64),@remediation_digest,2) AS remediationDigest,
  JSON_QUERY(@entity) AS entity,
  JSON_QUERY((SELECT 1 AS schemaVersion,''REWORK_SUBMITTED'' AS kind,''BRANCH_HK_SUPERVISORS'' AS audience FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@fingerprint,1,@receipt,SYSUTCDATETIME());
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,occurred_at)
 VALUES(@branch,@actor,''cleaning_reworks'',CONVERT(nvarchar(50),@rework),''UPDATE'',N''{"state":"IN_PROGRESS"}'',@receipt,@closed);
END');
ALTER AUTHORIZATION ON OBJECT::dbo.fe04_commit_rework_submission TO hotelops_rework_owner;
INSERT dbo.fe04_rework_authority_issuers(issuer,version,writer_object_id,source_commit,module_digest,is_active)
 SELECT 'FE04.UC036',1,object_id,@commit,HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),1
 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe04_commit_rework_submission');
IF @@ROWCOUNT<>1 THROW 51998,'Submission issuer registration failed',1;
CREATE ROLE hotelops_rework_submission_writer;
GRANT EXECUTE ON dbo.fe04_commit_rework_submission TO hotelops_rework_submission_writer;
GRANT VIEW DEFINITION ON dbo.fe04_commit_rework_submission TO hotelops_rework_submission_writer;
EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReworkSubmission.V1',@value=N'FE04_REWORK_SUBMISSION_V1';
