[CmdletBinding()]
param([switch]$RunApprovedTrial)
$ErrorActionPreference='Stop'
if(!$RunApprovedTrial){'PLAN_ONLY: exact FS013 synthetic trial, all component mutations roll back';return}
$c=New-Object System.Data.SqlClient.SqlConnection('Data Source=.\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_20261106_f130001_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5')
$tx=$null
function Query([string]$sql){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=15;$cmd.CommandText=$sql
 if($script:tx){$cmd.Transaction=$script:tx}
 try{return $cmd.ExecuteScalarAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}
}
function Execute([string]$sql){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=15;$cmd.CommandText=$sql
 if($script:tx){$cmd.Transaction=$script:tx}
 try{return $cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}
}
$results=New-Object Collections.Generic.List[object]
try {
 $c.OpenAsync().GetAwaiter().GetResult()
 if([int](Query "SELECT CASE WHEN DB_NAME()='HotelOps_Canonical_20261106_f130001_Fresh' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))='SQLEXPRESS' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.Owner' AND CONVERT(nvarchar(100),value)='DEV1_FS013') THEN 1 ELSE 0 END") -ne 1){throw 'FS013_TARGET_REQUIRED'}
 $snapshot="SELECT (SELECT cleaning_task_id,status,workflow_status,actual_start_at,due_at,CONVERT(varchar(16),row_version,2) AS revision FROM dbo.cleaning_tasks ORDER BY cleaning_task_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS tasks,(SELECT assignment_id,assignee_user_id,ended_at FROM dbo.cleaning_assignments ORDER BY assignment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS assignments,(SELECT COUNT(*) FROM dbo.fs013_reassignment_operations) AS operations,(SELECT COUNT(*) FROM dbo.fs013_reassignment_intents) AS intents,(SELECT COUNT(*) FROM dbo.audit_logs) AS audits,(SELECT COUNT(*) FROM dbo.workdays) AS days,(SELECT COUNT(*) FROM dbo.work_intervals) AS intervals,(SELECT COUNT(*) FROM dbo.room_readiness_events) AS readiness FOR JSON PATH,WITHOUT_ARRAY_WRAPPER"
 $before=[string](Query $snapshot)
 foreach($case in @('assigned-two-recipients','in-progress-close-time','mandatory-intent-rollback','open-interval-rejected','missing-recipient-rejected','restricted-direct-DML')) {
  $tx=$c.BeginTransaction()
  try {
   $prepare=@'
DECLARE @actor bigint=(SELECT user_id FROM dbo.users WHERE username='fs013.admin'),@owner bigint=(SELECT user_id FROM dbo.users WHERE username='fs013.owner'),@next bigint=(SELECT user_id FROM dbo.users WHERE username='fs013.other');
DECLARE @task bigint,@previous bigint,@branch bigint,@room bigint,@now datetime2(7)=SYSUTCDATETIME();
SELECT TOP(1) @task=t.cleaning_task_id,@previous=a.assignment_id,@branch=r.branch_id,@room=r.room_id FROM dbo.cleaning_tasks t JOIN dbo.cleaning_assignments a ON a.cleaning_task_id=t.cleaning_task_id JOIN dbo.rooms r ON r.room_id=t.room_id WHERE a.assignee_user_id=@owner AND a.ended_at IS NULL ORDER BY t.cleaning_task_id;
IF @task IS NULL THROW 51970,'Synthetic fixture required',1;
UPDATE dbo.cleaning_tasks SET workflow_status='ASSIGNED',status='PENDING' WHERE cleaning_task_id=@task;
IF EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WHERE room_id=@room) UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=@task WHERE room_id=@room;
ELSE INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
DECLARE @interval bigint=NULL;
'@
   $interval=@'
DECLARE @day bigint;
INSERT dbo.workdays(user_id,branch_id,department_id,work_date,is_unscheduled,unscheduled_reason,status,started_at) SELECT @owner,@branch,department_id,CONVERT(date,@now),1,'FS013 rollback component fixture','WORKING',DATEADD(minute,-2,@now) FROM dbo.users WHERE user_id=@owner;
SET @day=CONVERT(bigint,SCOPE_IDENTITY());
INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at) VALUES(@day,@owner,@branch,'CLEANING',@task,@owner,DATEADD(minute,-1,@now),DATEADD(minute,-1,@now));
SET @interval=CONVERT(bigint,SCOPE_IDENTITY());
UPDATE dbo.cleaning_tasks SET workflow_status='IN_PROGRESS',status='IN_PROGRESS' WHERE cleaning_task_id=@task;
'@
   $execute=@'
DECLARE @key uniqueidentifier=NEWID(),@tag binary(8)=(SELECT row_version FROM dbo.cleaning_tasks WHERE cleaning_task_id=@task),@due datetime2(7)=DATEADD(hour,1,@now);
EXEC dbo.fs013_reassign_with_evidence @actor,@branch,@task,@previous,@next,@key,0x0100000000000000000000000000000000000000000000000000000000000000,@tag,@due,N'FS013 rollback component command',@interval;
'@
   $verify=@'
IF (SELECT COUNT(*) FROM dbo.fs013_reassignment_intents WHERE actor_user_id=@actor AND operation_key=@key)<>2 THROW 51970,'Two intents required',1;
IF NOT EXISTS(SELECT 1 FROM dbo.cleaning_assignments WHERE assignment_id=@previous AND ended_at IS NOT NULL) THROW 51970,'Previous assignment not ended',1;
IF NOT EXISTS(SELECT 1 FROM dbo.cleaning_assignments WHERE cleaning_task_id=@task AND assignee_user_id=@next AND ended_at IS NULL) THROW 51970,'New owner missing',1;
IF @interval IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals w JOIN dbo.cleaning_assignments a ON a.assignment_id=@previous WHERE w.interval_id=@interval AND a.ended_at=w.closed_at) THROW 51970,'Close time mismatch',1;
SELECT 1;
'@
   $expected=$null
   $sql=switch($case){
    'assigned-two-recipients' {$prepare+$execute+$verify}
    'in-progress-close-time' {$prepare+$interval+"UPDATE dbo.work_intervals SET closed_at=@now,effective_ended_at=@now,closed_by=@actor WHERE interval_id=@interval;"+$execute+$verify}
    'mandatory-intent-rollback' {$expected=547;$prepare+"ALTER TABLE dbo.fs013_reassignment_intents WITH NOCHECK ADD CONSTRAINT CK_FS013_ComponentFault CHECK(intent_id<0);"+$execute}
    'open-interval-rejected' {$expected=51963;$prepare+$interval+$execute}
    'missing-recipient-rejected' {$expected=51962;$prepare+$execute+"DELETE TOP(1) FROM dbo.fs013_reassignment_intents WHERE actor_user_id=@actor AND operation_key=@key; EXEC dbo.fs013_verify_reassignment @actor,@key,1;"}
    'restricted-direct-DML' {$expected=229;"EXECUTE AS USER='fs013_app'; UPDATE dbo.fs013_reassignment_operations SET occurred_at=SYSUTCDATETIME(); REVERT;"}
   }
   $number=$null
   try{[void](Execute $sql)}catch [System.Data.SqlClient.SqlException]{$number=$_.Exception.Number}
   if($number -ne $expected){throw "UNEXPECTED_COMPONENT_OUTCOME_${case}_SQL_${number}"}
   $results.Add([ordered]@{case=$case;status='PASS';expectedSqlError=$expected;actualSqlError=$number;level='SQL_MODULE_COMPONENT_NOT_HTTP_AUTH';transaction='ROLLBACK'})
  } finally {
   if($tx.Connection){$tx.Rollback()};$tx.Dispose();$tx=$null
   # EXECUTE AS can outlive a rolled-back transaction; always restore before snapshot.
   [void](Query 'IF USER_NAME()=''fs013_app'' REVERT;')
  }
  if([string](Query $snapshot) -cne $before){throw 'COMPONENT_FIXTURE_NOT_ROLLED_BACK'}
 }
 [ordered]@{status='PASS_SQL_MODULE_COMPONENT';database='HotelOps_Canonical_20261106_f130001_Fresh';cases=$results.ToArray();retainedBusinessRows=$false;identityCountersMayAdvance=$true;httpAuthorizationVerified=$false;dotnetTransactionParticipantVerified=$false;acceptedNotification=$false}|ConvertTo-Json -Depth 6|Set-Content -Encoding utf8 docs/fs013/REASSIGNMENT_SQL_RESULTS.json
 [ordered]@{status='PASS_SQL_MODULE_COMPONENT';cases=$results.Count;retainedBusinessRows=$false;runtimeAcceptance=$false}|ConvertTo-Json
} finally {$c.Dispose()}
