param([string]$Server='.\SQLEXPRESS',[Parameter(Mandatory=$true)][ValidatePattern('^HotelOpsCompactApiTest[a-f0-9]{12}$')][string]$Database)
$ErrorActionPreference='Stop'
$saved=$env:HOTELOPS_COMPACT_WORKER_TEST
try {
    $resultPath='artifacts/compact-v2/backend/worker-verification.json'
    if(Test-Path -LiteralPath $resultPath){Remove-Item -LiteralPath $resultPath}
    $env:HOTELOPS_COMPACT_WORKER_TEST="Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    dotnet test tests/HotelOps.Compact.Tests/HotelOps.Compact.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~WorkerIntegrationTests --logger 'trx;LogFileName=worker.trx' --results-directory artifacts/compact-v2/backend
    if($LASTEXITCODE-ne 0){throw 'Worker integration test failed'}
    [xml]$trx=Get-Content artifacts/compact-v2/backend/worker.trx -Raw
    if([int]$trx.TestRun.ResultSummary.Counters.passed-ne 1-or [int]$trx.TestRun.ResultSummary.Counters.executed-ne 1){throw 'Expected exactly one executed, passing worker test. Build the test project first.'}
    @{database=$Database;passed=1;failed=0;checks=@('due schedule generates one occurrence','schedule advances and replay does not duplicate','low-stock episode alert persists and deduplicates');verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5|Set-Content artifacts/compact-v2/backend/worker-verification.json -Encoding UTF8
}finally{$env:HOTELOPS_COMPACT_WORKER_TEST=$saved}
