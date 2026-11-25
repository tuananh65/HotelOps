param([ValidateSet('Start','Setup','Insert','Rollback','Read','Revoke','Restrict','Unrestrict','RoleRevoke','RoleRestore','Cleanup','Stop')][string]$Action,
 [ValidateSet('A','B','Cross')][string]$Target='A')
$ErrorActionPreference='Stop'
$RunId=[string]$env:HOTELOPS_V2_RUN_ID
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $root
$database='HotelOpsCompactWorkdayTestV22611250010'
$directory=Join-Path $root 'artifacts/restructure-v2/realtime-private'
New-Item -ItemType Directory -Force $directory | Out-Null
$fixturePath=Join-Path $directory 'fixture.json'
$processPath=Join-Path $directory 'process.json'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
if($builder.InitialCatalog -ne $database){throw 'TEST database guard failed.'}
if($Action -eq 'Start'){
 if(Test-Path $processPath){throw 'Existing owned process metadata; stop/reconcile first.'}
 if(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5777){throw 'Port 5777 already owned; refusing to start or stop its listener.'}
 if(-not $RunId -or -not(Test-Path $fixturePath)){throw 'Owned fixture required before Start.'}
 $f=Get-Content $fixturePath -Raw|ConvertFrom-Json;if([string]$f.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString
 $env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Compact__WorkerEnabled='false';$env:Compact__CleaningOverdueEnabled='false';$env:Compact__AutoEndShiftsEnabled='false'
 $env:Email__Enabled='false';$env:AI__Enabled='false'
 $env:Logging__LogLevel__Default='Warning'
 $server=Join-Path $directory 'server';New-Item -ItemType Directory -Force $server|Out-Null
 $build=if($env:HOTELOPS_V2_TEST_BUILD){[IO.Path]::GetFullPath($env:HOTELOPS_V2_TEST_BUILD)}else{Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0'}
 if(-not $build.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path (Join-Path $build 'HotelOps.Api.dll'))){throw 'Build must be an existing workspace assembly directory.'}
 Copy-Item -Path (Join-Path $build '*') -Destination $server -Recurse -Force
 $dll=Join-Path $server 'HotelOps.Api.dll'
 $p=Start-Process dotnet -ArgumentList @($dll,'--urls','http://127.0.0.1:5777') -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'api.log') -RedirectStandardError (Join-Path $directory 'api.err.log')
 @{pid=$p.Id;database=$database;port=5777;dll=$dll;created=$p.StartTime.ToUniversalTime().ToString('o');runId=$RunId} | ConvertTo-Json | Set-Content $processPath -Encoding utf8
 for($i=0;$i -lt 120;$i++){
  try{$health=Invoke-RestMethod 'http://127.0.0.1:5777/health' -TimeoutSec 1}catch{$health=$null}
  if($health.tables -eq 57){
   $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5777)
   if($p.HasExited -or $listeners.Count -eq 0 -or @($listeners|Where-Object OwningProcess -ne $p.Id).Count -gt 0){throw 'TEST API listener ownership mismatch.'}
   @{ready=$true;pid=$p.Id;database=$database}|ConvertTo-Json -Compress;exit 0
  }
  if($p.HasExited){Remove-Item -LiteralPath $processPath;throw 'TEST API exited; inspect private logs.'};Start-Sleep -Milliseconds 250
 }
 Stop-Process -Id $p.Id -Force;Remove-Item -LiteralPath $processPath;throw 'TEST API readiness timeout.'
}
if($Action -eq 'Stop'){
 if(Test-Path $processPath){$owned=Get-Content $processPath -Raw|ConvertFrom-Json
  if([string]$owned.runId -ne $RunId){'{"stopped":false,"notOwned":true}';exit 0}
  $p=Get-Process -Id $owned.pid -ErrorAction SilentlyContinue
  if($p){if($p.StartTime.ToUniversalTime().ToString('o') -ne $owned.created){throw 'PID reuse: refusing stop.'};Stop-Process -Id $p.Id -Force}
  Remove-Item -LiteralPath $processPath
 };'{"stopped":true}';exit 0
}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
if($Action -eq 'Cleanup' -and !(Test-Path $fixturePath)){'{"cleaned":true,"fixtureAbsent":true}';exit 0}
function Query([string]$sql,[hashtable]$parameters=@{}){
 $cmd=$connection.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=35
 foreach($key in $parameters.Keys){$value=$parameters[$key];if($null -eq $value){$value=[DBNull]::Value};[void]$cmd.Parameters.AddWithValue('@'+$key,$value)}
 $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@()
 try{while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$value=$reader.GetValue($i);$row[$reader.GetName($i)]=if($value -is [DBNull]){$null}else{$value}};$rows+=,$row}}finally{$reader.Dispose();$cmd.Dispose()}
 return ,$rows
}
try{
 $connection.OpenAsync().GetAwaiter().GetResult()
 if($Action -eq 'Setup'){
  if(Test-Path $fixturePath){throw 'Fixture already exists; cleanup before rerun.'}
  $backup=Get-Content (Join-Path $root ('artifacts/workday/backup-'+$database+'.json')) -Raw|ConvertFrom-Json
  if(!$backup.verified -or $backup.database -ne $database -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 15){throw 'Fresh verified TEST backup required.'}
  $users=Query "SELECT TOP(2) u.user_id,ur.branch_id,ur.user_role_id,u.temporary_password_expires_at FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code='STOREKEEPER' AND r.is_active=1 AND ur.revoked_at IS NULL AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL ORDER BY ur.branch_id,u.user_id"
  if($users.Count -ne 2 -or $users[0].branch_id -eq $users[1].branch_id){throw 'Two active storekeepers in distinct branches are required.'}
  $fixture=@{database=$database;runId=$RunId;prefix='V2_REALTIME_'+[Guid]::NewGuid().ToString('N');actors=@{}}
  [void](Query 'BEGIN TRANSACTION')
  foreach($index in 0..1){$u=$users[$index];$bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose();$token=([BitConverter]::ToString($bytes)).Replace('-','');$sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
   $session=Query "INSERT dbo.user_sessions(user_id,token_hash,security_stamp,expires_at) OUTPUT inserted.session_id SELECT user_id,@hash,security_stamp,DATEADD(hour,1,SYSUTCDATETIME()) FROM dbo.users WHERE user_id=@user" @{hash=$hash;user=$u.user_id}
   $key=if($index -eq 0){'A'}else{'B'};$fixture.actors[$key]=@{user=$u.user_id;branch=$u.branch_id;role=$u.user_role_id;session=$session[0].session_id;token=$token}
   $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8
  }
  $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8
  [void](Query 'COMMIT TRANSACTION')
  @{setup=$true;database=$database;actors=2}|ConvertTo-Json -Compress;exit 0
 }
 $fixture=Get-Content $fixturePath -Raw|ConvertFrom-Json
 if($fixture.database -ne $database){throw 'Fixture database guard failed.'}
 if([string]$fixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $a=$fixture.actors.A;$b=$fixture.actors.B
 $actor=if($Target -eq 'B'){$b}else{$a};$branch=if($Target -eq 'Cross'){$b.branch}else{$actor.branch}
 $p=@{user=[long]$actor.user;branch=[long]$branch;title=$fixture.prefix;session=[long]$actor.session;role=[long]$actor.role}
 switch($Action){
  'Insert'{$rows=Query "INSERT dbo.notifications(branch_id,recipient_user_id,event_key,notification_type,title,message,requires_acknowledgment,attempt_count) OUTPUT inserted.notification_id VALUES(@branch,@user,NEWID(),'LOW_STOCK',@title,@title,0,0)" $p;@{id=$rows[0].notification_id}|ConvertTo-Json -Compress}
  'Rollback'{
   $row=Query "BEGIN TRANSACTION; INSERT dbo.notifications(branch_id,recipient_user_id,event_key,notification_type,title,message,requires_acknowledgment,attempt_count) VALUES(@branch,@user,NEWID(),'LOW_STOCK',@title,@title,0,0); DECLARE @inserted_id bigint=SCOPE_IDENTITY(); WAITFOR DELAY '00:00:05'; ROLLBACK; SELECT @inserted_id id;" $p
   @{rolledBack=$true;id=$row[0].id}|ConvertTo-Json -Compress
  }
  'Read'{[void](Query 'UPDATE dbo.notifications SET read_at=SYSUTCDATETIME() WHERE recipient_user_id=@user AND title=@title' $p);'{"read":true}'}
  'Revoke'{[void](Query 'UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id=@session AND user_id=@user' $p);'{"revoked":true}'}
  'Restrict'{
   $changed=Query 'BEGIN TRANSACTION; UPDATE dbo.users SET temporary_password_expires_at=DATEADD(day,1,SYSUTCDATETIME()) OUTPUT CONVERT(varchar(33),inserted.temporary_password_expires_at,126) changed_to WHERE user_id=@user AND temporary_password_expires_at IS NULL' $p
   if($changed.Count -ne 1){throw 'Restriction was not owned by this fixture.'}
   $actor|Add-Member -NotePropertyName restrictionSetAt -NotePropertyValue $changed[0].changed_to -Force
   $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8;[void](Query 'COMMIT TRANSACTION');'{"restricted":true}'
  }
  'Unrestrict'{
   if($actor.restrictionSetAt){$p.expected=$actor.restrictionSetAt;[void](Query 'UPDATE dbo.users SET temporary_password_expires_at=NULL WHERE user_id=@user AND temporary_password_expires_at=CONVERT(datetime2,@expected,126)' $p)}
   '{"restricted":false}'
  }
  'RoleRevoke'{
   $changed=Query 'BEGIN TRANSACTION; UPDATE dbo.user_roles SET revoked_at=SYSUTCDATETIME() OUTPUT CONVERT(varchar(33),inserted.revoked_at,126) changed_to WHERE user_role_id=@role AND user_id=@user AND revoked_at IS NULL' $p
   if($changed.Count -ne 1){throw 'Role change was not owned by this fixture.'}
   $actor|Add-Member -NotePropertyName roleRevokedAt -NotePropertyValue $changed[0].changed_to -Force
   $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8;[void](Query 'COMMIT TRANSACTION');'{"roleRevoked":true}'
  }
  'RoleRestore'{
   if($actor.roleRevokedAt){$p.expected=$actor.roleRevokedAt;[void](Query 'UPDATE dbo.user_roles SET revoked_at=NULL WHERE user_role_id=@role AND user_id=@user AND revoked_at=CONVERT(datetime2,@expected,126)' $p)}
   '{"roleRestored":true}'
  }
  'Cleanup'{
   foreach($member in @($a,$b)|Where-Object{$null -ne $_}){
    $ownedParams=@{user=[long]$member.user;role=[long]$member.role;session=[long]$member.session;title=$fixture.prefix}
    if($member.restrictionSetAt){$ownedParams.expected=$member.restrictionSetAt;[void](Query 'UPDATE dbo.users SET temporary_password_expires_at=NULL WHERE user_id=@user AND temporary_password_expires_at=CONVERT(datetime2,@expected,126)' $ownedParams)}
    if($member.roleRevokedAt){$ownedParams.expected=$member.roleRevokedAt;[void](Query 'UPDATE dbo.user_roles SET revoked_at=NULL WHERE user_role_id=@role AND user_id=@user AND revoked_at=CONVERT(datetime2,@expected,126)' $ownedParams)}
    [void](Query 'DELETE dbo.notifications WHERE title=@title AND recipient_user_id=@user; DELETE dbo.user_sessions WHERE session_id=@session AND user_id=@user' $ownedParams)
   }
   $remaining=Query 'SELECT COUNT(*) AS total FROM dbo.notifications WHERE title=@title' @{title=$fixture.prefix}
   if($remaining[0].total -ne 0){throw 'Fixture notification cleanup incomplete.'}
   foreach($member in @($a,$b)|Where-Object{$null -ne $_}){
    $remaining=Query 'SELECT (SELECT COUNT(*) FROM dbo.user_sessions WHERE session_id=@session AND user_id=@user) AS sessions,(SELECT COUNT(*) FROM dbo.users WHERE user_id=@user AND temporary_password_expires_at=CONVERT(datetime2,@restriction,126)) AS restrictions,(SELECT COUNT(*) FROM dbo.user_roles WHERE user_role_id=@role AND user_id=@user AND revoked_at=CONVERT(datetime2,@revocation,126)) AS revokedRoles' @{user=[long]$member.user;role=[long]$member.role;session=[long]$member.session;restriction=$member.restrictionSetAt;revocation=$member.roleRevokedAt}
    if($remaining[0].sessions -ne 0 -or $remaining[0].restrictions -ne 0 -or $remaining[0].revokedRoles -ne 0){throw 'Fixture auth cleanup incomplete.'}
   }
   Remove-Item -LiteralPath $fixturePath;'{"cleaned":true,"databaseVerified":true}'
  }
 }
}finally{$connection.Dispose()}
