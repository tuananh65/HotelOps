$ErrorActionPreference='Stop'
$task85bRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$task85bTarget=([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'TRIAL_NAME.proposed.txt'))).Trim()
if($task85bTarget -cne 'HotelOps_Canonical_20261104_133500_c03885b1_Fresh'){throw 'Unapproved trial name'}
$task85bReview=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SQL_SCOPE_REVIEW.proposal.json') -Raw | ConvertFrom-Json
foreach($task85bSource in $task85bReview.sources){
 if((Get-FileHash -LiteralPath (Join-Path $task85bRoot $task85bSource.path)).Hash -cne $task85bSource.sha256){throw 'Prerequisite bytes changed; stop before SQL'}
}
if((Get-FileHash -LiteralPath (Join-Path $task85bRoot 'scripts/verification/UserDirectory/Prepare-Database.ps1')).Hash -cne $task85bReview.prepareDatabaseSha256){throw 'Setup script changed; stop before SQL'}
# Existing setup checks exact instance and refuses any existing target; no reset/drop path.
& (Join-Path $task85bRoot 'scripts/verification/UserDirectory/Prepare-Database.ps1') -Database $task85bTarget
$task85bConnection=New-Object System.Data.SqlClient.SqlConnection "Data Source=REDACTED_LOCAL_SQL_HOST\SQLEXPRESS;Initial Catalog=$task85bTarget;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps Task85B approved new trial"
try {
 $task85bConnection.Open()
 $task85bIdentity=$task85bConnection.CreateCommand()
 $task85bIdentity.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')"
 $task85bReader=$task85bIdentity.ExecuteReader()
 if(!$task85bReader.Read() -or $task85bReader.GetString(0) -cne 'REDACTED_LOCAL_SQL_HOST\SQLEXPRESS' -or $task85bReader.GetString(1) -cne $task85bTarget -or $task85bReader.IsDBNull(2) -or $task85bReader.GetString(2) -cne 'Task17'){throw 'Trial identity/marker mismatch'}
 $task85bReader.Dispose();$task85bIdentity.Dispose()
 foreach($task85bPath in @((Join-Path $task85bRoot 'scripts/verification/UserProfileUpdate/rowversion-forward.sql'),(Join-Path $PSScriptRoot '../../Task84/05_UC017_Branch_RoomTypes.sql.proposal'),(Join-Path $PSScriptRoot '../../Task84/06_UC017_Verify.sql.proposal'))){
  $task85bCommand=$task85bConnection.CreateCommand();$task85bCommand.CommandTimeout=120
  $task85bCommand.CommandText=[IO.File]::ReadAllText($task85bPath)
  try{[void]$task85bCommand.ExecuteNonQuery()}finally{$task85bCommand.Dispose()}
 }
 Write-Output "PASS: guarded new Task85B trial and historical UC017 V1 prerequisites; retained $task85bTarget"
} finally {$task85bConnection.Dispose()}
