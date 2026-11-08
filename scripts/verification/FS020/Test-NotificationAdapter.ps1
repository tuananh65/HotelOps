param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$taskRoot = Join-Path ([IO.Path]::GetTempPath()) ('hotelops-fs020-' + [guid]::NewGuid().ToString('N'))
$taskPackageRoot = Join-Path $taskRoot 'package'
$taskSource = Join-Path $taskRoot 'source'
New-Item -ItemType Directory -Path $taskPackageRoot, $taskSource | Out-Null
Expand-Archive -LiteralPath $Package -DestinationPath $taskPackageRoot
$taskSeal = Get-Content (Join-Path $taskPackageRoot 'DELIVERY_SEAL.json') -Raw | ConvertFrom-Json
foreach ($taskFile in $taskSeal.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskPackageRoot $taskFile.path)).Hash.ToLowerInvariant() -ne $taskFile.sha256) {
        throw ('Package seal mismatch: ' + $taskFile.path)
    }
}
$taskManifest = Get-Content (Join-Path $taskPackageRoot 'review-package/SOURCE_INVENTORY.json') -Raw | ConvertFrom-Json
if ($taskManifest.laneSourceIdentity -ne 'd647a63f41fb8e7d5a4d1d51914195c27cecd8a670fd99f5c16aac1f48a509e4') {
    throw 'Unexpected DEV2 source identity.'
}
# Export committed baseline, then copy only this checkpoint's DEV1 additions.
# No Git apply, index, branch, secrets, SQL configuration or production registration.
& git -C $taskRepo archive --format=tar ('--output=' + (Join-Path $taskRoot 'baseline.tar')) HEAD
if ($LASTEXITCODE -ne 0) { throw 'Baseline export failed.' }
& tar -xf (Join-Path $taskRoot 'baseline.tar') -C $taskSource
if ($LASTEXITCODE -ne 0) { throw 'Baseline extraction failed.' }
$taskOverlay = Join-Path $taskPackageRoot 'core'
New-Item -ItemType Directory -Path $taskOverlay | Out-Null
& tar -xf (Join-Path $taskPackageRoot 'review-package/fs019-core-overlay.tar') -C $taskOverlay
if ($LASTEXITCODE -ne 0) { throw 'Core extraction failed.' }
$taskCoreFiles = @(Get-ChildItem $taskOverlay -Recurse -File)
if ($taskCoreFiles.Count -ne 14) { throw 'Expected exactly 14 core/dependency files.' }
foreach ($taskFile in $taskCoreFiles) {
    $taskRelative = $taskFile.FullName.Substring($taskOverlay.Length + 1).Replace('\', '/')
    $taskEntry = @($taskManifest.files | Where-Object path -eq $taskRelative)
    if ($taskEntry.Count -ne 1 -or (Get-FileHash $taskFile.FullName).Hash.ToLowerInvariant() -ne $taskEntry[0].sha256) {
        throw ('Core SHA mismatch: ' + $taskRelative)
    }
    $taskTarget = Join-Path $taskSource $taskRelative
    if (Test-Path -LiteralPath $taskTarget) { throw ('Overlay would overwrite baseline: ' + $taskRelative) }
    New-Item -ItemType Directory -Force (Split-Path $taskTarget) | Out-Null
    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget
}
$taskDev1Files = @(
    'src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentNotificationSource.cs',
    'src/HotelOps.Api/Services/Housekeeping/CleaningAssignmentNotification06R1Adapter.cs',
    'tests/HotelOps.Dev1.RegressionTests/CleaningAssignmentNotificationTests.cs',
    'tests/HotelOps.Dev1.RegressionTests/CleaningAssignmentNotification06R1Tests.cs'
)
foreach ($taskFile in $taskDev1Files) {
    Copy-Item -LiteralPath (Join-Path $taskRepo $taskFile) -Destination (Join-Path $taskSource $taskFile) -Force
}
# Only the disposable copy receives the extra mapping hook and compile symbol.
$taskHook = Join-Path $taskSource 'src/HotelOps.Api/Data/HotelOpsDbContext.RoomTypes.cs'
$taskHookText = [IO.File]::ReadAllText($taskHook)
if (-not $taskHookText.Contains('ConfigureNotificationCandidate(modelBuilder);')) { throw 'Existing hook not found.' }
[IO.File]::WriteAllText($taskHook, $taskHookText.Replace('ConfigureNotificationCandidate(modelBuilder);',
    'ConfigureNotificationCandidate(modelBuilder); ConfigureNotifications(modelBuilder);'))
Push-Location $taskSource
try {
    & dotnet build HotelOps.slnx -p:DefineConstants=HOTELOPS_NOTIFICATION_06R1
    if ($LASTEXITCODE -ne 0) { throw 'Disposable integration build failed.' }
    & dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj -p:DefineConstants=HOTELOPS_NOTIFICATION_06R1 --filter 'FullyQualifiedName~CleaningAssignmentNotification' --logger 'trx;LogFileName=integration.trx' --results-directory (Join-Path $taskRoot 'receipts')
    if ($LASTEXITCODE -ne 0) { throw 'Focused no-SQL integration tests failed.' }
    foreach ($taskFile in $taskCoreFiles) {
        $taskRelative = $taskFile.FullName.Substring($taskOverlay.Length + 1)
        if ((Get-FileHash $taskFile.FullName).Hash -ne (Get-FileHash (Join-Path $taskSource $taskRelative)).Hash) {
            throw ('DEV2 source changed: ' + $taskRelative)
        }
    }
    Write-Output ('PASS: disposable source/build/no-SQL receipts: ' + $taskRoot)
    Write-Output 'Expected incompatibility assertions passed; UC029 runtime delivery remains blocked. No SQL/HTTPS or production flags enabled.'
} finally { Pop-Location }
