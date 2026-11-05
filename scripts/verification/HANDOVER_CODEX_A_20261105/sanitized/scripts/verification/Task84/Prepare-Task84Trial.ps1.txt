$ErrorActionPreference='Stop'
$task84Root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$task84Target=([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'TRIAL_NAME.proposed.txt'))).Trim()
if($task84Target -cne 'HotelOps_Canonical_20261104_044211_7a6d6e19_Fresh'){throw 'Unapproved trial name'}
# Existing setup checks exact instance and refuses any existing target; no reset/drop path.
& (Join-Path $task84Root 'scripts/verification/UserDirectory/Prepare-Database.ps1') -Database $task84Target
$task84Connection=New-Object System.Data.SqlClient.SqlConnection "Data Source=REDACTED_LOCAL_SQL_HOST\SQLEXPRESS;Initial Catalog=$task84Target;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps Task84 approved new trial"
try {
 $task84Connection.Open()
 $task84Identity=$task84Connection.CreateCommand()
 $task84Identity.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')"
 $task84Reader=$task84Identity.ExecuteReader()
 if(!$task84Reader.Read() -or $task84Reader.GetString(0) -cne 'REDACTED_LOCAL_SQL_HOST\SQLEXPRESS' -or $task84Reader.GetString(1) -cne $task84Target -or $task84Reader.IsDBNull(2) -or $task84Reader.GetString(2) -cne 'Task17'){throw 'Trial identity/marker mismatch'}
 $task84Reader.Dispose();$task84Identity.Dispose()
 foreach($task84Path in @((Join-Path $task84Root 'scripts/verification/UserProfileUpdate/rowversion-forward.sql'),(Join-Path $PSScriptRoot '05_UC017_Branch_RoomTypes.sql.proposal'),(Join-Path $PSScriptRoot '06_UC017_Verify.sql.proposal'))){
  $task84Command=$task84Connection.CreateCommand();$task84Command.CommandTimeout=120
  $task84Command.CommandText=[IO.File]::ReadAllText($task84Path)
  try{[void]$task84Command.ExecuteNonQuery()}finally{$task84Command.Dispose()}
 }
 Write-Output "PASS: guarded new Task84 trial and historical UC017 V1 prerequisites; retained $task84Target"
} finally {$task84Connection.Dispose()}
