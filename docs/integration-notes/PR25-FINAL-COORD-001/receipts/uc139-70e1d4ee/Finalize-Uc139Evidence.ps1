$ErrorActionPreference='Stop'
$root='docs/integration-notes/PR25-FINAL-COORD-001'
$fresh="$root/receipts/uc139-70e1d4ee"
$identity=Get-Content -Raw "$fresh/02_source_identity.json"|ConvertFrom-Json
if((git rev-parse HEAD) -ne '2140522457e89b974cf2d65c64515ec15d733fa9' -or (git rev-parse MERGE_HEAD) -ne '70e1d4ee2c4b63b14543f90c1e315bec39585d80' -or $identity.postSyncSourceTree -ne 'b3d8dfe61a528c68002ce6d725d47840604d2e27'){throw 'Source identity drift'}
git diff --quiet
if($LASTEXITCODE -ne 0 -or @(git ls-files -u).Count -ne 0){throw 'Unstaged source or conflicts'}
$functionalPaths=@(git diff --cached --name-only MERGE_HEAD)
$integrationPaths=@(git diff --cached --name-only HEAD)
git diff --cached --quiet HEAD -- @functionalPaths
if($functionalPaths.Count -ne 24 -or $LASTEXITCODE -ne 0){throw 'Original24 changed'}
git diff --cached --check -- src tests scripts frontend
$functionalWhitespace=$LASTEXITCODE
if($functionalWhitespace -ne 0){throw 'Functional whitespace errors'}
$runs=@()
foreach($name in @('Dev2NoSql','BoundaryTests','WorkdayReadRouting','ScheduledWorkday','ShiftSchedulePlanning','ShiftPlanPreview','SecureShiftPreview','DiComposition')){
 $meta=Get-Content -Raw "$fresh/$name.command.json"|ConvertFrom-Json
 if($meta.exitCode -ne 0 -or $meta.sourceTree -ne $identity.postSyncSourceTree){throw "Invalid current receipt: $name"}
 $output=Get-Content -Raw -LiteralPath $meta.output
 $pass=0;$fail=0;$skip=0
 if($name -eq 'Dev2NoSql'){$data=($output -split "`n"|Where-Object{$_.Trim().StartsWith('{"pass":')}|Select-Object -Last 1)|ConvertFrom-Json;if(!$data.pass -or $data.sqlConnections -ne 0){throw 'NoSql failed'};$pass=[int]$data.tests}
 elseif($name -eq 'BoundaryTests'){if($output -notmatch 'Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)'){throw 'No xUnit discovery'};$fail=[int]$Matches[1];$pass=[int]$Matches[2];$skip=[int]$Matches[3];if([int]$Matches[4] -le 0){throw 'Zero discovered tests'}}
 else{if($output -notmatch '(?m)^RESULT:?\s+(\d+)(?:/\d+)? PASS'){throw "Missing summary: $name"};$pass=[int]$Matches[1]}
 $codes=@([regex]::Matches($output,'warning ([A-Z]+\d+):')|ForEach-Object{$_.Groups[1].Value}|Select-Object -Unique)
 $runs+=[ordered]@{suite=$name;command=$meta.command;workingDirectory=$meta.workingDirectory;sourceTree=$identity.postSyncSourceTree;exitCode=$meta.exitCode;pass=$pass;fail=$fail;skip=$skip;warningCodes=$codes;warningOccurrences=[regex]::Matches($output,'warning [A-Z]+\d+:').Count;modelWarning20602Occurrences=[regex]::Matches($output,'Model.Validation\[20602\]').Count;output=$meta.output;sha256Worktree=(Get-FileHash -Algorithm SHA256 -LiteralPath $meta.output).Hash.ToLowerInvariant();sqlExecution='NOT_RUN'}
}
$total=($runs|ForEach-Object{$_.pass}|Measure-Object -Sum).Sum
$failTotal=($runs|ForEach-Object{$_.fail}|Measure-Object -Sum).Sum
$skipTotal=($runs|ForEach-Object{$_.skip}|Measure-Object -Sum).Sum
$backendTotal=$total
$hostMeta=Get-Content -Raw "$fresh/host-frontend/scheduled-workday-browser.host.command.json"|ConvertFrom-Json
$hostText=Get-Content -Raw -LiteralPath $hostMeta.output
$observed=@{}
foreach($field in @('tests','suites','pass','fail','cancelled','skipped','todo')){
 $match=[regex]::Match($hostText,"(?m)^.*?\b$field (\d+)\r?$")
 if(!$match.Success){throw "Host receipt missing $field"};$observed[$field]=[int]$match.Groups[1].Value
}
if($observed.tests -ne 47 -or $observed.pass -ne 47 -or $observed.fail -ne 0 -or $observed.cancelled -ne 0 -or $observed.skipped -ne 0 -or $observed.todo -ne 0 -or $hostMeta.exitCode -ne 0 -or $hostMeta.sourceTree -ne $identity.postSyncSourceTree){throw 'Host frontend receipt failed or mismatched'}
$hostMeta|Add-Member -Force -NotePropertyName sha256Worktree -NotePropertyValue (Get-FileHash -Algorithm SHA256 -LiteralPath $hostMeta.output).Hash.ToLowerInvariant()
$frontend=[ordered]@{status='PASS_HOST_EXECUTED';pass=$observed.pass;fail=$observed.fail;skip=$observed.skipped;discovered=$observed.tests;cancelled=$observed.cancelled;todo=$observed.todo;execution=$hostMeta;canonicalInputs="$fresh/host-frontend/canonical_frontend_inputs.json";canonicalGitBlobs=415;hostReportedScratchRepairs=307;qualification='Scratch environment preparation only; no tracked source changes; host attested exit0, independently parsed raw receipt and verified415 blobs'}
$total+=$observed.pass
$build=Get-Content -Raw "$fresh/build.command.json"|ConvertFrom-Json
$buildText=Get-Content -Raw -LiteralPath $build.output
if($build.exitCode -ne 0){throw 'Build failed'}
$build|Add-Member -Force -NotePropertyName warnings -NotePropertyValue ([int]([regex]::Match($buildText,'(\d+) Warning\(s\)').Groups[1].Value))
$build|Add-Member -Force -NotePropertyName errors -NotePropertyValue ([int]([regex]::Match($buildText,'(\d+) Error\(s\)').Groups[1].Value))
$build|Add-Member -Force -NotePropertyName sha256Worktree -NotePropertyValue (Get-FileHash -Algorithm SHA256 -LiteralPath $build.output).Hash.ToLowerInvariant()
[ordered]@{source=$identity;strategy='Targeted UC13913-path regression; eight mandatory offline suites plus47 host browser tests; no historical rerun';build=$build;successfulBackendRuns=$runs;backendDiPass=$backendTotal;currentTreePass=$total;currentTreeFail=$failTotal;currentTreeSkip=$skipTotal;frontend=$frontend;hostFrontendRerunRequired=$false;hostCommitReady=$true;uc139AcceptanceRuntime='NOT_RUN';acceptanceReview="$fresh/acceptance_safety_review.json";historical688=[ordered]@{sourceTree='85bd4cfa8816736231449abeb985a5b573fb79ab';status='PREVIOUS_TREE_ONLY';pass=688;countedInCurrent=$false};sqlAcceptance19='NOT_RUN';businessActivation='BLOCKED'}|ConvertTo-Json -Depth 12|Set-Content -Encoding utf8 "$fresh/verification_summary.json"

$list="$root/receipts/final_evidence_paths.txt"
$paths=[string[]]@(git ls-files --others --exclude-standard -- $root)
[Array]::Sort($paths,[StringComparer]::Ordinal)
[IO.File]::WriteAllText((Join-Path (Get-Location).Path $list),($paths -join "`n")+"`n",(New-Object Text.UTF8Encoding($false)))
$rules=[ordered]@{PrivateKey='-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----';GitHubToken='(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})';AwsAccessKey='AKIA[A-Z0-9]{16}';SqlCredential='(?i)(?:;|^)(?:password|pwd)\s*=\s*[^;\s"''<>]+';CredentialLiteral='(?i)\b(?:password|pwd)\s*=\s*["''][^"'']+["'']'}
$findings=@();$whitespace=@();$exceptions=@()
foreach($base in @('HEAD','MERGE_HEAD')){$file='';foreach($line in @(git diff --cached --unified=0 $base)){if($line -match '^\+\+\+ b/(.+)$'){$file=$Matches[1]};if($line.StartsWith('+') -and !$line.StartsWith('+++')){foreach($rule in $rules.Keys){if($line -match $rules[$rule]){$findings+=[ordered]@{path=$file;scope="index vs $base";rule=$rule}}}}}
}
foreach($path in $paths){
 if($path -eq "$root/05_EXACT_DIFF_AND_HASHES.json"){continue}
 $text=Get-Content -Raw -LiteralPath $path
 if($path -notmatch '/(?:history-[^/]+|fallback-history)/'){foreach($rule in $rules.Keys){if($text -match $rules[$rule]){$findings+=[ordered]@{path=$path;scope='current evidence';rule=$rule}}};if($path -match '\.(md|cs|csproj|ps1)$' -and $text -match '(?m)[ \t]+\r?$'){$whitespace+=$path}}
 if($path.Contains('/receipts/') -and $path -match '\.(txt|tsv)$'){$n=[regex]::Matches($text,'(?m)[ \t]+\r?$').Count;$tail=[regex]::Match($text.Replace("`r`n","`n"),'\n(?:[ \t]*\n)+\z');$blank=if($tail.Success){[regex]::Matches($tail.Value,'\n').Count-1}else{0};if($n -gt 0 -or $blank -gt 0){$exceptions+=[ordered]@{path=$path;trailingWhitespaceLines=$n;trailingBlankLines=$blank;reason='Captured raw receipt bytes preserved; not functional code; no whitespace cleanup'}}}
}
[ordered]@{sourceTree=$identity.postSyncSourceTree;functionalWhitespaceExit=$functionalWhitespace;original24Preserved=$true;unmergedPaths=0;sourceFixes=@();integrationOwnedPaths=$integrationPaths.Count;secretPatternFindings=$findings;strictCurrentEvidenceWhitespaceFailures=$whitespace;rawReceiptWhitespaceExceptions=$exceptions;lineEndingPolicy='core.autocrlf=true; raw worktree SHA256 distinct from Git-normalized blob IDs; no attributes changes';frontend='47_PASS_HOST';sourceGuards='PASS_SOURCE_ONLY'}|ConvertTo-Json -Depth 8|Set-Content -Encoding utf8 "$fresh/source_checks.json"
if($findings.Count -ne 0 -or $whitespace.Count -ne 0){throw 'Review source checks before handoff'}

$index=@{};git ls-files --stage|ForEach-Object{if($_ -match '^(\d+) ([0-9a-f]+) 0\t(.+)$'){$index[$Matches[3]]=@{mode=$Matches[1];blob=$Matches[2]}}}
function BlobSha256([string]$blob){$s=New-Object Diagnostics.ProcessStartInfo;$s.FileName='git';$s.Arguments="cat-file blob $blob";$s.UseShellExecute=$false;$s.RedirectStandardOutput=$true;$s.CreateNoWindow=$true;$p=New-Object Diagnostics.Process;$p.StartInfo=$s;[void]$p.Start();$sha=[Security.Cryptography.SHA256]::Create();try{$b=$sha.ComputeHash($p.StandardOutput.BaseStream);$p.WaitForExit();if($p.ExitCode -ne 0){throw 'Blob read failed'};return ([BitConverter]::ToString($b)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$p.Dispose()}}
function SourceEntry([string]$path,[string]$owner){$item=$index[$path];[ordered]@{path=$path;owner=$owner;mode=$item.mode;gitBlob=$item.blob;sha256GitBlob=(BlobSha256 $item.blob);sha256Worktree=$(if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()}else{$null})}}
$functional=@($functionalPaths|ForEach-Object{SourceEntry $_ 'DEV2 original accepted PR25; unchanged'})
$integration=@($integrationPaths|ForEach-Object{SourceEntry $_ 'Integration-owned source preserved'})
$paths=[string[]]@(git ls-files --others --exclude-standard -- $root);[Array]::Sort($paths,[StringComparer]::Ordinal)
$normal=[string[]]@($paths|git hash-object --stdin-paths 2>$null)
if($normal.Count -ne $paths.Count){throw 'Normalized blob inventory mismatch'}
$metadata=@{};foreach($path in @($paths|Where-Object{$_.EndsWith('.command.json')})){$data=Get-Content -Raw -LiteralPath $path|ConvertFrom-Json;if($data.output){$metadata[$data.output]=$data}}
foreach($data in $runs){$metadata[$data.output]=$data};$metadata[$build.output]=$build;$metadata[$hostMeta.output]=$hostMeta
$evidence=@()
for($i=0;$i -lt $paths.Count;$i++){
 $path=$paths[$i];if($path -eq "$root/05_EXACT_DIFF_AND_HASHES.json"){continue}
 $tree=$identity.postSyncSourceTree;$kind='Current UC139 report/tooling';$data=$null
 if($path.Contains('/fallback-history/')){$tree='33ae51a1dbc228dc0338768ecbe9cd959dec0939';$kind='Historical initial fallback'}
 elseif($path.Contains('/history-8fd30f2/')){$tree='14f4595c0edeb1f6349d636ec1a08a22a287f569';$kind='Historical535 snapshot'}
 elseif($path.Contains('/history-688-85bd4cfa/') -or $path.Contains('/upstream-00bdc481/') -or $path.Contains('/history-frontend-blocked/')){$tree='85bd4cfa8816736231449abeb985a5b573fb79ab';$kind='Historical593/688 or retries'}
 elseif($path.Contains('/receipts/') -and !$path.Contains('/uc139-70e1d4ee/') -and $path -notin @("$root/receipts/Refresh-Evidence.ps1","$root/receipts/Verify-FinalEvidence.ps1",$list)){$tree='14f4595c0edeb1f6349d636ec1a08a22a287f569';$kind='Historical535 or shared probe'}
 if($path -eq "$fresh/pre-host-manifest.json"){$kind='Historical pre-host manifest210 evidence234 PR paths; current tree backend409, frontend pending'}
 if($path.EndsWith('.command.json')){$data=Get-Content -Raw -LiteralPath $path|ConvertFrom-Json;$kind='Command metadata'}elseif($metadata.ContainsKey($path)){$data=$metadata[$path];$kind='Console receipt'}
 $code=$null;$result=$null;$pass=$null;$fail=$null;$skip=$null
 if($data){$code=$data.exitCode;if($null -ne $code){$result=if($code -eq 0){'Passed'}else{'HistoricalFailedAttempt'}};$pass=$data.pass;$fail=$data.fail;$skip=$data.skip;if($data.sourceTree){$tree=$data.sourceTree}}
 $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
 $evidence+=[ordered]@{path=$path;sha256=$hash;sha256Worktree=$hash;gitNormalizedBlob=$normal[$i];stagedGitBlob=$(if($index.ContainsKey($path)){$index[$path].blob}else{$null});sourceTree=$tree;classification=$kind;testResult=$result;exitCode=$code;pass=$pass;fail=$fail;skip=$skip}
}
[ordered]@{status='SOURCE_VALIDATED_HOST_COMMIT_READY';hostCommitReady=$true;hostFrontendRerunRequired=$false;source=$identity;backendDiPass=$backendTotal;frontendPass=47;currentTreePass=$total;currentTreeFail=$failTotal;currentTreeSkip=$skipTotal;functionalPathCount=$functional.Count;functionalPaths=$functional;stagedIntegrationPathCount=$integration.Count;stagedIntegrationPaths=$integration;uc139Delta=[ordered]@{previousIntegration='00bdc481cb224d7117a79c95669abc7016f51ba4';commits=2;paths=@(git diff --name-only 00bdc481cb224d7117a79c95669abc7016f51ba4 MERGE_HEAD)};sourceFixes=@();evidencePathCount=$paths.Count;evidencePaths=$evidence;explicitEvidencePathList=$list;proposedPrDeltaCount=(24+$paths.Count);proposedPrDeltaPaths=@($functionalPaths+$paths);previous688Manifest=[ordered]@{path="$root/receipts/history-688-85bd4cfa/05_EXACT_DIFF_AND_HASHES.json";sha256='57b5d0a6096ccb6bedf25288343ec8142b54c7060fefcc121e3dfa949ceda42c'};lineEndingPolicy='Worktree SHA256 is raw current bytes; gitNormalizedBlob is read-only git hash-object clean-filter prediction (core.autocrlf=true); stagedGitBlob null until host stages; CRLF worktree != LF text blob bytes';manifestSelfHashExcluded=$true;ignoredArtifacts='bin/obj/node_modules/scratch/*.log excluded';sqlExecution='NOT_RUN'}|ConvertTo-Json -Depth 12|Set-Content -Encoding utf8 "$root/05_EXACT_DIFF_AND_HASHES.json"
Get-FileHash -Algorithm SHA256 "$root/05_EXACT_DIFF_AND_HASHES.json"
Write-Output "CURRENT_PASS=$total FAIL=$failTotal SKIP=$skipTotal EVIDENCE=$($paths.Count) PROPOSED_PR=$((24+$paths.Count)) HOST_FRONTEND_PASS=47"
