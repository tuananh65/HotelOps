# FASTTRACK-109A offline guard tests: no SQL/HTTPS calls. Run from repository clone.
[CmdletBinding()]
param([string]$RepoRoot = '')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
}
$installer=Join-Path $RepoRoot 'scripts/database/release/fe04-m0/Invoke-FE04M0SingleTarget.ps1'
$contract=Join-Path $RepoRoot 'scripts/database/release/fe04-m0/FE04M0-Contract.json'
if (-not (Test-Path $installer) -or -not (Test-Path $contract)) { throw 'Installer source is missing' }
$base=Get-Content $contract -Raw -Encoding UTF8 | ConvertFrom-Json
$temp=Join-Path ([IO.Path]::GetTempPath()) ('FE04M0_GUARDS_'+[Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $temp)
$pass=0
function Case([string]$label,[scriptblock]$change,[bool]$shouldPass) {
    $current=($base | ConvertTo-Json -Depth 40 | ConvertFrom-Json)
    if ($null -ne $change) { & $change $current }
    $file=Join-Path $temp ($label+'.json')
    $current | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $file -Encoding UTF8
    # Current PowerShell executes a fresh isolated child process. GuardCheck never connects to SQL.
    $pwsh=(Get-Process -Id $PID).Path
    $savedErrorPreference=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue' # PS5 represents expected child stderr as ErrorRecords.
        $output=& $pwsh -NoProfile -File $installer -Stage GuardCheck -ContractPath $file 2>&1
        $childExit=$LASTEXITCODE
    } finally { $ErrorActionPreference=$savedErrorPreference }
    $success=($childExit -eq 0 -and "$output" -match 'FE04_M0_GUARD_CHECK_PASS')
    if ($success -ne $shouldPass) { throw "GUARD_TEST_FAILED:$label exit=$childExit output=$output" }
    $script:pass++
}
try {
    Case 'valid_initial_shape' $null $true
    Case 'shared_database' {param($c) $c.sql.database='HotelOpsDB'} $false
    Case 'wrong_exact_target' {param($c) $c.sql.database+='-other'} $false
    Case 'wrong_instance' {param($c) $c.sql.server='OTHER\SQLEXPRESS'} $false
    Case 'trust_cert_bypass' {param($c) $c.sql.trust_server_certificate=$true} $false
    Case 'encrypt_disabled' {param($c) $c.sql.encrypt=$false} $false
    Case 'wrong_tls_hostname' {param($c) $c.sql.tls_hostname='wrong.invalid'} $false
    Case 'wrong_source_commit' {param($c) $c.source_commit='deadbeef'} $false
    Case 'three_db_install' {param($c) $c.install_mode='CANONICAL_THREE_DATABASES'} $false
    Case 'missing_task26' {param($c) $c.schema_artifacts=@($c.schema_artifacts | Where-Object {$_.id -ne 'task26_forward'})} $false
    Case 'schema_order_drift' {param($c) $x=$c.schema_artifacts[0];$c.schema_artifacts[0]=$c.schema_artifacts[1];$c.schema_artifacts[1]=$x} $false
    Case 'self_approved_gate_b' {param($c) $c.gate_b.provisioning_granted=$true} $false
    Case 'self_approved_gate_c' {param($c) $c.gate_c.runtime_granted=$true} $false
    Case 'scope_with_uc028' {param($c) $c.operation_allowlist=@('B1','B2','B3','UC028_CREATE')} $false
    Case 'approval_missing_marker' {param($c) $c.contract_id='OTHER'} $false
    Write-Output "FE04_M0_BUNDLE_GUARDS:$pass/15 PASS; SQL/HTTPS/DDL/DML NOT_RUN"
    Case 'missing_hash' {param($c) $c.schema_artifacts[0].sha256=$null} $false
    Case 'unapproved_hash' {param($c) $c.schema_artifacts[0].sha256=('0'*64)} $false
    Case 'tls_string_boolean' {param($c) $c.sql.trust_server_certificate='False'} $false
    Case 'missing_prerequisite_file' {param($c) $c.schema_artifacts[0].path='scripts/database/release/missing-fe04-prerequisite.sql'} $false
    # Import only definitions, then exercise the same pure guards used by InstallCore.
    . $installer
    function Reject([string]$label,[string]$code,[scriptblock]$operation) {
        $rejected=$false
        try { & $operation } catch {
            if ($_.Exception.Message -notlike "*FE04_M0_GUARD:$code*") { throw }
            $rejected=$true
        }
        if (-not $rejected) { throw "GUARD_TEST_FAILED:$label was accepted" }
        $script:pass++
    }
    Reject 'collision' 'DATABASE_COLLISION' { Require-NoCollision 42 }
    Reject 'owner_mismatch' 'DATABASE_OWNER_MISMATCH' { Require-OwnerSid '01' '02' }
    Reject 'missing_detached_approval' 'PL_TRUST_ANCHOR_NOT_SOURCE_APPROVED' { Verify-GateB $base @() }
    Reject 'task26_guard_drift' 'TASK26_GUARD_DRIFT' { Adapt-Task26 'ALTER TABLE dbo.users ADD fake int;' ('FE04_M0_'+('0'*32)) }
    Reject 'canonical_guard_drift' 'CANONICAL_GUARD_DRIFT' { Adapt-AcceptedTargetGuard 'SELECT 1' 'canonical_seed' ('FE04_M0_'+('0'*32)) }
    Reject 'expired_approval' 'APPROVAL_WINDOW_EXPIRED' { Verify-ApprovalWindow ([pscustomobject]@{starts_utc='2020-01-01T00:00:00Z';expires_utc='2020-01-02T00:00:00Z'}) }
    # Scalar is replaced by a metadata-only test stub; no connection object exists.
    function Scalar($connection,[string]$sql) { return 'WRONG_MARKER' }
    Reject 'wrong_marker' 'TRIAL_MARKER_MISMATCH' { Verify-Marker $null ('FE04_M0_'+('0'*32)) }
    function Scalar($connection,[string]$sql) { return 'WRONG_INSTANCE' }
    Reject 'server_mismatch' 'INSTANCE_DATABASE_MISMATCH' { Check-Server $null 'master' }
    function Scalar($connection,[string]$sql) { return [Guid]::Empty }
    Reject 'empty_guid' 'DATABASE_GUID_REQUIRED' { Get-DatabaseGuid $null }
    $marker='FE04_M0_'+('0'*32)
    $task26=Read-PinnedArtifactText (@($base.schema_artifacts|Where-Object id -eq 'task26_forward')[0])
    $adapted=Adapt-Task26 $task26 $marker
    $tail="IF OBJECT_ID(N'dbo.users',N'U') IS NULL"
    $original=$task26.Replace("`r`n","`n")
    if ($original.Substring($original.IndexOf($tail)) -cne $adapted.Substring($adapted.IndexOf($tail))) {
        throw 'Task26 schema semantics were modified'
    }
    $script:pass++
    $full=Adapt-FullSchema (Read-PinnedArtifactText $base.schema_artifacts[0])
    if ([regex]::Matches($full,'(?im)^CREATE DATABASE ').Count -ne 1 -or $full.Contains('SET NOEXEC ON;') -or
        -not $full.Contains("CREATE DATABASE [$PinnedTarget]")) { throw 'Full schema adapter lost single-target abort guards' }
    $script:pass++
    $reference=Read-PinnedArtifactText (@($base.schema_artifacts|Where-Object id -eq 'canonical_reference')[0])
    $reference=Adapt-AcceptedTargetGuard $reference 'canonical_reference' $marker
    if (-not $reference.Contains($PinnedTarget) -or -not $reference.Contains($marker) -or
        $reference.Contains("IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'") ) { throw 'Canonical guard adapter lost scope' }
    $script:pass++
    # These are pure function stubs: Verify-M0MinimumSchema must reject missing
    # tables independently of live SQL, certificate, or provisioning state.
    function Scalar($connection,[string]$sql) {
        if ($sql -match 'dbo\.cleaning_reworks') { return 0 }
        return 1
    }
    Reject 'm0_missing_cleaning_reworks' 'M0_TABLE_MISSING_cleaning_reworks' { Verify-M0MinimumSchema $null }
    function Scalar($connection,[string]$sql) {
        if ($sql -match 'dbo\.room_inspections') { return 0 }
        return 1
    }
    Reject 'm0_missing_room_inspections' 'M0_TABLE_MISSING_room_inspections' { Verify-M0MinimumSchema $null }
    function Scalar($connection,[string]$sql) {
        if ($sql -match 'FROM dbo\.roles') { return 12 }
        if ($sql -match 'FROM dbo\.permissions') { return 62 }
        if ($sql -match 'FROM dbo\.role_permissions') { return 140 }
        if ($sql -match 'FROM dbo\.branches') { return 2 }
        if ($sql -match 'FROM dbo\.departments') { return 8 }
        return 1 # All table metadata and Task26 condition are mocked present.
    }
    Verify-M0MinimumSchema $null
    $script:pass++
    Write-Output "FE04_M0_OFFLINE_GUARDS:$pass/34 PASS; SQL/HTTPS/DDL/DML NOT_RUN"
} finally {
    $resolvedTemp=[IO.Path]::GetFullPath($temp)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedTemp.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTemp) -notmatch '^FE04M0_GUARDS_[a-f0-9]{32}$') {
        throw 'Unsafe guard-test temp cleanup target'
    }
    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
}
