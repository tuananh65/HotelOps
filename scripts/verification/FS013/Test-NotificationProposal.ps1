$ErrorActionPreference='Stop'
$target='HotelOps_Canonical_20261106_f130001_Fresh'
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']=$target;$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(60000)
$results=New-Object Collections.Generic.List[object]
try{
$c.OpenAsync($budget.Token).GetAwaiter().GetResult()
function Query([string]$sql){$cmd=$c.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=20;try{return $cmd.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
if([int](Query "SELECT CASE WHEN DB_NAME()=N'HotelOps_Canonical_20261106_f130001_Fresh' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))=N'SQLEXPRESS' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS013.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS013') THEN 1 ELSE 0 END") -ne 1){throw 'TARGET_IDENTITY_MISMATCH'}
foreach($file in @('02_NotificationProposal.sql','02_NotificationProposal.sql','03_VerifyNotificationProposal.sql')){[void](Query ([IO.File]::ReadAllText((Join-Path $PWD ('scripts/database/release/fs013-candidate/'+$file)))))}
$baselineRows=[int](Query 'SELECT (SELECT COUNT(*) FROM dbo.notification_operation_reservations)+(SELECT COUNT(*) FROM dbo.notification_publications)+(SELECT COUNT(*) FROM dbo.notification_delivery_intents)')
$actor=[long](Query "SELECT user_id FROM dbo.users WHERE username='fs013.admin'")
$recipient=[long](Query "SELECT user_id FROM dbo.users WHERE username='fs013.owner'")
$assignment=[long](Query 'SELECT MIN(assignment_id) FROM dbo.cleaning_assignments')
foreach($case in @('valid-stage','duplicate-operation','duplicate-intent','forged-delivered','invalid-inbox-fk','claim-half-null')){
 $tx=$c.BeginTransaction()
 try{
  function TxQuery([string]$sql){$cmd=$c.CreateCommand();$cmd.Transaction=$tx;$cmd.CommandText=$sql;try{return $cmd.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
  $op=[guid]::NewGuid().ToString();$pub=[guid]::NewGuid().ToString()
  [void](TxQuery "UPDATE t SET due_at=COALESCE(t.due_at,DATEADD(hour,1,SYSUTCDATETIME())) FROM dbo.cleaning_tasks t JOIN dbo.cleaning_assignments a ON a.cleaning_task_id=t.cleaning_task_id WHERE a.assignment_id=$assignment; INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint) VALUES($actor,'$op',1,CONVERT(binary(32),1)); INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,source_revision,payload_json) VALUES('$pub',$actor,'$op','CLEANING_TASK_ASSIGNED',1,$assignment,(SELECT t.row_version FROM dbo.cleaning_tasks t JOIN dbo.cleaning_assignments a ON a.cleaning_task_id=t.cleaning_task_id WHERE a.assignment_id=$assignment),(SELECT t.due_at AS dueAtUtc FROM dbo.cleaning_tasks t JOIN dbo.cleaning_assignments a ON a.cleaning_task_id=t.cleaning_task_id WHERE a.assignment_id=$assignment FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)); INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel) VALUES('$pub',$recipient,'IN_APP');")
  $sql=switch($case){
   'valid-stage' {"UPDATE dbo.notification_operation_reservations SET completed=1,result_json=N'{`"id`":1}' WHERE actor_user_id=$actor AND operation_key='$op'"}
   'duplicate-operation' {"INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint) VALUES($actor,'$op',1,CONVERT(binary(32),1))"}
   'duplicate-intent' {"INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel) VALUES('$pub',$recipient,'IN_APP')"}
   'forged-delivered' {"UPDATE dbo.notification_delivery_intents SET status='DELIVERED' WHERE publication_id='$pub'"}
   'invalid-inbox-fk' {"UPDATE dbo.notification_delivery_intents SET status='DELIVERED',inbox_notification_id=9223372036854775807 WHERE publication_id='$pub'"}
   'claim-half-null' {"UPDATE dbo.notification_delivery_intents SET claim_id=NEWID() WHERE publication_id='$pub'"}
  }
  $failed=$false;$number=$null
  try{[void](TxQuery $sql)}catch [System.Data.SqlClient.SqlException]{$failed=$true;$number=$_.Exception.Number}
  if(($case -eq 'valid-stage' -and $failed) -or ($case -ne 'valid-stage' -and (!$failed -or $number -notin @(547,2601,2627)))){throw 'UNEXPECTED_CONSTRAINT_RESULT'}
  $results.Add([pscustomobject]@{case=$case;passed=$true;sqlError=$number;transaction='ROLLED_BACK_COMPONENT_TEST'})
 }finally{if($tx.Connection){$tx.Rollback()};$tx.Dispose()}
}
$remaining=[int](Query 'SELECT (SELECT COUNT(*) FROM dbo.notification_operation_reservations)+(SELECT COUNT(*) FROM dbo.notification_publications)+(SELECT COUNT(*) FROM dbo.notification_delivery_intents)')
if($remaining -ne $baselineRows){throw 'COMPONENT_TEST_DATA_NOT_ROLLED_BACK'}
[ordered]@{status='PASS_COMPONENT_SQL_ONLY';target=$target;tests=$results.ToArray();retainedRows=$remaining;remainingTestRows=($remaining-$baselineRows);acceptedA01=$false;runtimePublisher=$false;inboxDeliveryVerified=$false}|ConvertTo-Json -Depth 6|Set-Content docs/fs013/NOTIFICATION_SQL_RESULTS.json -Encoding UTF8
[ordered]@{status='PASS_COMPONENT_SQL_ONLY';cases=$results.Count;retainedRows=$remaining;remainingTestRows=($remaining-$baselineRows);runtimeAcceptance=$false}|ConvertTo-Json
}finally{$c.Dispose();$budget.Dispose()}
