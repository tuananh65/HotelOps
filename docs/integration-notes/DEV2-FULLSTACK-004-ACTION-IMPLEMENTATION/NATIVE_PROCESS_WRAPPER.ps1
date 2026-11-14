# Dot-sourced by the sealed host runner. No execution time limit and no automatic child termination.
function Receipt([string]$name,$value){
    $target=Join-Path $out ($name+'.json');$pending=$target+'.pending'
    [IO.File]::WriteAllText($pending,(ConvertTo-Json -Depth 30 -InputObject $value),$utf8)
    if(Test-Path -LiteralPath $target){[IO.File]::Replace($pending,$target,($target+'.previous'))}else{[IO.File]::Move($pending,$target)}
}
function Native([string]$name,[string]$exe,[string[]]$arguments){
    $log=Join-Path $out ($name+'.log');$stderr=Join-Path $out ($name+'.stderr.log')
    $process=$null;$code=$null;$started=[DateTime]::UtcNow
    foreach($arg in $arguments){if($arg.Contains('"') -or $arg.EndsWith('\')){throw 'Unsupported native argument quoting; not launched'}}
    $quoted=($arguments|ForEach-Object {'"'+$_+'"'}) -join ' '
    Receipt ($name+'.process') @{status='PREPARING';utc=$started.ToString('o');executable=[IO.Path]::GetFileName($exe);argumentsRecorded=$false}
    try{
        $process=Start-Process -FilePath $exe -ArgumentList $quoted -WorkingDirectory $repo -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $stderr -PassThru
        $handle=$process.Handle;$lastBeat=[DateTime]::MinValue
        do{
            if(([DateTime]::UtcNow-$lastBeat).TotalSeconds -ge 10){
                Receipt ($name+'.process') @{status='RUNNING';pid=$process.Id;startedUtc=$started.ToString('o');heartbeatUtc=[DateTime]::UtcNow.ToString('o');stdout=($name+'.log');stderr=($name+'.stderr.log');timeoutConfigured=$false}
                $lastBeat=[DateTime]::UtcNow
            }
        }while(!$process.WaitForExit(1000))
        $process.WaitForExit();$code=$process.ExitCode
        if($null -eq $code){throw 'Exited process has no native exit code'}
        [IO.File]::WriteAllText((Join-Path $out ($name+'.exitcode')),[string]$code,$utf8)
        Receipt ($name+'.process') @{status='EXITED';pid=$process.Id;startedUtc=$started.ToString('o');finishedUtc=[DateTime]::UtcNow.ToString('o');exitCode=$code}
        Receipt $name @{exitCode=$code;log=($name+'.log');stderr=($name+'.stderr.log');sha256=(Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant();stderrSha256=(Get-FileHash -LiteralPath $stderr -Algorithm SHA256).Hash.ToLowerInvariant()}
        if($code -ne 0){throw ('Step '+$name+' failed; inspect exact receipts')}
    }finally{
        if($process -and !$process.HasExited){Receipt ($name+'.supervision') @{status='SUPERVISION_INTERRUPTED_CHILD_MAY_BE_RUNNING';pid=$process.Id;utc=[DateTime]::UtcNow.ToString('o');childStopped=$false}}
        if($process -and $process.HasExited){$process.Dispose()}
    }
}
