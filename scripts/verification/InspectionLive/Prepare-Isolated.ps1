param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($out)|Out-Null
$server='DESKTOP-JI8U50T\SQLEXPRESS'
if($env:COMPUTERNAME -cne 'DESKTOP-JI8U50T'){throw 'Unexpected machine'}
$db='HotelOps_Canonical_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)+'_Fresh'
$owner=[Guid]::NewGuid().ToString('D')
$c=New-Object System.Data.SqlClient.SqlConnection 'Data Source=lpc:.\SQLEXPRESS;Initial Catalog=master;Integrated Security=true;Encrypt=false;Connect Timeout=5;Application Name=HotelOps FE05 FAST001 setup'
$marker=$false
function Scalar($sql){$q=$c.CreateCommand();$q.CommandText=$sql;try{return $q.ExecuteScalar()}finally{$q.Dispose()}}
function Guard {
 if((Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))") -cne $env:COMPUTERNAME -or (Scalar 'SELECT CONVERT(nvarchar(128),@@SERVERNAME)') -cne $server){throw 'Instance mismatch'}
 $current=Scalar 'SELECT DB_NAME()'
 if($current -cne 'master' -and $current -cne $db){throw 'Database mismatch'}
 if($marker -and $current -eq $db -and (Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner'") -cne $owner){throw 'Owner mismatch'}
}
function Execute($sql){Guard;$q=$c.CreateCommand();$q.CommandTimeout=120;$q.CommandText=$sql;try{[void]$q.ExecuteNonQuery()}finally{$q.Dispose()}}
$report=[ordered]@{server=$server;database=$db;owner=$owner;success=$false;sourceHashes=@{};createdByThisRun=$false;syntheticOnly=$true}
try{
 $c.Open();Guard
 if((Scalar "SELECT DB_ID(N'$db')") -isnot [DBNull]){throw 'Refusing existing DB'}
 foreach($relative in @('scripts/database/release/01_HotelOps_Full_Schema.sql','scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql','scripts/database/release/canonical/00_Canonical_Reference_Contract.sql','scripts/database/release/canonical/01_Canonical_Seed.sql','scripts/verification/UserProfileUpdate/rowversion-forward.sql')){
  $path=Join-Path $root $relative;$report.sourceHashes[$relative]=(Get-FileHash $path).Hash
  $sql=[IO.File]::ReadAllText($path)
  if($relative -notlike '*rowversion-forward.sql'){$sql=$sql.Replace('HotelOpsDB',$db).Replace('__REVIEWED_TARGET_DATABASE__',$db)}
  foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
  if(!$marker){
   if((Scalar 'SELECT DB_NAME()') -cne $db -or (Scalar 'SELECT COUNT(*) FROM dbo.users') -ne 0){throw 'Not empty new schema'}
   $report.createdByThisRun=$true
   Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE05INT001.Owner',@value=N'$owner'; EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserDirectory.Trial',@value=N'Task17';"
   $marker=$true
   Execute "ALTER DATABASE [$db] SET RECOVERY SIMPLE; ALTER DATABASE [$db] MODIFY FILE(NAME=N'$db',MAXSIZE=256MB); ALTER DATABASE [$db] MODIFY FILE(NAME=N'${db}_log',MAXSIZE=128MB);"
  }
 }
 foreach($module in @('uc017/Run-Uc017.ps1','checklists/Run-Checklists.ps1','housekeeping/Run-Housekeeping.ps1')){
  Guard
  & (Join-Path $root ('scripts/database/release/'+$module)) -Stage Apply -Server $server -Database $db -Receipt (Join-Path $out ($module.Split('/')[0]+'-install.json'))
 }
 $report.usersBeforeFixtures=Scalar 'SELECT COUNT(*) FROM dbo.users'
 $report.version=Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion'))"
 $report.success=$true
}finally{
 $c.Dispose();[IO.File]::WriteAllText((Join-Path $out 'environment.json'),($report|ConvertTo-Json -Depth 8),(New-Object System.Text.UTF8Encoding($false)))
}
Write-Output ($report|ConvertTo-Json -Depth 8)
