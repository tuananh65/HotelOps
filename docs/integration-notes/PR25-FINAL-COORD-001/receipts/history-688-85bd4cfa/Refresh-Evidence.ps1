# Read-only Git inspection; writes only the authorized evidence directory.
$ErrorActionPreference = 'Stop'
$root = 'docs/integration-notes/PR25-FINAL-COORD-001'
$receiptRoot = "$root/receipts/upstream-00bdc481"
$identity = Get-Content -Raw -LiteralPath "$receiptRoot/02_source_identity.json" | ConvertFrom-Json
if ((git rev-parse HEAD) -ne $identity.featureHead -or (git rev-parse MERGE_HEAD) -ne $identity.integrationParent) { throw 'Parent identity drift' }
if ($identity.featureHead -ne '2140522457e89b974cf2d65c64515ec15d733fa9' -or $identity.integrationParent -ne '00bdc481cb224d7117a79c95669abc7016f51ba4' -or $identity.postSyncSourceTree -ne '85bd4cfa8816736231449abeb985a5b573fb79ab') { throw 'Source identity drift' }
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

$runCounts = [ordered]@{ '03_dev2_nosql'=92; '04_boundary'=67; 'UserAccountDeactivation'=12; 'UserRoleRevocation'=54; 'RoomLifecycleCandidate'=29; 'DepartmentDeactivationCandidate'=25; 'StructureDeactivationPreview'=27; 'WorkdayReadRouting'=44; 'DepartmentChange'=45; 'ShiftSchedulePlanning'=24; 'ShiftPlanPreview'=57; 'ScheduledWorkday'=43; 'DiComposition_verified'=16; 'SecureShiftPreview'=58 }
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
$frontend=@()
$frontendFirst=@()
foreach($name in @('shift-plan-api','secure-shift-plan-browser','shift-plan-preview','shift-plan-preview-browser')) {
    $meta=Get-Content -Raw -LiteralPath "$receiptRoot/host-frontend/$name.final.command.json" | ConvertFrom-Json
    $output=Get-Content -Raw -LiteralPath $meta.output
    $counts=@{}
    foreach($key in @('tests','pass','fail','skipped')) { if($output -notmatch "(?m)^[^\r\n]*?\b$key\s+(\d+)\s*$"){throw "Frontend summary missing $key : $name"};$counts[$key]=[int]$Matches[1] }
    $frontend+=[ordered]@{suite=$name;command=$meta.command;workingDirectory=$meta.workingDirectory;sourceTree=$identity.postSyncSourceTree;exitCode=$meta.exitCode;exitCodeEvidence=$meta.exitCodeEvidence;executionOwner='HOST';tests=$counts.tests;pass=$counts.pass;fail=$counts.fail;skip=$counts.skipped;output=$meta.output;outputSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $meta.output).Hash.ToLowerInvariant();classification='Final successful host frontend verification';sqlExecution='NOT_RUN'}
    $first=Get-Content -Raw -LiteralPath "$receiptRoot/frontend-${name}_first_failure.command.json" | ConvertFrom-Json
    $frontendFirst+=[ordered]@{suite=$name;command=$first.command;workingDirectory=$first.workingDirectory;exitCode=$first.exitCode;output=$first.output;outputSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $first.output).Hash.ToLowerInvariant();qualification='Initial OS-temp attempt; retained, excluded from final-attempt totals'}
    $sandbox=Get-Content -Raw -LiteralPath "$receiptRoot/frontend-$name.command.json" | ConvertFrom-Json
    $frontendFirst+=[ordered]@{suite=$name;command=$sandbox.command;workingDirectory=$sandbox.workingDirectory;exitCode=$sandbox.exitCode;output=$sandbox.output;outputSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $sandbox.output).Hash.ToLowerInvariant();qualification='Second sandbox location; esbuild directory access denied before assertions; retained and excluded from final totals'}
}
$hostFailure=Get-Content -Raw -LiteralPath "$receiptRoot/host-frontend/shift-plan-preview.initial-failure.command.json"|ConvertFrom-Json
$frontendFirst+=[ordered]@{suite='shift-plan-preview';command=$hostFailure.command;workingDirectory=$hostFailure.workingDirectory;exitCode=$hostFailure.exitCode;pass=48;fail=1;skip=0;output=$hostFailure.output;outputSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $hostFailure.output).Hash.ToLowerInvariant();qualification='Host scratch missing production entry; superseded by canonical-entry rerun49; earlier48 passes not counted again'}
$frontendPass=($frontend | ForEach-Object {$_.pass} | Measure-Object -Sum).Sum
$frontendFail=($frontend | ForEach-Object {$_.fail} | Measure-Object -Sum).Sum
$frontendSkip=($frontend | ForEach-Object {$_.skip} | Measure-Object -Sum).Sum
$hostReady=($frontendFail -eq 0 -and @($frontend|Where-Object{$_.exitCode -ne 0 -or $_.tests -le 0}).Count -eq 0)
$inputReview=Get-Content -Raw -LiteralPath "$receiptRoot/host-frontend/input_verification.json"|ConvertFrom-Json
if($inputReview.originalRecordedInputMismatchCount -ne 0 -or $inputReview.normalizedTextMismatchCount -ne 0 -or !$inputReview.canonicalEntryMatchesIndexByteForByte){throw 'Host frontend inputs do not match tested index'}
[ordered]@{ source=$identity; verificationMode='Final evidence reconciliation; existing593 same-tree backend/DI receipts retained without rerunning';build=(Get-Content -Raw "$receiptRoot/02_build.command.json" | ConvertFrom-Json); successfulBackendRuns=$runs; frontendFinalRuns=$frontend; historicalFrontendAttempts=$frontendFirst; frontendInputVerification="$receiptRoot/host-frontend/input_verification.json";backendPass=593;frontendPass=$frontendPass; requiredPass=350; additionalExistingPass=169; diPass=16; secureShiftPreviewPass=58; totalPass=(593+$frontendPass); finalFail=$frontendFail; finalSkip=$frontendSkip;hostCommitReady=$hostReady;historical535='receipts/verification_summary.json; tree14f4595c0edeb1f6349d636ec1a08a22a287f569; not counted'; solutionTest='exit 0, zero discovered tests, excluded from PASS count'; warningLimitations=@('NU1900 vulnerability metadata unavailable','3 existing EF20602 RoomTypeBranchV1 index warnings'); sqlAcceptance19='NOT_RUN' } | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/verification_summary.json"

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
    if ($path -eq "$root/05_EXACT_DIFF_AND_HASHES.json" -or $path.Contains('/fallback-history/') -or $path.Contains('/history-8fd30f2/') -or $path.Contains('/history-frontend-blocked/')) { continue }
    $text = Get-Content -Raw -LiteralPath $path
    foreach ($rule in $secretRules.Keys) { if ($text -match $secretRules[$rule]) { $findings += [ordered]@{scope='evidence';path=$path;rule=$rule} } }
    if ($path -match '\.(md|cs|csproj|ps1)$') { if ($text -match '(?m)[ \t]+\r?$') { $trailing += $path } }
}
[ordered]@{sourceTree=$identity.postSyncSourceTree;stagedWhitespaceExit=$stagedWhitespaceExit;workingWhitespaceExit=$workingWhitespaceExit;sourceWorkingIndexMatchExit=$workingExit;original24UnchangedExit=$originalUnchangedExit;unmergedPaths=@(git ls-files -u).Count;stagedIntegrationPathCount=$integrationPaths.Count;functionalPathCount=$functionalPaths.Count;changedContentSecretScanFindings=$findings;evidenceTrailingWhitespacePaths=$trailing;secretScanQualification='high-confidence pattern review of added lines vs both parents and current nonignored evidence; no secret values printed; historical archive retained unchanged';protectedSourceChanges=@(git diff --cached --name-only HEAD -- src/HotelOps.Api/Data src/HotelOps.Api/Entities src/HotelOps.Api/Security src/HotelOps.Api/Services/Authentication src/HotelOps.Api/Services/Notifications src/HotelOps.Api/appsettings.json);ownership="$($integrationPaths.Count) upstream integration paths; original DEV2 24 blobs unchanged; no production source fixes"} | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/source_checks.json"
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
$pathList="$root/receipts/final_evidence_paths.txt"
if(!(Test-Path -LiteralPath $pathList)){[IO.File]::WriteAllText((Join-Path (Get-Location).Path $pathList),'')}
$evidencePaths=[string[]]@(git ls-files --others --exclude-standard -- $root)
[Array]::Sort($evidencePaths,[StringComparer]::Ordinal)
[IO.File]::WriteAllText((Join-Path (Get-Location).Path $pathList),($evidencePaths -join "`n")+"`n",(New-Object Text.UTF8Encoding($false)))
$receiptMetadata=@{}
foreach($metadataPath in @($evidencePaths|Where-Object{$_.EndsWith('.command.json')})){$commandData=Get-Content -Raw -LiteralPath $metadataPath|ConvertFrom-Json;if($commandData.output){$receiptMetadata[$commandData.output]=$commandData}}
foreach($row in @($runs)+@($frontend)+@($frontendFirst)){$receiptMetadata[$row.output]=$row}
$evidence=@($evidencePaths | Where-Object { $_ -ne "$root/05_EXACT_DIFF_AND_HASHES.json" } | ForEach-Object {
    $path=$_;$tree=$identity.postSyncSourceTree;$kind='Current report or evidence tooling'
    if($path.Contains('/fallback-history/')){$tree='33ae51a1dbc228dc0338768ecbe9cd959dec0939';$kind='Historical original feature fallback'}
    elseif($path.Contains('/history-8fd30f2/')){$tree='14f4595c0edeb1f6349d636ec1a08a22a287f569';$kind='Historical535 snapshot'}
    elseif($path.Contains('/history-frontend-blocked/')){$kind='Historical frontend-blocked snapshot'}
    elseif($path.Contains('/host-frontend/')){$kind='Host frontend verification evidence'}
    elseif($path -in @("$root/receipts/Refresh-Evidence.ps1","$root/receipts/Verify-FinalEvidence.ps1","$root/receipts/final_evidence_paths.txt")){$kind='Current evidence verification or staging tooling'}
    elseif($path.Contains('/receipts/DiComposition/')){$kind='Shared offline DI verification source, exercised on current tree'}
    elseif($path.Contains('/receipts/') -and !$path.Contains('/upstream-00bdc481/')){$tree='14f4595c0edeb1f6349d636ec1a08a22a287f569';$kind='Historical535 receipt or shared verification tooling'}
    $data=$null;if($path.EndsWith('.command.json')){$data=Get-Content -Raw -LiteralPath $path|ConvertFrom-Json;$kind='Command metadata'}elseif($receiptMetadata.ContainsKey($path)){$data=$receiptMetadata[$path];$kind='Console test receipt'}
    $exit=$null;$testResult=$null;$pass=$null;$fail=$null;$skip=$null
    if($data){$exit=$data.exitCode;if($null -ne $exit){$testResult=if($exit -eq 0){'Passed'}else{'HistoricalFailedAttempt'}};$pass=$data.pass;$fail=$data.fail;$skip=$data.skip;if($data.sourceTree){$tree=$data.sourceTree}}
    [ordered]@{path=$path;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant();sourceTree=$tree;classification=$kind;testResult=$testResult;exitCode=$exit;pass=$pass;fail=$fail;skip=$skip}
})
[ordered]@{status='SOURCE_VALIDATED_HOST_COMMIT_READY';hostCommitReady=$hostReady;source=$identity;functionalPathCount=$functional.Count;functionalPaths=$functional;stagedIntegrationPathCount=$integration.Count;stagedIntegrationPaths=$integration;upstreamDrift=[ordered]@{previousIntegration='8fd30f2f12e9dae102de2826e0be20e99e6a14ad';commits=2;paths=@(git diff --name-only 8fd30f2f12e9dae102de2826e0be20e99e6a14ad MERGE_HEAD)};sourceFixes=@();evidencePathCount=$evidencePaths.Count;evidencePaths=$evidence;explicitEvidencePathList=$pathList;proposedPrDeltaCount=(24+$evidencePaths.Count);proposedPrDeltaPaths=@($functionalPaths+$evidencePaths);qualification='Proposed delta vs exact integration parent after host stages evidence; not published GitHub diff';historicalManifestSha256='bb20b5bcd14bd4e56300a09633ffbd9b6d894394eb24a56152b82951895be12b';previous535Manifest=[ordered]@{path="$root/receipts/history-8fd30f2/05_EXACT_DIFF_AND_HASHES.json";sha256='2903d759931429874b2466265900fe0d7208518ab4d7f901cb45393cf80b9d3a'};previousBlockedManifest=[ordered]@{path="$root/receipts/history-frontend-blocked/05_EXACT_DIFF_AND_HASHES.json";sha256='6e3b8901c9d0eb046543ede6536829a936e4488b99c8fa7522f1f4cf47f9b485'};manifestSelfHashExcluded=$true;ignoredArtifacts='bin/obj, node_modules, frontend scratch and *.log excluded';sqlExecution='NOT_RUN'} | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath "$root/05_EXACT_DIFF_AND_HASHES.json"
Get-FileHash -Algorithm SHA256 -LiteralPath "$root/05_EXACT_DIFF_AND_HASHES.json"
Write-Output "FUNCTIONAL_PATHS=24 INTEGRATION_PATHS=$($integration.Count) EVIDENCE_PATHS=$($evidencePaths.Count) PROPOSED_PR_PATHS=$((24+$evidencePaths.Count)) BACKEND_PASS=593 FRONTEND_FAIL=$frontendFail HOST_COMMIT_READY=$hostReady"
