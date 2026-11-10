param(
    [Parameter(Mandatory=$true)][string]$EnvRoot,
    [string]$ExpectedApplicationSha='717715fa0f9fa51ba467592f1dd01ea06e8ce568'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HistoryAttribution.Assertions.ps1')
$out=Join-Path $PSScriptRoot '../../../obj/fe03-next-010'
$null=New-Item -ItemType Directory -Force $out
$checks=[Collections.Generic.List[object]]::new()
$script:sqlAttempted=$false; $script:httpAttempted=$false
$admin=$null; $sql=$null; $status='NOT_RUN'; $stage='RUNTIME_GUARD'
function Check($Name, [bool]$Pass, [int]$Http=0) {
    $checks.Add(@{name=$Name;pass=$Pass;http_status=$Http})
    if (!$Pass) { throw ('HISTORY_ACCEPTANCE_FAILED: '+$Name) }
}
function Send($Client, $Method, $Path, $Body=$null) {
    if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($m.lease_end_utc)) { throw 'BLOCKED_ENVIRONMENT: lease expired.' }
    $script:httpAttempted=$true
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method),$Path)
    if ($null -ne $Body) { $request.Content=[Net.Http.StringContent]::new(($Body|ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json') }
    try {
        $response=$Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $raw=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return @{status=[int]$response.StatusCode;data=$(if($raw){$raw|ConvertFrom-Json}else{$null})}
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
try {
    $c=Get-Content (Join-Path $EnvRoot 'ENV-CONTRACT.json') -Raw|ConvertFrom-Json
    $m=Get-Content (Join-Path $EnvRoot 'manifest.json') -Raw|ConvertFrom-Json
    # Fail before sourcing environment helpers, accessing credentials or connecting to SQL/HTTPS.
    Assert-HistoryRuntimeContract $c $m $ExpectedApplicationSha
    . (Join-Path $EnvRoot 'Common.ps1')
    Set-StrictMode -Off
    Assert-Hax
    Add-Type -AssemblyName System.Net.Http
    $dll=Join-Path $EnvRoot 'runtime/backend/HotelOps.Api.dll'
    Check 'Runtime binary hash/source' ((Get-FileHash $dll).Hash -ceq $m.runtime_binary_sha256 -and
        [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion -ceq ('1.0.0+'+$ExpectedApplicationSha))
    $listener=@(Get-NetTCPConnection -State Listen -LocalPort ([Uri]$m.https_base_url).Port)
    Check 'Listener identity' ($listener.Count -gt 0 -and @($listener|Where-Object OwningProcess -ne $m.runtime_pid).Count -eq 0)
    $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$m.runtime_pid)
    Check 'Frozen binary process path' ($process.CommandLine.Contains($dll))
    $script:sqlAttempted=$true
    $admin=Open-Sql $TrialDatabase
    Assert-Trial $admin $m -Lease
    Check 'Exact database GUID owner and lease markers' $true
    $sql=Open-Sql $TrialDatabase -Runtime
    Check 'Restricted SQL identity' ((Sql-Scalar $sql 'SELECT ORIGINAL_LOGIN()') -ceq $TrialLogin)
    $branchRows=@($c.BRANCH_ROOM_FIXTURES|Where-Object branch_code -CEQ $c.ALLOWED_ACTOR_FIXTURE.branch)
    $branchIds=@($branchRows.branch_id|Sort-Object -Unique)
    if ($branchIds.Count -ne 1) { throw 'BLOCKED_FIXTURE_COVERAGE: exactly one allowed branch required.' }
    $branch=[long]$branchIds[0]
    $foreign=@($c.BRANCH_ROOM_FIXTURES|Where-Object branch_id -ne $branch|Select-Object -First 1)
    if ($foreign.Count -ne 1) { throw 'BLOCKED_FIXTURE_COVERAGE: foreign room required.' }
    $rows=Sql-Rows $sql 'SELECT h.history_id,h.room_id,h.from_status,h.to_status,h.changed_at,h.changed_by FROM dbo.room_status_history h JOIN dbo.rooms r ON r.room_id=h.room_id WHERE r.branch_id=@branch ORDER BY h.changed_at DESC,h.history_id DESC' @{'@branch'=$branch}
    $recorded=@($rows.Rows|Where-Object { $_.changed_by -isnot [DBNull] })
    $missing=@($rows.Rows|Where-Object { $_.changed_by -is [DBNull] })
    Assert-HistoryFixtureCoverage @($rows.Rows)
    $stage='LIVE_HTTPS'
    foreach ($actor in @('Allowed','Denied','Anonymous')) {
        $handler=[Net.Http.HttpClientHandler]::new();$handler.CookieContainer=[Net.CookieContainer]::new();$handler.AllowAutoRedirect=$false
        $client=[Net.Http.HttpClient]::new($handler);$client.BaseAddress=[Uri]$m.https_base_url;$client.Timeout=[TimeSpan]::FromSeconds(30)
        $client.DefaultRequestHeaders.Add('Origin',$m.https_base_url)
        $logged=$false;$credential=$null
        try {
            if ($actor -ne 'Anonymous') {
                $credential=Import-Clixml (Join-Path $SecretDirectory ($actor+'.credential.clixml'))
                $expectedUser=if($actor -eq 'Allowed'){$c.ALLOWED_ACTOR_FIXTURE.username}else{$c.DENIED_ACTOR_FIXTURE.username}
                if ($credential.UserName -cne $expectedUser) { throw 'Credential reference identity mismatch.' }
                $csrf=Send $client GET '/api/auth/csrf';Check "$actor CSRF" ($csrf.status -eq 200) $csrf.status
                $client.DefaultRequestHeaders.Add('X-CSRF-TOKEN',[string]$csrf.data.csrfToken)
                $login=Send $client POST '/api/auth/login' @{username=$credential.UserName;password=$credential.GetNetworkCredential().Password}
                $logged=$login.status -eq 200
                Check "$actor login" ($logged -and $login.data.user.username -ceq $expectedUser) $login.status
                $null=$client.DefaultRequestHeaders.Remove('X-CSRF-TOKEN');$client.DefaultRequestHeaders.Add('X-CSRF-TOKEN',[string]$login.data.csrfToken)
            }
            $firstRoom=[long]$recorded[0].room_id
            if ($actor -ne 'Allowed') {
                $r=Send $client GET "/api/rooms/$firstRoom/history?branchId=$branch"
                $expected=if($actor -eq 'Denied'){403}else{401}
                Check "$actor history denied without room facts" ($r.status -eq $expected -and $null -eq $r.data.PSObject.Properties['items']) $r.status
            } else {
                $recordedSeen=$false;$nullSeen=$false;$multiEventRoom=$false
                foreach ($roomId in @($rows.Rows.room_id|Sort-Object -Unique)) {
                    $expectedRows=@($rows.Rows|Where-Object room_id -eq $roomId)
                    if($expectedRows.Count -ge 2){$multiEventRoom=$true}
                    $pages=[Math]::Max(1,[Math]::Ceiling($expectedRows.Count/20.0))
                    for ($page=1;$page -le $pages+1;$page++) {
                        $r=Send $client GET "/api/rooms/$roomId/history?branchId=$branch&page=$page&pageSize=20"
                        Check "Same-room history HTTP room=$roomId page=$page" ($r.status -eq 200) $r.status
                        Assert-RecordedHistoryPage $r.data $expectedRows $roomId $page
                        Check "SQL attribution ordering page room=$roomId page=$page" $true
                        foreach($item in $r.data.items){if($null -eq $item.changedByUserId){$nullSeen=$true}else{$recordedSeen=$true}}
                    }
                }
                Check 'Recorded actor persisted ID verified' $recordedSeen
                Check 'Null actor preserved without inference' $nullSeen
                if(!$multiEventRoom){throw 'BLOCKED_FIXTURE_COVERAGE: at least two same-room history rows needed for live ordering evidence.'}
                Check 'Multiple same-room ordering rows verified' $multiEventRoom
                foreach($case in @(
                    @("/api/rooms/$($foreign[0].room_id)/history?branchId=$branch",404),
                    @("/api/rooms/$($foreign[0].room_id)/history?branchId=$($foreign[0].branch_id)",403),
                    @("/api/rooms?branchId=$($foreign[0].branch_id)",403))) {
                    $r=Send $client GET $case[0]
                    Check ('Cross-branch isolation '+$case[0]) ($r.status -eq $case[1] -and $null -eq $r.data.PSObject.Properties['items']) $r.status
                }
            }
        } finally {
            try {
                if($logged){$r=Send $client POST '/api/auth/logout';Check "$actor logout" ($r.status -eq 204) $r.status}
            } finally { $credential=$null;$client.Dispose();$handler.Dispose() }
        }
    }
    $status='PASS'
} catch {
    $code=([string]$_.Exception.Message).Split(':')[0]
    $status=if($code -in @('BLOCKED_RUNTIME_VERSION','BLOCKED_ENVIRONMENT','BLOCKED_FIXTURE_COVERAGE')){$code}else{'FAIL'}
    # Avoid raw exception/request/response contents in evidence.
    Write-Output ('HISTORY_LIVE_ACCEPTANCE: '+$status+'; stage='+$stage)
} finally {
    if($sql){$sql.Dispose()};if($admin){$admin.Dispose()}
    @{task='FE03-NEXT-010';status=$status;expected_application_sha=$ExpectedApplicationSha;stage=$stage;checks=$checks.ToArray();business_writes=0;tls_validation_configured=$true;sql_attempted=$script:sqlAttempted;https_attempted=$script:httpAttempted;available_runtime_sha=$m.source_commit;utc=[DateTimeOffset]::UtcNow.ToString('o')}|
        ConvertTo-Json -Depth 7|Set-Content (Join-Path $out 'history-live-acceptance.json') -Encoding UTF8
}
if($status -eq 'PASS'){Write-Output ('HISTORY_LIVE_ACCEPTANCE: PASS checks='+$checks.Count);exit 0}
if($status -like 'BLOCKED_*'){exit 2}
exit 1
