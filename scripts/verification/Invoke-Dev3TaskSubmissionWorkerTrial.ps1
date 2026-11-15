param([Parameter(Mandatory=$true)][string]$CloneReceipt,
      [Parameter(Mandatory=$true)][string]$ReportPath,
      [ValidateSet('Submission','Verification','Decline')][string]$Kind='Submission')
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if(Test-Path -LiteralPath $ReportPath){throw 'Do not overwrite runtime evidence'}
$receipt=Get-Content -LiteralPath $CloneReceipt -Raw|ConvertFrom-Json
if($receipt.status -ne 'CLONE_READY' -or $receipt.run -notmatch '^[a-f0-9]{32}$' -or
 $receipt.database -cne ('HotelOps_DEV3_142A_CriteriaTrial_'+$receipt.run.Substring(0,8)) -or
 $receipt.source -cne 'HotelOps_DEV3_142A_20261114_cc78c13f' -or [Guid]$receipt.databaseGuid -eq [Guid]::Empty){throw 'Owned disposable clone required'}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database='+$receipt.database+';Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
$hostProcess=$null
$result=[ordered]@{database=$receipt.database;success=$false;hostStopped=$false;kind=$Kind;scope='Real opt-in hosted worker recovery of one committed synthetic event; no new business transition'}
try{
 $connection.Open()
 if([string](Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -cne 'meita' -or
    [string](Scalar 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ine $receipt.databaseGuid -or
    [string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'") -cne $receipt.run){throw 'Clone identity mismatch'}
 if(Get-NetTCPConnection -LocalPort 7142 -State Listen -ErrorAction SilentlyContinue){throw 'Stop previous owned host before worker trial'}
 if([int](Scalar "SELECT COUNT(*) FROM sys.triggers WHERE name LIKE 'TR_DEV3_Task%Fault'") -ne 0){throw 'Fault trigger remains'}
 $verification=$Kind -eq 'Verification'
 $decline=$Kind -eq 'Decline'
 $eventId=if($decline){[long](Scalar "SELECT MAX(audit_id) FROM dbo.audit_logs WHERE entity_type='operational_tasks' AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.operation')='DECLINE' AND (SELECT COUNT(*) FROM OPENJSON(new_values,'$.recipients'))=1")}elseif($verification){[long](Scalar 'SELECT MAX(submission_id) FROM dbo.task_verification_events')}else{[long](Scalar "SELECT MAX(progress_id) FROM dbo.task_submission_events WHERE ISJSON(recipients_json)=1 AND (SELECT COUNT(*) FROM OPENJSON(recipients_json))=1")}
 if($eventId -le 0){throw 'Committed recipient intent required'}
 $recipient=if($decline){[long](Scalar "SELECT TRY_CONVERT(bigint,JSON_VALUE(new_values,'$.recipients[0]')) FROM dbo.audit_logs WHERE audit_id=$eventId")}elseif($verification){[long](Scalar "SELECT submitted_by FROM dbo.task_submission_events WHERE progress_id=$eventId")}else{[long](Scalar "SELECT TRY_CONVERT(bigint,JSON_VALUE(recipients_json,'$[0]')) FROM dbo.task_submission_events WHERE progress_id=$eventId")}
 $namePattern=if($decline){'decline_mgr_%'}elseif($verification){'sprint142_%'}else{'submit_mgr_%'}
 if($recipient -le 0 -or [int](Scalar "SELECT COUNT(*) FROM dbo.users WHERE user_id=$recipient AND username LIKE '$namePattern' AND email LIKE '%@example.invalid'") -ne 1){throw 'Exact synthetic recipient required'}
 $table=if($decline){'dbo.audit_logs'}elseif($verification){'dbo.task_verification_events'}else{'dbo.task_submission_events'}
 $key=if($decline){'audit_id'}elseif($verification){'submission_id'}else{'progress_id'}
 $sourceType=if($decline){'TASK_DECLINE'}elseif($verification){'TASK_VERIFICATION'}else{'TASK_SUBMISSION'}
 $noticeType=if($decline){'TASK_DECLINED'}elseif($verification){[string](Scalar "SELECT CASE WHEN decision='ACCEPT' THEN 'TASK_VERIFIED' ELSE 'TASK_RETURNED' END FROM dbo.task_verification_events WHERE submission_id=$eventId")}else{'TASK_SUBMITTED'}
 if($noticeType -notin @('TASK_VERIFIED','TASK_RETURNED','TASK_SUBMITTED','TASK_DECLINED')){throw 'Unsupported technical notification kind'}
 $hashFields=if($decline){'new_values,old_values,changed_fields'}elseif($verification){'note,performed_by_json,requested_by,verified_by,decision,actual_effort_seconds'}else{'result_note,handover_note,criteria_json,evidence_json,recipients_json'}
 $audit=[long](Scalar "SELECT audit_id FROM $table WHERE $key=$eventId")
 $fingerprint=[string](Scalar "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT($hashFields)),2) FROM $table WHERE $key=$eventId")
 # Restore only this synthetic recipient and remove its delivered receipt to
 # simulate recovery. The immutable source event and audit are never rewritten.
 Scalar @"
 SET XACT_ABORT ON; BEGIN TRANSACTION;
 UPDATE ur SET is_active=1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
 WHERE ur.user_id=$recipient AND r.role_code='DEPT_MANAGER' AND '$Kind' IN('Submission','Decline');
 DELETE x FROM dbo.notification_recipients x JOIN dbo.notifications n ON n.notification_id=x.notification_id
 WHERE n.source_type='$sourceType' AND n.notification_type='$noticeType' AND n.source_id=$eventId;
 DELETE FROM dbo.notifications WHERE source_type='$sourceType' AND notification_type='$noticeType' AND source_id=$eventId;
 COMMIT;
"@ | Out-Null
 $env:ConnectionStrings__HotelOpsDb=$connection.ConnectionString
 $env:CanonicalFrontend__Root=Join-Path $workspace 'frontend/dist'
 $env:Evidence__PrivateRoot=Join-Path $workspace 'TestResults/sprint25-evidence'
 $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='https://localhost:7142'
 $env:TaskNotifications__Enabled='true';$env:TaskNotifications__PollSeconds='1'
 $hostProcess=Start-Process dotnet -ArgumentList 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll' -WorkingDirectory $workspace -WindowStyle Hidden -PassThru -RedirectStandardOutput ($ReportPath+'.host.log') -RedirectStandardError ($ReportPath+'.host-error.log')
 $delivered=0
 for($attempt=0;$attempt -lt 20;$attempt++){
  if($hostProcess.HasExited){throw 'Owned worker host exited'}
  $delivered=[int](Scalar "SELECT COUNT(*) FROM dbo.notifications n JOIN dbo.notification_recipients x ON x.notification_id=n.notification_id WHERE n.source_type='$sourceType' AND n.notification_type='$noticeType' AND n.source_id=$eventId AND x.user_id=$recipient AND x.delivery_channel='IN_APP' AND x.delivery_status='DELIVERED'")
  if($delivered -eq 1){break}
  Start-Sleep -Seconds 1
 }
 if($delivered -ne 1){throw 'Worker did not deliver committed intent'}
 Start-Sleep -Seconds 3
 if([int](Scalar "SELECT COUNT(*) FROM dbo.notifications WHERE source_type='$sourceType' AND notification_type='$noticeType' AND source_id=$eventId") -ne 1 -or
    [int](Scalar "SELECT COUNT(*) FROM dbo.notification_recipients x JOIN dbo.notifications n ON n.notification_id=x.notification_id WHERE n.source_type='$sourceType' AND n.source_id=$eventId") -ne 1){throw 'Worker created duplicate delivery'}
 if([string](Scalar "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT($hashFields)),2) FROM $table WHERE $key=$eventId AND audit_id=$audit") -cne $fingerprint){throw 'Worker changed source snapshot'}
 $result.success=$true;$result.eventId=$eventId;$result.singleDelivery=$true;$result.sourceUnchanged=$true
}catch{$result.failure=$_.Exception.Message;throw}
finally{
 if($hostProcess){if(!$hostProcess.HasExited){Stop-Process -Id $hostProcess.Id};$result.hostStopped=$true}
 $connection.Dispose()
 [IO.File]::WriteAllText($ReportPath,($result|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
}
$result|ConvertTo-Json
