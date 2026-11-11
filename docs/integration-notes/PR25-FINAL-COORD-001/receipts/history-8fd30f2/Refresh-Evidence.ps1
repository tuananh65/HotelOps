# Read-only Git inspection; writes only the authorized evidence directory.
$ErrorActionPreference = 'Stop'
$root = 'docs/integration-notes/PR25-FINAL-COORD-001'
$receiptRoot = "$root/receipts"
$identity = Get-Content -Raw -LiteralPath "$receiptRoot/02_source_identity.json" | ConvertFrom-Json
if ((git rev-parse HEAD) -ne $identity.featureHead -or (git rev-parse MERGE_HEAD) -ne $identity.integrationParent) { throw 'Parent identity drift' }
if ($identity.featureHead -ne '2140522457e89b974cf2d65c64515ec15d733fa9' -or $identity.integrationParent -ne '8fd30f2f12e9dae102de2826e0be20e99e6a14ad' -or $identity.postSyncSourceTree -ne '14f4595c0edeb1f6349d636ec1a08a22a287f569') { throw 'Source identity drift' }
git diff --quiet
$workingExit = $LASTEXITCODE
git diff --cached --check
$stagedWhitespaceExit = $LASTEXITCODE
git diff --check
$workingWhitespaceExit = $LASTEXITCODE
$functionalPaths = @(git diff --cached --name-only MERGE_HEAD)
$integrationPaths = @(git diff --cached --name-only HEAD)
git diff --cached --quiet HEAD -- @functionalPaths
$originalUnchangedExit = $LASTEXITCODE
if ($functionalPaths.Count -ne 24 -or $workingExit -ne 0 -or $originalUnchangedExit -ne 0) { throw 'Unexpected source delta' }

$runCounts = [ordered]@{ '03_dev2_nosql'=92; '04_boundary'=67; 'UserAccountDeactivation'=12; 'UserRoleRevocation'=54; 'RoomLifecycleCandidate'=29; 'DepartmentDeactivationCandidate'=25; 'StructureDeactivationPreview'=27; 'WorkdayReadRouting'=44; 'DepartmentChange'=45; 'ShiftSchedulePlanning'=24; 'ShiftPlanPreview'=57; 'ScheduledWorkday'=43; 'DiComposition_verified'=16 }
$runs = @()
foreach ($name in $runCounts.Keys) {
    $meta = Get-Content -Raw -LiteralPath "$receiptRoot/$name.command.json" | ConvertFrom-Json
    $output = Get-Content -Raw -LiteralPath $meta.output
    $count = $runCounts[$name]
    if ($meta.exitCode -ne 0) { throw "Failed final command: $name" }
    if ($name -eq '03_dev2_nosql') { $payload = ($output -split "`n" | Where-Object { $_.Trim().StartsWith('{"pass":') } | Select-Object -Last 1) | ConvertFrom-Json; if (!$payload.pass -or $payload.tests -ne $count -or $payload.sqlConnections -ne 0) { throw 'NoSql summary mismatch' } }
    elseif ($name -eq '04_boundary') { if ($output -notmatch 'Failed:\s+0, Passed:\s+67, Skipped:\s+0, Total:\s+67') { throw 'Boundary discovery mismatch' } }
    elseif ($output -notmatch "(?m)(?:^PASS: $count independent checks|^$count/$count (?:focused )?checks PASS|^RESULT:?\s+$count(?:/$count)? PASS)") { throw "Summary mismatch: $name" }
    $runs += [ordered]@{ suite=$name; command=$meta.command; workingDirectory=$meta.workingDirectory; sourceTree=$identity.postSyncSourceTree; exitCode=$meta.exitCode; pass=$count; fail=0; skip=0; sqlExecution='NOT_RUN'; output=$meta.output; outputSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $meta.output).Hash.ToLowerInvariant() }
}
$failures = @('DiComposition_first_failure','DiComposition_environment_failure','DiComposition_eventlog_failure') | ForEach-Object { $meta=Get-Content -Raw -LiteralPath "$receiptRoot/$_.command.json" | ConvertFrom-Json; [ordered]@{attempt=$_;command=$meta.command;exitCode=$meta.exitCode;exitCodeScope=$meta.exitCodeScope;output=$meta.output;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $meta.output).Hash.ToLowerInvariant();sqlExecution='NOT_RUN'} }
[ordered]@{ source=$identity; build=(Get-Content -Raw "$receiptRoot/02_build.command.json" | ConvertFrom-Json); successfulRuns=$runs; requiredPass=350; additionalExistingPass=169; diPass=16; totalPass=535; finalFail=0; finalSkip=0; priorProbeFailures=$failures; solutionTest='exit 0, zero discovered tests, excluded from PASS count'; warningLimitations=@('NU1900 vulnerability metadata unavailable','3 existing EF20602 RoomTypeBranchV1 index warnings'); sqlAcceptance19='NOT_RUN' } | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/verification_summary.json"

$secretRules = [ordered]@{
    PrivateKey='-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    GitHubToken='(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})'
    AwsAccessKey='AKIA[A-Z0-9]{16}'
    SlackToken='xox[baprs]-[A-Za-z0-9-]{20,}'
    SqlCredentialValue='(?i)(?:password|pwd)\s*=\s*["'']?[^;\s"''<>]+'
}
$findings = @()
foreach ($base in @('HEAD','MERGE_HEAD')) {
    $file = ''
    foreach ($line in @(git diff --cached --unified=0 $base)) {
        if ($line -match '^\+\+\+ b/(.+)$') { $file=$Matches[1]; continue }
        if ($line.StartsWith('+') -and !$line.StartsWith('+++')) { foreach ($rule in $secretRules.Keys) { if ($line -match $secretRules[$rule]) { $findings += [ordered]@{scope="index vs $base";path=$file;rule=$rule} } } }
    }
}
$evidencePaths = @(git ls-files --others --exclude-standard -- $root)
$trailing = @()
foreach ($path in $evidencePaths) {
    if ($path -eq "$root/05_EXACT_DIFF_AND_HASHES.json" -or $path.Contains('/fallback-history/')) { continue }
    $text = Get-Content -Raw -LiteralPath $path
    foreach ($rule in $secretRules.Keys) { if ($text -match $secretRules[$rule]) { $findings += [ordered]@{scope='evidence';path=$path;rule=$rule} } }
    if ($path -match '\.(md|cs|csproj|ps1)$') { if ($text -match '(?m)[ \t]+\r?$') { $trailing += $path } }
}
[ordered]@{sourceTree=$identity.postSyncSourceTree;stagedWhitespaceExit=$stagedWhitespaceExit;workingWhitespaceExit=$workingWhitespaceExit;sourceWorkingIndexMatchExit=$workingExit;original24UnchangedExit=$originalUnchangedExit;unmergedPaths=@(git ls-files -u).Count;stagedIntegrationPathCount=$integrationPaths.Count;functionalPathCount=$functionalPaths.Count;changedContentSecretScanFindings=$findings;evidenceTrailingWhitespacePaths=$trailing;secretScanQualification='high-confidence pattern review of added lines vs both parents and current nonignored evidence; no secret values printed; historical archive retained unchanged';protectedSourceChanges=@(git diff --cached --name-only HEAD -- src/HotelOps.Api/Data src/HotelOps.Api/Entities src/HotelOps.Api/Security src/HotelOps.Api/Services/Authentication src/HotelOps.Api/Services/Notifications src/HotelOps.Api/appsettings.json);ownership='111 upstream integration paths; original DEV2 24 blobs unchanged; no production source fixes'} | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/source_checks.json"
if ($findings.Count -ne 0 -or $trailing.Count -ne 0 -or $stagedWhitespaceExit -ne 0 -or $workingWhitespaceExit -ne 0) { throw 'Review source_checks.json before publication' }

$indexMap=@{}
foreach ($line in @(git ls-files --stage)) { if ($line -match '^([0-9]+) ([0-9a-f]+) 0\t(.+)$') { $indexMap[$Matches[3]]=@{mode=$Matches[1];blob=$Matches[2]} } }
function BlobSha256([string]$blob) {
    $start=New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName='git'; $start.Arguments="cat-file blob $blob"; $start.UseShellExecute=$false; $start.RedirectStandardOutput=$true; $start.CreateNoWindow=$true
    $process=New-Object System.Diagnostics.Process; $process.StartInfo=$start; [void]$process.Start()
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $digest=$sha.ComputeHash($process.StandardOutput.BaseStream); $process.WaitForExit(); if ($process.ExitCode -ne 0) { throw 'Cannot hash Git blob' }; return ([BitConverter]::ToString($digest)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose(); $process.Dispose() }
}
function SourceEntry([string]$path,[string]$owner) {
    $entry=$indexMap[$path]
    return [ordered]@{path=$path;owner=$owner;mode=$entry.mode;gitBlob=$entry.blob;sha256GitBlob=(BlobSha256 $entry.blob);sha256Worktree=$(if (Test-Path -LiteralPath $path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() } else { $null });worktreeNote='null means sparse path not materialized; canonical blob hashed directly'}
}
$functional=@($functionalPaths | ForEach-Object { SourceEntry $_ 'DEV2 original accepted PR25' })
$integration=@($integrationPaths | ForEach-Object { SourceEntry $_ 'Upstream integration preserved; not DEV2 correction' })
$evidencePaths=@(git ls-files --others --exclude-standard -- $root)
$evidence=@($evidencePaths | Where-Object { $_ -ne "$root/05_EXACT_DIFF_AND_HASHES.json" } | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant()} })
[ordered]@{status='POST_SYNC_SOURCE_VALIDATION_PASSED_HOST_COMMIT_PENDING';source=$identity;functionalPathCount=$functional.Count;functionalPaths=$functional;stagedIntegrationPathCount=$integration.Count;stagedIntegrationPaths=$integration;sourceFixes=@();evidencePathCount=$evidencePaths.Count;evidencePaths=$evidence;proposedPrDeltaCount=(24+$evidencePaths.Count);proposedPrDeltaPaths=@($functionalPaths+$evidencePaths);qualification='Proposed delta vs exact integration parent after host stages evidence; not published GitHub diff';historicalManifestSha256='bb20b5bcd14bd4e56300a09633ffbd9b6d894394eb24a56152b82951895be12b';manifestSelfHashExcluded=$true;ignoredArtifacts='bin/obj and *.log excluded; .txt sanitized receipt copies are included';sqlExecution='NOT_RUN'} | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath "$root/05_EXACT_DIFF_AND_HASHES.json"
Get-FileHash -Algorithm SHA256 -LiteralPath "$root/05_EXACT_DIFF_AND_HASHES.json"
Write-Output "FUNCTIONAL_PATHS=24 INTEGRATION_PATHS=$($integration.Count) EVIDENCE_PATHS=$($evidencePaths.Count) PROPOSED_PR_PATHS=$((24+$evidencePaths.Count)) TOTAL_PASS=535"
