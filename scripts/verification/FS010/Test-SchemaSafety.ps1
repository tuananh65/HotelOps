$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot '.private/processes.json')){throw 'Stop owned environment before schema fault proof.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$target='HotelOps_Canonical_20261106_f100001_Fresh'
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']='.\SQLEXPRESS';$builder['Initial Catalog']=$target;$builder['Integrated Security']=$true;$builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(60000)
$transaction=$null
function Query([string]$sql){
 $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=15;$command.CommandText=$sql
 try{return $command.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()}finally{$command.Dispose()}
}
try {
 $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
 if([int](Query "SELECT CASE WHEN DB_NAME()=N'HotelOps_Canonical_20261106_f100001_Fresh' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))='SQLEXPRESS' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS010.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS010') THEN 1 ELSE 0 END") -ne 1){throw 'ISOLATED_IDENTITY_MISMATCH'}
 $counter='SELECT (SELECT COUNT(*) FROM dbo.notification_operation_reservations)+(SELECT COUNT(*) FROM dbo.notification_publications)+(SELECT COUNT(*) FROM dbo.notification_delivery_intents)'
 $before=[int](Query $counter)
 $proposal=[IO.File]::ReadAllText((Join-Path $root 'scripts/database/release/fs010-candidate/02_NotificationProposal.sql'))
 [void](Query $proposal);[void](Query $proposal)
 $transaction=$connection.BeginTransaction()
 $refused=$false
 try {
  [void](Query "EXEC sys.sp_rename N'dbo.notification_delivery_intents',N'fs010_partial_probe'")
  try{[void](Query $proposal)}catch [System.Data.SqlClient.SqlException]{if($_.Exception.Number -ne 51891){throw};$refused=$true}
 } finally {if($transaction.Connection){$transaction.Rollback()};$transaction.Dispose();$transaction=$null}
 if(!$refused -or [int](Query "SELECT CASE WHEN OBJECT_ID('dbo.notification_delivery_intents') IS NOT NULL AND OBJECT_ID('dbo.fs010_partial_probe') IS NULL THEN 1 ELSE 0 END") -ne 1){throw 'PARTIAL_REFUSAL_OR_ROLLBACK_FAILED'}
 $recovery=[IO.File]::ReadAllText((Join-Path $root 'scripts/database/release/fs010-candidate/09_RecoveryCandidate.sql'))
 $preserved=$false
 try{[void](Query $recovery)}catch [System.Data.SqlClient.SqlException]{if($_.Exception.Number -ne 51917){throw};$preserved=$true}
 if(!$preserved -or [int](Query $counter) -ne $before){throw 'POPULATED_EVIDENCE_NOT_PRESERVED'}
 $receipt=[ordered]@{level='COMPONENT_REAL_SQL';idempotentProposal='PASS_TWO_EXECUTIONS';partialPackageRefusal='PASS_51891';rollbackRestoredOriginalTable=$true;populatedRecoveryRefusal='PASS_51917';actualRecoveryDropExecuted=$false;retainedRows=$before;rowDelta=0;resetOrReseed=$false}
 $receipt|ConvertTo-Json|Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'docs/fs010/SCHEMA_SAFETY_RESULTS.json')
 $receipt|ConvertTo-Json
}finally{if($transaction){$transaction.Dispose()};$connection.Dispose();$budget.Dispose()}
