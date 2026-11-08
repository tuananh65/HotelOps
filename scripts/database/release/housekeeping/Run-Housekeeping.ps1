param(
 [ValidateSet('Apply','Verify','Rollback')][string]$Stage='Verify',
 [Parameter(Mandatory=$true)][string]$Database,
 [Parameter(Mandatory=$true)][string]$Receipt,
 [string]$Server='DESKTOP-JI8U50T\SQLEXPRESS',
 [switch]$EvidenceInvalidation
)
$ErrorActionPreference='Stop'
if($Server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $Database -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}_(Fresh|Upgrade|AuthUpgrade)$'){throw 'Only reviewed disposable SQL trials are allowed'}
if(Test-Path -LiteralPath $Receipt){throw 'Receipt already exists'}
$hashes=[ordered]@{}
foreach($source in @('01_Housekeeping.sql','02_Verify.sql','03_Rollback.sql','04_EvidenceInvalidation.sql','05_VerifyEvidence.sql','Run-Housekeeping.ps1')){$hashes[$source]=(Get-FileHash (Join-Path $PSScriptRoot $source)).Hash}
$report=[ordered]@{stage=$Stage;database=$Database;startedUtc=[DateTime]::UtcNow.ToString('O');sourceHashes=$hashes;success=$false;trialOnly=$true;error=$null}
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Server;$builder['Initial Catalog']=$Database;$builder['Integrated Security']=$true;$builder['TrustServerCertificate']=$true;$builder['Application Name']='HotelOps Housekeeping V1 explicit disposable deployment'
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$transaction=$null
function Execute([string]$sql){$cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=45;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=15;$cmd.CommandText=$sql;try{return $cmd.ExecuteScalarAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}}
function Run([string]$source){foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PSScriptRoot $source)),'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch.Trim()}}}
function Definitions([string]$source){
 $definitions=@{}
 foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PSScriptRoot $source)),'(?im)^\s*GO\s*\r?$')){
  if($batch.Trim() -match '^CREATE(?: OR ALTER)? TRIGGER dbo\.([A-Za-z0-9_]+)'){$definitions[$Matches[1]]=$batch.Trim()}
 }
 return $definitions
}
function DefinitionText([string]$definition){
 # SQL Server persists CREATE OR ALTER as CREATE followed by padding. Only normalize DDL header.
 return [regex]::Replace($definition.Trim(),'^(?:CREATE OR ALTER|ALTER|CREATE)\s+TRIGGER\b','CREATE TRIGGER')
}
function VerifyDefinitions($definitions){
 foreach($triggerName in $definitions.Keys){
  $actual=Scalar ("SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo."+$triggerName+"'))")
  if($null -eq $actual -or $actual -is [DBNull] -or (DefinitionText $actual) -cne (DefinitionText $definitions[$triggerName])){throw ('Trigger definition drift: '+$triggerName)}
 }
}
try{
 $connection.OpenAsync().GetAwaiter().GetResult()
 if((Scalar 'SELECT DB_NAME()') -cne $Database -or (Scalar 'SELECT CONVERT(nvarchar(128),@@SERVERNAME)') -cne $Server -or (Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserDirectory.Trial'") -cne 'Task17'){throw 'Trial identity/marker mismatch'}
 $transaction=$connection.BeginTransaction()
 Execute "SET XACT_ABORT ON; SET LOCK_TIMEOUT 10000; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @r<0 THROW 51730,'Checklist deployment lock unavailable.',1;"
 $installed=Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.V1'"
 if($installed -isnot [DBNull] -and $null -ne $installed -and $installed -cne $hashes['01_Housekeeping.sql']){throw 'Unknown package hash; no automatic repair'}
 if($Stage -eq 'Apply' -and ($installed -is [DBNull] -or $null -eq $installed)){
  Run '01_Housekeeping.sql'
  Execute ("EXEC sys.sp_addextendedproperty @name=N'HotelOps.Housekeeping.V1',@value=N'"+$hashes['01_Housekeeping.sql']+"';")
 }elseif($installed -is [DBNull] -or $null -eq $installed){throw 'Housekeeping module is not deployed'}
 Run '02_Verify.sql'
 $expected=Definitions '01_Housekeeping.sql'
 $evidenceInstalled=Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.EvidenceRevision.V1'"
 $hasEvidence=$null -ne $evidenceInstalled -and $evidenceInstalled -isnot [DBNull]
 if($hasEvidence -and $evidenceInstalled -cne $hashes['04_EvidenceInvalidation.sql']){throw 'Unknown evidence package hash; no automatic repair'}
 if(!$hasEvidence){
  if((Scalar "SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_hk_attachment_revision_v1')") -ne 0){throw 'Partial evidence amendment; no automatic repair'}
  VerifyDefinitions $expected
  if($EvidenceInvalidation){
   if($Stage -ne 'Apply'){throw 'Evidence revision amendment is not deployed'}
   Run '04_EvidenceInvalidation.sql'
   Execute ("EXEC sys.sp_addextendedproperty @name=N'HotelOps.Housekeeping.EvidenceRevision.V1',@value=N'"+$hashes['04_EvidenceInvalidation.sql']+"';")
   $hasEvidence=$true
  }
 }
 if($hasEvidence){
  $amended=Definitions '04_EvidenceInvalidation.sql'
  foreach($name in $amended.Keys){$expected[$name]=$amended[$name]}
  Run '05_VerifyEvidence.sql'
 }
 VerifyDefinitions $expected
 $report['evidenceInvalidation']=$hasEvidence
 if($Stage -eq 'Rollback'){Run '03_Rollback.sql'}
 $transaction.Commit();$report.success=$true
}catch{if($null -ne $transaction -and $null -ne $transaction.Connection){$transaction.Rollback()};$report.error=$_.Exception.Message;throw}
finally{if($null -ne $transaction){$transaction.Dispose()};$connection.Dispose();$report.finishedUtc=[DateTime]::UtcNow.ToString('O');$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $Receipt}
