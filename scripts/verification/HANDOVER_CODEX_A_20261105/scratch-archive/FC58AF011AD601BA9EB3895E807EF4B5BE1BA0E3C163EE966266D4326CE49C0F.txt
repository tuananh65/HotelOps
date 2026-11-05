param(
 [ValidateSet('Preflight','Apply','Verify','Rollback')][string]$Stage='Preflight',
 [Parameter(Mandatory=$true)][string]$Server,
 [Parameter(Mandatory=$true)][string]$Database,
 [Parameter(Mandatory=$true)][string]$Receipt
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Explicit trial-only extension to the existing release/canonical workflow.
# Server identity is supplied locally, never a credential or shared default.
if($Database -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}_(Fresh|Upgrade|AuthUpgrade)$'){throw 'Only disposable canonical trials are permitted'}
if(Test-Path -LiteralPath $Receipt){throw 'Receipt already exists; preserve evidence and choose an unused path'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$receiptPath=[IO.Path]::GetFullPath($Receipt)
if(!(Test-Path -LiteralPath (Split-Path $receiptPath) -PathType Container)){throw 'Receipt directory must already exist'}
$sourceNames=@('05_UC017_Branch_RoomTypes.sql','06_UC017_Verify.sql','07_UC017_Rollback.sql','Run-Uc017.ps1')
$hashes=[ordered]@{}
foreach($source in $sourceNames){$hashes[$source]=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $source) -Algorithm SHA256).Hash}
$report=[ordered]@{stage=$Stage;database=$Database;startedUtc=[DateTime]::UtcNow.ToString('o');sourceHashes=$hashes;trialOnly=$true;success=$false;stateBefore=$null;stateAfter=$null;error=$null}
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Server;$builder['Initial Catalog']=$Database;$builder['Integrated Security']=$true
$builder['TrustServerCertificate']=$true;$builder['Connect Timeout']=10;$builder['Application Name']='HotelOps UC017 explicit trial deployment'
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$transaction=$null
function Execute([string]$sql){
 $command=$connection.CreateCommand();$command.CommandTimeout=45;$command.CommandText=$sql
 if($null -ne $transaction){$command.Transaction=$transaction}
 try{[void]$command.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$command.Dispose()}
}
function State {
 $command=$connection.CreateCommand();$command.CommandTimeout=15
 if($null -ne $transaction){$command.Transaction=$transaction}
 $command.CommandText=@'
SELECT CASE
 WHEN NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract')
 AND COL_LENGTH(N'dbo.room_types',N'branch_id') IS NULL AND COL_LENGTH(N'dbo.room_types',N'standard_cleaning_minutes') IS NULL
 AND COL_LENGTH(N'dbo.room_types',N'default_checklist_id') IS NULL THEN N'Legacy'
 WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)=N'branch-v1') THEN N'BranchV1'
 ELSE N'Incompatible' END
'@
 try{return [string]$command.ExecuteScalarAsync().GetAwaiter().GetResult()}finally{$command.Dispose()}
}
try{
 $connection.OpenAsync().GetAwaiter().GetResult()
 $command=$connection.CreateCommand()
 $command.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial')"
 $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult()
 try{if(!$reader.Read() -or $reader.GetString(0) -cne $Server -or $reader.GetString(1) -cne $Database -or $reader.IsDBNull(2) -or $reader.GetString(2) -cne 'Task17'){throw 'Trial identity/marker mismatch'}}finally{$reader.Dispose();$command.Dispose()}
 $report.stateBefore=State
 if($report.stateBefore -eq 'Incompatible'){throw 'Unknown/partial UC017 schema; no automatic repair'}
 $transaction=$connection.BeginTransaction()
 Execute "SET LOCK_TIMEOUT 10000; SET XACT_ABORT ON;"
 if($Stage -eq 'Apply'){
  Execute ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '05_UC017_Branch_RoomTypes.sql')))
  Execute ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '06_UC017_Verify.sql')))
 }elseif($Stage -eq 'Rollback'){
  if($report.stateBefore -eq 'BranchV1'){
   # Validate the package before removing anything. Scoped data makes rollback THROW.
   Execute ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '06_UC017_Verify.sql')))
   Execute ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '07_UC017_Rollback.sql')))
  }
 }elseif($report.stateBefore -eq 'BranchV1'){
  Execute ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '06_UC017_Verify.sql')))
 }elseif($Stage -eq 'Verify'){throw 'UC017 migration is not deployed'}
 $report.stateAfter=State
 if($report.stateAfter -eq 'Legacy'){
  Execute "IF COL_LENGTH(N'dbo.room_types',N'type_code')<>30 OR COL_LENGTH(N'dbo.room_types',N'type_name')<>300 OR NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.room_types') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0 AND INDEX_COL(N'dbo.room_types',i.index_id,1)=N'type_code' AND INDEX_COL(N'dbo.room_types',i.index_id,2) IS NULL) THROW 51068,'Legacy metadata mismatch.',1;"
 }
 foreach($source in $sourceNames){if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $source) -Algorithm SHA256).Hash -cne $hashes[$source]){throw 'Deployment source changed during execution'}}
 $transaction.Commit();$transaction.Dispose();$transaction=$null
 $report.success=$true
 Write-Output ("PASS: UC017 "+$Stage+" "+$Database+" "+$report.stateBefore+" -> "+$report.stateAfter)
}catch{
 if($null -ne $transaction){try{$transaction.Rollback()}catch{}}
 # Do not persist provider messages, connection strings or credentials in evidence.
 $report.error='Deployment rejected or failed; transaction rolled back. Inspect the local console exception.'
 throw
}finally{
 if($null -ne $transaction){$transaction.Dispose()}
 $connection.Dispose()
 $report.finishedUtc=[DateTime]::UtcNow.ToString('o')
 $report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $receiptPath -Encoding UTF8
}
