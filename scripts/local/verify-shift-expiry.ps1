param([switch]$RequireClosed)
$ErrorActionPreference='Stop'
$config=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$results=@()
foreach($name in @('HotelOpsCompactWorkdayTestLocalDevV2261125','HotelOpsCompactWorkdayTestLocalProductionV2261125')){
 $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$name
 $db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
 try{
  $db.OpenAsync().GetAwaiter().GetResult();$cmd=$db.CreateCommand()
  $cmd.CommandText=@'
SELECT a.assignment_id,a.status,a.work_date,a.started_at,a.ended_at,s.end_minute,b.timezone,
 (SELECT COUNT(*) FROM dbo.audit_logs l WHERE l.entity_type='Workday' AND l.entity_key=CONVERT(varchar(30),a.assignment_id) AND l.action='AUTO_CANCEL_EXPIRED' AND l.actor_user_id IS NULL) expiry_count
FROM dbo.workday_assignments a JOIN dbo.workday_shifts s ON a.shift_id=s.shift_id JOIN dbo.branches b ON b.branch_id=s.branch_id
'@
  $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$overdue=0;$closed=0;$invalid=0;$future=0
  while($reader.Read()){
   $zone=[string]$reader['timezone'];if($zone -eq 'Asia/Ho_Chi_Minh'){$zone='SE Asia Standard Time'}
   $end=[TimeZoneInfo]::ConvertTimeToUtc([DateTime]::SpecifyKind(([datetime]$reader['work_date']).AddMinutes([int]$reader['end_minute']),[DateTimeKind]::Unspecified),[TimeZoneInfo]::FindSystemTimeZoneById($zone))
   if($reader['status'] -eq 'PLANNED'){if($end.AddHours(1) -le [DateTime]::UtcNow){$overdue++}else{$future++}}
   if([int]$reader['expiry_count'] -gt 0){$closed++;if($reader['status'] -ne 'CANCELLED' -or $reader['started_at'] -isnot [DBNull] -or $reader['ended_at'] -isnot [DBNull] -or [int]$reader['expiry_count'] -ne 1){$invalid++}}
  }
  $reader.Close()
  if($invalid -gt 0 -or ($RequireClosed -and $overdue -gt 0)){throw "Expiry verification failed: $name overdue=$overdue invalid=$invalid"}
  $results+=@{database=$name;overduePlanned=$overdue;automaticallyClosed=$closed;invalidAttendanceOrDuplicateAudit=$invalid;remainingPlannedWithinWindow=$future}
 }finally{$db.Dispose()}
}
$results|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot '../../artifacts/local-runtime/shift-expiry-verification.json') -Encoding UTF8
$results|ForEach-Object {Write-Output "$($_.database): overdue=$($_.overduePlanned), closed=$($_.automaticallyClosed), invalid=$($_.invalidAttendanceOrDuplicateAudit), validPlanned=$($_.remainingPlannedWithinWindow)"}
