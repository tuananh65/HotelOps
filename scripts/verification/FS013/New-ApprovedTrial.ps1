[CmdletBinding()]
param([switch]$ExecuteApprovedCreate,[switch]$AllowLocalSqlCertificateException)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$database='HotelOps_Canonical_20261106_f130001_Fresh'
$manifest=Get-Content -Raw -Encoding utf8 (Join-Path $root 'docs/fs013/SOURCE_MANIFEST_BASE.json')|ConvertFrom-Json
function TestSourceHash($entry){
 $text=[IO.File]::ReadAllText((Join-Path $root $entry.path)).Replace("`r`n","`n").TrimStart([char]0xFEFF)
 $hasher=[Security.Cryptography.SHA256]::Create()
 try{$actual=([BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','')}finally{$hasher.Dispose()}
 if(!$entry.normalizedSha256 -or $actual -ine $entry.normalizedSha256){throw 'SOURCE_NORMALIZED_HASH_MISMATCH'}
}
foreach($entry in $manifest){TestSourceHash $entry}
if(!$ExecuteApprovedCreate){[ordered]@{mode='PLAN_ONLY';server='.\SQLEXPRESS';database=$database;sourceCount=$manifest.Count;sourceHashes='MatchedNormalizedTextLF';createsDatabase=$false;requiresUserApproval=$true;resetOrDropSupported=$false}|ConvertTo-Json;return}
# This switch is an execution interlock, not a substitute for explicit user approval in the task.
$connection=$null;$transaction=$null;$created=$false
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(180000)
function Execute([string]$sql){$cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=60;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQueryAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=15;$cmd.CommandText=$sql;try{return $cmd.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
function Source([int]$index){
 $source=[IO.File]::ReadAllText((Join-Path $root $manifest[$index].path))
 if($index -le 5){$source=$source.Replace('HotelOpsDB',$database)}
 foreach($batch in [regex]::Split($source,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
}
function Module([int]$install,[int]$verify,[string]$marker){
 $transaction=$connection.BeginTransaction()
 try{
  Source $install
  if($marker){Execute ("EXEC sys.sp_addextendedproperty @name=N'"+$marker+"',@value=N'"+$manifest[$install].sha256.ToUpperInvariant()+"';")}
  Source $verify
  $transaction.Commit()
 }catch{if($transaction.Connection){$transaction.Rollback()};throw}finally{$transaction.Dispose();$transaction=$null}
}
try{
 $b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
 $b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='master';$b['Integrated Security']=$true;$b['Encrypt']=$true
 $b['TrustServerCertificate']=[bool]$AllowLocalSqlCertificateException;$b['Connect Timeout']=5;$b['Application Name']='HotelOps FS013 approved new disposable trial'
 $connection=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
 $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
 if([string](Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))") -ine [Environment]::MachineName -or [string](Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))") -ine 'SQLEXPRESS'){throw 'LOCAL_SQL_IDENTITY_MISMATCH'}
 Execute ("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:FS013:"+$database+"',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=5000; IF @r<0 THROW 51990,'FS013 target lock unavailable',1;")
 if([int](Scalar ("SELECT COUNT(*) FROM sys.databases WHERE name=N'"+$database+"'")) -ne 0){throw 'TARGET_ALREADY_EXISTS_NO_OVERWRITE'}
 # Installer guards and original source hashes are preserved; no existing database is altered.
 Source 0;$created=$true;$connection.ChangeDatabase($database)
 if([string](Scalar 'SELECT DB_NAME()') -cne $database){throw 'TARGET_IDENTITY_MISMATCH'}
 Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS013.Owner',@value=N'DEV1_FS013'; EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserDirectory.Trial',@value=N'Task17';"
 foreach($index in 1..8){Source $index}
 Source 9
 Module 10 11 ''
 Module 12 13 'HotelOps.Checklists.V1'
 Module 14 15 'HotelOps.Housekeeping.V1'
 Module 16 17 'HotelOps.Progress.V1'
 foreach($entry in $manifest){TestSourceHash $entry}
 [ordered]@{mode='CREATED_NEW_DISPOSABLE';database=$database;owner='DEV1_FS013';sourceHashes='MatchedNormalizedTextLF';syntheticSeedOnly=$true;usersSeeded=$false;runtimeSchemaValidators='NotYetRun';existingDatabaseWritten=$false;dropOrReset=$false}|ConvertTo-Json
}catch{
 # Preserve any partial target; no cleanup DROP and no provider/credential payload in stdout.
 [ordered]@{mode='SETUP_FAILED';database=$database;createPhaseCompleted=$created;partialTargetMayExist=$true;resetAttempted=$false;error='SQL_SETUP_FAILED_REVIEW_PRIVATE_DIAGNOSTICS'}|ConvertTo-Json
 throw 'FS013 setup did not complete; no automatic rollback/drop of a created database.'
}finally{if($transaction){$transaction.Dispose()};if($connection){$connection.Dispose()};$budget.Dispose()}
