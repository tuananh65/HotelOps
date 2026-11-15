-- Called after SprintReinspectionWorkflow.sql in its guarded disposable transaction.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR DB_NAME()<>'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>'meita'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Reinspection.V1' AND CONVERT(nvarchar(128),value)='FE05_REINSPECTION_V1')
 THROW 51999,'Guarded disposable reinspection transaction required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'DEV3.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 THROW 51999,'Actual source commit required',1;
EXEC(N'CREATE PROCEDURE dbo.fe05_bind_reinspection
 @actor bigint,@branch bigint,@rework bigint,@inspection bigint,@operation uniqueidentifier,@fingerprint varbinary(max),@submission uniqueidentifier
WITH EXECUTE AS OWNER AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51999,''Serializable reinspection assignment required'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
  WHERE x.issuer=''FE05.UC041'' AND x.version=1 AND x.writer_object_id=@@PROCID AND x.is_active=1
   AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51999,''Registered assignment writer required'',1;
 IF @actor<=0 OR @branch<=0 OR @rework<=0 OR @inspection<=0 OR @operation IS NULL OR ISNULL(DATALENGTH(@fingerprint),0)<>32
  THROW 51999,''Assignment parameters invalid'',1;
 DECLARE @task bigint,@room bigint,@original bigint,@version bigint,@inspector bigint,@executor bigint,@cycle binary(8),@revision binary(8),@now datetime2(7)=SYSUTCDATETIME();
 SELECT @task=w.cleaning_task_id,@room=t.room_id,@original=w.inspection_id,@version=i.version_id,@inspector=i.inspector_user_id,
  @executor=w.assignee_user_id,@cycle=c.row_version,@revision=w.row_version
 FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id AND t.workflow_status=''REWORK_REQUIRED'' AND t.status=''IN_PROGRESS''
 JOIN dbo.rooms r ON r.room_id=t.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
 JOIN dbo.room_inspections original ON original.inspection_id=w.inspection_id AND original.cleaning_task_id=t.cleaning_task_id AND original.room_id=r.room_id
  AND original.workflow_status=''FAILED'' AND original.result=''FAIL'' AND original.status=''COMPLETED''
 JOIN dbo.room_inspections i ON i.inspection_id=@inspection AND i.inspection_id<>original.inspection_id AND i.cleaning_task_id=t.cleaning_task_id AND i.room_id=r.room_id
  AND i.version_id=original.version_id AND i.workflow_status=''ASSIGNED'' AND i.status=''PENDING'' AND i.result IS NULL AND i.due_at>@now
 JOIN dbo.inspection_checklist_versions v ON v.version_id=i.version_id AND v.status=''PUBLISHED'' AND (v.effective_at IS NULL OR v.effective_at<=@now)
 JOIN dbo.notification_operation_reservations o ON o.actor_user_id=w.assignee_user_id AND o.operation_key=@submission AND o.completed=1
 CROSS APPLY(SELECT CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N''[]'' END AS receipt) j
 WHERE w.rework_id=@rework AND w.workflow_status=''PENDING_REINSPECTION'' AND w.status=''IN_PROGRESS'' AND w.completed_at IS NULL
  AND JSON_VALUE(j.receipt,''$.contract'')=''FE04_REWORK_SUBMISSION_V1'' AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,''$.reworkId''))=w.rework_id
  AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,''$.branchId''))=@branch AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,''$.writerTransactionId''))<>CURRENT_TRANSACTION_ID();
 IF @task IS NULL OR @inspector=@executor OR (SELECT COUNT(*) FROM dbo.room_inspections WHERE cleaning_task_id=@task AND status NOT IN(''COMPLETED'',''CANCELLED''))<>1
  OR EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id
    WHERE b.rework_id=@rework AND i.status<>''CANCELLED'')
  THROW 51999,''Exact current submitted rework assignment required'',1;
 IF EXISTS(SELECT 1 FROM dbo.cleaning_assignments WHERE cleaning_task_id=@task AND assignee_user_id=@inspector AND assignment_status<>''CANCELLED'')
  OR EXISTS(SELECT 1 FROM dbo.cleaning_progress_logs WHERE cleaning_task_id=@task AND logged_by=@inspector)
  OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WHERE cleaning_task_id=@task AND assignee_user_id=@inspector)
  THROW 51999,''Inspector cleaner conflict'',1;
 INSERT dbo.fe05_reinspection_bindings_v1(inspection_id,rework_id,original_inspection_id,cleaning_task_id,room_id,branch_id,version_id,
  cycle_revision,rework_revision,submission_actor,submission_operation,reinspector_user_id,bound_by,bound_at,touched_at)
 VALUES(@inspection,@rework,@original,@task,@room,@branch,@version,@cycle,@revision,@executor,@submission,@inspector,@actor,@now,@now);
 INSERT dbo.fe05_reinspection_defect_results_v1(inspection_id,rework_id,defect_id)
 SELECT @inspection,@rework,defect_id FROM dbo.cleaning_rework_defects WHERE rework_id=@rework;
 IF @@ROWCOUNT NOT BETWEEN 1 AND 200 THROW 51999,''Exact submitted defects required'',1;
 DECLARE @tag varchar(18)=(SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) FROM dbo.room_inspections WHERE inspection_id=@inspection),@receipt nvarchar(max);
 SET @receipt=(SELECT ''FE05_REINSPECTION_ASSIGNMENT_V1'' AS contract,''FE05.UC041'' AS issuer,1 AS issuerVersion,
  @actor AS actorUserId,@branch AS branchId,@inspection AS inspectionId,@rework AS reworkId,@operation AS operationId,CURRENT_TRANSACTION_ID() AS writerTransactionId,
  JSON_QUERY((SELECT @inspection AS id,@rework AS reworkId,@branch AS branchId,''ASSIGNED'' AS state,@tag AS sourceETag,@operation AS operationId,
    ''PENDING_DELIVERY'' AS notification,''FE05_REINSPECTION_ASSIGNMENT_V1'' AS contract FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS entity,
  JSON_QUERY((SELECT 1 AS schemaVersion,''REINSPECTION_ASSIGNED'' AS kind,@inspector AS recipientUserId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@fingerprint,1,@receipt,@now);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
 VALUES(@branch,@actor,''REINSPECTION'',CONVERT(nvarchar(50),@inspection),''CREATE'',@receipt,@now);
END');
EXEC(N'CREATE PROCEDURE dbo.fe05_commit_reinspection
 @actor bigint,@branch bigint,@inspection bigint,@operation uniqueidentifier,@fingerprint varbinary(max),@entity nvarchar(max)
WITH EXECUTE AS OWNER AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51999,''Serializable reinspection conclusion required'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
  WHERE x.issuer=''FE05.UC047'' AND x.version=1 AND x.writer_object_id=@@PROCID AND x.is_active=1
   AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51999,''Registered conclusion writer required'',1;
 IF @actor<=0 OR @branch<=0 OR @inspection<=0 OR @operation IS NULL OR ISNULL(DATALENGTH(@fingerprint),0)<>32 OR ISNULL(ISJSON(@entity),0)<>1
  THROW 51999,''Conclusion parameters invalid'',1;
 DECLARE @rework bigint,@task bigint,@room bigint,@executor bigint,@state varchar(30),@reworkState varchar(30),@ended datetime2(7),@inspectionTag varchar(18),@reworkTag varchar(18);
 SELECT @rework=w.rework_id,@task=t.cleaning_task_id,@room=r.room_id,@executor=w.assignee_user_id,@state=i.workflow_status,
  @reworkState=w.workflow_status,@ended=i.inspected_at,@inspectionTag=CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),i.row_version),2)+CHAR(34),@reworkTag=CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),w.row_version),2)+CHAR(34)
 FROM dbo.fe05_reinspection_bindings_v1 b WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.fe05_reinspection_responsibilities_v1 authority ON authority.inspection_id=b.inspection_id
 JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id=authority.inspector_user_id AND i.version_id=b.version_id
  AND i.room_id=b.room_id AND i.cleaning_task_id=b.cleaning_task_id AND i.status=''COMPLETED'' AND i.inspected_at IS NOT NULL
 JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.inspection_id=b.original_inspection_id AND w.cleaning_task_id=b.cleaning_task_id
  AND w.status=''COMPLETED'' AND w.completed_at=i.inspected_at AND w.assignee_user_id=b.submission_actor
 JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id AND t.room_id=b.room_id
 JOIN dbo.rooms r ON r.room_id=b.room_id AND r.branch_id=b.branch_id AND r.is_active=1
 JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision
 WHERE b.inspection_id=@inspection AND b.branch_id=@branch AND authority.inspector_user_id=@actor AND w.assignee_user_id<>@actor
  AND ((i.workflow_status=''PASSED'' AND i.result=''PASS'' AND w.workflow_status=''COMPLETED'' AND t.workflow_status=''COMPLETED'' AND t.status=''COMPLETED'')
   OR(i.workflow_status=''FAILED'' AND i.result=''FAIL'' AND w.workflow_status=''FAILED'' AND t.workflow_status=''REWORK_REQUIRED'' AND t.status=''IN_PROGRESS''));
 IF @rework IS NULL OR (SELECT COUNT(*) FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection) NOT BETWEEN 1 AND 200
  OR EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection AND(fixed IS NULL OR recorded_by<>@actor OR recorded_at<>@ended))
  OR(@state=''PASSED'' AND EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection AND fixed=0))
  OR(@state=''FAILED'' AND NOT EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection AND fixed=0))
  THROW 51999,''Persisted complete reinspection outcome required'',1;
 IF EXISTS(SELECT defect_id FROM dbo.cleaning_rework_defects WHERE rework_id=@rework
   EXCEPT SELECT defect_id FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection AND rework_id=@rework)
  OR EXISTS(SELECT defect_id FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection
   EXCEPT SELECT defect_id FROM dbo.cleaning_rework_defects WHERE rework_id=@rework)
  OR (SELECT COUNT(*) FROM dbo.inspection_defects WHERE inspection_id=@inspection)
    <>(SELECT COUNT(*) FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=@inspection AND fixed=0)
  OR EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 x JOIN dbo.inspection_defects original ON original.defect_id=x.defect_id
    WHERE x.inspection_id=@inspection AND x.fixed=0 AND NOT EXISTS(SELECT 1 FROM dbo.inspection_defects d
      JOIN dbo.inspection_results r ON r.inspection_id=d.inspection_id AND r.checklist_item_id=d.checklist_item_id AND r.outcome=''FAIL''
      WHERE d.inspection_id=@inspection AND d.checklist_item_id=original.checklist_item_id AND d.description=original.description AND d.status=''OPEN''))
  THROW 51999,''Exact selected results and remaining defects required'',1;
 DECLARE @readiness varchar(30)=CASE WHEN EXISTS(SELECT 1 FROM dbo.room_blocks WITH(HOLDLOCK) WHERE room_id=@room
   AND blocked_at<=@ended AND(released_at IS NULL OR released_at>@ended)) THEN ''BLOCKED''
   WHEN @state=''PASSED'' THEN ''READY'' ELSE ''REWORK_REQUIRED'' END;
 IF ISNULL(JSON_VALUE(@entity,''$.effectiveReadiness''),'''')<>@readiness
  OR ISNULL(JSON_VALUE(@entity,''$.messageCode''),'''')<>CASE WHEN @readiness=''BLOCKED'' THEN ''MSG17'' ELSE ''MSG08'' END
  OR ISNULL(JSON_VALUE(@entity,''$.notification''),'''')<>''PENDING_DELIVERY''
  OR(@readiness=''READY'' AND NOT EXISTS(SELECT 1 FROM dbo.rooms WHERE room_id=@room AND operational_status=''READY''))
  THROW 51999,''Reinspection effective readiness receipt mismatch'',1;
 IF ISNULL(JSON_VALUE(@entity,''$.contract''),'''')<>''FE05_REINSPECTION_CONCLUSION_V1''
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.id'')),0)<>@inspection OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.reworkId'')),0)<>@rework
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.branchId'')),0)<>@branch OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.roomId'')),0)<>@room
  OR ISNULL(TRY_CONVERT(bigint,JSON_VALUE(@entity,''$.cleaningTaskId'')),0)<>@task
  OR ISNULL(JSON_VALUE(@entity,''$.state''),'''')<>@state OR ISNULL(JSON_VALUE(@entity,''$.reworkState''),'''')<>@reworkState
  OR ISNULL(JSON_VALUE(@entity,''$.sourceETag''),'''')<>@inspectionTag OR ISNULL(JSON_VALUE(@entity,''$.reworkETag''),'''')<>@reworkTag
  OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.operationId'')) IS NULL OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(@entity,''$.operationId''))<>@operation
  THROW 51999,''Conclusion receipt does not match persisted outcome'',1;
 DECLARE @receipt nvarchar(max);
 SET @receipt=(SELECT ''FE05_REINSPECTION_CONCLUSION_V1'' AS contract,''FE05.UC047'' AS issuer,1 AS issuerVersion,
  @actor AS actorUserId,@branch AS branchId,@inspection AS inspectionId,@rework AS reworkId,@operation AS operationId,CURRENT_TRANSACTION_ID() AS writerTransactionId,
  JSON_QUERY(@entity) AS entity,
  JSON_QUERY((SELECT 1 AS schemaVersion,CASE WHEN @state=''PASSED'' THEN ''REINSPECTION_PASSED'' ELSE ''REINSPECTION_FAILED'' END AS kind,
    @executor AS recipientUserId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@fingerprint,1,@receipt,@ended);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
 VALUES(@branch,@actor,''REINSPECTION'',CONVERT(nvarchar(50),@inspection),''UPDATE'',@receipt,@ended);
END');
INSERT dbo.fe04_rework_authority_issuers(issuer,version,writer_object_id,source_commit,module_digest,is_active)
 SELECT CASE WHEN object_id=OBJECT_ID('dbo.fe05_bind_reinspection') THEN 'FE05.UC041' ELSE 'FE05.UC047' END,
  1,object_id,@commit,HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),1
 FROM sys.sql_modules WHERE object_id IN(OBJECT_ID('dbo.fe05_bind_reinspection'),OBJECT_ID('dbo.fe05_commit_reinspection'));
IF @@ROWCOUNT<>2 THROW 51999,'Both reinspection writers must be registered',1;
GRANT EXECUTE ON dbo.fe05_bind_reinspection TO hotelops_reinspection_writer;
GRANT EXECUTE ON dbo.fe05_commit_reinspection TO hotelops_reinspection_writer;
GRANT VIEW DEFINITION ON dbo.fe05_bind_reinspection TO hotelops_reinspection_writer;
GRANT VIEW DEFINITION ON dbo.fe05_commit_reinspection TO hotelops_reinspection_writer;
