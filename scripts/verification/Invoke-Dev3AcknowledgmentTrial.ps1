$runtime=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Invoke-Dev3NotificationsTrial.ps1')).Replace('checkpoint80','checkpoint81')
[IO.File]::WriteAllText((Join-Path (Get-Location) 'artifacts/checkpoint81-runtime.ps1'),$runtime,[Text.UTF8Encoding]::new($false))
$ErrorActionPreference='Stop'
$db='HotelOps_DEV3_142A_20261114_cc78c13f'
$installed=$false
try {
 sqlcmd -S localhost -d $db -E -C -b -i scripts/verification/CanonicalDatabase/NotificationAcknowledgmentIsolated.sql -o artifacts/checkpoint81-schema.txt
 if($LASTEXITCODE -ne 0){throw 'Schema install failed'}
 $installed=$true
 sqlcmd -S localhost -d $db -E -C -b -i scripts/verification/CanonicalDatabase/NotificationAcknowledgmentIsolated.sql -o artifacts/checkpoint81-schema-repeat.txt
 if($LASTEXITCODE -ne 0){throw 'Schema idempotency failed'}
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/checkpoint81-runtime.ps1
 if($LASTEXITCODE -ne 0){throw 'Runtime failed'}
}finally{
 if($installed){
  sqlcmd -S localhost -d $db -E -C -b -i scripts/verification/CanonicalDatabase/NotificationAcknowledgmentRollbackIsolated.sql -o artifacts/checkpoint81-schema-rollback.txt
  if($LASTEXITCODE -ne 0){throw 'Schema rollback failed'}
 }
}