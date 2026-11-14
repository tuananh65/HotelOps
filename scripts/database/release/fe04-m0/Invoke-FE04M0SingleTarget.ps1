# FASTTRACK-109A: SOURCE-WRITE APPROVED, runtime/provisioning NOT AUTHORIZED.
# InstallCore is fail-closed until the Project Leader's certificate fingerprint is source-pinned,
# the detached Gate B envelope is verified and every accepted artifact hash is populated.
[CmdletBinding()]
param(
    [ValidateSet('GuardCheck','Plan','Inspect','InstallCore','Verify')]
    [string]$Stage = 'Plan',
    [string]$ContractPath = '',
    [string]$GateBEnvelope = '',
    [string]$GateBSignature = '',
    [string]$GateBSignerCertificate = '',
    [string]$EvidencePath = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ContractPath)) {
    $ContractPath = Join-Path $PSScriptRoot 'FE04M0-Contract.json'
}
# This MUST be replaced in an explicitly reviewed source increment only after PL distributes
# their approved public certificate. Never accept a cert fingerprint from the contract itself.
$PinnedProjectLeaderCertificateSha256 = 'PENDING_PROJECT_LEADER_CERT_PIN'
$PinnedRepositoryCommit = '084d06596d5026a050c18bc47df19942114755de'
$PinnedSqlInstance = 'DESKTOP-JI8U50T\SQLEXPRESS'
$PinnedTarget = 'HotelOps_FE04_Trial_20261113_ENV001_U107_7e94a2bf'
$MarkerName = 'HotelOps.FE04.M0.Trial'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path

function Require([bool]$condition, [string]$failure) {
    if (-not $condition) { throw "FE04_M0_GUARD:$failure" }
}
function Sha256Bytes([byte[]]$data) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hash.ComputeHash($data))).Replace('-','') }
    finally { $hash.Dispose() }
}
function Sha256File([string]$path) { return Sha256Bytes ([IO.File]::ReadAllBytes($path)) }
function Load-Contract {
    Require (Test-Path -LiteralPath $ContractPath -PathType Leaf) 'CONTRACT_MISSING'
    return Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Validate-Contract($c) {
    Require ($c.contract_id -ceq 'FE04_M0_SINGLE_TARGET_V1') 'CONTRACT_ID'
    Require ($c.source_commit -ceq $PinnedRepositoryCommit) 'COMPOSITION_NOT_PINNED'
    Require ($c.sql.server -ceq $PinnedSqlInstance) 'SQL_INSTANCE'
    Require ($c.sql.database -ceq $PinnedTarget) 'SQL_TARGET'
    Require ($c.sql.database -cne 'HotelOpsDB') 'SHARED_DATABASE'
    Require ($c.sql.database -cmatch '^HotelOps_FE04_Trial_[0-9]{8}_ENV001_U107_[a-f0-9]{8}$') 'TARGET_PATTERN'
    Require ($c.sql.encrypt -is [bool] -and $c.sql.encrypt -eq $true -and
        $c.sql.trust_server_certificate -is [bool] -and $c.sql.trust_server_certificate -eq $false) 'TLS_BYPASS'
    Require ($c.sql.tls_hostname -ceq 'DESKTOP-JI8U50T') 'TLS_HOSTNAME'
    Require ($c.sql.server.StartsWith($c.sql.tls_hostname + '\',[StringComparison]::Ordinal)) 'SQL_TLS_HOST_MISMATCH'
    Require ($c.install_mode -ceq 'FRESH_ONE_DATABASE_ONLY') 'INSTALL_MODE'
    Require ($c.gate_b.provisioning_granted -ceq $false -and $c.gate_c.runtime_granted -ceq $false) 'SELF_APPROVAL_DISALLOWED'
    Require (@($c.schema_artifacts).Count -ge 8) 'SCHEMA_ARTIFACTS_MISSING'
    Require (@($c.schema_artifacts | Where-Object { $_.id -eq 'task26_forward' }).Count -eq 1) 'TASK26_REQUIRED'
    Require (@($c.schema_artifacts | Where-Object { $_.id -eq 'full_schema' }).Count -eq 1) 'FULL_SCHEMA_REQUIRED'
    Require (@($c.schema_artifacts | Select-Object -ExpandProperty id -Unique).Count -eq @($c.schema_artifacts).Count) 'DUPLICATE_ARTIFACT'
    foreach ($artifact in @($c.schema_artifacts)) {
        Require (([string]$artifact.sha256) -cmatch '^[a-fA-F0-9]{64}$') ('HASH_NOT_ACCEPTED_' + $artifact.id)
    }
    Require (@($c.schema_artifacts | Where-Object { $_.path -match '(?i)upgrade_auth|rollback' }).Count -eq 0) 'UPGRADE_OR_ROLLBACK_NOT_M0'
    Require (@($c.operation_allowlist) -join ',' -ceq 'B1,B2,B3') 'SCOPE_NOT_B1_B2_B3'
}
function Get-ArtifactPath($a) {
    $rel = [string]$a.path
    Require ($rel -cmatch '^(scripts/(database/release|verification/UserProfileUpdate)/|database/patches/)[a-zA-Z0-9_./-]+\.(sql)$') 'ARTIFACT_PATH'
    Require (-not $rel.Contains('..')) 'TRAVERSAL'
    $path = [IO.Path]::GetFullPath((Join-Path $root $rel))
    Require ($path.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'ARTIFACT_OUTSIDE_REPO'
    Require (Test-Path -LiteralPath $path -PathType Leaf) ('MISSING_ARTIFACT_' + $a.id)
    return $path
}
function Resolve-Artifacts($c, [bool]$enforcePin) {
    $observed = @()
    foreach ($a in @($c.schema_artifacts)) {
        $path = Get-ArtifactPath $a
        $actual = Sha256File $path
        if ($enforcePin) {
            Require (([string]$a.sha256) -cmatch '^[a-fA-F0-9]{64}$') ('HASH_NOT_ACCEPTED_' + $a.id)
            Require ($actual -ceq ([string]$a.sha256).ToUpperInvariant()) ('HASH_DRIFT_' + $a.id)
        }
        $observed += [pscustomobject]@{ id=$a.id; path=$a.path; actual_sha256=$actual; accepted_sha256=$a.sha256 }
    }
    return $observed
}
function Validate-Order($c) {
    $ids = @($c.schema_artifacts | ForEach-Object { [string]$_.id })
    $expected = @('full_schema','rbac_catalog','patch_inventory','patch_material_request',
        'patch_replenishment','patch_maintenance','canonical_reference','canonical_seed',
        'read_only_verify','canonical_verify_reference_only','task26_forward')
    Require (($ids -join ',') -ceq ($expected -join ',')) 'INSTALL_ORDER_NOT_ACCEPTED'
}
function Read-PinnedArtifactText($artifact) {
    # Use the *same* byte buffer for hashing and SQL execution: prevent source TOCTOU drift.
    $path = Get-ArtifactPath $artifact
    $bytes = [IO.File]::ReadAllBytes($path)
    Require ((Sha256Bytes $bytes) -ceq ([string]$artifact.sha256).ToUpperInvariant()) ('HASH_DRIFT_AT_USE_' + $artifact.id)
    $stream = [IO.MemoryStream]::new($bytes, $false)
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true)
    try { return $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
}
function Verify-M0MinimumSchema($connection) {
    # M0 read-scope metadata only. NOT a substitute for full canonical (82 tables/9 rowversions)
    # comparison or FE04 writer acceptance. Requires explicit signoff in the detached Gate B envelope.
    $expected = @('users','user_roles','user_sessions','user_branch_access','roles','role_permissions',
        'permissions','branches','departments','rooms','floors','reference_values',
        'cleaning_tasks','cleaning_assignments','cleaning_progress_logs',
        'cleaning_reworks','room_inspections','room_blocks','audit_logs')
    foreach ($table in $expected) {
        Require ((Scalar $connection "SELECT CASE WHEN OBJECT_ID(N'dbo.$table',N'U') IS NOT NULL THEN 1 ELSE 0 END") -eq 1) ('M0_TABLE_MISSING_' + $table)
    }
    Require ((Scalar $connection "SELECT COUNT(*) FROM dbo.roles") -eq 12) 'M0_ROLE_CATALOG_DRIFT'
    Require ((Scalar $connection "SELECT COUNT(*) FROM dbo.permissions") -eq 62) 'M0_PERMISSION_CATALOG_DRIFT'
    Require ((Scalar $connection "SELECT COUNT(*) FROM dbo.role_permissions") -eq 140) 'M0_RBAC_GRANTS_DRIFT'
    Require ((Scalar $connection "SELECT COUNT(*) FROM dbo.branches WHERE is_active=1") -eq 2) 'M0_BRANCH_CATALOG_DRIFT'
    Require ((Scalar $connection "SELECT COUNT(*) FROM dbo.departments WHERE is_active=1") -eq 8) 'M0_DEPARTMENT_CATALOG_DRIFT'
    Verify-Task26 $connection
}
function Strict-Connection([string]$database) {
    # System.Data.SqlClient performs normal TLS chain/hostname checks when TrustServerCertificate=False.
    # Never supply an alternate HostNameInCertificate or TrustServerCertificate override.
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $PinnedSqlInstance
    $builder['Initial Catalog'] = $database
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $false
    $builder['Connect Timeout'] = 10
    $builder['Application Name'] = 'HotelOps FE04 M0 exact isolated provisioner'
    $conn = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    $conn.Open() # Failed TLS/hostname verification aborts; never fallback.
    return $conn
}
function Scalar($connection,[string]$sql) {
    $command = $connection.CreateCommand(); $command.CommandTimeout = 15; $command.CommandText=$sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Run-Batches($connection,[string]$sql,[scriptblock]$afterBatch = $null) {
    foreach ($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')) {
        if (-not $batch.Trim()) { continue }
        $command=$connection.CreateCommand(); $command.CommandTimeout=300; $command.CommandText=$batch
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        if ($null -ne $afterBatch) { & $afterBatch $batch }
    }
}
function Check-Server($connection, [string]$expectedDb) {
    $server = Scalar $connection "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))"
    $db = Scalar $connection 'SELECT DB_NAME()'
    Require ($server -ceq $PinnedSqlInstance -and $db -ceq $expectedDb) 'INSTANCE_DATABASE_MISMATCH'
}
function Get-DatabaseGuid($master) {
    $guid = Scalar $master "SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID(N'$PinnedTarget')"
    Require ($null -ne $guid -and $guid -is [Guid] -and $guid -ne [Guid]::Empty) 'DATABASE_GUID_REQUIRED'
    return $guid
}
function Verify-Marker($target,[string]$marker) {
    Require ($marker -cmatch '^FE04_M0_[a-f0-9]{32}$') 'MARKER_FORMAT'
    $v = Scalar $target "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'$MarkerName'"
    Require ($v -ceq $marker) 'TRIAL_MARKER_MISMATCH'
}
function Require-NoCollision($databaseId) {
    Require ($null -eq $databaseId -or $databaseId -is [DBNull]) 'DATABASE_COLLISION'
}
function Require-OwnerSid([string]$actual,[string]$expected) {
    Require ($expected -cmatch '^(?:[A-F0-9]{2})+$' -and $actual -ceq $expected) 'DATABASE_OWNER_MISMATCH'
}
function Verify-Owner($master,[string]$expected) {
    $actual = Scalar $master "SELECT CONVERT(varchar(256),owner_sid,2) FROM sys.databases WHERE name=N'$PinnedTarget'"
    Require-OwnerSid ([string]$actual) $expected
}
function Verify-ApprovalWindow($approval) {
    $now = [DateTimeOffset]::UtcNow
    Require ([DateTimeOffset]::Parse($approval.starts_utc) -le $now -and
        $now -lt [DateTimeOffset]::Parse($approval.expires_utc)) 'APPROVAL_WINDOW_EXPIRED'
}
function Verify-Task26($target) {
    $ok = Scalar $target @'
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.users') AND name=N'row_version'
      AND system_type_id=189 AND max_length=8 AND is_nullable=0 AND is_computed=0)
  AND EXISTS(SELECT 1 FROM sys.extended_properties
    WHERE class=0 AND name=N'HotelOps.UserProfile.RowVersion'
      AND CONVERT(nvarchar(128),value)=N'Task26') THEN 1 ELSE 0 END
'@
    Require ($ok -eq 1) 'TASK26_POSTFLIGHT_REQUIRED'
}
function Adapt-FullSchema([string]$sql) {
    Require ($sql.Contains('CREATE DATABASE [HotelOpsDB]') -and $sql.Contains('USE [HotelOpsDB]') -and
        $sql.Contains("DB_ID(N'HotelOpsDB')")) 'FULL_SCHEMA_GUARD_MISSING'
    Require ([regex]::Matches($sql,'(?im)^CREATE DATABASE \[HotelOpsDB\];\s*$').Count -eq 1) 'SINGLE_CREATE_REQUIRED'
    Require ([regex]::Matches($sql,'SET NOEXEC ON;').Count -eq 2) 'FULL_SCHEMA_ABORT_GUARD_DRIFT'
    # Preserve both collision/context checks, but abort instead of silently setting NOEXEC.
    $out=$sql.Replace('HotelOpsDB',$PinnedTarget).Replace('SET NOEXEC ON;',
        "THROW 51999,'FE04 exact-target collision/context guard refused execution',1;")
    Require ($out.Contains("DB_ID(N'$PinnedTarget')") -and $out.Contains("CREATE DATABASE [$PinnedTarget]") -and
        $out.Contains("USE [$PinnedTarget]") -and -not $out.Contains('HotelOpsDB')) 'FULL_SCHEMA_ADAPTER_FAILED'
    return $out
}
function Adapt-AcceptedTargetGuard([string]$sql,[string]$id,[string]$marker) {
    # Never remove a target guard. Replace source-pinned canonical-only eligibility with
    # an exact FE04 database AND the newly generated ownership marker.
    if ($id -in @('rbac_catalog','patch_inventory','patch_material_request','patch_replenishment','patch_maintenance')) {
        Require ($sql.Contains('HotelOpsDB')) ('EXPECTED_TARGET_TOKEN_MISSING_' + $id)
        $out=$sql.Replace('HotelOpsDB',$PinnedTarget)
        Require (-not $out.Contains('HotelOpsDB') -and $out.Contains($PinnedTarget)) ('TARGET_SUBSTITUTION_FAILED_' + $id)
        return $out
    }
    if ($id -in @('canonical_reference','canonical_seed','canonical_verify')) {
        $prefix = "IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'"
        Require ($sql.Contains($prefix) -and $sql.IndexOf($prefix) -eq $sql.LastIndexOf($prefix)) ('CANONICAL_GUARD_DRIFT_' + $id)
        $newGuard = "IF DB_NAME() <> N'$PinnedTarget' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'$MarkerName' AND CONVERT(nvarchar(128),value)=N'$marker')"
        $out=$sql.Replace($prefix,$newGuard)
        Require (-not $out.Contains('HotelOps[_]Canonical[_]%') -and $out.Contains($newGuard)) ('CANONICAL_GUARD_NOT_PRESERVED_' + $id)
        return $out
    }
    Require ($id -eq 'read_only_verify') 'UNREVIEWED_SCHEMA_PHASE'
    return $sql
}
function Adapt-Task26([string]$sql,[string]$marker) {
    # Exact, reviewed Task26 V1 guard substitution; preserve SQL's rowversion logic and transaction.
    $guard = "IF DB_NAME()=N'HotelOpsDB'`n   OR DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh'`n   OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0`n                 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')`n    THROW 51970,'Independent verified personnel trial required.',1;"
    $canonical = $sql.Replace("`r`n","`n")
    Require ($canonical.Contains($guard) -and $canonical.IndexOf($guard) -eq $canonical.LastIndexOf($guard)) 'TASK26_GUARD_DRIFT'
    $newGuard = "IF DB_NAME()<>N'$PinnedTarget'`n   OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0`n                 AND name=N'$MarkerName' AND CONVERT(nvarchar(128),value)=N'$marker')`n    THROW 51970,'Independent owner-bound FE04 trial required.',1;"
    $updated = $canonical.Replace($guard,$newGuard)
    Require ($updated.Contains("DB_NAME()<>N'$PinnedTarget'") -and -not $updated.Contains('HotelOps[_]Canonical[_]')) 'TASK26_ADAPTER_FAILED'
    return $updated
}
function Verify-GateB($c, $observed) {
    Require ($PinnedProjectLeaderCertificateSha256 -cmatch '^[A-F0-9]{64}$') 'PL_TRUST_ANCHOR_NOT_SOURCE_APPROVED'
    Require ((Test-Path $GateBEnvelope -PathType Leaf) -and (Test-Path $GateBSignature -PathType Leaf) -and
        (Test-Path $GateBSignerCertificate -PathType Leaf)) 'SIGNED_GATE_B_REQUIRED'
    $cert=[Security.Cryptography.X509Certificates.X509Certificate2]::new($GateBSignerCertificate)
    $certSha=Sha256Bytes $cert.RawData
    Require ($certSha -ceq $PinnedProjectLeaderCertificateSha256) 'PL_SIGNER_PIN_MISMATCH'
    $now=[DateTimeOffset]::UtcNow
    Require ($cert.NotBefore.ToUniversalTime() -le $now.UtcDateTime -and
        $now.UtcDateTime -lt $cert.NotAfter.ToUniversalTime()) 'PL_SIGNER_CERT_EXPIRED'
    $payload=[IO.File]::ReadAllBytes($GateBEnvelope)
    $sig=[Convert]::FromBase64String(([IO.File]::ReadAllText($GateBSignature)).Trim())
    $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
    Require ($null -ne $rsa) 'PL_SIGNER_RSA_REQUIRED'
    try { Require ($rsa.VerifyData($payload,$sig,[Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)) 'PL_SIGNATURE_INVALID' }
    finally { $rsa.Dispose(); $cert.Dispose() }
    $approval=[Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    $sourceFile = Join-Path $PSScriptRoot 'Invoke-FE04M0SingleTarget.ps1'
    Require ($approval.project_leader_reference -is [string] -and $approval.project_leader_reference.Trim().Length -ge 8) 'PL_REFERENCE_REQUIRED'
    Require ($approval.target -ceq $PinnedTarget -and $approval.instance -ceq $PinnedSqlInstance) 'APPROVAL_TARGET_MISMATCH'
    Require ($approval.source_commit -ceq $PinnedRepositoryCommit) 'APPROVAL_SOURCE_COMPOSITION'
    Require ($approval.installer_sha256 -ceq (Sha256File $sourceFile)) 'APPROVAL_INSTALLER_SHA'
    Require ($approval.contract_sha256 -ceq (Sha256File $ContractPath)) 'APPROVAL_CONTRACT_SHA'
    Require ($approval.schema_fingerprints_sha256 -ceq (Sha256Bytes ([Text.Encoding]::UTF8.GetBytes(($observed | ConvertTo-Json -Compress -Depth 8))))) 'APPROVAL_SCHEMA_SHA'
    Require (([string]$c.accepted_fe04_composition) -cmatch '^[a-f0-9]{40}$') 'FE04_COMPOSITION_OWNER_PENDING'
    Require ($approval.accepted_fe04_composition -ceq $c.accepted_fe04_composition) 'FE04_COMPOSITION_SIGNOFF_MISMATCH'
    Require ($approval.fe04_owner_signoff_reference -is [string] -and $approval.fe04_owner_signoff_reference.Trim().Length -ge 8) 'FE04_OWNER_SIGNOFF_REQUIRED'
    Require ($approval.env_source_signoff_reference -is [string] -and $approval.env_source_signoff_reference.Trim().Length -ge 8) 'ENV_SOURCE_SIGNOFF_REQUIRED'
    Require ($approval.task26_fe04_guard_adapter_accepted -ceq $true) 'TASK26_ADAPTER_REVIEW_REQUIRED'
    Require ($approval.canonical_fe04_guard_adapter_accepted -ceq $true) 'CANONICAL_ADAPTER_REVIEW_REQUIRED'
    Require ($approval.full_schema_abort_adapter_accepted -ceq $true) 'FULL_SCHEMA_ABORT_ADAPTER_REVIEW_REQUIRED'
    Require ($approval.m0_minimum_schema_verifier_accepted -ceq $true) 'M0_VERIFIER_SCOPE_REVIEW_REQUIRED'
    Require ((@($approval.operations) -join ',') -ceq 'B2,B3') 'APPROVAL_OPERATION_SCOPE'
    Require (([string]$approval.database_owner_sid_hex) -cmatch '^(?:[A-F0-9]{2})+$') 'APPROVED_OWNER_SID_REQUIRED'
    Require ($approval.setup_operator -is [string] -and $approval.setup_operator.Length -gt 0) 'APPROVED_OPERATOR_REQUIRED'
    Verify-ApprovalWindow $approval
    return $approval
}
function New-Evidence([hashtable]$value) {
    Require (-not [string]::IsNullOrWhiteSpace($EvidencePath)) 'EVIDENCE_PATH_REQUIRED'
    Require (-not (Test-Path -LiteralPath $EvidencePath)) 'EVIDENCE_OVERWRITE_FORBIDDEN'
    $parent=[IO.Path]::GetFullPath((Split-Path -Parent $EvidencePath))
    Require (Test-Path -LiteralPath $parent -PathType Container) 'PROTECTED_EVIDENCE_DIRECTORY_REQUIRED'
    # ACL is an operator-side Gate B prerequisite; do not imply this file creation proves ACL.
    $json=($value | ConvertTo-Json -Depth 20)
    $stream=[IO.File]::Open($EvidencePath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes($json);$stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
}

# Dot-sourcing imports definitions only. In particular it never dispatches SQL stages.
if ($MyInvocation.InvocationName -eq '.') { return }
$c=Load-Contract
Validate-Contract $c
Validate-Order $c
if ($Stage -eq 'GuardCheck') {
    $null = Resolve-Artifacts $c $true
    Write-Output 'FE04_M0_GUARD_CHECK_PASS: contract, target and all artifact hashes; NO SQL CONNECTION'
    return
}
$observed=Resolve-Artifacts $c ($Stage -ne 'Plan')
if ($Stage -eq 'Plan') {
    [pscustomobject]@{ stage='PLAN_ONLY'; source_commit=$PinnedRepositoryCommit; instance=$PinnedSqlInstance;
        database=$PinnedTarget; artifact_sha256=$observed; gate_b='NOT_GRANTED';gate_c='NOT_GRANTED';
        task26_adapter='REQUIRES_SEPARATE_SOURCE_OWNER_APPROVAL'; sql_connections=0; mutations=0 } | ConvertTo-Json -Depth 12
    return
}
# No unapproved SQL connection on a requested mutation stage: validate detached Gate B
# before opening even a read-only metadata session. Real provisioning remains externally gated.
if ($Stage -eq 'InstallCore') {
    $approved = Verify-GateB $c $observed
    Require (-not [string]::IsNullOrWhiteSpace($EvidencePath)) 'RECEIPT_REQUIRED'
    Require (-not (Test-Path -LiteralPath $EvidencePath)) 'RECEIPT_COLLISION'
}
# Strict SQL TLS is mandatory for ALL SQL stages, including read-only Inspect/Verify.
$master=$null;$target=$null;$sessionLock=$false
try {
    $master=Strict-Connection 'master'
    Check-Server $master 'master'
    $existing=Scalar $master "SELECT DB_ID(N'$PinnedTarget')"
    if ($Stage -eq 'Inspect') {
        [pscustomobject]@{ stage='INSPECT_READ_ONLY'; server=$PinnedSqlInstance; database=$PinnedTarget;
            collision=($null -ne $existing -and $existing -isnot [DBNull]); tls='STRICT_HANDSHAKE_SUCCEEDED';
            operation='NO_DDL_DML'; approval='NOT_GRANTED' } | ConvertTo-Json
        return
    }
    if ($Stage -eq 'Verify') {
        Require ($null -ne $existing -and $existing -isnot [DBNull]) 'TRIAL_MISSING'
        $target=Strict-Connection $PinnedTarget
        Check-Server $target $PinnedTarget
        $marker=[string]$c.post_provision.marker
        Verify-Marker $target $marker
        Verify-Task26 $target
        $guid=Get-DatabaseGuid $master
        Require ($guid.ToString() -ceq ([string]$c.post_provision.database_guid)) 'GUID_RECONCILIATION_REQUIRED'
        Verify-Owner $master ([string]$c.post_provision.owner_sid_hex)
        Verify-M0MinimumSchema $target
        [pscustomobject]@{stage='VERIFY_READ_ONLY';instance=$PinnedSqlInstance;database=$PinnedTarget;
            database_guid=$guid; marker_verified=$true; task26_verified=$true; tls='STRICT_HANDSHAKE_SUCCEEDED'} | ConvertTo-Json
        return
    }
    Require ($Stage -eq 'InstallCore') 'UNKNOWN_STAGE'
    Require-NoCollision $existing
    Verify-ApprovalWindow $approved
    Require ((Scalar $master 'SELECT ORIGINAL_LOGIN()') -ceq $approved.setup_operator) 'SETUP_OPERATOR_MISMATCH'
    Require-OwnerSid ([string](Scalar $master 'SELECT CONVERT(varchar(256),SUSER_SID(),2)')) ([string]$approved.database_owner_sid_hex)
    # No source, contract or external reviewer can authorize itself. The signed envelope and
    # source-pinned public certificate must be independently approved and distributed.
    # Master-scoped app lock serializes this exact target across cooperatively authorized installers.
    $result=Scalar $master "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:FE04:M0:U108',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r"
    Require ($result -is [int] -and $result -ge 0) 'INSTALLER_LOCK_UNAVAILABLE'
    $sessionLock=$true
    Require-NoCollision (Scalar $master "SELECT DB_ID(N'$PinnedTarget')")
    Require ((Test-Path -LiteralPath (Split-Path -Parent $EvidencePath) -PathType Container)) 'EVIDENCE_DIRECTORY_REQUIRED'
    $outcome=@{status='BEGIN';database=$PinnedTarget;instance=$PinnedSqlInstance;source_commit=$PinnedRepositoryCommit;
        schema_sha256=$observed;started_utc=[DateTimeOffset]::UtcNow.ToString('O');stages=@();guid=$null;marker=$null}
    try {
        $a=@($c.schema_artifacts)
        $full=Adapt-FullSchema (Read-PinnedArtifactText $a[0])
        Run-Batches $master $full {
            param($batch)
            if ($batch.Trim() -ceq "CREATE DATABASE [$PinnedTarget];") {
                # Persist the created identity in recovery metadata before subsequent schema batches.
                $createdGuid=Get-DatabaseGuid $master
                $outcome.guid=$createdGuid.ToString()
                Verify-Owner $master ([string]$approved.database_owner_sid_hex)
                $outcome.owner_sid_hex=$approved.database_owner_sid_hex
                $outcome.stages+= 'B2_SINGLE_DATABASE_CREATED'
            }
            Verify-ApprovalWindow $approved
        } # The only CREATE DATABASE comes from the exact pinned Full Schema.
        # Full Schema switches SQL session context to the new trial via USE [target].
        # Restore master before GUID checks/releasing the master-scoped session application lock.
        Run-Batches $master 'USE [master];'
        Check-Server $master 'master'
        Require ($null -ne $outcome.guid) 'CREATE_IDENTITY_NOT_OBSERVED'
        $guid=Get-DatabaseGuid $master
        Require ($guid.ToString() -ceq $outcome.guid) 'GUID_CHANGED'
        Verify-Owner $master ([string]$approved.database_owner_sid_hex)
        $outcome.owner_sid_hex=$approved.database_owner_sid_hex
        $target=Strict-Connection $PinnedTarget
        Check-Server $target $PinnedTarget
        $marker='FE04_M0_'+[Guid]::NewGuid().ToString('N')
        # Marker is generated only on an actually-created disposable target; never pre-fabricated.
        $cmd=$target.CreateCommand()
        try {
            $cmd.CommandText="EXEC sys.sp_addextendedproperty @name=N'$MarkerName',@value=@marker;"
            [void]$cmd.Parameters.Add('@marker',[System.Data.SqlDbType]::NVarChar,128)
            $cmd.Parameters['@marker'].Value=$marker
            [void]$cmd.ExecuteNonQuery()
        } finally { $cmd.Dispose() }
        Verify-Marker $target $marker
        $outcome.marker=$marker
        for ($i=1;$i -lt $a.Length;$i++) {
            Verify-ApprovalWindow $approved
            Check-Server $target $PinnedTarget
            Verify-Marker $target $marker
            Verify-Owner $master ([string]$approved.database_owner_sid_hex)
            Require ((Get-DatabaseGuid $master) -eq $guid) 'GUID_CHANGED'
            # Canonical_Verify contains full-trial fixed table/rowversion counts. Executing it
            # here before installing UC017/Checklist/Housekeeping would be a false equivalence.
            # Its exact source bytes are pinned for provenance, but M0 uses the review-gated
            # read-only minimal compatibility oracle instead.
            if ($a[$i].id -eq 'canonical_verify_reference_only') {
                $outcome.stages+= 'FULL_CANONICAL_VERIFY_NOT_APPLICABLE_TO_M0'
                continue
            }
            $sql=Read-PinnedArtifactText $a[$i]
            if ($a[$i].id -eq 'task26_forward') { $sql=Adapt-Task26 $sql $marker }
            else { $sql=Adapt-AcceptedTargetGuard $sql ([string]$a[$i].id) $marker }
            Run-Batches $target $sql
            $outcome.stages+= $a[$i].id
        }
        Verify-M0MinimumSchema $target
        Verify-Marker $target $marker
        Verify-Owner $master ([string]$approved.database_owner_sid_hex)
        Require ((Get-DatabaseGuid $master) -eq $guid) 'GUID_CHANGED'
        $outcome.stages+= 'M0_MINIMUM_SCHEMA_READONLY_VERIFIED'
        $outcome.guid=$guid.ToString();$outcome.marker=$marker;$outcome.status='INSTALLED_B2_B3_ONLY'
    } catch {
        $outcome.status='UNKNOWN_OR_PARTIAL_REQUIRES_OWNER_RECONCILIATION'
        $outcome.failure_code='STOP_NO_RETRY_NO_AUTO_DROP'
        throw
    } finally {
        $outcome.completed_utc=[DateTimeOffset]::UtcNow.ToString('O')
        New-Evidence $outcome
    }
} finally {
    if ($null -ne $target) {$target.Dispose()}
    if ($null -ne $master) {
        if ($sessionLock) {
            try {
                # A failed Full Schema batch may have left this session in the target database.
                Run-Batches $master 'USE [master];'
                $released=Scalar $master "DECLARE @r int; EXEC @r=sys.sp_releaseapplock @Resource=N'HotelOps:FE04:M0:U108',@LockOwner='Session'; SELECT @r;"
                Require ($released -is [int] -and $released -ge 0) 'INSTALLER_LOCK_RELEASE_UNCONFIRMED'
            } catch {
                # Do not return a session with an unconfirmed lock to the connection pool.
                [System.Data.SqlClient.SqlConnection]::ClearPool($master)
                Write-Warning 'INSTALLER_LOCK_RELEASE_UNCONFIRMED_POOL_CLEARED_OWNER_RECONCILIATION_REQUIRED'
            }
        }
        $master.Dispose()
    }
}
