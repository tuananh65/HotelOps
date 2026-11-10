param([string]$EnvRoot='D:\SEP490\Projects\HotelOps-Integration\obj\env-fe03-001')
$ErrorActionPreference='Stop'
. (Join-Path $EnvRoot 'Common.ps1')
Set-StrictMode -Off
Assert-Hax
Add-Type -AssemblyName System.Net.Http
$m=Get-Content (Join-Path $EnvRoot 'manifest.json') -Raw|ConvertFrom-Json
$out=Join-Path $PSScriptRoot '../../../obj/fe03-independent-004'
$null=New-Item -ItemType Directory -Force $out
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$ok,$status=0){$checks.Add(@{name=$name;pass=[bool]$ok;http_status=$status});if(!$ok){throw ('Acceptance failed: '+$name)}}
function Send($client,$method,$path,$body=$null){
 if([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($m.lease_end_utc)){throw 'Lease expired'}
 $req=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
 if($null -ne $body){$req.Content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json')}
 try{$res=$client.SendAsync($req).GetAwaiter().GetResult();try{$raw=$res.Content.ReadAsStringAsync().GetAwaiter().GetResult();$data=if($raw){$raw|ConvertFrom-Json}else{$null};return @{status=[int]$res.StatusCode;data=$data}}finally{$res.Dispose()}}finally{$req.Dispose()}
}
$admin=$null;$sql=$null
try{
 Check 'Manifest frozen source and GUID' ($m.source_commit -ceq '6a0c9248008203c245c347d9d6831279835ee544' -and $m.database_guid -ceq 'be23f102-29fd-44b5-bfb0-d66e59bb5be4')
 $admin=Open-Sql $TrialDatabase;Assert-Trial $admin $m -Lease
 Check 'SQL exact GUID owner token lease verified' $true
 $dll=Join-Path $EnvRoot 'runtime/backend/HotelOps.Api.dll'
 Check 'Runtime binary SHA and product source' ((Get-FileHash $dll).Hash -ceq $m.runtime_binary_sha256 -and [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion -ceq ('1.0.0+'+$m.source_commit))
 $listener=@(Get-NetTCPConnection -State Listen -LocalPort 7039)
 Check 'HTTPS listener PID matches ENV runtime' ($listener.Count -gt 0 -and @($listener|Where-Object OwningProcess -ne $m.runtime_pid).Count -eq 0)
 $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$m.runtime_pid)
 Check 'Runtime process uses frozen binary path' ($process.CommandLine.Contains($dll))
 $sql=Open-Sql $TrialDatabase -Runtime
 Check 'Restricted SQL identity' ((Sql-Scalar $sql 'SELECT ORIGINAL_LOGIN()') -ceq $TrialLogin)
 $rooms=Sql-Rows $sql 'SELECT room_id,branch_id,floor_id,area_id,room_type_id,room_number,operational_status,is_active FROM dbo.rooms ORDER BY room_id'
 $history=Sql-Rows $sql 'SELECT history_id,room_id,from_status,to_status,changed_at,reason FROM dbo.room_status_history ORDER BY history_id'
 $blocks=Sql-Rows $sql 'SELECT block_id,room_id,reason,blocked_at,expected_release_at,released_at FROM dbo.room_blocks ORDER BY block_id'
 foreach($actor in @('Allowed','Denied','Anonymous')){
  $handler=[Net.Http.HttpClientHandler]::new();$handler.CookieContainer=[Net.CookieContainer]::new();$handler.AllowAutoRedirect=$false
  $client=[Net.Http.HttpClient]::new($handler);$client.BaseAddress=[Uri]$m.https_base_url;$client.Timeout=[TimeSpan]::FromSeconds(30);$client.DefaultRequestHeaders.Add('Origin',$m.https_base_url)
  $logged=$false
  try{
   if($actor -ne 'Anonymous'){
    $cred=Import-Clixml (Join-Path $SecretDirectory ($actor+'.credential.clixml'))
    $csrf=Send $client GET '/api/auth/csrf';Check "$actor CSRF" ($csrf.status -eq 200) $csrf.status
    $client.DefaultRequestHeaders.Add('X-CSRF-TOKEN',[string]$csrf.data.csrfToken)
    $login=Send $client POST '/api/auth/login' @{username=$cred.UserName;password=$cred.GetNetworkCredential().Password}
    Check "$actor login" ($login.status -eq 200 -and $login.data.user.username -ceq $cred.UserName) $login.status;$logged=$true
    $null=$client.DefaultRequestHeaders.Remove('X-CSRF-TOKEN');$client.DefaultRequestHeaders.Add('X-CSRF-TOKEN',[string]$login.data.csrfToken)
    $session=Send $client GET '/api/auth/session';Check "$actor real session" ($session.status -eq 200 -and $session.data.user.username -ceq $cred.UserName) $session.status
   }
   if($actor -ne 'Allowed'){
    foreach($path in @('/api/rooms?branchId=1','/api/rooms/1?branchId=1','/api/rooms/1/history?branchId=1')){
     $r=Send $client GET $path;$expected=if($actor -eq 'Anonymous'){401}else{403};Check "$actor $path" ($r.status -eq $expected -and $null -eq $r.data.PSObject.Properties['items'] -and $null -eq $r.data.PSObject.Properties['room']) $r.status
    }
   }else{
    $list=Send $client GET '/api/rooms?branchId=1';$own=@($rooms.Rows|Where-Object branch_id -eq 1)
    Check 'Scoped total and room IDs match SQL' ($list.status -eq 200 -and $list.data.totalCount -eq $own.Count -and (@($list.data.items.id|Sort-Object)-join ',') -ceq (@($own.room_id|Sort-Object)-join ',')) $list.status
    foreach($r in $list.data.items){$row=@($own|Where-Object room_id -eq $r.id)[0];Check ('Room status SQL '+$r.id) ($r.operationalStatusCode -ceq $row.operational_status -and $r.branchId -eq $row.branch_id)}
    foreach($g in @($own|Group-Object operational_status)){Check ('Counter SQL '+$g.Name) (@($list.data.statusCounts|Where-Object operationalStatusCode -eq $g.Name)[0].count -eq $g.Count)}
    foreach($query in @('operationalStatusCode=DIRTY','floorId=1','floorId=2','areaId=1','roomTypeId=1','search=ENVFE03-10','search=NO_MATCH','floorId=999999')){
     $pair=$query.Split('=');$expected=@($own|Where-Object {$fixtureRow=$_;switch($pair[0]){'operationalStatusCode'{$fixtureRow.operational_status -eq $pair[1]};'floorId'{$fixtureRow.floor_id -eq [long]$pair[1]};'areaId'{$fixtureRow.area_id -eq [long]$pair[1]};'roomTypeId'{$fixtureRow.room_type_id -eq [long]$pair[1]};'search'{$fixtureRow.room_number.Contains($pair[1])}}})
     $r=Send $client GET ('/api/rooms?branchId=1&'+$query)
     Check ('Filter SQL '+$query) ($r.status -eq 200 -and $r.data.totalCount -eq $expected.Count -and (@($r.data.items.id|Sort-Object)-join ',') -ceq (@($expected.room_id|Sort-Object)-join ',')) $r.status
     $sum=0;foreach($c in $r.data.statusCounts){$sum+=$c.count;Check ('Filtered counter '+$query+' '+$c.operationalStatusCode) ($c.count -eq @($expected|Where-Object operational_status -eq $c.operationalStatusCode).Count)}
     Check ('Counter total '+$query) ($sum -eq $expected.Count)
    }
    $seen=@();for($page=1;$page -le 4;$page++){$r=Send $client GET ('/api/rooms?branchId=1&pageSize=1&sort=roomNumber&page='+$page);Check ('Page '+$page) ($r.status -eq 200 -and $r.data.totalCount -eq 3 -and @($r.data.items).Count -eq $(if($page -le 3){1}else{0})) $r.status;$seen+=@($r.data.items.id);Check ('Page counter unaffected '+$page) (($r.data.statusCounts|Measure-Object count -Sum).Sum -eq 3)}
    Check 'Pagination unique complete IDs' ((@($seen|Where-Object {$_}|Sort-Object -Unique)-join ',') -ceq '1,2,3')
    foreach($case in @(@('/api/rooms?branchId=2',403),@('/api/rooms?branchId=2&floorId=1',403),@('/api/rooms/4?branchId=2',403),@('/api/rooms/4/history?branchId=2',403),@('/api/rooms/4?branchId=1',404),@('/api/rooms/4/history?branchId=1',404),@('/api/rooms/999999?branchId=1',404),@('/api/rooms/999999/history?branchId=1',404),@('/api/rooms?branchId=1&operationalStatusCode=BLOCKED',400),@('/api/rooms?branchId=1&page=0',400),@('/api/rooms?branchId=1&floorId=0',400),@('/api/rooms?branchId=1&allBranches=true',400),@('/api/rooms?allBranches=true',403))){$r=Send $client GET $case[0];Check ('Authorization/validation '+$case[0]) ($r.status -eq $case[1] -and $null -eq $r.data.PSObject.Properties['items'] -and $null -eq $r.data.PSObject.Properties['room']) $r.status}
    $r=Send $client GET '/api/rooms?branchId=1';Check 'Scope restored after denial' ($r.status -eq 200 -and $r.data.totalCount -eq 3) $r.status
    $r=Send $client GET '/api/rooms/1?branchId=1';$h=@($history.Rows|Where-Object room_id -eq 1);$b=@($blocks.Rows|Where-Object { $_.room_id -eq 1 -and $_.released_at -is [DBNull] })
    Check 'Detail status matches SQL' ($r.status -eq 200 -and $r.data.room.operationalStatusCode -ceq $own[0].operational_status) $r.status
    Check 'Unreleased block matches SQL' (@($r.data.unreleasedBlocks).Count -eq $b.Count -and $r.data.unreleasedBlocks[0].id -eq $b[0].block_id -and $r.data.unreleasedBlocks[0].reason -ceq $b[0].reason)
    Check 'No inferred readiness or cleaning evidence' ($null -eq $r.data.PSObject.Properties['effectiveReadiness'] -and $null -eq $r.data.PSObject.Properties['cleaningStatus'])
    $r=Send $client GET '/api/rooms/1/history?branchId=1';Check 'Recorded history matches SQL' ($r.status -eq 200 -and $r.data.totalCount -eq $h.Count -and $r.data.items[0].id -eq $h[0].history_id -and $r.data.items[0].toStatusCode -ceq $h[0].to_status -and $r.data.coverage -ceq 'RECORDED_STATUS_EVENTS_ONLY') $r.status
    $r=Send $client GET '/api/rooms/1/history?branchId=1&page=2';Check 'History empty next page' ($r.status -eq 200 -and @($r.data.items).Count -eq 0 -and $r.data.totalCount -eq 1) $r.status
   }
  }finally{
   if($logged){$logout=Send $client POST '/api/auth/logout';Check "$actor logout" ($logout.status -eq 204) $logout.status;$after=Send $client GET '/api/rooms?branchId=1';Check "$actor after logout" ($after.status -eq 401) $after.status}
   $cred=$null;$client.Dispose();$handler.Dispose()
  }
 }
 $binding=Sql-Rows $admin "SELECT s.host_process_id,s.login_name,DB_NAME(s.database_id) AS database_name,c.encrypt_option,c.auth_scheme FROM sys.dm_exec_sessions s JOIN sys.dm_exec_connections c ON c.session_id=s.session_id WHERE s.host_process_id=@pid AND s.login_name=@login" @{'@pid'=[int]$m.runtime_pid;'@login'=$TrialLogin}
 Check 'Actual runtime SQL PID binding/TLS' ($binding.Rows.Count -gt 0 -and @($binding.Rows|Where-Object {$_.database_name -cne $TrialDatabase -or $_.encrypt_option -cne 'TRUE' -or $_.auth_scheme -cne 'SQL'}).Count -eq 0)
 $receipt=@{task='FE03-INDEPENDENT-004';status='PASS';source_commit=$m.source_commit;sql_database=$TrialDatabase;database_guid=$m.database_guid;https=$m.https_base_url;lease_expiry=$m.lease_end_utc;utc=[DateTimeOffset]::UtcNow.ToString('o');checks=$checks.ToArray();tls_validation='ENABLED';business_writes=0}
 $receipt|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'backend-acceptance.json') -Encoding UTF8
 Write-Output ('LIVE_BACKEND_PASS checks='+$checks.Count)
} catch {
 @{task='FE03-INDEPENDENT-004';status='FAIL';checks=$checks.ToArray();error_type=$_.Exception.GetType().Name}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'backend-failure.json') -Encoding UTF8
 throw
} finally {if($sql){$sql.Dispose()};if($admin){$admin.Dispose()}}
