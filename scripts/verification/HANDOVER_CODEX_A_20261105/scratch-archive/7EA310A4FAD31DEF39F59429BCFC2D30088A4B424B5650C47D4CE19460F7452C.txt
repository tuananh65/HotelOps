param([switch]$IncludeLiveReadOnly)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$output=Join-Path $PSScriptRoot 'obj/validation';[void](New-Item -ItemType Directory -Force -Path $output)
$runs=@(
    @{name='build';args=@('build','HotelOps.slnx')},
    @{name='dotnet-test';args=@('test','HotelOps.slnx','--no-restore')},
    @{name='CurrentUserAccessor';args=@('run','--project','scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj','--no-restore')},
    @{name='DataScope-model';args=@('run','--project','scripts/verification/DataScope/DataScope.Verification.csproj','--no-restore','--','--model-only')},
    @{name='PermissionService-doubles';args=@('run','--project','scripts/verification/PermissionService/PermissionService.Verification.csproj','--no-restore')},
    @{name='Authorization-loopback';args=@('run','--project','scripts/verification/Authorization/Authorization.Verification.csproj','--no-restore')},
    @{name='ResourceAuthorization-loopback';args=@('run','--project','scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj','--no-restore')},
    @{name='Health-guards-cancellation';args=@('run','--project','scripts/verification/AuthorizationHardening/AuthorizationHardening.Verification.csproj','--no-restore')},
    @{name='DataScope-readonly';args=@('run','--project','scripts/verification/DataScope/DataScope.Verification.csproj','--no-restore')}
)
if($IncludeLiveReadOnly){foreach($name in @('PermissionService','Authorization','ResourceAuthorization')){$runs+=@{name=$name+'-live';args=@('run','--project',"scripts/verification/$name/$name.Verification.csproj",'--no-restore','--','--live')}}}
$receipt=@();$failed=$false
Push-Location $repo
try {
    foreach($run in $runs){
        Write-Output "RUN: $($run.name)"
        $arguments=$run.args
        $ErrorActionPreference='Continue'
        $captured=@(& dotnet @arguments 2>&1);$code=$LASTEXITCODE
        $ErrorActionPreference='Stop'
        $captured|Out-String|Set-Content -LiteralPath (Join-Path $output ($run.name+'.log')) -Encoding UTF8
        $body=$captured|Out-String
        $state=if($code -eq 0){'PASSED'}elseif($body -match 'READ_ONLY_(SETUP_REQUIRED|PRIVILEGE_REJECTED)'){'BLOCKED'}else{'FAILED'}
        if($state -eq 'FAILED'){$failed=$true}
        Write-Output "$state`: $($run.name) (exit $code)"
        $captured|Select-Object -Last 2|ForEach-Object {Write-Output $_}
        $receipt+=@{name=$run.name;status=$state;exitCode=$code;completedUtc=[DateTime]::UtcNow.ToString('o')}
        $receipt|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding UTF8
    }
    if($failed){throw 'One or more verification runs failed; inspect local validation logs'}
    if(@($receipt|Where-Object {$_.status -eq 'BLOCKED'}).Count){Write-Output 'WITH NOTES: live SELECT verification blocked pending approved read-only principal; not reported as Passed'}
}finally{$ErrorActionPreference='Stop';Pop-Location}
