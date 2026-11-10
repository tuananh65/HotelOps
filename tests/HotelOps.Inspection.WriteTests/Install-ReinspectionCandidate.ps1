param([Parameter(Mandatory=$true)][string]$EnvironmentReceipt,[Parameter(Mandatory=$true)][string]$Receipt)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expected=[IO.Path]::GetFullPath((Join-Path $root 'TestResults/fast015/sql-final/environment.json'))
if([IO.Path]::GetFullPath($EnvironmentReceipt) -cne $expected){throw 'Only current FAST015 fresh setup receipt permitted'}
$e=Get-Content -LiteralPath $EnvironmentReceipt -Raw|ConvertFrom-Json
if(!$e.success -or !$e.createdByThisRun -or !$e.syntheticOnly -or $e.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $e.database -cnotmatch '^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$'){throw 'Invalid isolated receipt'}
$c=[Data.SqlClient.SqlConnection]::new(('Data Source=lpc:.\SQLEXPRESS;Initial Catalog='+$e.database+';Integrated Security=true;Encrypt=false;Connect Timeout=5'))
try {
 $c.Open();$q=$c.CreateCommand();$q.CommandTimeout=120
 $q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner')"
 $r=$q.ExecuteReader();if(!$r.Read() -or $r.GetString(0) -cne $e.database -or $r.GetString(1) -cne $e.server -or $r.IsDBNull(2) -or $r.GetString(2) -cne $e.owner){throw 'Actual database/owner mismatch'};$r.Dispose()
 $q.CommandText="IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05.FAST014.Purpose') THROW 51997,'No overwrite',1; EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE05.FAST014.Purpose',@value=N'OWNED_COMPONENT_ONLY'; EXEC sys.sp_set_session_context @key=N'FE05.FAST014.Owner',@value=@owner;"
 [void]$q.Parameters.AddWithValue('@owner',[string]$e.owner);[void]$q.ExecuteNonQuery();$q.Parameters.Clear()
 $script=Join-Path $root 'scripts/database/fe05-fast014/01_ReinspectionCandidate.sql'
 $q.CommandText=[IO.File]::ReadAllText($script);[void]$q.ExecuteNonQuery()
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Receipt)))|Out-Null
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Receipt),(@{success=$true;database=$e.database;owner=$e.owner;componentOnly=$true;sourceSha256=(Get-FileHash -LiteralPath $script).Hash;productionSchemaAccepted=$false}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
 Write-Output 'PASS isolated UC047 component schema installed on new owned candidate target only'
}finally{$c.Dispose()}
