param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root= [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot ('run-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Output directory must be new; existing evidence is never overwritten.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Push-Location $root
try {
    & dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --logger 'trx;LogFileName=task87.trx' --results-directory $OutputDirectory *> (Join-Path $OutputDirectory 'framework.log')
    $code=$LASTEXITCODE
    $trxPath=Join-Path $OutputDirectory 'task87.trx'
    if (!(Test-Path $trxPath)) { throw "No TRX produced; dotnet test exit=$code" }
    [xml]$trx=Get-Content -LiteralPath $trxPath
    $counts=$trx.TestRun.ResultSummary.Counters
    @{exitCode=$code;total=[int]$counts.total;passed=[int]$counts.passed;failed=[int]$counts.failed;skipped=[int]$counts.notExecuted;sqlExecuted=$false;httpServerExecuted=$false} | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'summary.json')
    if ($code -ne 0 -or [int]$counts.total -eq 0) { throw 'Framework tests failed or no cases discovered.' }
} finally { Pop-Location }
