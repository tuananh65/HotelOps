-- Proposed future schema-owner probe; never an approval artifact or standalone launcher.
-- C# owns the ONLY Serializable transaction, positive single-row prechecks, lease and cleanup.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;
IF @@TRANCOUNT<>1 OR XACT_STATE()<>1 OR
 (SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id=@@SPID)<>4
 THROW 51990,'Exact owned Serializable probe transaction required',1;
IF @ProbeId NOT IN('n01-duplicate-event','legacy-duplicate-assignment','legacy-cross-family-duplicate')
 OR @ExpectedTargetTable<>'dbo.notification_publications' OR ISJSON(@RowsJson)<>1
 OR (SELECT COUNT(*) FROM OPENJSON(@RowsJson))<>2
 OR (@ProbeId='n01-duplicate-event' AND @ExpectedIndexName<>'UX_n01_operation_event_v1')
 OR (@ProbeId<>'n01-duplicate-event' AND @ExpectedIndexName<>'UX_fs009_publication_operation')
 THROW 51990,'Exact reviewed named-index case/fixture binding required',1;
DECLARE @ErrorNumber int=NULL,@ErrorMessage nvarchar(4000)=NULL,@ErrorProcedure nvarchar(128)=NULL,
 @ErrorLine int=NULL,@CatchState int=NULL,@CatchCount int=NULL;
BEGIN TRY
 INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,
  assignment_id,rework_id,inspection_id,n01_payload_hash,source_revision,payload_json,occurrence_key)
 SELECT publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,rework_id,inspection_id,
  CONVERT(binary(32),payload_hash_hex,2),CONVERT(binary(8),revision_hex,2),payload_json,occurrence_key
 FROM OPENJSON(@RowsJson) WITH(
  publication_id uniqueidentifier,actor_user_id bigint,operation_key uniqueidentifier,event_type varchar(60),schema_version int,
  assignment_id bigint,rework_id bigint,inspection_id bigint,payload_hash_hex varchar(64),revision_hex varchar(16),
  payload_json nvarchar(max),occurrence_key varchar(120));
END TRY
BEGIN CATCH
 SELECT @ErrorNumber=ERROR_NUMBER(),@ErrorMessage=ERROR_MESSAGE(),@ErrorProcedure=ERROR_PROCEDURE(),
  @ErrorLine=ERROR_LINE(),@CatchState=XACT_STATE(),@CatchCount=@@TRANCOUNT;
END CATCH;
-- Read-only output is allowed even when XACT_STATE=-1. C# MUST rollback before any next action.
-- No SQL PASS is minted here. No error, wrong error/index/table, missing metadata or1205 => FAIL in validator.
SELECT @ProbeId AS ProbeId,@ExpectedTargetTable AS TargetTable,@ExpectedIndexName AS ExpectedIndexName,
 CASE WHEN @ErrorNumber IS NULL THEN 'NO_DUPLICATE_ERROR' ELSE 'ERROR_CAPTURED' END AS ExecutionResult,
 @ErrorNumber AS ErrorNumber,@ErrorMessage AS ErrorMessage,@ErrorProcedure AS ErrorProcedure,@ErrorLine AS ErrorLine,
 COALESCE(@CatchState,XACT_STATE()) AS XactStateAtCatch,COALESCE(@CatchCount,@@TRANCOUNT) AS TransactionsAtCatch;
