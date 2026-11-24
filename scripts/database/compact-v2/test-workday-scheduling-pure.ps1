param([string]$Root = (Resolve-Path "$PSScriptRoot/../../..").Path)
$ErrorActionPreference = 'Stop'
# Compile only the owned Workday sources against existing build outputs. No build, server or SQL writes.
$target = Join-Path $Root 'artifacts/workday-validation'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$apiBin = Join-Path $Root 'src/HotelOps.Api/bin/Debug/net10.0'
$sdk = Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$references = @{}
foreach ($dll in Get-ChildItem $apiBin -Filter '*.dll') { $references[$dll.Name] = $dll.FullName }
foreach ($pack in @('Microsoft.NETCore.App.Ref', 'Microsoft.AspNetCore.App.Ref')) {
    $version = Get-ChildItem "C:/Program Files/dotnet/packs/$pack" -Directory | Sort-Object Name -Descending | Select-Object -First 1
    foreach ($dll in Get-ChildItem "$($version.FullName)/ref/net10.0" -Filter '*.dll') { $references[$dll.Name] = $dll.FullName }
}
$sources = @(
    'src/HotelOps.Api/obj/Debug/net10.0/HotelOps.Api.GlobalUsings.g.cs',
    'src/HotelOps.Api/Compact/Services/WorkdayService.cs',
    'src/HotelOps.Api/Compact/Services/WorkdayService.Scheduling.cs',
    'src/HotelOps.Api/Compact/Services/WorkdayCalendar.cs',
    'src/HotelOps.Api/Compact/Contracts/WorkdayRequests.cs',
    'src/HotelOps.Api/Compact/Controllers/WorkdayController.cs',
    'scripts/database/compact-v2/WorkdaySchedulingChecks.cs'
)
$argsFile = Join-Path $target 'compile.rsp'
# CS0436 is expected: the source WorkdayService intentionally replaces the old type in the referenced API assembly.
$compilerArgs = @('/nologo', '/target:exe', '/langversion:latest', '/nullable:enable', '/nowarn:0436', ('/out:"' + $target + '/WorkdayChecks.dll"'))
$compilerArgs += $references.Values | ForEach-Object { '/reference:"' + $_ + '"' }
$compilerArgs += $sources | ForEach-Object { '"' + (Join-Path $Root $_) + '"' }
[IO.File]::WriteAllLines($argsFile, $compilerArgs)
& dotnet "$($sdk.FullName)/Roslyn/bincore/csc.dll" "@$argsFile"
if ($LASTEXITCODE -ne 0) { throw 'Workday source compilation failed.' }
Copy-Item -LiteralPath (Join-Path $apiBin 'HotelOps.Api.runtimeconfig.json') -Destination (Join-Path $target 'WorkdayChecks.runtimeconfig.json')
Get-ChildItem $apiBin -Filter '*.dll' | Copy-Item -Destination $target
& dotnet (Join-Path $target 'WorkdayChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Workday pure tests failed.' }
