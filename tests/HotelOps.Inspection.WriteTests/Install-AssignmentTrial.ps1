param([Parameter(Mandatory=$true)][string]$EnvironmentReceipt,[Parameter(Mandatory=$true)][string]$Receipt)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if([IO.Path]::GetFullPath($EnvironmentReceipt) -cne [IO.Path]::GetFullPath((Join-Path $root 'TestResults/fast017/sql/environment.json'))){throw 'Exact FAST017 receipt required'}
$e=Get-Content -LiteralPath $EnvironmentReceipt -Raw|ConvertFrom-Json
if(!$e.success -or !$e.createdByThisRun -or !$e.syntheticOnly -or $e.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $e.database -cnotmatch '^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$'){throw 'Isolation receipt rejected'}
$c=[Data.SqlClient.SqlConnection]::new(('Data Source=lpc:.\SQLEXPRESS;Initial Catalog='+$e.database+';Integrated Security=true;Encrypt=false;Connect Timeout=5'))
try{
 $c.Open();$q=$c.CreateCommand();$q.CommandTimeout=120
 $q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner')"
 $r=$q.ExecuteReader();if(!$r.Read() -or $r.GetString(0) -cne $e.database -or $r.GetString(1) -cne $e.server -or $r.IsDBNull(2) -or $r.GetString(2) -cne $e.owner){throw 'Actual database/owner rejected'};$r.Dispose()
 $q.CommandText="IF EXISTS(SELECT 1 FROM dbo.users) THROW 51997,'Fresh empty synthetic trial required',1;";[void]$q.ExecuteNonQuery()
 $tx=$c.BeginTransaction([Data.IsolationLevel]::Serializable);$q.Transaction=$tx
 try{
  $script=Join-Path $root 'scripts/database/fe05-fast008/01_SupervisorBindingCandidate.sql'
  foreach($batch in [regex]::Split([IO.File]::ReadAllText($script),'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){$q.CommandText=$batch;[void]$q.ExecuteNonQuery()}}
  $q.CommandText="EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE05.AssignmentDomain',@value=N'FAST017_COMPONENT_NOT_ACCEPTED';";[void]$q.ExecuteNonQuery();$tx.Commit()
 }catch{$tx.Rollback();throw}finally{$tx.Dispose()}
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Receipt),(@{success=$true;database=$e.database;owner=$e.owner;sourceSha256=(Get-FileHash -LiteralPath $script).Hash;componentOnly=$true;notificationAccepted=$false}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
 'PASS unchanged supervisor candidate installed on owned FAST017 trial only'
}finally{$c.Dispose()}
