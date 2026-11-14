#requires -Version 5.1
[CmdletBinding()]
param(
 [ValidateSet('Install','Verify','Rollback')][string]$Stage='Verify',
 [Parameter(Mandatory=$true)][string]$Manifest,
 [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
 [switch]$ConsumersStopped
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
. (Join-Path $PSScriptRoot 'Safety.ps1')
$m=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$lease=Assert-NotificationTrialManifest $m ([Security.Principal.WindowsIdentity]::GetCurrent().Name)
if(!$ConsumersStopped -and $Stage -ne 'Verify'){throw 'Mutation requires owned consumers stopped and exclusive runner confirmation.'}
$start=$lease.Start;$end=$lease.End
$null=New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$capturePath=Join-Path $EvidenceDirectory 'prior-trigger.clixml'
$lockPath=Join-Path $EvidenceDirectory 'schema002.runner.lock'
$fileLock=[IO.File]::Open($lockPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
$cs=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$cs['Data Source']='tcp:DESKTOP-JI8U50T,1433'
$cs['Initial Catalog']=$m.database
$cs['Integrated Security']=$true
$cs['Encrypt']=$true
$cs['TrustServerCertificate']=$false
$cs['Connect Timeout']=10
$cs['Pooling']=$false
$c=New-Object System.Data.SqlClient.SqlConnection $cs.ConnectionString
function Rows([string]$sql,[hashtable]$parameters=@{}){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=$sql
 foreach($key in $parameters.Keys){$null=$cmd.Parameters.AddWithValue($key,$parameters[$key])}
 try{$reader=$cmd.ExecuteReader();$table=New-Object System.Data.DataTable;$table.Load($reader);$reader.Dispose();return ,$table}
 finally{$cmd.Dispose()}
}
function Execute([string]$sql,[hashtable]$parameters=@{}){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=$sql
 foreach($key in $parameters.Keys){$null=$cmd.Parameters.AddWithValue($key,$parameters[$key])}
 try{$null=$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}
}
function Read-Sql([string]$relative){[IO.File]::ReadAllText((Join-Path $root $relative))}
function Render-N01([string]$relative){
 # Frozen source bytes remain unchanged; retain every guard with an exact approved target substitution.
 $text=Read-Sql $relative
 if(!$text.Contains("N'KIENPC'") -or !$text.Contains("N'HotelOps_DEV2_FullStack_Trial'")){throw 'Unexpected N01 template.'}
 $text.Replace("N'KIENPC'","N'DESKTOP-JI8U50T\SQLEXPRESS'").Replace("N'HotelOps_DEV2_FullStack_Trial'","N'HotelOps_ENVNOTIF001_Trial_20261109'")
}
function Preflight {
 $result=Rows (Read-Sql 'scripts/database/notifications/Schema002/Preflight.sql') @{
 '@LeaseId'=[guid]$m.lease_id;'@LeaseStart'=$start.UtcDateTime;'@LeaseEnd'=$end.UtcDateTime;'@LeaseEndText'=[string]$m.lease_end_utc}
 if($result.Rows.Count -ne 1 -or [string]$result.Rows[0].database_guid -ne [string]$m.database_guid){throw 'Database GUID mismatch.'}
}
function Capture {
 $r=Rows "SELECT definition,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),2) AS sha FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source')"
 if($r.Rows.Count -ne 1){throw 'Readable prior trigger required.'}
 return [pscustomobject]@{definition=[string]$r.Rows[0].definition;sha=[string]$r.Rows[0].sha;database_guid=$m.database_guid;lease_id=$m.lease_id}
}
try{
 $c.OpenAsync().GetAwaiter().GetResult() | Out-Null
 $lock=Rows "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:NotificationSchema002:Runner',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r AS result;"
 if([int]$lock.Rows[0].result -lt 0){throw 'Another mutation/verification runner owns SQL lease.'}
 Preflight
 Execute "EXEC sys.sp_set_session_context @key=N'HotelOps.FE04.ApprovedTrial',@value=N'FE04-P0-APPROVED-SCHEMA'; EXEC sys.sp_set_session_context @key=N'HotelOps.N01.ApprovedTrial',@value=N'N01-IMPLEMENT06-APPROVED-ISOLATED-SCHEMA';"
 if($Stage -eq 'Install'){
  if(Test-Path $capturePath){throw 'Capture already exists: inspect partial installation; never overwrite recovery evidence.'}
  $capture=Capture
  if($capture.sha -cne $m.prior_trigger_sha256){throw 'Prior trigger differs from owner-approved manifest.'}
  # Export before the first DDL. Capture UTF-16 SQL definition hash, not file/UTF-8 hash.
  $capture | Export-Clixml -LiteralPath $capturePath
  Execute (Render-N01 'scripts/database/notifications/N01Implement06/01_ForwardCandidate.sql')
  Preflight
  Execute "EXEC sys.sp_set_session_context @key=N'HotelOps.FE04.PreviousTriggerSha256',@value=@sha" @{'@sha'=[byte[]]([System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary]::Parse($capture.sha).Value)}
  Execute (Read-Sql 'scripts/database/notifications/Fe04UnblockP0/01_ReconcileCandidate.sql')
  $installed=Capture
  $installed | Export-Clixml -LiteralPath (Join-Path $EvidenceDirectory 'installed-trigger.clixml')
 }
 if($Stage -eq 'Rollback'){
  $capture=Import-Clixml -LiteralPath $capturePath
  if($capture.database_guid -ne $m.database_guid -or $capture.lease_id -ne $m.lease_id){throw 'Recovery capture belongs to another trial/lease.'}
  if($capture.sha -cne $m.prior_trigger_sha256){throw 'Prior trigger recovery hash not approved.'}
  $installedPath=Join-Path $EvidenceDirectory 'installed-trigger.clixml'
  $marker=Rows "SELECT COUNT(*) AS count FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.NotificationReconciliation'"
  if([int]$marker.Rows[0].count -eq 1 -and (Test-Path $installedPath)){
   $installed=Import-Clixml -LiteralPath $installedPath
   Execute (Read-Sql 'scripts/database/notifications/Fe04UnblockP0/02_RollbackCandidate.sql') @{
   '@PriorTrigger'=[string]$capture.definition;
   '@PriorSha'=[byte[]]([System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary]::Parse($capture.sha).Value);
   '@InstalledSha'=[byte[]]([System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary]::Parse($installed.sha).Value)}
  }else{
   # N01 may have committed before reconciliation failed. Never invent an installed hash.
   $current=Capture
   if($current.sha -cne $capture.sha -or [int]$marker.Rows[0].count -ne 0){throw 'Partial install requires owner recovery; installed capture missing.'}
  }
  Preflight
  Execute (Render-N01 'scripts/database/notifications/N01Implement06/02_RollbackCandidate.sql')
 } else {
  Execute (Read-Sql 'scripts/database/notifications/Schema002/Verify.sql')
  $expected=Import-Clixml -LiteralPath (Join-Path $EvidenceDirectory 'installed-trigger.clixml')
  $actual=Capture
  if($actual.sha -cne $expected.sha){throw 'Deployed trigger differs from captured installation.'}
 }
 $catalog=Rows "SELECT OBJECT_NAME(object_id) AS table_name,name,is_unique,has_filter,filter_definition,is_disabled FROM sys.indexes WHERE object_id IN(OBJECT_ID('dbo.notification_publications'),OBJECT_ID('dbo.notification_operation_reservations'),OBJECT_ID('dbo.notification_delivery_intents')) AND index_id>0"
 @($catalog.Rows | ForEach-Object {[pscustomobject]@{table_name=$_.table_name;name=$_.name;is_unique=$_.is_unique;has_filter=$_.has_filter;filter_definition=if($_.filter_definition -is [DBNull]){$null}else{$_.filter_definition};is_disabled=$_.is_disabled}}) |
  ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory ('catalog-'+$Stage+'.json')) -Encoding UTF8
 [pscustomobject]@{stage=$Stage;status='PASS';server=$m.server;database=$m.database;database_guid=$m.database_guid;lease_id=$m.lease_id;utc=[DateTimeOffset]::UtcNow.ToString('o');acceptance_mutations='NOT_RUN'} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory ($Stage+'-'+[guid]::NewGuid().ToString('N')+'.json')) -Encoding UTF8
} catch {
 if($c.State -eq 'Open'){try{Execute 'IF @@TRANCOUNT>0 ROLLBACK;'}catch{}}
 throw
} finally {
 $c.Dispose();$fileLock.Dispose();Remove-Item -LiteralPath $lockPath
}

