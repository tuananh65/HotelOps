#requires -Version 5.1
# Explicit PL approval ENV-NOTIF-001: one NEW database, hax existing rights only.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory,[switch]$Execute,
 [guid]$ResumeDatabaseGuid=[guid]::Empty,[guid]$ResumeLeaseId=[guid]::Empty)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$target='HotelOps_ENVNOTIF001_Trial_20261109'
if([Security.Principal.WindowsIdentity]::GetCurrent().Name -cne 'DESKTOP-JI8U50T\hax'){throw 'Run only as interactive hax.'}
$null=New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$manifestPath=Join-Path $EvidenceDirectory 'manifest.json'
if(Test-Path $manifestPath){throw 'Existing manifest: no overwrite/adoption/lease extension.'}
$spec=@(
 'scripts/database/release/01_HotelOps_Full_Schema.sql',
 'scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql',
 'database/patches/20261019_inventory_contract.sql',
 'database/patches/20261020_material_request_contract.sql',
 'database/patches/20261021_replenishment_contract.sql',
 'database/patches/20261022_maintenance_material_usage_contract.sql',
 'scripts/database/release/canonical/00_Canonical_Reference_Contract.sql',
 'scripts/database/release/canonical/01_Canonical_Seed.sql',
 'scripts/verification/UserProfileUpdate/rowversion-forward.sql',
 'scripts/database/release/uc017/05_UC017_Branch_RoomTypes.sql',
 'scripts/database/release/checklists/01_Checklists.sql',
 'scripts/database/release/housekeeping/01_Housekeeping.sql',
 'scripts/database/release/progress/01_Progress.sql',
 'scripts/database/release/fs013-candidate/01_Install.sql',
 'scripts/database/release/fs013-candidate/02_NotificationProposal.sql',
 'scripts/database/release/fs013-candidate/07_ExecutableCandidate.sql',
 'scripts/database/release/fs013-candidate/10_SqlWriteBoundary.sql',
 'scripts/database/release/fs013-candidate/11_CleaningExecution.sql',
 'scripts/database/release/fs014-candidate/01_ResumeV1.sql',
 'scripts/database/release/fs015-candidate/01_ClosureWorkflow.sql',
 'scripts/database/release/fs016-candidate/01_Rework.sql'
)
function Render([string]$text,[bool]$legacy){
 # Only exact-target adaptation in copies. No removal of identity/schema/permission guards.
 if($legacy){$text=$text.Replace('HotelOpsDB',$target)}
 $text=$text.Replace('__REVIEWED_TARGET_DATABASE__',$target)
 foreach($pattern in @('HotelOps[_]Canonical[_]%[_]AuthUpgrade','HotelOps[_]Canonical[_]%[_]Upgrade','HotelOps[_]Canonical[_]%[_]Fresh','HotelOps[_]Canonical[_]%')){
  $text=$text.Replace($pattern,$target.Replace('_','[_]'))
 }
 foreach($old in @('HotelOps_Canonical_20261106_f130001_Fresh','HotelOps_Canonical_FS014_Resume_V1_Fresh','HotelOps_Canonical_FS015_Cleaning_V1_Fresh','HotelOps_Canonical_FS016_Rework_V1_Fresh')){
  $text=$text.Replace($old,$target)
 }
 return $text
}
$stages=@();$i=0
foreach($path in $spec){
 $source=Join-Path $repo $path;$rendered=Render ([IO.File]::ReadAllText($source)) ($i -le 5)
 $output=Join-Path $EvidenceDirectory ('{0:D2}.sql' -f $i)
 [IO.File]::WriteAllText($output,$rendered,[Text.UTF8Encoding]::new($false))
 $stages+=[pscustomobject]@{path=$path;source_sha256=(Get-FileHash $source).Hash;rendered_sha256=(Get-FileHash $output).Hash;file=$output};$i++
}
if(!$Execute){$stages | ConvertTo-Json -Depth 5;return}
$stages | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'source-manifest.json') -Encoding UTF8
$cs=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$cs['Data Source']='tcp:DESKTOP-JI8U50T,1433';$cs['Initial Catalog']='master'
$cs['Integrated Security']=$true;$cs['Encrypt']=$true;$cs['TrustServerCertificate']=$false
$cs['Pooling']=$false;$cs['Connect Timeout']=10
$master=New-Object System.Data.SqlClient.SqlConnection $cs.ConnectionString
$c=$null
function Command($connection,[string]$sql,[hashtable]$parameters=@{},[switch]$Scalar){
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql
 foreach($k in $parameters.Keys){$null=$cmd.Parameters.AddWithValue($k,$parameters[$k])}
 try{if($Scalar){return $cmd.ExecuteScalar()}else{$null=$cmd.ExecuteNonQuery()}}finally{$cmd.Dispose()}
}
function Marker([string]$name,[string]$value){
 Command $c 'EXEC sys.sp_addextendedproperty @name=@name,@value=@value' @{'@name'=$name;'@value'=$value}
}
function Module-Marker([int]$index){
 $names=@{10='HotelOps.Checklists.V1';11='HotelOps.Housekeeping.V1';12='HotelOps.Progress.V1'}
 if($names.ContainsKey($index)){
  $name=$names[$index]
  $current=Command $c 'SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=@n' @{'@n'=$name} -Scalar
  if($null -eq $current -or $current -is [DBNull]){Marker $name $stages[$index].source_sha256}
  elseif($current -cne $stages[$index].source_sha256){throw 'Module provenance marker mismatch.'}
 }
}
$fileLock=$null
try{
 $fileLock=[IO.File]::Open((Join-Path $EvidenceDirectory 'provision.runner.lock'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 $master.OpenAsync().GetAwaiter().GetResult() | Out-Null
 $identity=Command $master "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))+N'|'+ORIGINAL_LOGIN()" @{} -Scalar
 if($identity -cne 'DESKTOP-JI8U50T\SQLEXPRESS|DESKTOP-JI8U50T\hax'){throw 'Actual SQL identity mismatch.'}
 $lock=Command $master "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:ENVNOTIF001:Provision',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0;SELECT @r" @{} -Scalar
 if($lock -lt 0){throw 'Another provisioning runner active.'}
 $exists=Command $master 'SELECT DB_ID(@name)' @{'@name'=$target} -Scalar
 $resuming=$null -ne $exists -and $exists -isnot [DBNull]
 if($resuming -and ($ResumeDatabaseGuid -eq [guid]::Empty -or $ResumeLeaseId -eq [guid]::Empty)){throw 'Target exists: explicit captured GUID/token required to resume own partial provisioning.'}
 # Original release installer creates only this exact database and retains its no-overwrite checks.
 if(!$resuming){foreach($batch in [regex]::Split([IO.File]::ReadAllText($stages[0].file),'(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$')){
  if($batch.Trim()){Command $master $batch}
 }}
 $cs['Initial Catalog']=$target;$c=New-Object System.Data.SqlClient.SqlConnection $cs.ConnectionString
 $c.OpenAsync().GetAwaiter().GetResult() | Out-Null
 if($resuming){
  $actualGuid=Command $c 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()' @{} -Scalar
  $actualToken=Command $c "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaTrial'" @{} -Scalar
  $owner=Command $c "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaOwner'" @{} -Scalar
  if([guid]$actualGuid -ne $ResumeDatabaseGuid -or [string]$actualToken -ne $ResumeLeaseId.ToString() -or $owner -cne 'DEV1 ENV-001 / DEV2'){throw 'Partial database ownership mismatch.'}
  $leaseId=$ResumeLeaseId
 }else{
 $leaseId=[guid]::NewGuid()
 Marker 'HotelOps.NotificationSchemaOwner' 'DEV1 ENV-001 / DEV2'
 Marker 'HotelOps.NotificationSchemaTrial' $leaseId.ToString()
 Marker 'HotelOps.UserDirectory.Trial' 'Task17'
 foreach($module in @('FS013','FS014','FS015','FS016')){Marker ('HotelOps.'+$module+'.Owner') ('DEV1_'+$module)}
 $createdGuid=Command $c 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()' @{} -Scalar
 [pscustomobject]@{database_guid=[string]$createdGuid;lease_id=$leaseId.ToString();target=$target;utc=[DateTimeOffset]::UtcNow.ToString('o')} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'created.json') -Encoding UTF8
 }
 for($i=1;$i -lt $stages.Count;$i++){
  $installed=Command $c 'SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=@n' @{'@n'=('HotelOps.NotificationStage.'+$i)} -Scalar
  if($null -ne $installed -and $installed -isnot [DBNull]){
   if($installed -cne $stages[$i].rendered_sha256){throw 'Installed stage hash drift: no overwrite.'}
   Module-Marker $i
   continue
  }
  # Outer SQL transaction makes a whole stage + provenance marker atomic across GO batches.
  # Existing inner transactions remain nested; a failure rolls back this stage, not previous stages.
  Command $c 'BEGIN TRANSACTION;'
  try{
  foreach($batch in [regex]::Split([IO.File]::ReadAllText($stages[$i].file),'(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$')){
   if($batch.Trim()){Command $c $batch}
  }
  Marker ('HotelOps.NotificationStage.'+$i) $stages[$i].rendered_sha256
  Module-Marker $i
  Command $c 'COMMIT;'
  }catch{try{Command $c 'IF @@TRANCOUNT>0 ROLLBACK;'}catch{};throw}
  [pscustomobject]@{stage=$i;status='PASS';utc=[DateTimeOffset]::UtcNow.ToString('o')} |
   ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory ('stage-'+$i+'.json')) -Encoding UTF8
 }
 Marker 'HotelOps.IntegrationTrial' 'DEV2-INTEGRATION-02-SQL-PROVISIONAL'
 # Actual foundation provisioning completion; the 24-hour lease is never guessed or extended.
 $completed=[DateTimeOffset]::UtcNow;$leaseEnd=$completed.AddHours(24).ToString('o')
 Marker 'HotelOps.NotificationSchemaLeaseEndUtc' $leaseEnd
 $guid=Command $c 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()' @{} -Scalar
 $prior=Command $c "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)),2) FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source')" @{} -Scalar
 $head=(& git -C $repo rev-parse HEAD).Trim()
 [ordered]@{server='DESKTOP-JI8U50T\SQLEXPRESS';database=$target;database_guid=[string]$guid;
  owner='DEV1 ENV-001';schema_owner='DEV2';lease_id=$leaseId.ToString();lease_start_utc=$completed.ToString('o');lease_end_utc=$leaseEnd;
  source_commit=$head;prior_trigger_sha256=$prior;stages=$stages;producers_enabled=$false;workers_enabled=$false;
  foundation_provisioning='PASS';reconciliation='PENDING';acceptance_mutations='NOT_RUN'} |
  ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
} catch {
 [ordered]@{status='BLOCKED';target=$target;utc=[DateTimeOffset]::UtcNow.ToString('o');automatic_cleanup=$false} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'provision-blocked.json') -Encoding UTF8
 throw
} finally{
 if($null -ne $c){$c.Dispose()};$master.Dispose()
 if($null -ne $fileLock){$fileLock.Dispose();Remove-Item -LiteralPath (Join-Path $EvidenceDirectory 'provision.runner.lock')}
}

