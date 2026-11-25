param([switch]$InitializeSessions)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$results=@()
foreach($name in @('HotelOpsCompactWorkdayTestLocalDevV2261125','HotelOpsCompactWorkdayTestLocalProductionV2261125')){
 $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$name
 $db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
 try{
  $db.OpenAsync().GetAwaiter().GetResult();$cmd=$db.CreateCommand();$cmd.CommandTimeout=90
  $cmd.CommandText=@'
IF DB_NAME() NOT IN('HotelOpsCompactWorkdayTestLocalDevV2261125','HotelOpsCompactWorkdayTestLocalProductionV2261125') THROW 51990,'Local clone required',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>57 THROW 51990,'Schema mismatch',1;
IF EXISTS(SELECT 1 FROM dbo.users u JOIN HotelOpsCompactWorkdayTestV22611250010.dbo.users original ON original.user_id=u.user_id
 WHERE u.username<>original.username OR ISNULL(u.password_hash,'')<>ISNULL(original.password_hash,'')) THROW 51990,'Account preservation mismatch',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) THROW 51990,'Constraint trust mismatch',1;
SELECT COUNT(DISTINCT u.user_id) FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id
 WHERE r.code='MAINTENANCE_STAFF' AND r.is_active=1 AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE'
 AND u.deleted_at IS NULL AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME());
'@
  $staff=[int]$cmd.ExecuteScalarAsync().GetAwaiter().GetResult();if($staff -ne 10){throw 'Expected ten recovered maintenance staff.'}
  $revoked=0
  if($InitializeSessions){
   $cmd.CommandText="IF DB_NAME() NOT IN('HotelOpsCompactWorkdayTestLocalDevV2261125','HotelOpsCompactWorkdayTestLocalProductionV2261125') THROW 51990,'Local clone required',1; UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE revoked_at IS NULL;"
   $revoked=$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()
  }
  $results+=@{database=$name;tables=57;maintenanceStaff=$staff;accountCredentialsPreserved=$true;trustedConstraints=$true;importedSessionsRevoked=$revoked}
 }finally{$db.Dispose()}
}
$folder=Join-Path $root 'artifacts/local-runtime';[void][IO.Directory]::CreateDirectory($folder)
@{at=[DateTime]::UtcNow.ToString('o');source='HotelOpsCompactWorkdayTestV22611250010';initializesCloneSessions=[bool]$InitializeSessions;results=$results}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $folder 'database-verification.json') -Encoding UTF8
$results|ForEach-Object {Write-Output "PASS $($_.database): 57 tables, 10 maintenance staff, credentials preserved"}
