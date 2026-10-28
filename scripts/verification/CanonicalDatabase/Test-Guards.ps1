param([Parameter(Mandatory=$true)][string]$RunId)
$ErrorActionPreference='Stop'
if($RunId -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$'){throw 'Invalid trial RunId'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$runner=Join-Path $root 'scripts/database/release/canonical/Run-Canonical.ps1'
$results=[ordered]@{}
function Reject([string]$label,[scriptblock]$action,[string]$expected){
 try{& $action | Out-Null;throw 'Expected rejection did not happen'}catch{
  if($_.Exception.Message -notlike "*$expected*"){throw}
  $results[$label]='PASS'
 }
}
Reject 'mainDatabaseTarget' { & $runner -Stage Trial -RunId HotelOpsDB } 'Explicit unique trial RunId required'
Reject 'unreviewedInstance' { & $runner -Stage Preflight -Server 'unreviewed' } 'Unreviewed SQL instance'
Reject 'existingDatabase' { & $runner -Stage Trial -RunId $RunId } 'Refusing to overwrite existing database'
$target=$RunId+'_Fresh'
$c=New-Object System.Data.SqlClient.SqlConnection "Data Source=DESKTOP-JI8U50T\SQLEXPRESS;Initial Catalog=$target;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
function Execute([string]$sql){$cmd=$c.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Fingerprint{
 $cmd=$c.CreateCommand();$cmd.CommandText="SELECT (SELECT * FROM dbo.branches ORDER BY branch_id FOR JSON PATH) b,(SELECT * FROM dbo.departments ORDER BY department_id FOR JSON PATH) d,(SELECT * FROM dbo.reference_values ORDER BY reference_id FOR JSON PATH) r FOR JSON PATH"
 try{return [string]$cmd.ExecuteScalar()}finally{$cmd.Dispose()}
}
try{
 $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText='SELECT CONVERT(nvarchar(128),@@SERVERNAME)+''|''+DB_NAME()'
 if($cmd.ExecuteScalar() -cne ('DESKTOP-JI8U50T\SQLEXPRESS|'+$target)){throw 'Identity mismatch'};$cmd.Dispose()
 $before=Fingerprint
 Execute ([IO.File]::ReadAllText((Join-Path $root 'scripts/database/release/canonical/01_Canonical_Seed.sql')))
 if((Fingerprint) -cne $before){throw 'Canonical data changed on seed replay'}
 $results.seedReplayExactData='PASS'
 # Constraint rejection tests run in an explicit rollback-only transaction on the trial.
 Execute 'BEGIN TRANSACTION;'
 Reject 'foreignKeyEnforced' {Execute "INSERT dbo.departments(branch_id,department_code,department_name,is_active) VALUES(-1,'HK',N'Trial invalid FK',1)"} 'FOREIGN KEY'
 Reject 'scopeCheckEnforced' {Execute "UPDATE TOP(1) dbo.role_permissions SET data_scope='INVALID'"} 'CHECK constraint'
 Reject 'urgentPriorityRejected' {Execute "INSERT dbo.reference_values(group_code,value_code,display_name,is_active) VALUES('PRIORITY','URGENT',N'Must be rejected',1)"} 'CHECK constraint'
 Execute 'IF @@TRANCOUNT>0 ROLLBACK;'
 foreach($priority in @('LOW','NORMAL','HIGH','CRITICAL')){
  Execute "IF (SELECT COUNT(*) FROM dbo.reference_values WHERE group_code='PRIORITY' AND value_code='$priority' AND is_active=1)<>1 THROW 51930,'Missing canonical priority',1;"
 }
 Execute "IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='CRITICAL' AND is_active=1) THROW 51931,'SEVERITY changed',1;"
 $results.fourCanonicalPrioritiesAndSeparateSeverity='PASS'
 if((Fingerprint) -cne $before){throw 'Guard test changed canonical data'}
 $results.rollbackPreservedCanonicalData='PASS'
}finally{if($c.State -eq 'Open'){Execute 'IF @@TRANCOUNT>0 ROLLBACK;'};$c.Dispose()}
$output=Join-Path $PSScriptRoot ('evidence/'+$RunId+'-guards.json')
if(Test-Path $output){throw 'Guard evidence already exists'}
$results | ConvertTo-Json | Set-Content -LiteralPath $output -Encoding UTF8
$results | ConvertTo-Json
