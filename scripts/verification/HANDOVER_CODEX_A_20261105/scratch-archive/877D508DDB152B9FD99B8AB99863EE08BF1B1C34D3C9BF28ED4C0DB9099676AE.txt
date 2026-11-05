# Portable test-state verification only; no main database configuration or deployment state.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Integration-Environment.ps1')
$test=Open-IntegrationDb 'HotelOps_Authorization_Test'
try {
    $receipt=Get-Content -Raw (Join-Path $PSScriptRoot 'environment.json')|ConvertFrom-Json
    Assert-IntegrationIdentity $test $receipt.identity
    $counts=(Query $test 'SELECT (SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) mappings,(SELECT COUNT(*) FROM dbo.user_sessions WHERE revoked_at IS NULL) unrevoked_sessions').Tables[0].Rows[0]
    if($counts.roles -ne 12 -or $counts.permissions -ne 62 -or $counts.mappings -ne 140){throw 'Test catalog changed'}
    Save-Json ([ordered]@{identity=(Identity $test);roles=[int]$counts.roles;permissions=[int]$counts.permissions;mappings=[int]$counts.mappings;unrevokedSessions=[int]$counts.unrevoked_sessions;checkedUtc=[DateTime]::UtcNow.ToString('o')}) (Join-Path $PSScriptRoot 'test-final-state.json')
    Write-Output 'PASS: isolated integration state checked via SELECT only; no main database access'
}finally{$test.Dispose()}
