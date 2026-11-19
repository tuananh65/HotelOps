-- Approved rework workflow package. Execute only through the pinned transactional installer.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database')) IS NULL
 OR DB_NAME()<>CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database'))
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'ReworkWriter.DatabaseGuid')))
 THROW 51999,'Pinned database transaction required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'ReworkWriter.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 OR OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NULL
 OR OBJECT_ID('dbo.fe05_reassign_inspection','P') IS NOT NULL
 OR DATABASE_PRINCIPAL_ID('hotelops_initial_inspection_writer') IS NULL
 THROW 51999,'Actual source commit and empty owned writer baseline required',1;
IF COL_LENGTH('dbo.room_inspections','row_version') IS NULL OR COL_LENGTH('dbo.room_inspections','due_at') IS NULL
 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL OR COL_LENGTH('dbo.room_types','default_checklist_id') IS NULL
 OR OBJECT_ID('dbo.notification_operation_reservations','U') IS NULL OR OBJECT_ID('dbo.room_cleaning_cycle_state','U') IS NULL
 THROW 51999,'Existing schema compatibility required',1;
EXEC(N'CREATE PROCEDURE dbo.fe05_reassign_inspection
 @actor bigint,@branch bigint,@inspection bigint,@inspector bigint,@revision binary(8),@cycleRevision binary(8),
 @reason nvarchar(500),@operation uniqueidentifier,@fingerprint varbinary(max)
WITH EXECUTE AS OWNER AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51999,''Serializable assignment required'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
  WHERE x.issuer=''FE05.UC041.REASSIGN'' AND x.version=1 AND x.writer_object_id=@@PROCID AND x.is_active=1
   AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51999,''Registered assignment writer required'',1;
 DECLARE @now datetime2(7)=SYSUTCDATETIME(),@room bigint,@task bigint,@version bigint,@previous bigint,@state varchar(30),@due datetime2(7);
 IF @actor<=0 OR @branch<=0 OR @inspection<=0 OR @inspector<=0 OR @operation IS NULL
  OR ISNULL(DATALENGTH(@fingerprint),0)<>32 OR NULLIF(LTRIM(RTRIM(@reason)),N'''') IS NULL
  THROW 51999,''Invalid reassignment'',1;
 SELECT @room=i.room_id,@task=i.cleaning_task_id,@version=i.version_id,@previous=i.inspector_user_id,@state=i.workflow_status,@due=i.due_at
 FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
 JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.branches b ON b.branch_id=r.branch_id AND b.is_active=1
 JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.is_active=1 LEFT JOIN dbo.areas a ON a.area_id=r.area_id
 JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id AND c.row_version=@cycleRevision
 WHERE i.inspection_id=@inspection AND i.row_version=@revision AND i.status NOT IN(''COMPLETED'',''CANCELLED'')
  AND i.workflow_status IN(''ASSIGNED'',''INSPECTING'',''PENDING_CONCLUSION'') AND t.workflow_status=''PENDING_INSPECTION'' AND t.status=''IN_PROGRESS''
  AND(r.area_id IS NULL OR a.is_active=1);
 IF @room IS NULL OR @previous=@inspector OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WHERE cleaning_task_id=@task)
  OR (SELECT COUNT(*) FROM dbo.room_inspections WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id=@task AND status NOT IN(''COMPLETED'',''CANCELLED''))<>1
  THROW 51999,''Exact current open initial inspection required'',1;
 IF EXISTS(SELECT 1 FROM(VALUES(@actor),(@inspector)) selected(user_id) WHERE NOT EXISTS(
  SELECT 1 FROM dbo.users u JOIN dbo.user_branch_access ba ON ba.user_id=u.user_id AND ba.branch_id=@branch AND ba.revoked_at IS NULL AND ba.granted_at<=@now
  JOIN dbo.user_roles ur ON ur.user_id=u.user_id AND ur.branch_id=@branch AND ur.is_active=1 AND ur.assigned_at<=@now AND(ur.expires_at IS NULL OR ur.expires_at>@now)
  JOIN dbo.roles r ON r.role_id=ur.role_id AND r.is_active=1 AND r.role_code=''HK_SUPERVISOR''
  WHERE u.user_id=selected.user_id AND u.account_status=''ACTIVE'' AND u.employment_status=''ACTIVE'' AND u.deleted_at IS NULL))
  THROW 51999,''Current branch supervisor eligibility required'',1;
 IF EXISTS(SELECT 1 FROM dbo.cleaning_assignments WHERE cleaning_task_id=@task AND assignee_user_id=@inspector AND assignment_status<>''CANCELLED'')
  OR EXISTS(SELECT 1 FROM dbo.cleaning_progress_logs WHERE cleaning_task_id=@task AND logged_by=@inspector)
  OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WHERE cleaning_task_id=@task AND assignee_user_id=@inspector)
  THROW 51999,''Inspector cleaner conflict'',1;
 UPDATE dbo.room_inspections SET inspector_user_id=@inspector WHERE inspection_id=@inspection AND row_version=@revision;
 IF @@ROWCOUNT<>1 THROW 51999,''Concurrent inspection update'',1;
 DECLARE @tag varchar(18),@receipt nvarchar(max);
 SELECT @tag=CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) FROM dbo.room_inspections WHERE inspection_id=@inspection;
 SET @receipt=(SELECT ''FE05_INSPECTION_REASSIGNMENT_V1'' AS contract,''FE05.UC041.REASSIGN'' AS issuer,1 AS issuerVersion,
  @actor AS actorUserId,@branch AS branchId,@inspection AS inspectionId,@operation AS operationId,CURRENT_TRANSACTION_ID() AS writerTransactionId,@reason AS reason,@due AS dueAt,
  JSON_QUERY((SELECT @inspection AS id,@task AS taskId,@branch AS branchId,@room AS roomId,@version AS versionId,@previous AS previousInspectorId,@inspector AS inspectorId,
    @state AS state,@tag AS sourceETag,@operation AS operationId,''PENDING_DELIVERY'' AS notification,
    ''FE05_INSPECTION_REASSIGNMENT_V1'' AS contract FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS entity,
  JSON_QUERY((SELECT 1 AS schemaVersion,''INSPECTION_ASSIGNED'' AS kind,@inspector AS recipientUserId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@fingerprint,1,@receipt,@now);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
 VALUES(@branch,@actor,''INSPECTION_ASSIGNMENT'',CONVERT(nvarchar(50),@inspection),''UPDATE'',@receipt,@now);
END');
ALTER AUTHORIZATION ON OBJECT::dbo.fe05_reassign_inspection TO hotelops_rework_owner;
INSERT dbo.fe04_rework_authority_issuers(issuer,version,writer_object_id,source_commit,module_digest,is_active)
 SELECT 'FE05.UC041.REASSIGN',1,object_id,@commit,HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),1
 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_reassign_inspection');
IF @@ROWCOUNT<>1 THROW 51999,'Initial writer registration failed',1;
GRANT SELECT ON dbo.fe04_rework_authority_issuers TO hotelops_initial_inspection_writer;
DENY INSERT,UPDATE,DELETE ON dbo.fe04_rework_authority_issuers TO hotelops_initial_inspection_writer;
DENY INSERT,UPDATE,DELETE ON dbo.notification_operation_reservations TO hotelops_initial_inspection_writer;
GRANT EXECUTE ON dbo.fe05_reassign_inspection TO hotelops_initial_inspection_writer;
GRANT VIEW DEFINITION ON dbo.fe05_reassign_inspection TO hotelops_initial_inspection_writer;
