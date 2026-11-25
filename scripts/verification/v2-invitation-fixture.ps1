param([ValidateSet('Start','Setup','OwnerSession','DomainBoundary','Expire','RetryNow','Inspect','Cleanup','Stop','Identity','RecoveryReady')][string]$Action,[switch]$EmailEnabled,[long]$UserId=0,[string]$Identifier='')
$ErrorActionPreference='Stop'
$RunId=[string]$env:HOTELOPS_V2_RUN_ID
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $root
$database='HotelOpsCompactWorkdayTestV22611250010'
$directory=Join-Path $root 'artifacts/restructure-v2/invitation-private'
New-Item -ItemType Directory -Force $directory | Out-Null
$fixturePath=Join-Path $directory 'fixture.json';$processPath=Join-Path $directory 'process.json'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
if($builder.InitialCatalog -ne $database){throw 'TEST database guard failed.'}
if($Action -eq 'Start'){
 if(Test-Path $processPath){throw 'Owned API process metadata exists; reconcile first.'}
 if(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5778){throw 'TEST API port already in use; refusing to attach.'}
 $ownedFixture=Get-Content $fixturePath -Raw|ConvertFrom-Json
 if([string]$ownedFixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString;$env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Compact__WorkerEnabled='false';$env:Compact__CleaningOverdueEnabled='false';$env:Compact__AutoEndShiftsEnabled='false'
 $env:Email__Enabled=$EmailEnabled.ToString().ToLowerInvariant();$env:AI__Enabled='false'
 $env:Email__SmtpHost='127.0.0.1';$env:Email__SmtpPort='2526';$env:Email__FromAddress='hotelops-test@example.invalid'
 $env:Email__PublicBaseUrl='http://127.0.0.1:5778';$env:Email__UseTls='false';$env:Email__AllowLiveDelivery='false'
 $env:Email__Username='';$env:Email__Password='';$env:Email__DataProtectionKeysPath=(Join-Path $directory 'keys')
 $build=if($env:HOTELOPS_V2_TEST_BUILD){[IO.Path]::GetFullPath($env:HOTELOPS_V2_TEST_BUILD)}else{Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0'}
 if(-not $build.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path (Join-Path $build 'HotelOps.Api.dll'))){throw 'Build must be an existing workspace assembly directory.'}
 $server=Join-Path $directory 'server';New-Item -ItemType Directory -Force $server|Out-Null
 Copy-Item -Path (Join-Path $build '*') -Destination $server -Recurse -Force
 $dll=Join-Path $server 'HotelOps.Api.dll'
 $p=Start-Process dotnet -ArgumentList @($dll,'--urls','http://127.0.0.1:5778') -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'api.log') -RedirectStandardError (Join-Path $directory 'api.err.log')
 @{pid=$p.Id;database=$database;port=5778;dll=$dll;created=$p.StartTime.ToUniversalTime().ToString('o');runId=$RunId} | ConvertTo-Json | Set-Content $processPath -Encoding utf8
 for($i=0;$i -lt 160;$i++){
  try{$health=Invoke-RestMethod 'http://127.0.0.1:5778/health' -TimeoutSec 1}catch{$health=$null}
  if($health.tables -eq 57){
   $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5778)
   if($p.HasExited -or $listeners.Count -eq 0 -or @($listeners|Where-Object OwningProcess -ne $p.Id).Count -gt 0){throw 'TEST API listener ownership mismatch.'}
   @{ready=$true;pid=$p.Id;database=$database;emailEnabled=[bool]$EmailEnabled}|ConvertTo-Json -Compress;exit 0
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
  if(!$backup.verified -or $backup.database -ne $database -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 15){throw 'Fresh CHECKSUM/VERIFYONLY TEST backup required.'}
  $pending=Query "SELECT COUNT(*) n FROM dbo.account_invitations WHERE delivery_status<>'SENT' AND used_at IS NULL AND revoked_at IS NULL AND expires_at>SYSUTCDATETIME()"
  if($pending[0].n -ne 0){throw 'Existing pending invitations found; refusing to run SMTP worker over nonfixture invitations.'}
  $admins=Query "SELECT TOP(1) u.user_id FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code='SYS_ADMIN' AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.branch_id IS NULL AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL ORDER BY u.user_id"
  $branch=Query 'SELECT TOP(1) branch_id FROM dbo.branches WHERE is_active=1 ORDER BY branch_id'
  if($admins.Count -ne 1 -or $branch.Count -ne 1){throw 'Administrator/branch fixture prerequisite missing.'}
  $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose();$token=([BitConverter]::ToString($bytes)).Replace('-','');$sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
  $session=Query 'BEGIN TRANSACTION; INSERT dbo.user_sessions(user_id,token_hash,security_stamp,expires_at) OUTPUT inserted.session_id SELECT user_id,@hash,security_stamp,DATEADD(hour,1,SYSUTCDATETIME()) FROM dbo.users WHERE user_id=@user' @{hash=$hash;user=$admins[0].user_id}
  $fixture=@{database=$database;runId=$RunId;prefix='V2_INV_'+[Guid]::NewGuid().ToString('N').Substring(0,12);admin=$admins[0].user_id;branch=$branch[0].branch_id;session=$session[0].session_id;token=$token;backupAt=$backup.at}
  $fixture|ConvertTo-Json -Depth 4|Set-Content $fixturePath -Encoding utf8
  [void](Query 'COMMIT TRANSACTION')
  @{setup=$true;database=$database}|ConvertTo-Json -Compress;exit 0
 }
 $fixture=Get-Content $fixturePath -Raw|ConvertFrom-Json
 if($fixture.database -ne $database){throw 'Fixture database guard failed.'}
 if([string]$fixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $p=@{prefix=$fixture.prefix+'%';user=$UserId;session=[long]$fixture.session;admin=[long]$fixture.admin}
 if($Action -in 'Expire','RetryNow','Identity','RecoveryReady'){
  $owned=Query 'SELECT user_id FROM dbo.users WHERE user_id=@user AND username LIKE @prefix' $p
  if($owned.Count -ne 1){throw 'Refusing to mutate nonfixture user.'}
 }
 switch($Action){
  'OwnerSession'{
   if($fixture.ownerSession){throw 'Owned owner session already exists.'}
   $owners=Query "SELECT TOP(1) u.user_id FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code='OWNER' AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND ur.branch_id IS NULL AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL ORDER BY u.user_id"
   if($owners.Count -ne 1){throw 'Active global OWNER prerequisite missing.'}
   $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose();$token=([BitConverter]::ToString($bytes)).Replace('-','');$sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
   $session=Query 'BEGIN TRANSACTION; INSERT dbo.user_sessions(user_id,token_hash,security_stamp,expires_at) OUTPUT inserted.session_id SELECT user_id,@hash,security_stamp,DATEADD(minute,15,SYSUTCDATETIME()) FROM dbo.users WHERE user_id=@user' @{hash=$hash;user=$owners[0].user_id}
   $fixture|Add-Member -NotePropertyName owner -NotePropertyValue $owners[0].user_id
   $fixture|Add-Member -NotePropertyName ownerSession -NotePropertyValue $session[0].session_id
   $fixture|Add-Member -NotePropertyName ownerToken -NotePropertyValue $token
   $fixture|ConvertTo-Json -Depth 4|Set-Content $fixturePath -Encoding utf8
   [void](Query 'COMMIT TRANSACTION');'{"created":true}'
  }
  'DomainBoundary'{
   if(-not $fixture.owner){throw 'Owned OWNER session required.'}
   $boundary=Query "SELECT @branch branch_id,(SELECT TOP(1) warehouse_id FROM dbo.warehouses WHERE branch_id=@branch AND is_active=1 ORDER BY warehouse_id) warehouse_id,(SELECT TOP(1) item_id FROM dbo.inventory_items WHERE is_active=1 ORDER BY item_id) item_id,(SELECT COUNT_BIG(*) FROM dbo.work_items WHERE created_by IN(@admin,@owner)) work_count,(SELECT COUNT_BIG(*) FROM dbo.inventory_documents WHERE created_by IN(@admin,@owner)) document_count,(SELECT COUNT_BIG(*) FROM dbo.stock_movements m JOIN dbo.inventory_document_lines l ON l.line_id=m.line_id JOIN dbo.inventory_documents d ON d.document_id=l.document_id WHERE d.created_by IN(@admin,@owner)) movement_count" @{branch=[long]$fixture.branch;admin=[long]$fixture.admin;owner=[long]$fixture.owner}
   ConvertTo-Json -InputObject @($boundary) -Depth 4 -Compress
  }
  'Identity'{
   if($Identifier.Length -gt 30 -or !$Identifier.StartsWith($fixture.prefix+'_')){throw 'Only owned synthetic identifiers allowed.'}
   $p.identifier=$Identifier
   [void](Query 'UPDATE dbo.users SET employee_code=@identifier WHERE user_id=@user AND username LIKE @prefix' $p);'{"updated":true}'
  }
  'RecoveryReady'{[void](Query 'UPDATE dbo.account_invitations SET created_at=DATEADD(minute,-6,SYSUTCDATETIME()) WHERE user_id=@user AND created_at>DATEADD(minute,-6,SYSUTCDATETIME())' $p);'{"ready":true}'}
  'Expire'{[void](Query "UPDATE dbo.account_invitations SET created_at=DATEADD(day,-2,SYSUTCDATETIME()),expires_at=DATEADD(day,-1,SYSUTCDATETIME()) WHERE user_id=@user AND used_at IS NULL AND revoked_at IS NULL" $p);'{"expired":true}'}
  'RetryNow'{[void](Query "UPDATE dbo.account_invitations SET next_attempt_at=SYSUTCDATETIME() WHERE user_id=@user AND delivery_status='FAILED' AND used_at IS NULL AND revoked_at IS NULL" $p);'{"retryScheduled":true}'}
  'Inspect'{$rows=Query "SELECT u.user_id,u.account_status,u.temporary_password_expires_at,(SELECT COUNT(*) FROM dbo.account_invitations i WHERE i.user_id=u.user_id) invitations,(SELECT COUNT(*) FROM dbo.user_sessions s WHERE s.user_id=u.user_id AND revoked_at IS NULL) live_sessions FROM dbo.users u WHERE u.username LIKE @prefix" $p;ConvertTo-Json -InputObject @($rows) -Depth 4 -Compress}
  'Cleanup'{
   if($fixture.ownerSession){[void](Query 'DELETE dbo.user_sessions WHERE session_id=@session AND user_id=@user' @{session=[long]$fixture.ownerSession;user=[long]$fixture.owner})}
   # Catalog roles intentionally do not expose IsActive. Retire only the exact
   # role created by this owned run; never alter a shared role's permissions.
   $p.roleCode=$fixture.prefix+'_CONFIG'
   [void](Query "BEGIN TRANSACTION; IF EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.users u ON u.user_id=ur.user_id WHERE r.code=@roleCode AND u.username NOT LIKE @prefix) BEGIN ROLLBACK; THROW 51990,'Synthetic role has a foreign assignment; refusing cleanup.',1; END; UPDATE ur SET revoked_at=COALESCE(ur.revoked_at,SYSUTCDATETIME()) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code=@roleCode; UPDATE dbo.roles SET is_active=0 WHERE code=@roleCode; COMMIT;" $p)
   [void](Query "BEGIN TRANSACTION; UPDATE dbo.account_invitations SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()),protected_token=NULL,lease_until=NULL,lease_id=NULL WHERE user_id IN(SELECT user_id FROM dbo.users WHERE username LIKE @prefix); UPDATE dbo.user_sessions SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE user_id IN(SELECT user_id FROM dbo.users WHERE username LIKE @prefix); UPDATE dbo.users SET account_status='INACTIVE',security_stamp=NEWID() WHERE username LIKE @prefix; DELETE dbo.user_sessions WHERE session_id=@session AND user_id=@admin; COMMIT;" $p)
   $rows=Query "SELECT COUNT(*) fixture_accounts,SUM(CASE WHEN account_status='ACTIVE' THEN 1 ELSE 0 END) active_accounts FROM dbo.users WHERE username LIKE @prefix;" $p
   Remove-Item -LiteralPath $fixturePath;@{cleaned=$true;retainedInactiveAccounts=$rows[0].fixture_accounts;activeAccounts=$rows[0].active_accounts}|ConvertTo-Json -Compress
  }
 }
}finally{$connection.Dispose()}
