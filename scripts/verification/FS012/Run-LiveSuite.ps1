[CmdletBinding()]
param([ValidateSet('enabled','default-off')][string]$Mode='enabled',[ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunId='run')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$certificate=Join-Path $PSScriptRoot '.private/localhost-public.pem'
if(!(Test-Path -LiteralPath $certificate)){throw 'Start owned HTTPS environment first.'}
$priorCa=$env:NODE_EXTRA_CA_CERTS
$priorFlag=$env:FS012_LIVE_ENABLED
$priorRun=$env:FS012_LIVE_RUN_ID
try {
 $env:NODE_EXTRA_CA_CERTS=$certificate
 $env:FS012_LIVE_ENABLED='APPROVED_FS012_LOCAL_TRIAL'
 $env:FS012_LIVE_RUN_ID=[guid]::NewGuid().ToString()
 Push-Location (Join-Path $root 'frontend')
 try {
  $configuration=if($Mode -eq 'enabled'){'playwright.fs012-live.config.ts'}else{'playwright.fs012-default-off.config.ts'}
  & npx.cmd playwright test --config $configuration
  $suiteExit=$LASTEXITCODE
 } finally { Pop-Location }
 $report=if($Mode -eq 'enabled'){'LIVE_RESULTS.json'}else{'DEFAULT_OFF_RESULTS.json'}
 if(Test-Path -LiteralPath (Join-Path $root "docs/fs012/$report")){
  Copy-Item -LiteralPath (Join-Path $root "docs/fs012/$report") -Destination (Join-Path $root "docs/fs012/${RunId}_${report}") -ErrorAction Stop
 }
 exit $suiteExit
} finally {
 $env:NODE_EXTRA_CA_CERTS=$priorCa
 $env:FS012_LIVE_ENABLED=$priorFlag
 $env:FS012_LIVE_RUN_ID=$priorRun
}
