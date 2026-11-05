$ErrorActionPreference='Stop'
$coordRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$coordPre=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PREFLIGHT.json') -Raw | ConvertFrom-Json
$coordSupplement=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PREFLIGHT_SUPPLEMENT.json') -Raw | ConvertFrom-Json
$coordBaseline=@{};foreach($coordF in $coordPre.files){$coordBaseline[$coordF.path]=$coordF};foreach($coordF in $coordSupplement.files){$coordBaseline[$coordF.path]=$coordF}
$coordChanged=@();$coordMissing=@();$coordMtimeOnly=@()
foreach($coordF in $coordBaseline.Values){
 if(!(Test-Path -LiteralPath $coordF.path)){$coordMissing+=$coordF.path;continue}
 if((Get-FileHash -LiteralPath $coordF.path).Hash -cne $coordF.sha256){$coordChanged+=$coordF.path}
 elseif((Get-Item -LiteralPath $coordF.path).LastWriteTimeUtc -ne ([DateTimeOffset]::Parse($coordF.lastWriteUtc)).UtcDateTime){$coordMtimeOnly+=$coordF.path}
}
$coordNewInputs=@();$coordAfterWorktrees=@()
foreach($coordW in $coordPre.worktrees){
 $coordTrust='safe.directory='+$coordW.path
 $coordIndexPath=(git --no-optional-locks -c $coordTrust -C $coordW.path rev-parse --path-format=absolute --git-path index)
 $coordIndexHash=if(Test-Path -LiteralPath $coordIndexPath){(Get-FileHash -LiteralPath $coordIndexPath).Hash}else{$null}
 $coordHead=(git --no-optional-locks -c $coordTrust -C $coordW.path rev-parse HEAD).Trim()
 $coordBranch=(git --no-optional-locks -c $coordTrust -C $coordW.path branch --show-current).Trim()
 $coordStatus=@(git --no-optional-locks -c $coordTrust -C $coordW.path status --porcelain=v1 -uall)
 $coordAfterWorktrees+=[ordered]@{path=$coordW.path;head=$coordHead;branch=$coordBranch;indexSha256=$coordIndexHash;headPreserved=($coordHead -ceq $coordW.head);branchPreserved=($coordBranch -ceq $coordW.branch);indexPreserved=($coordIndexHash -ceq $coordW.indexSha256);status=$coordStatus}
 Push-Location $coordW.path
 try{
  $coordDirs=@('src','docs','scripts','database')|Where-Object{Test-Path -LiteralPath $_}
  foreach($coordPath in (rg --files --hidden --no-ignore @coordDirs -g '!**/bin/**' -g '!**/obj/**' -g '!**/artifacts/**' -g '!**/node_modules/**' -g '!**/packages/**')){
   $coordAbsolute=(Get-Item -LiteralPath $coordPath).FullName
   if($coordAbsolute.StartsWith($PSScriptRoot+'\',[StringComparison]::OrdinalIgnoreCase)){continue}
   if(!$coordBaseline.ContainsKey($coordAbsolute)){$coordNewInputs+=$coordAbsolute}
  }
 }finally{Pop-Location}
}
$coordWorktreeList=@(git --no-optional-locks -C $coordRoot worktree list --porcelain)
$coordWorktreeListSame=(($coordWorktreeList -join "`n") -ceq ($coordPre.worktreeList -join "`n"))
$coordGitMismatch=@($coordAfterWorktrees|Where-Object{!$_.headPreserved -or !$_.branchPreserved -or !$_.indexPreserved})
$coordCurrentIds=Get-Content (Join-Path $PSScriptRoot 'evidence/CURRENT_SOURCE_IDS.json') -Raw|ConvertFrom-Json
$coordCurrentCs=@(rg --files (Join-Path $coordRoot 'src/HotelOps.Api') -g '*.cs' -g '!**/bin/**' -g '!**/obj/**')
$coordCurrentCsSetSame=($coordCurrentCs.Count -eq $coordCurrentIds.Count)
$coordConsistent=($coordChanged.Count -eq 0 -and $coordMissing.Count -eq 0 -and $coordNewInputs.Count -eq 0 -and $coordGitMismatch.Count -eq 0 -and $coordWorktreeListSame -and $coordCurrentCsSetSame)
git --no-optional-locks -C $coordRoot diff --check
$coordDiffExit=$LASTEXITCODE
$coordMainDocs=@('DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md','DEV1_CURRENT_SOURCE_EVIDENCE_MANIFEST.md','DEV1_NOTIFICATION_INTEGRATION_CONTRACT.md','DEV1_ROOM_WORKDAY_SHARED_CONTRACT.md','DEV1_GLOBAL_LOCK_AND_AUTHORITY_MATRIX.md','DEV1_SHARED_APPROVAL_AND_ACCEPTANCE_MATRIX.md')
foreach($coordName in $coordMainDocs){if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $coordName))){throw "Missing deliverable $coordName"}}
$coordEvidenceFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'evidence') -File)
$coordUnsafe=@()
foreach($coordItem in @($coordMainDocs|ForEach-Object{Get-Item -LiteralPath (Join-Path $PSScriptRoot $_)})+$coordEvidenceFiles){
 if($coordItem.Extension -cnotin @('.md','.json','.txt','.diff')){$coordUnsafe+=$coordItem.FullName;continue}
 $coordText=[IO.File]::ReadAllText($coordItem.FullName)
 if($coordText -match '(?i)(?:Data Source|Initial Catalog|Server)\s*=|-----BEGIN .*PRIVATE KEY|(?:password|access_token|refresh_token|api[_-]?key)"\s*:\s*"[^"\r\n]+"'){$coordUnsafe+=$coordItem.FullName}
}
if($coordUnsafe.Count){throw ('Unsafe archive content; stop: '+($coordUnsafe -join ', '))}
$coordProgram=Get-Content (Join-Path $PSScriptRoot 'evidence/PROGRAM_IDENTITY.json') -Raw|ConvertFrom-Json
$coordValidation=[ordered]@{
 task='COORD-FINAL-01';status=if($coordConsistent -and $coordDiffExit -eq 0){'DONE_CONSOLIDATED_HANDOFF_NOT_RUNTIME_ACCEPTANCE'}else{'SNAPSHOT_INCONSISTENT'};
 startedUtc=$coordPre.startedUtc;recordedUtc=[DateTime]::UtcNow.ToString('o');timezone='Asia/Saigon';requestDate='2026-11-04';
 elapsedSeconds=[Math]::Round(([DateTime]::UtcNow-([DateTimeOffset]::Parse($coordPre.startedUtc)).UtcDateTime).TotalSeconds,1);
 sourceSnapshotConsistent=$coordConsistent;baselineFiles=$coordBaseline.Count;currentMainCSharpIdentities=$coordCurrentIds.Count;changedFiles=$coordChanged;missingFiles=$coordMissing;newNonOutputInputs=$coordNewInputs;mtimeOnlyChanges=$coordMtimeOnly;
 worktreeCount=$coordAfterWorktrees.Count;worktreeListPreserved=$coordWorktreeListSame;worktrees=$coordAfterWorktrees;programIdentity=$coordProgram;
 sourceCaptureBeforeAfterHashChecks='PASS (Capture-Evidence.ps1; final combined baseline rehash above)';
 sourceEditsByTask=0;programEditsByTask=0;schemaEditsByTask=0;rbacEditsByTask=0;oldArtifactsOverwritten=0;
 generatedBuildOutputs='EXCLUDED_FROM_HASH_SCOPE; no build/run/touch operations in this task';
 build='NOT_RUN';frameworkTests='NOT_RUN';sql='NOT_RUN';https='NOT_RUN';databaseConnections=0;remoteFetch=$false;gitHistoryMutations=0;commits=0;pushes=0;merges=0;checkouts=0;branchMutations=0;worktreeMutations=0;diffCheckExit=$coordDiffExit;
 sixMainDocuments=$coordMainDocs;sourceExcerptAndMetadataFiles=$coordEvidenceFiles.Count;archiveSafety=[ordered]@{allowlistedTextOnly=$true;literalConnectionConfigurationHits=$coordUnsafe.Count;privateKeyOrCredentialValueHits=0;rawReceiptKeyWhitelistsChecked=$true;entireRepositoryIncluded=$false};
 archiveVerificationFile='ZIP_VERIFICATION.json (created after ZIP, outside archive to avoid self-hash recursion)';
 approvalBoundaries=[ordered]@{A01='ACCEPTED_USER_CONFIRMED_SOURCE_NOT_PRESENT_LOCALLY';A02='DEV2_REPORTED_STRUCTURAL_COMPLETION_NOT_RUNTIME_ACCEPTED';B01Architecture='APPROVED_DESIGN_ONLY';B01ExecutableDdl='REVISE';B02='REVISE';C01='REVISE';roomSharedDiffs='NOT_APPROVED';globalTotalLockOrder='NOT_SOURCE_VERIFIED';notificationRuntime='NOT_ACCEPTED'};
 progress=[ordered]@{completeUCs=26;totalUCs=75;points=60;totalPoints=256;percent=23.4375;ledgerModified=$false;UC012='PARTIAL';UC037='PARTIAL';UC139='BLOCKED_EXTERNAL';task87Started=$false};
 limitations=@('Historical receipts are not new execution results','Task79 SQL instance absent from receipts; not inferred','DEV2 accepted A01/A02/revised DDL/mapping/guard and external joint originals not locally verified','Missing current six producer writers/cancellation/conclusion, final completer and cross-user source-read are explicit dependencies','No global SQL lock/race runtime acceptance claimed');
 preflightDiagnostics=@('Unsuffixed existing handoff preserved; new _01 directory only','Command-scoped safe.directory used for read-only worktree Git, no global config changes','Relative-path supplement restored coverage of worktrees under parent obj before their source was consumed')
}
$coordValidation | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $PSScriptRoot 'HANDOFF_VALIDATION.json') -Encoding UTF8
$coordPackageFiles=@($coordMainDocs|ForEach-Object{Get-Item -LiteralPath (Join-Path $PSScriptRoot $_)})+$coordEvidenceFiles+@(Get-Item -LiteralPath (Join-Path $PSScriptRoot 'HANDOFF_VALIDATION.json'))
$coordFileManifest=@();foreach($coordItem in $coordPackageFiles){$coordRelative=$coordItem.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/');$coordFileManifest+=[ordered]@{path=$coordRelative;sha256=(Get-FileHash -LiteralPath $coordItem.FullName).Hash;bytes=$coordItem.Length}}
[ordered]@{task='COORD-FINAL-01';snapshotStatus=$coordValidation.status;worktree=$coordRoot;head=$coordPre.worktrees[0].head;branch=$coordPre.worktrees[0].branch;programSha256=$coordProgram.program.sha256;sourceIdentityManifest='evidence/CURRENT_SOURCE_IDS.json';historicalIdentityManifest='evidence/HISTORICAL_SOURCE_IDS.json';files=$coordFileManifest;manifestSelfHashOmitted=$true;excluded=@('All production source outside curated numbered excerpts','Secrets/User Secrets/appsettings values/cookies/passwords/private keys','Build outputs, binaries, repository .git','Capture/validation helper executables and preflight full inventory','Existing unsuffixed handoff output');packagePurpose='SOURCE_EVIDENCE_AND_CONTRACTS_ONLY; NOT_APPROVAL_TO_APPLY_SHARED_DIFFS_OR_RUNTIME_ACCEPTANCE'} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $PSScriptRoot 'HANDOFF_MANIFEST.json') -Encoding UTF8
$coordPackageFiles+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'HANDOFF_MANIFEST.json')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$coordZipPath=Join-Path $PSScriptRoot 'DEV1_DEV2_FINAL_HANDOFF.zip'
if(Test-Path -LiteralPath $coordZipPath){throw 'ZIP exists; never overwrite previous package'}
$coordArchive=[IO.Compression.ZipFile]::Open($coordZipPath,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($coordItem in $coordPackageFiles){$coordRelative=$coordItem.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/');if($coordRelative.Contains('..') -or !$coordItem.FullName.StartsWith($PSScriptRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Archive path outside output'};[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($coordArchive,$coordItem.FullName,$coordRelative,[IO.Compression.CompressionLevel]::Optimal)}}finally{$coordArchive.Dispose()}
$coordArchive=[IO.Compression.ZipFile]::OpenRead($coordZipPath);$coordEntryFailures=@();$coordArchiveEntries=$coordArchive.Entries.Count
try{foreach($coordExpected in $coordFileManifest){$coordEntry=$coordArchive.GetEntry($coordExpected.path);if(!$coordEntry){$coordEntryFailures+=$coordExpected.path;continue};$coordStream=$coordEntry.Open();$coordSha=[Security.Cryptography.SHA256]::Create();try{$coordHash=[BitConverter]::ToString($coordSha.ComputeHash($coordStream)).Replace('-','')}finally{$coordStream.Dispose();$coordSha.Dispose()};if($coordHash -cne $coordExpected.sha256){$coordEntryFailures+=$coordExpected.path}}}finally{$coordArchive.Dispose()}
$coordZipHash=(Get-FileHash -LiteralPath $coordZipPath).Hash
[ordered]@{recordedUtc=[DateTime]::UtcNow.ToString('o');archive=$coordZipPath;sha256=$coordZipHash;bytes=(Get-Item -LiteralPath $coordZipPath).Length;entries=$coordArchiveEntries;manifestContentHashesVerified=$coordFileManifest.Count;entryHashFailures=$coordEntryFailures;pass=($coordEntryFailures.Count -eq 0 -and $coordArchiveEntries -eq $coordPackageFiles.Count);sourceSnapshotConsistent=$coordConsistent;note='Manifest excludes its own hash; no recursive ZIP hash. This verification file stays outside the ZIP.'} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot 'ZIP_VERIFICATION.json') -Encoding UTF8
"$coordZipHash  DEV1_DEV2_FINAL_HANDOFF.zip" | Set-Content (Join-Path $PSScriptRoot 'ZIP_SHA256.txt') -Encoding ASCII
Write-Output "STATUS=$($coordValidation.status) BASELINE=$($coordBaseline.Count) CHANGED=$($coordChanged.Count) MISSING=$($coordMissing.Count) ADDED=$($coordNewInputs.Count) WORKTREES=$($coordAfterWorktrees.Count)"
Write-Output "ZIP=$coordZipPath ENTRIES=$coordArchiveEntries VERIFIED=$($coordFileManifest.Count) HASH_FAILURES=$($coordEntryFailures.Count) SHA256=$coordZipHash"
if(!$coordConsistent -or $coordDiffExit -ne 0 -or $coordEntryFailures.Count){exit 2}
