# REVIEW ONLY. Requires separate user approval for one new database and fixtures.
$ErrorActionPreference='Stop'
$task86Root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$task86Target='HotelOps_Canonical_20261104_223000_c01286a1_Fresh'
$task86Review=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SQL_SCOPE_REVIEW.proposal.json') -Raw | ConvertFrom-Json
if($task86Review.target -cne $task86Target -or $task86Review.instance -cne 'REDACTED_LOCAL_SQL_HOST\SQLEXPRESS'){throw 'Exact proposed target required'}
foreach($task86Source in $task86Review.sources){
 if((Get-FileHash -LiteralPath (Join-Path $task86Root $task86Source.path)).Hash -cne $task86Source.sha256){throw 'Reviewed prerequisite bytes changed; stop before SQL'}
}
if((Get-FileHash -LiteralPath (Join-Path $task86Root 'scripts/verification/UserDirectory/Prepare-Database.ps1')).Hash -cne $task86Review.prepareDatabaseSha256){throw 'Setup script changed'}
# This existing setup validates @@SERVERNAME and target nonexistence before CREATE.
# It has no reset/drop path. Only its template USE/CREATE name is substituted in memory.
& (Join-Path $task86Root 'scripts/verification/UserDirectory/Prepare-Database.ps1') -Database $task86Target
$task86Connection=New-Object System.Data.SqlClient.SqlConnection "Data Source=REDACTED_LOCAL_SQL_HOST\SQLEXPRESS;Initial Catalog=$task86Target;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps Task86 approved new trial"
try {
 $task86Connection.Open()
 $task86Identity=$task86Connection.CreateCommand()
 $task86Identity.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')"
 $task86Reader=$task86Identity.ExecuteReader()
 if(!$task86Reader.Read() -or $task86Reader.GetString(0) -cne 'REDACTED_LOCAL_SQL_HOST\SQLEXPRESS' -or $task86Reader.GetString(1) -cne $task86Target -or $task86Reader.IsDBNull(2) -or $task86Reader.GetString(2) -cne 'Task17'){throw 'Trial identity mismatch'}
 $task86Reader.Dispose();$task86Identity.Dispose()
 $task86Schema=$task86Connection.CreateCommand()
 $task86Schema.CommandText=@'
IF DB_NAME()<>N'HotelOps_Canonical_20261104_223000_c01286a1_Fresh' THROW 51986,'Wrong target',1;
IF COL_LENGTH(N'dbo.branches',N'branch_id')<>8 OR COL_LENGTH(N'dbo.branches',N'branch_code')<>30
 OR COL_LENGTH(N'dbo.branches',N'branch_name')<>400 OR COL_LENGTH(N'dbo.branches',N'row_version') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches') AND system_type_id=189)
 THROW 51986,'Fresh legacy branch schema does not match reviewed model',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches') AND name=N'branch_code' AND collation_name LIKE N'%[_]CI[_]%')
 THROW 51986,'Case insensitive branch code collation required; no automatic schema rewrite',1;
IF EXISTS(SELECT UPPER(LTRIM(RTRIM(branch_code))) FROM dbo.branches GROUP BY UPPER(LTRIM(RTRIM(branch_code))) HAVING COUNT_BIG(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.branches WHERE NULLIF(LTRIM(RTRIM(branch_code)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(branch_name)),N'') IS NULL)
 THROW 51986,'Existing canonical branch data incompatible',1;
'@
 try{[void]$task86Schema.ExecuteNonQuery()}finally{$task86Schema.Dispose()}
 # Rehearse transactional DDL recovery first, only in this newly created trial.
 $task86Recovery=$task86Connection.BeginTransaction()
 try{
  $task86Command=$task86Connection.CreateCommand();$task86Command.Transaction=$task86Recovery
  $task86Command.CommandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'branch-rowversion-forward.sql.proposal'))
  try{[void]$task86Command.ExecuteNonQuery()}finally{$task86Command.Dispose()}
  $task86Recovery.Rollback()
 }finally{$task86Recovery.Dispose()}
 $task86Probe=$task86Connection.CreateCommand();$task86Probe.CommandText="SELECT COL_LENGTH(N'dbo.branches',N'row_version')"
 try{if($task86Probe.ExecuteScalar() -isnot [DBNull]){throw 'Transactional DDL rollback did not restore legacy schema'}}finally{$task86Probe.Dispose()}
 foreach($task86Path in @('scripts/verification/UserProfileUpdate/rowversion-forward.sql','scripts/verification/Task84/05_UC017_Branch_RoomTypes.sql.proposal','scripts/verification/Task84/06_UC017_Verify.sql.proposal','scripts/verification/Task86/branch-rowversion-forward.sql.proposal','scripts/verification/Task86/branch-rowversion-forward.sql.proposal')){
  # Last script deliberately executed twice to verify migration idempotency.
  $task86Command=$task86Connection.CreateCommand();$task86Command.CommandTimeout=120
  $task86Command.CommandText=[IO.File]::ReadAllText((Join-Path $task86Root $task86Path))
  try{[void]$task86Command.ExecuteNonQuery()}finally{$task86Command.Dispose()}
 }
 Write-Output "PASS: new guarded Task86 trial prepared, branch rowversion forward executed twice; retained $task86Target"
}finally{$task86Connection.Dispose()}
