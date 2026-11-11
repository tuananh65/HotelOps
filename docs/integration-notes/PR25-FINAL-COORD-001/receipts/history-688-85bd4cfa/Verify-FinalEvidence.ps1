# Read-only verification for the host before and after evidence staging.
$ErrorActionPreference='Stop'
$root='docs/integration-notes/PR25-FINAL-COORD-001'
$manifest=Get-Content -Raw -LiteralPath "$root/05_EXACT_DIFF_AND_HASHES.json"|ConvertFrom-Json
if((git branch --show-current) -ne 'feature/dev2-dev3-master001-responsibility-readers'){throw 'Wrong branch'}
if((git rev-parse HEAD) -ne $manifest.source.featureHead -or (git rev-parse MERGE_HEAD) -ne $manifest.source.integrationParent){throw 'Wrong merge parents'}
if(@(git ls-files -u).Count -ne 0){throw 'Unresolved conflicts'}
git diff --quiet
if($LASTEXITCODE -ne 0){throw 'Unstaged tracked source changes'}
$index=@{}
git ls-files --stage|ForEach-Object{if($_ -match '^\d+ ([0-9a-f]+) 0\t(.+)$'){$index[$Matches[2]]=$Matches[1]}}
foreach($entry in @($manifest.functionalPaths)+@($manifest.stagedIntegrationPaths)){if($index[$entry.path] -ne $entry.gitBlob){throw "Source blob drift: $($entry.path)"}}
foreach($entry in $manifest.evidencePaths){if((Get-FileHash -Algorithm SHA256 -LiteralPath $entry.path).Hash.ToLowerInvariant() -ne $entry.sha256){throw "Evidence hash drift: $($entry.path)"}}
$listed=@(Get-Content -LiteralPath $manifest.explicitEvidencePathList)
if($listed.Count -ne $manifest.evidencePathCount){throw 'Evidence path count mismatch'}
$actual=@(git ls-files --cached --others --exclude-standard -- $root)|Select-Object -Unique
if(@(Compare-Object $listed $actual).Count -ne 0){throw 'Unexpected or missing evidence paths'}
$summary=Get-Content -Raw -LiteralPath "$root/receipts/upstream-00bdc481/verification_summary.json"|ConvertFrom-Json
if(!$summary.hostCommitReady -or $summary.totalPass -ne 688 -or $summary.finalFail -ne 0 -or $summary.finalSkip -ne 0){throw 'Final successful verification missing'}
git diff --cached --check
if($LASTEXITCODE -ne 0){throw 'Staged whitespace failure'}
Write-Output "VERIFIED: source blobs unchanged; evidence paths=$($listed.Count); combined688PASS; zero final failures/skips; host commit ready."
