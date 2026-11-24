param([Parameter(Mandatory=$true)][string]$Database,[string]$Server='.\SQLEXPRESS')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompactApiTest[a-z0-9]+$'){throw 'Independent fixture only'}
$db=New-Object Data.SqlClient.SqlConnection("Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
$db.Open()
try {
 $cmd=$db.CreateCommand()
 $cmd.CommandText="SELECT COUNT(*) FROM roles WHERE code='DEPT_MANAGER'"
 if([int]$cmd.ExecuteScalar()-ne 1){throw 'Fixture role missing'}
 $cmd.CommandText="SELECT COUNT(*) FROM role_permissions WHERE role_id<>(SELECT role_id FROM roles WHERE code='DEPT_MANAGER')"
 $otherBefore=[int]$cmd.ExecuteScalar()
 foreach($iteration in 1..2){
  $cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'grant-room-remediation.sql') -Raw -Encoding UTF8
  [void]$cmd.ExecuteNonQuery()
  $cmd.CommandText="SELECT COUNT(*) FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='DEPT_MANAGER' AND p.code IN ('INCIDENT_VIEW','INCIDENT_TRIAGE','MR_CREATE') AND rp.data_scope='BRANCH'"
  if([int]$cmd.ExecuteScalar()-ne 3){throw 'Expected exactly three reviewed grants'}
 }
 $cmd.CommandText="SELECT COUNT(*) FROM role_permissions WHERE role_id<>(SELECT role_id FROM roles WHERE code='DEPT_MANAGER')"
 if([int]$cmd.ExecuteScalar()-ne $otherBefore){throw 'Other roles modified'}
 Write-Output 'PASS: proposal grant script idempotent; three BRANCH grants; other roles unchanged; fixture only'
} finally {$db.Dispose()}
