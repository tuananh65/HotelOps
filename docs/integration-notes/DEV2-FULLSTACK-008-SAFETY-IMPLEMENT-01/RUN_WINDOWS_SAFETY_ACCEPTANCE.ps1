[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('fixture-acceptance','browser-acceptance')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$AuthorizationPath
)
# NEW candidate only. Never invokes Runtime007, Full61, old broad cleanup or a demo mode.
# DO NOT EXECUTE without separately approved, exact-release-bound safety test authority.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$releasePath=Join-Path $PSScriptRoot 'CANDIDATE_RELEASE.json'
$utf8=[Text.UTF8Encoding]::new($false)
function ReadJson([string]$path){ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path))}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function SafePath([string]$relative){$p=[IO.Path]::GetFullPath((Join-Path $repo $relative));if(!$p.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside workspace'};return $p}
if($env:COMPUTERNAME -cne 'KIENPC'){throw 'Exact Windows host required; no sandbox SQL retry'}
$authority=ReadJson $AuthorizationPath
if($authority.Approved -ne $true -or $authority.Scope -cne 'CHECKPOINT008_SAFETY_ACCEPTANCE_ONLY' -or $authority.AllowedMode -cne $Mode){throw 'Separate concrete execution authorization required'}
if($authority.Run -cnotmatch '^SAFETY008_[A-Za-z0-9_]{8,80}$'){throw 'Invalid bound run'}
if((Hash $releasePath) -cne $authority.CandidateReleaseSha256){throw 'Release not approved'}
$release=ReadJson $releasePath
foreach($pin in $release.files){if((Hash (SafePath $pin.path)) -cne $pin.sha256){throw ('Release drift: '+$pin.path)}}
$nodeCommand=Get-Command node.exe -CommandType Application -ErrorAction SilentlyContinue
$node=if($nodeCommand){$nodeCommand.Source}else{Join-Path $env:ProgramFiles 'nodejs/node.exe'}
if(!(Test-Path -LiteralPath $node -PathType Leaf)){throw 'Installed Node executable not found'}
$node=& $node -p 'process.execPath';if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $node -PathType Leaf)){throw 'Actual Node executable resolution failed'}
$dotnet=(Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
$out=SafePath ('docs/integration-notes/DEV2-FULLSTACK-008-SAFETY-IMPLEMENT-01/host-receipts/'+$authority.Run)
if(Test-Path -LiteralPath $out){throw 'New receipt directory required; no automatic resume'}
[void][IO.Directory]::CreateDirectory($out)
function Receipt([string]$name,$value){$p=Join-Path $out ($name+'.json');if(Test-Path -LiteralPath $p){throw 'No receipt overwrite'};[IO.File]::WriteAllText($p,(ConvertTo-Json -InputObject $value -Depth 40),$utf8)}
function Identity($process){$process.Refresh();$c=Get-CimInstance Win32_Process -Filter ('ProcessId='+$process.Id) -ErrorAction Stop;if(!$c){throw 'Process metadata unavailable'};return @{Pid=$process.Id;ParentPid=[int]$c.ParentProcessId;Executable=$process.MainModule.FileName;StartedUtc=$process.StartTime.ToUniversalTime().ToString('o')}}
function Matches($id){$p=Get-Process -Id $id.Pid -ErrorAction SilentlyContinue;if(!$p){return $false};return $p.StartTime.ToUniversalTime() -eq [DateTime]$id.StartedUtc -and $p.MainModule.FileName -ieq $id.Executable}
function StartOwned([string]$exe,[string[]]$arguments,[hashtable]$environment,[string]$cwd){
    $si=New-Object Diagnostics.ProcessStartInfo;$si.FileName=$exe;$si.WorkingDirectory=$cwd;$si.UseShellExecute=$false;$si.CreateNoWindow=$true
    $si.RedirectStandardInput=$true;$si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true
    # Windows PowerShell 5.1 lacks ArgumentList. No cmd/npm shims or shell evaluation.
    if(@($arguments|Where-Object {$_ -match '["\r\n]'}).Count){throw 'Unsupported native argument'}
    $si.Arguments=(@($arguments|ForEach-Object {'"'+$_+'"'}) -join ' ')
    foreach($k in @('DOTNET_STARTUP_HOOKS','ASPNETCORE_HOSTINGSTARTUPASSEMBLIES','DEV2_TRIAL_HOOK_DIRECTORY','NODE_OPTIONS','NODE_TLS_REJECT_UNAUTHORIZED','DEBUG','PWDEBUG')){[void]$si.EnvironmentVariables.Remove($k)}
    foreach($k in $environment.Keys){$si.EnvironmentVariables[$k]=$environment[$k]}
    $p=New-Object Diagnostics.Process;$p.StartInfo=$si;if(!$p.Start()){throw 'Owned process failed to start'};return $p
}
$owned=New-Object Collections.Generic.List[object]
$drains=New-Object Collections.Generic.List[object]
$worker=$null;$workerExit=$null;$tls=$null;$status='FAIL';$phase='candidate authority';$workerErr=$null;$workerLog=$null;$stdout=$null
$baseEnv=@{ASPNETCORE_ENVIRONMENT='Development';DOTNET_ENVIRONMENT='Development';DEV2_DEMO_AUTHORITY_V1='456249493fc4ee7eb81ba18ef18ff44a0b55a70064736fbdb78bae1784cf74be'}
function Heartbeat{if($worker -and !$worker.HasExited){$worker.StandardInput.WriteLine('HEARTBEAT');$worker.StandardInput.Flush()}}
function StopOwnedRuntime{
    # Snapshot only descendants of the exact roots launched by this controller.
    $map=@(Get-CimInstance Win32_Process -ErrorAction Stop)
    $ids=New-Object Collections.Generic.List[object]
    foreach($item in $owned){
        if(!(Matches $item.identity)){throw 'Owned root absent or reused before shutdown; no guessed stop'}
        $ids.Add($item.identity);$parents=@([int]$item.identity.Pid)
        while($parents.Count){$next=@();foreach($entry in $map){if($parents -contains [int]$entry.ParentProcessId){$p=Get-Process -Id $entry.ProcessId -ErrorAction Stop;$id=Identity $p;if([DateTime]$id.StartedUtc -lt [DateTime]$item.identity.StartedUtc){throw 'Descendant predates root'};$ids.Add($id);$next+=[int]$id.Pid}};$parents=$next}
    }
    Receipt 'OWNED_RUNTIME_SHUTDOWN_INTENT' @{identities=@($ids.ToArray());scope='EXACT_CURRENT_PID_START_EXE_PARENT_TREE_ONLY';utc=[DateTime]::UtcNow.ToString('o')}
    # Root first, then individually verified descendants. Never taskkill /T or Kill(true).
    foreach($id in $ids){Heartbeat;$p=Get-Process -Id $id.Pid -ErrorAction SilentlyContinue;if(!$p){continue};if(!(Matches $id)){throw 'PID reuse or identity drift; abort stop'};$p.Kill();$until=[DateTime]::UtcNow.AddSeconds(10);while(!$p.WaitForExit(100)){Heartbeat;if([DateTime]::UtcNow -gt $until){throw 'Owned process exit unconfirmed'}}}
    foreach($id in $ids){if(Get-Process -Id $id.Pid -ErrorAction SilentlyContinue){throw 'Owned PID remains or reused'}}
    foreach($entry in $drains){$null=$entry.stdout.GetAwaiter().GetResult();$null=$entry.stderr.GetAwaiter().GetResult()}
    Receipt 'OWNED_RUNTIME_SHUTDOWN' @{status='PASS_EXACT_IDENTITIES_ABSENT';identities=@($ids.ToArray());nativeExits=@($owned|ForEach-Object {@{pid=$_.process.Id;exit=$_.process.ExitCode}});rawApiOutput='DRAINED_NOT_EXPORTED_TO_AVOID_PRIVATE_PAYLOADS';utc=[DateTime]::UtcNow.ToString('o')}
    $owned.Clear()
}
try{
    $conflicts=@();foreach($p in (Get-CimInstance Win32_Process -ErrorAction Stop)){
        if($p.ProcessId -eq $PID){continue}
        if($p.Name -notmatch '^(dotnet|HotelOps.Api|Dev2AllActions|Dev2Runtime007|Dev2Safety008|node|powershell|pwsh|msedge|chrome|chromium)(\.exe)?$'){continue}
        $readable=![string]::IsNullOrWhiteSpace($p.CommandLine)
        $isTrial=$p.CommandLine -match 'HotelOps\.Api|Dev2(AllActions|Runtime007|Safety008)|RUN_(ALL61|CHECKPOINT007|CHECKPOINT008|WINDOWS_SAFETY)|Dev2Safety008[/\\]browser[/\\]child\.mjs|dev2-runtime007-|vite[/\\]bin[/\\]vite\.js|--remote-debugging-(pipe|port)|playwright'
        if(!$readable -or $isTrial){$conflicts+=@{pid=$p.ProcessId;parentPid=$p.ParentProcessId;name=$p.Name;readable=$readable;trialOrAutomation=$isTrial}}
    }
    Receipt 'LIVE_PROCESS_EXCLUSION' @{status=$(if($conflicts.Count){'BLOCKED'}else{'PASS_READONLY_AT_CAPTURE'});conflicts=$conflicts;commandLinesExported=$false;utc=[DateTime]::UtcNow.ToString('o')}
    if($conflicts.Count){throw 'Existing or uninspectable trial/browser process; no new worker'}
    foreach($port in @(7044,5173)){if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue){throw 'Port occupied; existing listeners never stopped'}}
    foreach($key in @('DOTNET_STARTUP_HOOKS','ASPNETCORE_HOSTINGSTARTUPASSEMBLIES','DEV2_TRIAL_HOOK_DIRECTORY','NODE_OPTIONS','NODE_TLS_REJECT_UNAUTHORIZED')){if([Environment]::GetEnvironmentVariable($key,'Process')){throw 'Unexpected runtime override'}}
    # Worker validates 682 pins, fresh sealed preflight, backup and one-use authority
    # before any SQL connection or application startup.
    $worker=StartOwned $dotnet @((SafePath $release.runtimeDll),'run',$Mode,[IO.Path]::GetFullPath($AuthorizationPath),$releasePath,$repo,(Join-Path $out 'worker')) $baseEnv $repo
    Receipt 'CONTROLLER_AND_WORKER_PROCESS' @{controller=(Identity (Get-Process -Id $PID));worker=(Identity $worker);mode=$Mode;releaseSha256=(Hash $releasePath);utc=[DateTime]::UtcNow.ToString('o')}
    $workerErr=$worker.StandardError.ReadToEndAsync();$stdout=$worker.StandardOutput.ReadLineAsync()
    $workerLog=New-Object IO.StreamWriter((Join-Path $out 'worker.stdout.log'),$false,$utf8)
    while(!$stdout.IsCompleted -and !$worker.HasExited){Start-Sleep -Milliseconds 50}
    $line=$stdout.GetAwaiter().GetResult();$workerLog.WriteLine($line);$workerLog.Flush()
    if($line -cne 'CONTROL:AUTHORIZATION_VALIDATED'){throw 'Worker rejected authority before SQL'}
    if($Mode -eq 'browser-acceptance'){
        if(!$authority.AllowDeveloperTlsExport){throw 'Explicit temporary development TLS export authority required'}
        $tls=SafePath ('.tmp-safety008-tls/'+$authority.Run);if(Test-Path -LiteralPath $tls){throw 'TLS directory must be new'};[void][IO.Directory]::CreateDirectory($tls)
        $cert=Join-Path $tls 'localhost.pem';$key=Join-Path $tls 'localhost.key'
        & $dotnet dev-certs https --check *> (Join-Path $out 'certificate-check.log');if($LASTEXITCODE -ne 0){throw 'Existing trusted development certificate required'}
        & $dotnet dev-certs https --export-path $cert --format Pem --no-password *> (Join-Path $out 'certificate-export.log');if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $key)){throw 'Development TLS export failed'}
    }
    $controller=Identity (Get-Process -Id $PID)
    $bootstrap=@{ControllerPid=$PID;ControllerParentPid=$controller.ParentPid;ControllerExecutable=$controller.Executable;ControllerStartedUtc=$controller.StartedUtc;Frontend='https://localhost:5173';NodeExecutable=$node;BrowserScript=(SafePath 'scripts/verification/Dev2Safety008/browser/child.mjs');ApiFrontendProcesses=@()}
    $worker.StandardInput.WriteLine((ConvertTo-Json -InputObject $bootstrap -Depth 10 -Compress));$worker.StandardInput.Flush()
    $stdout=$worker.StandardOutput.ReadLineAsync();$phase='supervised candidate';$nextHeartbeat=[DateTime]::UtcNow;$deadline=[DateTime]::UtcNow.AddMinutes([int]$authority.MaximumMinutes+5)
    while(!$worker.HasExited -or $stdout.IsCompleted){
        if([DateTime]::UtcNow -gt $deadline){throw 'Controller deadline; no forced harness termination or automatic cleanup'}
        if([DateTime]::UtcNow -ge $nextHeartbeat){Heartbeat;$nextHeartbeat=[DateTime]::UtcNow.AddSeconds(1)}
        if(!$stdout.IsCompleted){Start-Sleep -Milliseconds 50;continue}
        $line=$stdout.GetAwaiter().GetResult();if($null -eq $line){break};$workerLog.WriteLine($line);$workerLog.Flush()
        if($line -ceq 'CONTROL:START_API_FRONTEND_AFTER_FIXTURE'){
            if($Mode -cne 'browser-acceptance'){throw 'Unexpected application startup request'}
            $envApi=$baseEnv.Clone();$envApi['ConnectionStrings__HotelOpsDb']='Data Source=lpc:KIENPC;Initial Catalog=HotelOps_DEV2_FullStack_Trial;Integrated Security=True;Encrypt=False;Pooling=False';$envApi['ASPNETCORE_URLS']='https://localhost:7044'
            $api=StartOwned $dotnet @((SafePath $release.apiDll)) $envApi (SafePath 'src/HotelOps.Api');$apiId=Identity $api;$owned.Add(@{process=$api;identity=$apiId});$drains.Add(@{stdout=$api.StandardOutput.ReadToEndAsync();stderr=$api.StandardError.ReadToEndAsync()})
            $envFront=@{DEV_HTTPS_CERT=$cert;DEV_HTTPS_KEY=$key;NODE_EXTRA_CA_CERTS=$cert;API_PROXY_TARGET='https://localhost:7044'}
            $front=StartOwned $node @((SafePath 'frontend/node_modules/vite/bin/vite.js'),'--host','localhost','--port','5173','--strictPort') $envFront (SafePath 'frontend');$frontId=Identity $front;$owned.Add(@{process=$front;identity=$frontId});$drains.Add(@{stdout=$front.StandardOutput.ReadToEndAsync();stderr=$front.StandardError.ReadToEndAsync()})
            $ready=$false;for($i=0;$i -lt 60;$i++){Heartbeat;if($api.HasExited -or $front.HasExited){throw 'Owned API/frontend exited at startup'};try{$r=Invoke-WebRequest -Uri 'https://localhost:5173/login' -UseBasicParsing -TimeoutSec 2;if($r.StatusCode -eq 200 -and (Get-NetTCPConnection -LocalPort 7044 -State Listen -ErrorAction SilentlyContinue)){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 500};if(!$ready){throw 'Trusted HTTPS API/frontend not ready'}
            Receipt 'NORMAL_API_FRONTEND_STARTUP' @{hooksDisabled=$true;authorizationBypass=$false;development=$true;trialOnly=$true;owned=@($apiId,$frontId);utc=[DateTime]::UtcNow.ToString('o')}
            $worker.StandardInput.WriteLine('RUNTIME_READY:'+(ConvertTo-Json -InputObject @($apiId,$frontId) -Compress));$worker.StandardInput.Flush()
        }
        if($line -ceq 'CONTROL:QUIESCE_API_FRONTEND'){
            StopOwnedRuntime
            $worker.StandardInput.WriteLine('API_FRONTEND_STOPPED');$worker.StandardInput.Flush()
        }
        $stdout=$worker.StandardOutput.ReadLineAsync()
    }
    $worker.WaitForExit();$workerExit=$worker.ExitCode
    [IO.File]::WriteAllText((Join-Path $out 'worker.exitcode'),[string]$workerExit,$utf8)
    $result=ReadJson (Join-Path $out 'worker/CANDIDATE_SAFETY_RESULT.json')
    if($workerExit -ne 0 -or $result.status -cne 'PASS_CANDIDATE_SAFETY_REQUIRES_REVIEW'){throw 'Candidate worker failed; preserve exact marker/journal for separate recovery review'}
    $status='PASS_CANDIDATE_SAFETY_REQUIRES_INDEPENDENT_REVIEW'
}catch{Receipt 'CONTROLLER_FAILURE' @{phase=$phase;status='FAIL';category=$_.Exception.GetType().Name;cleanupCertified=$false;automaticRecovery=$false;utc=[DateTime]::UtcNow.ToString('o')};$status='FAIL'}
finally{
    # EOF causes cooperative worker cancellation and blocks its cleanup if controller
    # ownership is lost. Never force-kill the harness or invoke old broad cleanup.
    if($worker){try{$worker.StandardInput.Close()}catch{};if($worker.HasExited){$workerExit=$worker.ExitCode;if($workerErr){[IO.File]::WriteAllText((Join-Path $out 'worker.stderr.log'),$workerErr.GetAwaiter().GetResult(),$utf8)}}}
    if($workerLog){$workerLog.Dispose()}
    if($owned.Count){Receipt 'OWNED_PROCESSES_REQUIRE_REVIEW' @{identities=@($owned|ForEach-Object {$_.identity});automaticTermination=$false;newRuntimeProhibited=$true};$status='FAIL'}
    $tlsRemoved=$null
    if($tls -and $owned.Count -eq 0 -and $worker -and $worker.HasExited){foreach($name in @('localhost.pem','localhost.key')){$p=Join-Path $tls $name;if(Test-Path -LiteralPath $p){if((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'No deletion of reparse TLS path'};Remove-Item -LiteralPath $p -ErrorAction Stop}};$tlsRemoved=$true}
    Receipt 'HOST_SAFETY_SUMMARY' @{status=$status;mode=$Mode;workerNativeExit=$workerExit;tlsFilesRemoved=$tlsRemoved;ownedRuntimeRootsRemaining=$owned.Count;checkpoint008ExecutionSignoff='NOT_GRANTED';checkpoint008DemoAcceptance='NOT_ACHIEVED';sqlBackupOrDdlPerformed=$false;utc=[DateTime]::UtcNow.ToString('o')}
    $pins=@(Get-ChildItem -LiteralPath $out -File|ForEach-Object {@{file=$_.Name;sha256=(Hash $_.FullName)}});Receipt 'RECEIPT_SHA256' $pins
}
if($status -cne 'PASS_CANDIDATE_SAFETY_REQUIRES_INDEPENDENT_REVIEW'){exit 1}
