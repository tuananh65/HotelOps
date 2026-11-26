param([switch]$Browser)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$database='HotelOpsCompactWorkdayTestNote2511260145'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$connection.Open()
$sessions=[Collections.Generic.List[long]]::new()
$results=[Collections.Generic.List[object]]::new()
$server=$null
$client=[Net.Http.HttpClient]::new()
$client.Timeout=[TimeSpan]::FromSeconds(25)
function Scalar([string]$sql){$command=$connection.CreateCommand();$command.CommandText=$sql;try{return $command.ExecuteScalar()}finally{$command.Dispose()}}
function Account([string]$role){
 $command=$connection.CreateCommand()
 $command.CommandText="SELECT TOP(1) u.user_id,ur.branch_id FROM dbo.users u JOIN dbo.user_roles ur ON u.user_id=ur.user_id JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code=@role AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL ORDER BY ur.branch_id,u.user_id"
 $null=$command.Parameters.AddWithValue('@role',$role)
 $reader=$command.ExecuteReader();if(!$reader.Read()){$reader.Close();$command.Dispose();throw "No eligible account for $role"}
 $user=[long]$reader[0];$branch=if($reader.IsDBNull(1)){0}else{[long]$reader[1]};$reader.Close();$command.Dispose()
 $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose()
 $sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
 $command=$connection.CreateCommand()
 $command.CommandText='INSERT dbo.user_sessions(user_id,token_hash,security_stamp,created_at,expires_at) OUTPUT inserted.session_id SELECT user_id,@hash,security_stamp,SYSUTCDATETIME(),DATEADD(minute,10,SYSUTCDATETIME()) FROM dbo.users WHERE user_id=@user'
 $null=$command.Parameters.AddWithValue('@hash',$hash);$null=$command.Parameters.AddWithValue('@user',$user)
 $sessions.Add([long]$command.ExecuteScalar());$command.Dispose()
 return @{User=$user;Branch=$branch;Token=([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()}
}
function Check([string]$name,[string]$path,[int]$expected,[string]$token=''){
 $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get,"http://127.0.0.1:5092/api/v2/$path")
 if($token){$request.Headers.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)}
 try{
  $response=$client.SendAsync($request).GetAwaiter().GetResult();$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  $status=[int]$response.StatusCode;$response.Dispose()
  if($status -ne $expected){throw "$name expected HTTP $expected, received $status. $body"}
  $results.Add(@{name=$name;status=$status;result='PASS'})
  if($body){return ($body|ConvertFrom-Json)}
 }finally{$request.Dispose()}
}
try{
 if((Scalar 'SELECT DB_NAME()') -ne $database){throw 'Refusing non-test database'}
 if(Get-NetTCPConnection -LocalPort 5092 -State Listen -ErrorAction SilentlyContinue){throw 'Port 5092 already in use'}
 $env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString
 $env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Compact__WorkerEnabled='false';$env:Compact__CleaningOverdueEnabled='false';$env:Compact__AutoEndShiftsEnabled='false';$env:Email__Enabled='false'
 $artifact=Join-Path $root '.artifacts/note-25-11-http';New-Item $artifact -ItemType Directory -Force|Out-Null
 $dll=Join-Path $root 'src/HotelOps.Api/bin/Release/net10.0/HotelOps.Api.dll'
 $server=Start-Process dotnet -ArgumentList @($dll,'--urls','http://127.0.0.1:5092') -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifact 'stdout.log') -RedirectStandardError (Join-Path $artifact 'stderr.log')
 $ready=$false
 for($attempt=0;$attempt -lt 80;$attempt++){
  if($server.HasExited){throw 'HTTP test server exited; inspect .artifacts/note-25-11-http logs'}
  try{$response=$client.GetAsync('http://127.0.0.1:5092/api/v2/notifications/unread').GetAwaiter().GetResult();$ready=([int]$response.StatusCode -eq 401);$response.Dispose()}catch{}
  if($ready){break};Start-Sleep -Milliseconds 250
 }
 if(!$ready){throw 'HTTP server was not ready'}
 $om=Account 'OPERATION_MANAGER';$store=Account 'STOREKEEPER';$hk=Account 'HK_STAFF'
 $null=Check 'Anonymous notifications denied' 'notifications/unread' 401
 $null=Check 'OM operational list denied' "work?branchId=$($om.Branch)&kind=OPERATIONAL" 403 $om.Token
 $null=Check 'OM operational report denied' "insights/reports/tasks?branchId=$($om.Branch)" 403 $om.Token
 $foreign=Scalar "SELECT TOP(1) branch_id FROM dbo.branches WHERE is_active=1 AND branch_id<>$($store.Branch) ORDER BY branch_id"
 $null=Check 'Store cannot read other branch stock' "insights/stock?branchId=$foreign" 403 $store.Token
 $null=Check 'Store cannot read cross-branch transfer catalog' "documents/transfer-catalog?branchId=$($store.Branch)" 403 $store.Token
 $null=Check 'OM cannot read foreign branch full stock' "insights/stock?branchId=$foreign" 403 $om.Token
 $null=Check 'OM narrow cross-branch catalog allowed' "documents/transfer-catalog?branchId=$($om.Branch)" 200 $om.Token
 $personnel=Check 'OM personnel endpoint available' "workday/personnel?branchId=$($om.Branch)" 200 $om.Token
 if(!$personnel.Count){throw 'Personnel clone has no rows to verify'}
 foreach($person in $personnel){
  $excluded=Scalar "SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.user_id=$([long]$person.userId) AND (ur.branch_id=$($om.Branch) OR ur.branch_id IS NULL) AND r.code IN ('OPERATION_MANAGER','OWNER','SYS_ADMIN') AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())"
  if($excluded -ne 0){throw 'Personnel endpoint includes excluded management role'}
 }
 $results.Add(@{name='Personnel excludes all active OM Owner SYS_ADMIN roles against SQL';result='PASS'})
 $hkLead=Account 'HOUSEKEEPING_SUPERVISOR'
 $housekeeping=Check 'HK workload endpoint available' "insights/housekeeping?branchId=$($hkLead.Branch)" 200 $hkLead.Token
 $expectedCleaning=[int](Scalar "SELECT COUNT(DISTINCT work_id) FROM dbo.work_items WHERE branch_id=$($hkLead.Branch) AND kind='CLEANING' AND status NOT IN ('CLOSED','COMPLETED','CANCELLED','PASSED','FAILED','REJECTED')")
 $actualCleaning=0
 foreach($group in $housekeeping.workload){
  if($group.status -notmatch '^[A-Z_]+$'){throw 'Unexpected workload status'}
  $expectedGroup=[int](Scalar "SELECT COUNT(DISTINCT work_id) FROM dbo.work_items WHERE branch_id=$($hkLead.Branch) AND kind='CLEANING' AND status='$($group.status)' AND status NOT IN ('CLOSED','COMPLETED','CANCELLED','PASSED','FAILED','REJECTED')")
  if($group.count -ne $expectedGroup){throw 'Workload status count differs from unique original cleaning work'}
  $actualCleaning += $group.count
 }
 if($actualCleaning -ne $expectedCleaning){throw 'Workload total differs from unique original cleaning work'}
 $results.Add(@{name='HK workload equals distinct original cleaning work per status';result='PASS';count=$actualCleaning})
 $null=Check 'Staff can read shift command availability' "workday/command-access?branchId=$($hk.Branch)" 200 $hk.Token
 $count=Check 'Own unread count available' "notifications/unread?branchId=$($hk.Branch)" 200 $hk.Token
 $maximum=[int](Scalar "SELECT COUNT(*) FROM dbo.notifications WHERE recipient_user_id=$($hk.User) AND read_at IS NULL AND available_at<=SYSUTCDATETIME() AND (expires_at IS NULL OR expires_at>SYSUTCDATETIME())")
 if($count.count -lt 0 -or $count.count -gt $maximum){throw 'Unread count exceeds recipient-owned notifications'}
 $null=Check 'Own inbox available' "notifications?branchId=$($hk.Branch)" 200 $hk.Token
 if($Browser){
  $accounts=@{OPERATION_MANAGER=$om;STOREKEEPER=$store;HK_STAFF=$hk;HOUSEKEEPING_SUPERVISOR=$hkLead;OWNER=(Account 'OWNER');MAINTENANCE_SUPERVISOR=(Account 'MAINTENANCE_SUPERVISOR');MAINTENANCE_STAFF=(Account 'MAINTENANCE_STAFF')}
  $env:HOTELOPS_BROWSER_ACCOUNTS=$accounts|ConvertTo-Json -Depth 4 -Compress
  & node (Join-Path $root 'frontend/tests/note-25-11-shell-browser.mjs')
  if($LASTEXITCODE){throw 'Actual shell browser checks failed'}
 }
 $results|ConvertTo-Json -Depth 5|Set-Content (Join-Path $artifact 'results.json') -Encoding UTF8
 Write-Output "PASS: $($results.Count) actual HTTP checks; clone=$database; fixture sessions removed in finally."
}finally{
 if($server -and !$server.HasExited){Stop-Process -Id $server.Id -Force}
 foreach($id in $sessions){$null=Scalar "DELETE dbo.user_sessions WHERE session_id=$id"}
 $connection.Dispose();$client.Dispose()
 foreach($name in @('HOTELOPS_BROWSER_ACCOUNTS','ConnectionStrings__HotelOpsDb','ASPNETCORE_ENVIRONMENT','Compact__WorkerEnabled','Compact__CleaningOverdueEnabled','Compact__AutoEndShiftsEnabled','Email__Enabled')){Remove-Item "Env:$name" -ErrorAction SilentlyContinue}
}
