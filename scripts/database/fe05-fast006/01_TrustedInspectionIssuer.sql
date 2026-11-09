-- FE05 candidate only. No grants, issuer registration, activation, table or ledger creation.
-- Deploy only through a separately reviewed installation. This file never commits domain work.
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')
 THROW 51996,'Owned isolated FE05 trial required; shared deployment not authorized',1;
IF OBJECT_ID(N'dbo.fe05_inspection_issuer_v1',N'P') IS NOT NULL
 THROW 51996,'No overwrite: inspect installed module and its registered hash first',1;
GO
CREATE PROCEDURE dbo.fe05_inspection_issuer_v1
 @action varchar(24),@actor bigint,@session bigint,@session_hash varchar(71),@session_created datetime2(7),
 @branch bigint,@inspection bigint,@expected binary(8),@operation uniqueidentifier,@fingerprint binary(32),
 @reason nvarchar(max)=NULL,@confirmed bit=0,@assignee bigint=NULL,@due datetime2(7)=NULL,
 @selected nvarchar(max)=NULL,@rework bigint=NULL,@source_digest binary(32)=NULL,@evidence_digest binary(32)=NULL
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 OR XACT_STATE()<>1
  OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE session_id=@@SPID AND transaction_isolation_level=4)
  THROW 51996,'FE05_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED',1;
 IF @action IS NULL OR @action NOT IN('VALIDATE_REJECT','REJECT','REWORK','COMPLETE_REJECT','COMPLETE_REWORK')
  OR @actor<=0 OR @branch<=0 OR @inspection<=0 OR @session<=0 OR @expected IS NULL
  OR @operation IS NULL OR @operation='00000000-0000-0000-0000-000000000000' OR @fingerprint IS NULL
  THROW 51996,'FE05_COMMAND_INVALID',1;
 DECLARE @now datetime2(7)=SYSUTCDATETIME(),@room bigint,@task bigint,@version bigint,@inspector bigint,
  @state varchar(40),@status varchar(30),@result varchar(10),@revision binary(8),@inspected datetime2(7);
 -- Same account/session/grant predicates as SqlSessionPolicy and PermissionGrantRules.
 IF NOT EXISTS(SELECT 1 FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE user_id=@actor AND account_status='ACTIVE'
  AND deleted_at IS NULL AND temporary_password_expires_at IS NULL)
  THROW 51996,'FE05_SESSION_INVALID',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.user_sessions WITH(UPDLOCK,HOLDLOCK) WHERE session_id=@session AND user_id=@actor
  AND session_key_hash COLLATE Latin1_General_100_BIN2=@session_hash COLLATE Latin1_General_100_BIN2 AND created_at=@session_created AND revoked_at IS NULL
  AND created_at<=@now AND expires_at>@now AND expires_at>created_at
  AND @now<DATEADD(hour,12,created_at) AND expires_at<=DATEADD(hour,12,created_at)
  AND (last_used_at IS NULL OR last_used_at BETWEEN created_at AND expires_at))
  THROW 51996,'FE05_SESSION_INVALID',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id=@branch AND is_active=1)
  THROW 51996,'FE05_BRANCH_FORBIDDEN',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.user_roles ur WITH(HOLDLOCK)
  JOIN dbo.roles ro WITH(HOLDLOCK) ON ro.role_id=ur.role_id
  JOIN dbo.role_permissions rp WITH(HOLDLOCK) ON rp.role_id=ur.role_id
  JOIN dbo.permissions p WITH(HOLDLOCK) ON p.permission_id=rp.permission_id
  WHERE ur.user_id=@actor AND ur.is_active=1 AND ro.is_active=1 AND ur.assigned_at<=@now
   AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND p.permission_code='INSPECTION_PERFORM'
   AND ((rp.data_scope='ALL' AND ur.branch_id IS NULL)
    OR (rp.data_scope IN('ALL','BRANCH') AND ur.branch_id=@branch AND EXISTS(
      SELECT 1 FROM dbo.user_branch_access a WITH(HOLDLOCK) WHERE a.user_id=@actor AND a.branch_id=@branch
       AND a.revoked_at IS NULL AND a.granted_at<=@now))))
  THROW 51996,'FE05_PERMISSION_FORBIDDEN',1;
 IF ISNULL(APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction'),'NoLock')<>'Exclusive'
  THROW 51996,'FE05_OWNER_CATALOG_LOCK_REQUIRED',1;
 SELECT @room=i.room_id,@task=i.cleaning_task_id,@version=i.version_id,@inspector=i.inspector_user_id,
  @state=i.workflow_status,@status=i.status,@result=i.result,@revision=i.row_version,@inspected=i.inspected_at
 FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id AND r.branch_id=@branch AND r.is_active=1
 JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=r.floor_id AND f.branch_id=r.branch_id AND f.is_active=1
 WHERE i.inspection_id=@inspection AND (r.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas a WITH(HOLDLOCK)
  WHERE a.area_id=r.area_id AND a.branch_id=r.branch_id AND a.is_active=1 AND (a.floor_id IS NULL OR a.floor_id=r.floor_id)));
 IF @room IS NULL OR @task IS NULL THROW 51996,'FE05_SOURCE_UNAVAILABLE',1;
 IF @revision<>@expected THROW 51996,'FE05_REVISION_STALE',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id=@task AND room_id=@room)
  OR NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id=@room AND cleaning_task_id=@task)
  OR EXISTS(SELECT 1 FROM dbo.room_inspections WITH(HOLDLOCK) WHERE cleaning_task_id=@task AND inspection_id>@inspection)
  THROW 51996,'FE05_CURRENT_CYCLE_REQUIRED',1;
 IF @action IN('VALIDATE_REJECT','REJECT')
 BEGIN
  IF @inspector<>@actor THROW 51996,'FE05_ASSIGNED_INSPECTOR_REQUIRED',1;
  IF ISNULL(@state,'')<>'PENDING_CONCLUSION' OR @status<>'IN_PROGRESS' OR @result IS NOT NULL
   OR NOT EXISTS(SELECT 1 FROM dbo.cleaning_tasks WITH(HOLDLOCK) WHERE cleaning_task_id=@task AND workflow_status='PENDING_INSPECTION' AND status='IN_PROGRESS')
   THROW 51996,'FE05_PENDING_CONCLUSION_REQUIRED',1;
  IF ISNULL(@confirmed,0)<>1 OR @reason IS NULL OR LEN(LTRIM(RTRIM(@reason)))=0 OR DATALENGTH(@reason)>1000
   THROW 51996,'FE05_REJECT_INPUT_INVALID',1;
  ;WITH digit(n) AS(SELECT n FROM(VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9))x(n)),
   positions(n) AS(SELECT a.n+10*b.n+100*c.n+1 FROM digit a CROSS JOIN digit b CROSS JOIN digit c)
  SELECT @confirmed=0 FROM positions WHERE n<=DATALENGTH(@reason)/2
   AND (UNICODE(SUBSTRING(@reason,n,1))<32 OR UNICODE(SUBSTRING(@reason,n,1)) BETWEEN 127 AND 159);
  IF ISNULL(@confirmed,0)<>1 THROW 51996,'FE05_REJECT_INPUT_INVALID',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_items WITH(HOLDLOCK) WHERE version_id=@version AND is_active=1)
   OR EXISTS(SELECT 1 FROM dbo.inspection_checklist_items c WITH(HOLDLOCK) WHERE c.version_id=@version AND c.is_active=1 AND c.is_required=1
    AND NOT EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK) WHERE ir.inspection_id=@inspection AND ir.checklist_item_id=c.item_id AND ir.outcome IN('PASS','FAIL')))
   OR EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK) LEFT JOIN dbo.inspection_checklist_items c WITH(HOLDLOCK) ON c.item_id=ir.checklist_item_id
    WHERE ir.inspection_id=@inspection AND (c.version_id<>@version OR c.is_active<>1 OR ir.outcome NOT IN('PASS','FAIL')))
   OR EXISTS(SELECT checklist_item_id FROM dbo.inspection_results WITH(HOLDLOCK) WHERE inspection_id=@inspection GROUP BY checklist_item_id HAVING COUNT(*)<>1)
   OR NOT EXISTS(SELECT 1 FROM dbo.inspection_results WITH(HOLDLOCK) WHERE inspection_id=@inspection AND outcome='FAIL')
   OR EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK) WHERE ir.inspection_id=@inspection AND ir.outcome='FAIL'
    AND NOT EXISTS(SELECT 1 FROM dbo.inspection_defects d WITH(HOLDLOCK) WHERE d.inspection_id=@inspection AND d.checklist_item_id=ir.checklist_item_id AND LEN(LTRIM(RTRIM(d.description)))>0))
   THROW 51996,'FE05_CONCLUSION_RESULTS_INVALID',1;
  IF @action='VALIDATE_REJECT' BEGIN SELECT @inspection AS InspectionId,@task AS CleaningTaskId,@revision AS Revision; RETURN; END;
 END
 ELSE IF ISNULL(@state,'')<>'FAILED' OR ISNULL(@status,'')<>'COMPLETED' OR ISNULL(@result,'')<>'FAIL' OR @inspected IS NULL
  OR NOT EXISTS(SELECT 1 FROM dbo.cleaning_tasks WITH(HOLDLOCK) WHERE cleaning_task_id=@task AND workflow_status='REWORK_REQUIRED' AND status='IN_PROGRESS')
  THROW 51996,'FE05_FAILED_POSTSTATE_REQUIRED',1;
 -- Domain mutations cannot bypass the existing N01 reservation, even in a test.
 IF ISNULL(COL_LENGTH('dbo.notification_operation_reservations','n01_operation_hash'),-1)<>32
  OR COL_LENGTH('dbo.notification_operation_reservations','n01_receipt_json') IS NULL
  THROW 51996,'FE05_N01_RESERVED_OPERATION_REQUIRED',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK)
  WHERE actor_user_id=@actor AND operation_key=@operation AND fingerprint=@fingerprint
   AND completed=0 AND result_json IS NULL AND n01_operation_hash IS NOT NULL)
  THROW 51996,'FE05_OPERATION_CONFLICT_OR_REPLAY_REQUIRED',1;
 IF @action='REJECT'
 BEGIN
  -- FAST003 conditional transition, now behind the owner module's SQL authorization.
  UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',inspected_at=@now,summary_note=@reason
   WHERE inspection_id=@inspection AND row_version=@expected AND workflow_status='PENDING_CONCLUSION';
  IF @@ROWCOUNT<>1 THROW 51996,'FE05_REVISION_STALE',1;
  UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id=@task AND workflow_status='PENDING_INSPECTION' AND status='IN_PROGRESS';
  IF @@ROWCOUNT<>1 THROW 51996,'FE05_CLEANING_PHASE_CONFLICT',1;
  SELECT inspection_id AS InspectionId,cleaning_task_id AS CleaningTaskId,row_version AS Revision FROM dbo.room_inspections WHERE inspection_id=@inspection; RETURN;
 END;
 IF @action='REWORK'
 BEGIN
  -- A raw FAILED row never supplies prior conclusion authority.
  IF NOT EXISTS(SELECT 1 FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
   JOIN dbo.fe04_rework_authority_issuers x WITH(HOLDLOCK) ON x.issuer='FE05.UC044.UC045' AND x.version=1 AND x.is_active=1
   JOIN sys.sql_modules m ON m.object_id=x.writer_object_id AND m.execute_as_principal_id=-2
   WHERE HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest AND o.completed=1 AND ISJSON(o.result_json)=1
    AND JSON_VALUE(o.result_json,'$.contract')='FE05_INSPECTION_CONCLUSION_V1'
    AND TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.inspectionId'))=@inspection
    AND JSON_VALUE(o.result_json,'$.inspectionETag')=CHAR(34)+CONVERT(varchar(16),@revision,2)+CHAR(34))
   THROW 51996,'FE05_PRIOR_CONCLUSION_RECEIPT_REQUIRED',1;
  IF @assignee IS NULL OR @assignee<=0 OR @due IS NULL OR @due<=@now OR @reason IS NULL OR LEN(LTRIM(RTRIM(@reason)))=0 OR DATALENGTH(@reason)>1000
   OR ISJSON(@selected)<>1 OR LEFT(LTRIM(@selected),1)<>'['
   THROW 51996,'FE05_REWORK_INPUT_INVALID',1;
  DECLARE @defects TABLE(id bigint PRIMARY KEY);
  IF EXISTS(SELECT 1 FROM OPENJSON(@selected) WHERE type<>2 OR TRY_CONVERT(bigint,value) IS NULL OR TRY_CONVERT(bigint,value)<=0)
   OR (SELECT COUNT(*) FROM OPENJSON(@selected)) NOT BETWEEN 1 AND 200
   OR EXISTS(SELECT value FROM OPENJSON(@selected) GROUP BY value HAVING COUNT(*)>1)
   THROW 51996,'FE05_SELECTED_DEFECTS_INVALID',1;
  INSERT @defects SELECT CONVERT(bigint,value) FROM OPENJSON(@selected);
  IF EXISTS(SELECT 1 FROM @defects x WHERE NOT EXISTS(SELECT 1 FROM dbo.inspection_defects d WITH(HOLDLOCK)
    JOIN dbo.inspection_results ir WITH(HOLDLOCK) ON ir.inspection_id=d.inspection_id AND ir.checklist_item_id=d.checklist_item_id AND ir.outcome='FAIL'
    JOIN dbo.inspection_checklist_items c WITH(HOLDLOCK) ON c.item_id=d.checklist_item_id AND c.version_id=@version
    WHERE d.defect_id=x.id AND d.inspection_id=@inspection))
   OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks WITH(UPDLOCK,HOLDLOCK) WHERE inspection_id=@inspection AND status NOT IN('COMPLETED','CANCELLED'))
   THROW 51996,'FE05_REWORK_SOURCE_OR_DUPLICATE',1;
  INSERT dbo.cleaning_reworks(inspection_id,cleaning_task_id,assignee_user_id,reason,status,workflow_status,due_at,requested_at)
   VALUES(@inspection,@task,@assignee,@reason,'PENDING','PENDING_ACCEPTANCE',@due,@now);
  SET @rework=CONVERT(bigint,SCOPE_IDENTITY());
  INSERT dbo.cleaning_rework_defects(rework_id,defect_id) SELECT @rework,id FROM @defects;
  SELECT @rework AS ReworkId; RETURN;
 END;
 -- Completion is never inferred from an N01 C# DTO. Require persisted graph and pinned owner module.
 DECLARE @issuer varchar(80)=CASE WHEN @action='COMPLETE_REJECT' THEN 'FE05.UC044.UC045' ELSE 'FE05.UC045.UC046' END,
  @event varchar(60)=CASE WHEN @action='COMPLETE_REJECT' THEN 'inspection.failed' ELSE 'cleaning.rework.requested' END,
  @json nvarchar(max),@writer bigint=CURRENT_TRANSACTION_ID();
 IF NOT EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x WITH(HOLDLOCK) JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
  WHERE x.issuer=@issuer AND x.version=1 AND x.is_active=1 AND x.writer_object_id=@@PROCID AND m.execute_as_principal_id=-2
   AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest)
  THROW 51996,'FE05_REGISTERED_ISSUER_REQUIRED',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
  JOIN dbo.notification_publications p WITH(HOLDLOCK) ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key
  WHERE o.actor_user_id=@actor AND o.operation_key=@operation AND ISJSON(o.n01_receipt_json)=1
   AND p.event_type=@event AND p.inspection_id=@inspection AND p.source_revision=@expected
   AND ((@action='COMPLETE_REJECT' AND p.rework_id IS NULL) OR (@action='COMPLETE_REWORK' AND p.rework_id=@rework))
   AND EXISTS(SELECT 1 FROM dbo.notification_delivery_intents di WITH(HOLDLOCK) WHERE di.publication_id=p.publication_id AND di.delivery_channel='IN_APP'))
  THROW 51996,'FE05_MANDATORY_N01_GRAPH_REQUIRED',1;
 IF @action='COMPLETE_REJECT'
 BEGIN
  IF @inspector<>@actor THROW 51996,'FE05_ASSIGNED_INSPECTOR_REQUIRED',1;
  SELECT @json=(SELECT 'FE05_INSPECTION_CONCLUSION_V1' AS contract,@issuer AS issuer,1 AS issuerVersion,
   @inspection AS inspectionId,@task AS cleaningTaskId,@room AS roomId,@branch AS branchId,
   CHAR(34)+CONVERT(varchar(16),@revision,2)+CHAR(34) AS inspectionETag,'FAILED' AS conclusion FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 END
 ELSE
 BEGIN
  IF @source_digest IS NULL OR @evidence_digest IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.cleaning_reworks WITH(UPDLOCK,HOLDLOCK)
   WHERE rework_id=@rework AND inspection_id=@inspection AND cleaning_task_id=@task AND workflow_status='PENDING_ACCEPTANCE' AND status='PENDING')
   THROW 51996,'FE05_REWORK_PROOF_BINDING_REQUIRED',1;
  SELECT @json=(SELECT 'FE05_REWORK_AUTHORITY_V1' AS contract,'FE05_COMMITTED_V1' AS origin,1 AS version,
   @issuer AS issuer,1 AS issuerVersion,@actor AS actorUserId,@operation AS operationId,@writer AS writerTransactionId,
   @rework AS reworkId,@inspection AS inspectionId,@task AS cleaningTaskId,@room AS roomId,@branch AS branchId,@version AS versionId,
   CHAR(34)+CONVERT(varchar(16),@revision,2)+CHAR(34) AS inspectionETag,
   CONVERT(varchar(64),@source_digest,2) AS sourceDigest,CONVERT(varchar(64),@evidence_digest,2) AS evidenceDigest,'FAILED' AS conclusion
   FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 END;
 UPDATE dbo.notification_operation_reservations SET result_json=@json,completed=1
  WHERE actor_user_id=@actor AND operation_key=@operation AND completed=0 AND fingerprint=@fingerprint;
 IF @@ROWCOUNT<>1 THROW 51996,'FE05_OPERATION_CONFLICT_OR_REPLAY_REQUIRED',1;
 IF @action='COMPLETE_REWORK'
 BEGIN
  INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
   VALUES(@branch,@actor,'cleaning_reworks',CONVERT(nvarchar(100),@rework),'CREATE',
    (SELECT @operation AS operationId,'FE05_REWORK_AUTHORITY_V1' AS authorityContract,CONVERT(varchar(64),@source_digest,2) AS sourceDigest FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@now);
  INSERT dbo.fs016_rework_source_proofs(rework_id,inspection_id,inspection_revision,source_digest,origin,
   contract_version,issuer,issuer_version,actor_user_id,operation_key,writer_transaction_id,cleaning_task_id,room_id,branch_id,version_id,evidence_digest)
   VALUES(@rework,@inspection,@revision,@source_digest,'FE05_COMMITTED_V1',1,@issuer,1,@actor,@operation,@writer,@task,@room,@branch,@version,@evidence_digest);
 END
 ELSE INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
  VALUES(@branch,@actor,'room_inspections',CONVERT(nvarchar(100),@inspection),'UPDATE',@json,@now);
 SELECT @json AS ResultJson;
END
GO
