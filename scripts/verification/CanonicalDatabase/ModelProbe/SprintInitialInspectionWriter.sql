-- Disposable trial only. No shared/production installation or new business schema.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR DB_NAME()<>'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>'meita'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51999,'Exact disposable caller transaction required',1;
DECLARE @commit varchar(40)=CONVERT(varchar(40),SESSION_CONTEXT(N'DEV3.SourceCommit'));
IF @commit IS NULL OR LEN(@commit)<>40 OR @commit COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9a-f]%'
 OR OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
 OR OBJECT_ID('dbo.fe05_assign_initial_inspection','P') IS NOT NULL
 OR DATABASE_PRINCIPAL_ID('hotelops_initial_inspection_writer') IS NOT NULL
 THROW 51999,'Actual source commit and empty owned writer baseline required',1;
IF COL_LENGTH('dbo.room_inspections','row_version') IS NULL OR COL_LENGTH('dbo.room_inspections','due_at') IS NULL
 OR COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL OR COL_LENGTH('dbo.room_types','default_checklist_id') IS NULL
 OR OBJECT_ID('dbo.notification_operation_reservations','U') IS NULL OR OBJECT_ID('dbo.room_cleaning_cycle_state','U') IS NULL
 THROW 51999,'Existing schema compatibility required',1;
CREATE TABLE dbo.fe04_rework_authority_issuers(
 issuer varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,version int NOT NULL,writer_object_id int NOT NULL,
 source_commit char(40) COLLATE Latin1_General_100_BIN2 NOT NULL,module_digest binary(32) NOT NULL,is_active bit NOT NULL,
 PRIMARY KEY(issuer,version));
CREATE ROLE hotelops_initial_inspection_writer;
EXEC(N'CREATE PROCEDURE dbo.fe05_assign_initial_inspection
 @actor bigint,@branch bigint,@task bigint,@inspector bigint,@version bigint,@taskRevision binary(8),@cycleRevision binary(8),
 @due datetime2(7),@reason nvarchar(500),@operation uniqueidentifier,@fingerprint varbinary(max)
WITH EXECUTE AS OWNER AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51999,''Serializable assignment required'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
  WHERE x.issuer=''FE05.UC041.INITIAL'' AND x.version=1 AND x.writer_object_id=@@PROCID AND x.is_active=1
   AND HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51999,''Registered assignment writer required'',1;
 DECLARE @now datetime2(7)=SYSUTCDATETIME(),@room bigint;
 IF @actor<=0 OR @branch<=0 OR @task<=0 OR @inspector<=0 OR @version<=0 OR @operation IS NULL
  OR ISNULL(DATALENGTH(@fingerprint),0)<>32 OR @due IS NULL OR @due<=@now OR NULLIF(LTRIM(RTRIM(@reason)),N'''') IS NULL
  THROW 51999,''Invalid initial assignment'',1;
 SELECT @room=r.room_id FROM dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.rooms r ON r.room_id=t.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.branches b ON b.branch_id=r.branch_id AND b.is_active=1
 JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.is_active=1 LEFT JOIN dbo.areas a ON a.area_id=r.area_id
 JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id AND c.row_version=@cycleRevision
 JOIN dbo.room_types rt ON rt.room_type_id=r.room_type_id AND rt.is_active=1 AND(rt.branch_id=@branch OR rt.branch_id IS NULL)
 JOIN dbo.inspection_checklists cl ON cl.checklist_id=rt.default_checklist_id AND cl.is_active=1
  AND(cl.branch_id=@branch OR cl.branch_id IS NULL) AND(cl.room_type_id IS NULL OR cl.room_type_id=r.room_type_id)
 JOIN dbo.inspection_checklist_versions v ON v.checklist_id=cl.checklist_id AND v.version_id=@version AND v.status=''PUBLISHED'' AND(v.effective_at IS NULL OR v.effective_at<=@now)
 WHERE t.cleaning_task_id=@task AND t.row_version=@taskRevision AND t.workflow_status=''PENDING_INSPECTION'' AND t.status=''IN_PROGRESS''
  AND(r.area_id IS NULL OR a.is_active=1)
  AND EXISTS(SELECT 1 FROM dbo.inspection_checklist_items WHERE version_id=v.version_id AND is_active=1)
  AND NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_versions newer WHERE newer.checklist_id=v.checklist_id AND newer.version_no>v.version_no
    AND newer.status=''PUBLISHED'' AND(newer.effective_at IS NULL OR newer.effective_at<=@now)
    AND EXISTS(SELECT 1 FROM dbo.inspection_checklist_items WHERE version_id=newer.version_id AND is_active=1));
 IF @room IS NULL OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WHERE cleaning_task_id=@task)
  OR EXISTS(SELECT 1 FROM dbo.room_inspections WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id=@task AND status NOT IN(''COMPLETED'',''CANCELLED''))
  THROW 51999,''Exact current unassigned cleaning cycle required'',1;
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
 INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,due_at,created_at)
 VALUES(''INSP-''+REPLACE(CONVERT(varchar(36),NEWID()),''-'',''''),@room,@task,@version,@inspector,''PENDING'',''ASSIGNED'',@due,@now);
 DECLARE @inspection bigint=SCOPE_IDENTITY(),@tag varchar(18),@receipt nvarchar(max);
 SELECT @tag=CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) FROM dbo.room_inspections WHERE inspection_id=@inspection;
 SET @receipt=(SELECT ''FE05_INITIAL_INSPECTION_ASSIGNMENT_V1'' AS contract,''FE05.UC041.INITIAL'' AS issuer,1 AS issuerVersion,
  @actor AS actorUserId,@branch AS branchId,@inspection AS inspectionId,@operation AS operationId,CURRENT_TRANSACTION_ID() AS writerTransactionId,@reason AS reason,@due AS dueAt,
  JSON_QUERY((SELECT @inspection AS id,@task AS taskId,@branch AS branchId,@room AS roomId,@version AS versionId,@inspector AS inspectorId,
    ''ASSIGNED'' AS state,@tag AS sourceETag,@operation AS operationId,''PENDING_DELIVERY'' AS notification,
    ''FE05_INITIAL_INSPECTION_ASSIGNMENT_V1'' AS contract FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS entity,
  JSON_QUERY((SELECT 1 AS schemaVersion,''INSPECTION_ASSIGNED'' AS kind,@inspector AS recipientUserId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS notification
  FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
 VALUES(@actor,@operation,1,@fingerprint,1,@receipt,@now);
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
 VALUES(@branch,@actor,''INSPECTION_ASSIGNMENT'',CONVERT(nvarchar(50),@inspection),''CREATE'',@receipt,@now);
END');
INSERT dbo.fe04_rework_authority_issuers(issuer,version,writer_object_id,source_commit,module_digest,is_active)
 SELECT 'FE05.UC041.INITIAL',1,object_id,@commit,HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),1
 FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_assign_initial_inspection');
IF @@ROWCOUNT<>1 THROW 51999,'Initial writer registration failed',1;
GRANT SELECT ON dbo.fe04_rework_authority_issuers TO hotelops_initial_inspection_writer;
DENY INSERT,UPDATE,DELETE ON dbo.fe04_rework_authority_issuers TO hotelops_initial_inspection_writer;
DENY INSERT,UPDATE,DELETE ON dbo.notification_operation_reservations TO hotelops_initial_inspection_writer;
GRANT EXECUTE ON dbo.fe05_assign_initial_inspection TO hotelops_initial_inspection_writer;
GRANT VIEW DEFINITION ON dbo.fe05_assign_initial_inspection TO hotelops_initial_inspection_writer;
