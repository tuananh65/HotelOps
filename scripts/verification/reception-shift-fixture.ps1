param([ValidateSet('save','planned','ended','expired','restore')][string]$Mode)
$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestWarehouse261124'
$backup=Get-Content "artifacts/workday/backup-$database.json" -Raw | ConvertFrom-Json
if(!$backup.verified -or $backup.database -ne $database){throw 'Verified TEST backup required'}
$fixture=Get-Content artifacts/p0-workflow/private/runtime-fixture.json -Raw | ConvertFrom-Json
$user=$fixture.actors.'DVBV.reception'.userId
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$conn=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$conn.Open()
try {
 $cmd=$conn.CreateCommand();[void]$cmd.Parameters.AddWithValue('@user',[long]$user)
 $cmd.CommandText="SELECT COUNT(*) FROM users u WHERE u.user_id=@user AND u.full_name=N'P0 TEST reception DVBV' AND EXISTS(SELECT 1 FROM user_roles ur JOIN branches b ON b.branch_id=ur.branch_id WHERE ur.user_id=u.user_id AND b.code='P0_TEST_DVBV')"
 if([int]$cmd.ExecuteScalar() -ne 1){throw 'Only synthetic TEST actor allowed'}
 $file='artifacts/workday-role-validation/reception-shifts.json'
 if($Mode -eq 'save'){
  $cmd.CommandText='SELECT assignment_id,work_date,status,started_at,ended_at FROM workday_assignments WHERE user_id=@user FOR JSON PATH,INCLUDE_NULL_VALUES'
  $snapshot=[string]$cmd.ExecuteScalar()
  if((Test-Path $file) -and (Get-Content $file -Raw).Trim() -ne $snapshot.Trim()){throw 'Fixture changed since snapshot; restore or review before repeating'}
  $snapshot | Set-Content $file -Encoding utf8
 } else {
  $snapshot=Get-Content $file -Raw;[void]$cmd.Parameters.AddWithValue('@snapshot',$snapshot)
  $cmd.CommandText=@'
UPDATE a SET work_date=j.work_date,status=j.status,started_at=j.started_at,ended_at=j.ended_at
FROM workday_assignments a JOIN OPENJSON(@snapshot) WITH(assignment_id bigint,work_date date,status varchar(20),started_at datetime2,ended_at datetime2) j ON j.assignment_id=a.assignment_id WHERE a.user_id=@user;
'@
  switch($Mode){
   'planned' {$cmd.CommandText+="UPDATE workday_assignments SET status='PLANNED',started_at=NULL,ended_at=NULL WHERE user_id=@user;"}
   'ended' {$cmd.CommandText+="UPDATE workday_assignments SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id=@user AND status='WORKING';"}
   'expired' {$cmd.CommandText+="UPDATE workday_assignments SET work_date=DATEADD(day,-1,work_date) WHERE user_id=@user;"}
  }
  [void]$cmd.ExecuteNonQuery()
 }
 Write-Output "TEST reception fixture: $Mode"
}finally{$conn.Dispose()}
