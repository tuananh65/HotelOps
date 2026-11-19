-- Approved rework workflow package. Execute only through the pinned transactional installer.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database')) IS NULL
 OR DB_NAME()<>CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database'))
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'ReworkWriter.DatabaseGuid')))
 THROW 51999,'Pinned database transaction required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'ReworkWriter.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 THROW 51996,'Actual source commit required',1;
IF OBJECT_ID('dbo.fs016_rework_source_proofs') IS NOT NULL
 OR OBJECT_ID('dbo.fe05_issue_rework_authority') IS NOT NULL OR DATABASE_PRINCIPAL_ID('hotelops_rework_writer') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.ReworkAuthority.V1')
 THROW 51996,'No overwrite of existing authority objects',1;
IF OBJECT_ID('dbo.notification_operation_reservations','U') IS NULL OR COL_LENGTH('dbo.cleaning_reworks','row_version')<>8
 THROW 51996,'Existing operation ledger and lifecycle schema required',1;
CREATE TABLE dbo.fs016_rework_source_proofs(
 rework_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.cleaning_reworks(rework_id),
 inspection_id bigint NOT NULL REFERENCES dbo.room_inspections(inspection_id),cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 room_id bigint NOT NULL REFERENCES dbo.rooms(room_id),branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 version_id bigint NOT NULL REFERENCES dbo.inspection_checklist_versions(version_id),inspection_revision binary(8) NOT NULL,
 source_digest binary(32) NOT NULL,evidence_digest binary(32) NOT NULL,
 origin varchar(30) NOT NULL CONSTRAINT CK_fe04_authority_origin CHECK(origin='FE05_COMMITTED_V1'),
 contract_version int NOT NULL CHECK(contract_version=1),issuer varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,issuer_version int NOT NULL,
 actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),operation_key uniqueidentifier NOT NULL,writer_transaction_id bigint NOT NULL CHECK(writer_transaction_id>0),
 CONSTRAINT FK_fe04_authority_issuer FOREIGN KEY(issuer,issuer_version) REFERENCES dbo.fe04_rework_authority_issuers(issuer,version),
 CONSTRAINT FK_fe04_authority_operation FOREIGN KEY(actor_user_id,operation_key) REFERENCES dbo.notification_operation_reservations(actor_user_id,operation_key));
EXEC(N'CREATE PROCEDURE dbo.fe05_issue_rework_authority @actor bigint,@branch bigint,@rework bigint,@operation uniqueidentifier,
 @source_digest varbinary(max),@evidence_digest varbinary(max)
WITH EXECUTE AS OWNER AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51996,''Caller SERIALIZABLE transaction required'',1;
 IF @actor<=0 OR @branch<=0 OR @rework<=0 OR @operation IS NULL OR @operation=''00000000-0000-0000-0000-000000000000''
  OR @source_digest IS NULL OR DATALENGTH(@source_digest)<>32 OR @evidence_digest IS NULL OR DATALENGTH(@evidence_digest)<>32
  THROW 51996,''Authority input invalid'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
   WHERE x.issuer=''FE05.UC045.UC046'' AND x.version=1 AND x.is_active=1 AND x.writer_object_id=@@PROCID
   AND m.execute_as_principal_id=-2 AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51996,''Issuer registration invalid'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id
   JOIN dbo.roles role ON role.role_id=ur.role_id JOIN dbo.role_permissions rp ON rp.role_id=role.role_id
   JOIN dbo.permissions p ON p.permission_id=rp.permission_id JOIN dbo.branches b ON b.branch_id=@branch
   WHERE u.user_id=@actor AND u.account_status=''ACTIVE'' AND u.employment_status=''ACTIVE'' AND u.deleted_at IS NULL AND b.is_active=1
   AND ur.is_active=1 AND role.is_active=1 AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
   AND p.permission_code=''INSPECTION_PERFORM'' AND ((ur.branch_id IS NULL AND rp.data_scope=''ALL'') OR
    (ur.branch_id=@branch AND rp.data_scope IN(''ALL'',''BRANCH'') AND EXISTS(SELECT 1 FROM dbo.user_branch_access a
      WHERE a.user_id=u.user_id AND a.branch_id=@branch AND a.revoked_at IS NULL AND a.granted_at<=SYSUTCDATETIME()))))
  THROW 51996,''Authority actor denied'',1;
 DECLARE @inspection bigint,@task bigint,@room bigint,@version bigint,@revision binary(8),@now datetime2(7)=SYSUTCDATETIME();
 SELECT @inspection=i.inspection_id,@task=w.cleaning_task_id,@room=i.room_id,@version=i.version_id,@revision=i.row_version
 FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK) JOIN dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK) ON i.inspection_id=w.inspection_id
 JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id AND t.room_id=i.room_id
 JOIN dbo.rooms r WITH(UPDLOCK,HOLDLOCK) ON r.room_id=i.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
 WHERE w.rework_id=@rework AND w.workflow_status=''PENDING_ACCEPTANCE'' AND w.status=''PENDING'' AND w.assignee_user_id IS NOT NULL
 AND w.due_at>@now AND w.requested_at>=i.inspected_at AND i.inspected_at IS NOT NULL
 AND i.cleaning_task_id=t.cleaning_task_id AND i.workflow_status=''FAILED'' AND i.status=''COMPLETED'' AND i.result=''FAIL''
 AND t.workflow_status=''REWORK_REQUIRED'' AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WHERE newer.cleaning_task_id=t.cleaning_task_id AND newer.inspection_id>i.inspection_id);
 IF @inspection IS NULL OR (SELECT COUNT(*) FROM dbo.cleaning_reworks WHERE inspection_id=@inspection AND status NOT IN(''COMPLETED'',''CANCELLED''))<>1
  THROW 51996,''Current failed source required'',1;
 IF (SELECT COUNT(*) FROM dbo.cleaning_rework_defects WHERE rework_id=@rework) NOT BETWEEN 1 AND 200
  OR EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects l LEFT JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id
   LEFT JOIN dbo.inspection_checklist_items c ON c.item_id=d.checklist_item_id
   LEFT JOIN dbo.inspection_results r ON r.inspection_id=d.inspection_id AND r.checklist_item_id=d.checklist_item_id
   WHERE l.rework_id=@rework AND (d.inspection_id IS NULL OR d.inspection_id<>@inspection OR c.version_id IS NULL OR c.version_id<>@version OR r.outcome IS NULL OR r.outcome<>''FAIL''))
  THROW 51996,''Selected failed defects required'',1;
 IF EXISTS(SELECT 1 FROM dbo.fs016_rework_source_proofs WHERE rework_id=@rework)
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.fs016_rework_source_proofs WHERE rework_id=@rework AND actor_user_id=@actor AND operation_key=@operation
    AND source_digest=@source_digest AND evidence_digest=@evidence_digest AND inspection_revision=@revision) RETURN;
  THROW 51996,''Authority occurrence conflict'',1;
 END;
 DECLARE @writer bigint=CURRENT_TRANSACTION_ID(),@receipt nvarchar(max),@audit nvarchar(max);
 SET @receipt=(SELECT ''FE05_REWORK_AUTHORITY_V1'' AS contract,''FE05_COMMITTED_V1'' AS origin,1 AS version,''FE05.UC045.UC046'' AS issuer,1 AS issuerVersion,
 @actor AS actorUserId,@operation AS operationId,@writer AS writerTransactionId,@rework AS reworkId,@inspection AS inspectionId,@task AS cleaningTaskId,
 @room AS roomId,@branch AS branchId,@version AS versionId,CHAR(34)+CONVERT(varchar(16),@revision,2)+CHAR(34) AS inspectionETag,
 CONVERT(varchar(64),@source_digest,2) AS sourceDigest,CONVERT(varchar(64),@evidence_digest,2) AS evidenceDigest,''FAILED'' AS conclusion
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@source_digest,1,@receipt,@now);
 INSERT dbo.fs016_rework_source_proofs(rework_id,inspection_id,cleaning_task_id,room_id,branch_id,version_id,inspection_revision,source_digest,evidence_digest,
 origin,contract_version,issuer,issuer_version,actor_user_id,operation_key,writer_transaction_id)
 VALUES(@rework,@inspection,@task,@room,@branch,@version,@revision,@source_digest,@evidence_digest,''FE05_COMMITTED_V1'',1,''FE05.UC045.UC046'',1,@actor,@operation,@writer);
 SET @audit=(SELECT @operation AS operationId,''FE05_REWORK_AUTHORITY_V1'' AS authorityContract,CONVERT(varchar(64),@source_digest,2) AS sourceDigest,
 CONVERT(varchar(64),@evidence_digest,2) AS evidenceDigest,@writer AS authorityWriterTransactionId,
 JSON_QUERY((SELECT 1 AS schemaVersion,''REWORK_ASSIGNED'' AS kind,w.assignee_user_id AS recipientUserId FROM dbo.cleaning_reworks w WHERE w.rework_id=@rework FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification,
 JSON_QUERY((SELECT w.rework_id AS id,''PENDING_ACCEPTANCE'' AS state,w.assignee_user_id AS assigneeId,w.due_at AS dueAt,w.reason,
 JSON_QUERY((SELECT defect_id AS defectId FROM dbo.cleaning_rework_defects WHERE rework_id=@rework ORDER BY defect_id FOR JSON PATH)) AS defects
 FROM dbo.cleaning_reworks w WHERE w.rework_id=@rework FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS entity FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
 VALUES(@branch,@actor,''cleaning_reworks'',CONVERT(nvarchar(50),@rework),''CREATE'',@audit,@now);
END');

ALTER AUTHORIZATION ON OBJECT::dbo.fe05_issue_rework_authority TO hotelops_rework_owner;
INSERT dbo.fe04_rework_authority_issuers(issuer,version,writer_object_id,source_commit,module_digest,is_active)
 SELECT 'FE05.UC045.UC046',1,object_id,@commit,HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),1
 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_issue_rework_authority');
IF @@ROWCOUNT<>1 THROW 51996,'Issuer registration failed',1;
EXEC(N'CREATE TRIGGER dbo.TR_fe04_authority_immutable ON dbo.fs016_rework_source_proofs AFTER UPDATE,DELETE AS BEGIN THROW 51996,''Rework authority is immutable'',1; END');
CREATE ROLE hotelops_rework_writer;
GRANT EXECUTE ON dbo.fe05_issue_rework_authority TO hotelops_rework_writer;
GRANT VIEW DEFINITION ON dbo.fe05_issue_rework_authority TO hotelops_rework_writer;
GRANT SELECT ON dbo.fe04_rework_authority_issuers TO hotelops_rework_writer;
GRANT SELECT ON dbo.fs016_rework_source_proofs TO hotelops_rework_writer;
DENY INSERT,UPDATE,DELETE ON dbo.fs016_rework_source_proofs TO hotelops_rework_pipeline;
DENY INSERT,UPDATE,DELETE ON dbo.fe04_rework_authority_issuers TO hotelops_rework_pipeline;
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.ReworkAuthority.V1',@value=N'FE05_COMMITTED_V1';
