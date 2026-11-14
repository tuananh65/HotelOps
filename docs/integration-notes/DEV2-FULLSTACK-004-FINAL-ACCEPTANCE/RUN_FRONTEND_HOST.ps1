param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$utf8=[Text.UTF8Encoding]::new($false)
Push-Location (Join-Path $repo 'frontend')
try{
    $commands=@(
        @{name='frontend-typecheck';exe='cmd.exe';arguments=@('/d','/c','npm.cmd','run','typecheck')},
        @{name='frontend-build';exe='cmd.exe';arguments=@('/d','/c','npm.cmd','run','build')},
        @{name='inventory-api';exe='node';arguments=@('--test','--test-reporter=tap','tests/inventory-api.test.mjs')},
        @{name='inventory-router';exe='node';arguments=@('--test','--test-reporter=tap','tests/inventory-router.test.mjs')},
        @{name='auth-regression';exe='node';arguments=@('--test','--test-reporter=tap','tests/auth-http.test.mjs','tests/auth-state.test.mjs','tests/auth-login-ui.test.mjs','tests/auth-profile-logout.test.mjs','tests/auth-change-password-ui.test.mjs','tests/auth-router-integration.test.mjs','tests/forgot-password.test.mjs')}
    )
    $failed=$false
    foreach($step in $commands){
        $log=Join-Path $OutputDirectory ($step.name+'.log');$executable=$step.exe;$arguments=$step.arguments
        $preference=$ErrorActionPreference
        try{$ErrorActionPreference='Continue'; & $executable @arguments *> $log; $code=$LASTEXITCODE}
        finally{$ErrorActionPreference=$preference}
        [IO.File]::WriteAllText((Join-Path $OutputDirectory ($step.name+'.exitcode')),[string]$code,$utf8)
        $text=Get-Content -LiteralPath $log -Raw
        $counter=@{};foreach($key in @('tests','pass','fail','cancelled','skipped')){if($text -match ('(?m)^# '+$key+' (\d+)\s*$')){$counter[$key]=[int]$Matches[1]}}
        $receipt=@{exitCode=$code;command=@($executable)+$arguments;sha256=(Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant();counters=$counter}
        [IO.File]::WriteAllText((Join-Path $OutputDirectory ($step.name+'.json')),(ConvertTo-Json -Depth 10 -InputObject $receipt),$utf8)
        if($code -ne 0){$failed=$true}
    }
    if($failed){throw 'Frontend acceptance failed; all original logs and exits preserved'}
}finally{Pop-Location}
