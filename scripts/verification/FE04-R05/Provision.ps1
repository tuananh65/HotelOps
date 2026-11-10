$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$target='HotelOps_Canonical_FE04_R05_20261110_Fresh'
$c=[System.Data.SqlClient.SqlConnection]::new('Data Source=tcp:DESKTOP-JI8U50T,1433;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5;Application Name=FE04-R05-disposable')
$budget=[Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(15))
function Scalar($s){$q=$c.CreateCommand();$q.CommandText=$s;try{$q.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()}finally{$q.Dispose()}}
function Raw($s){$q=$c.CreateCommand();$q.CommandTimeout=90;$q.CommandText=$s;try{[void]$q.ExecuteNonQueryAsync($budget.Token).GetAwaiter().GetResult()}finally{$q.Dispose()}}
function Identity{
 if((Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))") -cne 'DESKTOP-JI8U50T' -or (Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))") -cne 'SQLEXPRESS'){throw 'INSTANCE_MISMATCH'}
}
function Guard{
 Identity
 if([int](Scalar 'SELECT COUNT(*) FROM sys.databases WHERE database_id=DB_ID() AND owner_sid=0x01') -ne 1){throw 'R05_DATABASE_OWNER_MISMATCH'}
 if((Scalar 'SELECT DB_NAME()') -cne $target -or (Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.UnblockR01.Owner'") -cne 'DEV1_DISPOSABLE' -or (Scalar "SELECT CONVERT(nvarchar(40),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.R05.Source'") -cne 'd37e99f42eaaf3089eb510c6298296a0f0743faa'){throw 'TARGET_OWNERSHIP_MISMATCH'}
}
function Exec($s){Guard;Raw $s}
function Batches($s){foreach($b in [regex]::Split($s,'(?im)^\s*GO\s*\r?$')){if($b.Trim()){Exec $b}}}
function Source($index){$s=[IO.File]::ReadAllText((Join-Path $root $manifest[$index].path));if($index -le 5){$s=$s.Replace('HotelOpsDB',$target)};if($index -eq 0){$parts=[regex]::Split($s,'(?im)^\s*GO\s*\r?$');$s=($parts[4..($parts.Length-1)] -join "`nGO`n")};Batches $s}
try{
 $manifest=Get-Content (Join-Path $PSScriptRoot 'SOURCE_MANIFEST_BASE.json') -Raw|ConvertFrom-Json
 foreach($e in $manifest){$txt=[IO.File]::ReadAllText((Join-Path $root $e.path)).Replace("`r`n","`n").TrimStart([char]0xFEFF);$h=[Security.Cryptography.SHA256]::Create();try{$hash=([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($txt)))).Replace('-','')}finally{$h.Dispose()};if($hash -ine $e.normalizedSha256){throw ('SOURCE_HASH_MISMATCH '+$e.path)}}
 $c.OpenAsync($budget.Token).GetAwaiter().GetResult();Identity
 if((Scalar 'SELECT DB_NAME()') -cne 'master' -or [int](Scalar "SELECT COUNT(*) FROM sys.databases WHERE name=N'$target'") -ne 0){throw 'NO_OVERWRITE_NEW_TARGET_REQUIRED'}
 # Only master mutation: create the exact absent disposable database.
 Raw "CREATE DATABASE [$target]";Raw "ALTER AUTHORIZATION ON DATABASE::[$target] TO sa";$c.ChangeDatabase($target);Identity
 if((Scalar 'SELECT DB_NAME()') -cne $target -or [int](Scalar 'SELECT COUNT(*) FROM sys.tables') -ne 0){throw 'NEW_EMPTY_TARGET_REQUIRED'}
 Raw "EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.UnblockR01.Owner',@value=N'DEV1_DISPOSABLE';EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.R05.Source',@value=N'd37e99f42eaaf3089eb510c6298296a0f0743faa';EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS016.Owner',@value=N'DEV1_FS016';EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserDirectory.Trial',@value=N'Task17';"
 Source 0;foreach($i in 1..9){Source $i}
 foreach($pair in @(@(10,11,''),@(12,13,'HotelOps.Checklists.V1'),@(14,15,'HotelOps.Housekeeping.V1'),@(16,17,'HotelOps.Progress.V1'))){Source $pair[0];if($pair[2]){Exec ("EXEC sys.sp_addextendedproperty @name=N'"+$pair[2]+"',@value=N'"+$manifest[$pair[0]].sha256.ToUpperInvariant()+"'")};Source $pair[1]}
 foreach($file in @('fs013-candidate/01_Install.sql','fs013-candidate/11_CleaningExecution.sql','fs014-candidate/01_ResumeV1.sql','fs013-candidate/02_NotificationProposal.sql','fs015-candidate/01_ClosureWorkflow.sql','fs016-candidate/01_Rework.sql')){
  if($file -like '*11_CleaningExecution*'){Exec 'CREATE USER fs013_app WITHOUT LOGIN;'}
  $s=[IO.File]::ReadAllText(($root+'/scripts/database/release/'+$file))
  foreach($name in @('HotelOps_Canonical_20261106_f130001_Fresh','HotelOps_Canonical_FS014_Resume_V1_Fresh','HotelOps_Canonical_FS015_Cleaning_V1_Fresh','HotelOps_Canonical_FS016_Rework_V1_Fresh')){$s=$s.Replace($name,$target)}
  foreach($v in @('FS013','FS014','FS015')){$s=$s.Replace(('HotelOps.'+$v+'.Owner'),'HotelOps.FS016.Owner').Replace(('DEV1_'+$v),'DEV1_FS016')};Batches $s
 }
 foreach($file in @('01_AuthorityCandidate.sql','02_ProjectionCandidate.sql')){Batches ([IO.File]::ReadAllText(($root+'/scripts/database/release/fe04-authority-r01/'+$file)))}
 Guard
 [pscustomobject]@{status='CANDIDATES_INSTALLED';database=$target;owner='DEV1_DISPOSABLE';source='d37e99f42eaaf3089eb510c6298296a0f0743faa';sharedWrites=0;productionWriter=$false}|ConvertTo-Json -Compress|Tee-Object (Join-Path $env:TEMP 'fe04-r05-provision-result.json')
}finally{$c.Dispose();$budget.Dispose()}
