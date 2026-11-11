param([switch]$AllowPendingFrontend)
$ErrorActionPreference='Stop'
$root='docs/integration-notes/PR25-FINAL-COORD-001'
$fresh="$root/receipts/uc139-70e1d4ee"
$m=Get-Content -Raw "$root/05_EXACT_DIFF_AND_HASHES.json"|ConvertFrom-Json
if((git branch --show-current) -ne 'feature/dev2-dev3-master001-responsibility-readers' -or (git rev-parse HEAD) -ne $m.source.featureHead -or (git rev-parse MERGE_HEAD) -ne $m.source.integrationParent){throw 'Wrong branch or merge parents'}
if(@(git ls-files -u).Count -ne 0){throw 'Unmerged paths'}
git diff --quiet
if($LASTEXITCODE -ne 0){throw 'Unstaged tracked changes'}
$index=@{};git ls-files --stage|ForEach-Object{if($_ -match '^\d+ ([0-9a-f]+) 0\t(.+)$'){$index[$Matches[2]]=$Matches[1]}}
foreach($entry in @($m.functionalPaths)+@($m.stagedIntegrationPaths)){if($index[$entry.path] -ne $entry.gitBlob){throw "Source drift: $($entry.path)"}}
git diff --cached --quiet $m.source.postSyncSourceTree -- . ":(exclude)$root"
if($LASTEXITCODE -ne 0){throw 'Source differs from tested combined tree'}
$outside=@(git ls-files --others --exclude-standard | Where-Object{!$_.StartsWith("$root/")})
if($outside.Count -ne 0){throw 'Unexpected untracked paths outside evidence'}
$expectedStaged=@($m.stagedIntegrationPaths.path)+@($m.evidencePaths|Where-Object{$index.ContainsKey($_.path)}|ForEach-Object{$_.path})
if($index.ContainsKey("$root/05_EXACT_DIFF_AND_HASHES.json")){$expectedStaged+="$root/05_EXACT_DIFF_AND_HASHES.json"}
if(@(Compare-Object @($expectedStaged|Sort-Object -Unique) @(git diff --cached --name-only HEAD|Sort-Object -Unique)).Count -ne 0){throw 'Unexpected staged paths'}
$normalized=@($m.evidencePaths.path | git hash-object --stdin-paths 2>$null)
if($LASTEXITCODE -ne 0 -or $normalized.Count -ne $m.evidencePaths.Count){throw 'Normalized evidence hash failure'}
$position=0
foreach($entry in $m.evidencePaths){
 if($normalized[$position++] -ne $entry.gitNormalizedBlob){throw "Git-normalized content drift: $($entry.path)"}
 if((Get-FileHash -Algorithm SHA256 -LiteralPath $entry.path).Hash.ToLowerInvariant() -ne $entry.sha256Worktree){throw "Worktree receipt hash drift: $($entry.path)"}
 if($index.ContainsKey($entry.path) -and $index[$entry.path] -ne $entry.gitNormalizedBlob){throw "Normalized staged evidence mismatch: $($entry.path)"}
}
$listed=@(Get-Content -LiteralPath $m.explicitEvidencePathList)
$actual=@(git ls-files --cached --others --exclude-standard -- $root)|Select-Object -Unique
if($listed.Count -ne $m.evidencePathCount -or @(Compare-Object $listed $actual).Count -ne 0){throw 'Evidence inventory drift'}
$inputs=Get-Content -Raw "$fresh/host-frontend/canonical_frontend_inputs.json"|ConvertFrom-Json
if($inputs.verifiedFiles -ne 415 -or $inputs.files.Count -ne 415 -or $inputs.sourceTree -ne $m.source.postSyncSourceTree){throw 'Wrong frontend canonical input identity'}
$scratchFiles=@($inputs.files|ForEach-Object{Join-Path $inputs.scratchDirectory $_.path.Substring('frontend/'.Length)})
$scratchBlobs=@($scratchFiles|git hash-object --no-filters --stdin-paths)
if($LASTEXITCODE -ne 0 -or $scratchBlobs.Count -ne 415){throw 'Canonical frontend read failure'}
for($i=0;$i -lt 415;$i++){$item=$inputs.files[$i];if($scratchBlobs[$i] -ne $item.integrationGitBlob -or $index[$item.path] -ne $item.integrationGitBlob -or (Get-FileHash -Algorithm SHA256 -LiteralPath $scratchFiles[$i]).Hash.ToLowerInvariant() -ne $item.sha256Worktree){throw "Frontend scratch input drift: $($item.path)"}}
$checks=Get-Content -Raw "$fresh/source_checks.json"|ConvertFrom-Json
$strict=@('diff','--cached','--check','--','.')
foreach($exception in $checks.rawReceiptWhitespaceExceptions){$strict+=":(exclude)$($exception.path)"}
& git @strict
if($LASTEXITCODE -ne 0){throw 'Whitespace failure outside documented verbatim receipt exceptions'}
$summary=Get-Content -Raw "$fresh/verification_summary.json"|ConvertFrom-Json
if($summary.hostStageRecheckRequired){
 foreach($path in $listed){if(!$index.ContainsKey($path)){throw "Host must re-stage evidence: $path"}}
 $manifestPrediction=git hash-object -- "$root/05_EXACT_DIFF_AND_HASHES.json"
 if($index["$root/05_EXACT_DIFF_AND_HASHES.json"] -ne $manifestPrediction){throw 'Host must re-stage updated manifest'}
}
if(!$summary.hostCommitReady -and !$summary.hostStageRecheckRequired -and !$AllowPendingFrontend){throw 'HOST_COMMIT_READY=NO: new-tree ScheduledWorkday frontend host verification and evidence refresh required'}
if($summary.backendDiPass -ne 409 -or $summary.frontend.pass -ne 47 -or $summary.currentTreePass -ne 456 -or $summary.currentTreeFail -ne 0 -or $summary.currentTreeSkip -ne 0){throw 'Unexpected final counts'}
Write-Output "EVIDENCE_VALIDATED: paths=$($m.evidencePathCount); raw and Git-normalized staged hashes checked; backend=409; frontend=47; total=456; HOST_STAGE_RECHECK=PASS; HOST_COMMIT_READY=True."
