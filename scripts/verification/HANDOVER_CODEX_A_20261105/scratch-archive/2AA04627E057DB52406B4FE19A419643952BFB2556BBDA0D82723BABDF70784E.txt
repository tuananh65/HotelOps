$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $repo
try {
    $runs=@(
        @{Name='build';Args=@('build','HotelOps.slnx')},
        @{Name='dotnet-test';Args=@('test','HotelOps.slnx','--no-restore')},
        @{Name='CurrentUserAccessor';Args=@('run','--project','scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj','--no-restore')},
        @{Name='DataScope';Args=@('run','--project','scripts/verification/DataScope/DataScope.Verification.csproj','--no-restore')},
        @{Name='PermissionService';Args=@('run','--project','scripts/verification/PermissionService/PermissionService.Verification.csproj','--no-restore','--','--live')},
        @{Name='Authorization';Args=@('run','--project','scripts/verification/Authorization/Authorization.Verification.csproj','--no-restore','--','--live')},
        @{Name='ResourceAuthorization';Args=@('run','--project','scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj','--no-restore','--','--live')}
    )
    $receipt=@()
    foreach($run in $runs){
        Write-Output "RUN: $($run.Name) (sequential)"
        $arguments=$run.Args
        & dotnet @arguments 2>&1|Tee-Object -FilePath (Join-Path $PSScriptRoot ($run.Name+'.log'))
        $code=$LASTEXITCODE
        $receipt+=@{name=$run.Name;exitCode=$code;completedUtc=[DateTime]::UtcNow.ToString('o')}
        $receipt|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'regressions.json')
        if($code -ne 0){throw "Regression failed: $($run.Name)"}
    }
}finally{Pop-Location}
