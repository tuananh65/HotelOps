$ErrorActionPreference = 'Stop'
$coordRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$coordStarted = [DateTimeOffset]::UtcNow
$coordPrevious = Join-Path $PSScriptRoot 'FINAL_DEV1_DEV2_HANDOFF_20261104_01'
$coordLabel = 'FINAL_DEV1_DEV2_HANDOFF_20261104'
$coordOrdinal = 2
do {
    $coordOutput = Join-Path $PSScriptRoot ($coordLabel + ('_{0:D2}' -f $coordOrdinal))
    $coordOrdinal++
} while (Test-Path -LiteralPath $coordOutput)

function Get-CoordWorktrees {
    $coordRaw = @(git --no-optional-locks -C $coordRoot worktree list --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate worktrees' }
    $coordEntries = @()
    $coordEntry = $null
    foreach ($coordLine in $coordRaw) {
        if ($coordLine.StartsWith('worktree ')) {
            if ($null -ne $coordEntry) { $coordEntries += $coordEntry }
            $coordEntry = [ordered]@{path=$coordLine.Substring(9); head=$null; branch=$null; flags=@()}
        } elseif ($coordLine.StartsWith('HEAD ')) { $coordEntry.head=$coordLine.Substring(5)
        } elseif ($coordLine.StartsWith('branch ')) { $coordEntry.branch=$coordLine.Substring(7).Replace('refs/heads/','')
        } elseif ($coordLine -and $null -ne $coordEntry) { $coordEntry.flags += $coordLine }
    }
    if ($null -ne $coordEntry) { $coordEntries += $coordEntry }
    foreach ($coordEntry in $coordEntries) {
        $coordTrust = 'safe.directory=' + $coordEntry.path
        $coordIndex = git --no-optional-locks -c $coordTrust -C $coordEntry.path rev-parse --path-format=absolute --git-path index
        if ($LASTEXITCODE -ne 0) { throw ('Cannot inspect index metadata: '+$coordEntry.path) }
        $coordStatus = @(git --no-optional-locks -c $coordTrust -C $coordEntry.path status --porcelain=v1 -uall)
        if ($LASTEXITCODE -ne 0) { throw ('Cannot inspect status: '+$coordEntry.path) }
        $coordEntry.indexSha256 = if (Test-Path -LiteralPath $coordIndex) { (Get-FileHash -LiteralPath $coordIndex).Hash } else { $null }
        $coordEntry.status = $coordStatus
        $coordEntry.statusLineCount = $coordStatus.Count
        $coordEntry.role = if ($coordEntry.path.Replace('/','\').TrimEnd('\') -ieq $coordRoot.TrimEnd('\')) {'CURRENT_DEV1_SOURCE'} elseif ($coordEntry.branch -eq 'ai/codex-b' -or [IO.Path]::GetFileName($coordEntry.path) -eq 'HotelOps-Codex-B') {'CODEX_B_INDEPENDENT_METADATA_ONLY'} else {'OTHER_WORKTREE_METADATA_ONLY'}
    }
    [ordered]@{recordedUtc=[DateTimeOffset]::UtcNow.ToString('o');count=$coordEntries.Count;discovery='git worktree list --porcelain; no fixed count or root list';raw=$coordRaw;worktrees=$coordEntries}
}

function Get-CoordMainInputs {
    Push-Location $coordRoot
    try {
        $coordDirs = @('src','docs','scripts','database') | Where-Object { Test-Path -LiteralPath $_ }
        $coordPaths = @(rg --files --hidden --no-ignore @coordDirs -g '!**/bin/**' -g '!**/obj/**' -g '!**/artifacts/**' -g '!**/node_modules/**' -g '!**/packages/**' -g '!scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104*/**')
        if ($LASTEXITCODE -ne 0) { throw 'Main input enumeration failed' }
        if (Test-Path -LiteralPath 'AGENTS.md') { $coordPaths += 'AGENTS.md' }
        foreach ($coordPath in ($coordPaths | Sort-Object -Unique)) {
            $coordFile = Get-Item -LiteralPath $coordPath
            [ordered]@{path=$coordPath.Replace('\','/');sha256=(Get-FileHash -LiteralPath $coordFile.FullName).Hash;bytes=$coordFile.Length}
        }
    } finally { Pop-Location }
}

function Write-CoordJson($coordPath, $coordValue) {
    $coordValue | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $coordPath -Encoding UTF8
}

$coordPre = Get-CoordWorktrees
$coordInputsBefore = @(Get-CoordMainInputs)
$coordPriorManifest = Get-Content -LiteralPath (Join-Path $coordPrevious 'HANDOFF_MANIFEST.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($coordFile in $coordPriorManifest.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $coordPrevious $coordFile.path)).Hash -cne $coordFile.sha256) { throw ('Previous package file changed: '+$coordFile.path) }
}
$coordPriorProtected = @(Get-ChildItem -LiteralPath $PSScriptRoot -Directory | Where-Object Name -like ($coordLabel+'*') | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse } | ForEach-Object { [ordered]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$coordIds = Get-Content -LiteralPath (Join-Path $coordPrevious 'evidence/CURRENT_SOURCE_IDS.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($coordId in $coordIds) {
    if (!(Test-Path -LiteralPath $coordId.absolutePath) -or (Get-FileHash -LiteralPath $coordId.absolutePath).Hash -cne $coordId.sha256) { throw ('Relied-on current source differs from prior capture; review before reuse: '+$coordId.path) }
}
$coordCs = @(rg --files (Join-Path $coordRoot 'src/HotelOps.Api') -g '*.cs' -g '!**/obj/**' -g '!**/bin/**')
if ($coordCs.Count -ne $coordIds.Count) { throw 'C# source set changed; refresh evidence before packaging' }
$coordMainBefore = @($coordPre.worktrees | Where-Object role -eq 'CURRENT_DEV1_SOURCE')[0]
if ($coordMainBefore.head -cne $coordPriorManifest.head -or $coordMainBefore.branch -cne $coordPriorManifest.branch) { throw 'Current DEV1 branch/HEAD changed; review required' }

[void](New-Item -ItemType Directory -Path $coordOutput)
[void](New-Item -ItemType Directory -Path (Join-Path $coordOutput 'evidence'))
$coordDocs = @('DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md','DEV1_CURRENT_SOURCE_EVIDENCE_MANIFEST.md','DEV1_NOTIFICATION_INTEGRATION_CONTRACT.md','DEV1_ROOM_WORKDAY_SHARED_CONTRACT.md','DEV1_GLOBAL_LOCK_AND_AUTHORITY_MATRIX.md','DEV1_SHARED_APPROVAL_AND_ACCEPTANCE_MATRIX.md')
foreach ($coordFile in Get-ChildItem -LiteralPath (Join-Path $coordPrevious 'evidence') -File) { Copy-Item -LiteralPath $coordFile.FullName -Destination (Join-Path $coordOutput 'evidence') }
$coordParallel = @'
## Quy tắc song song bổ sung — có hiệu lực cho bản handoff này

Codex B đang làm các UC DEV1 độc lập trong worktree riêng. COORD-FINAL-01 chỉ kiểm chứng current source của worktree DEV1 chính và shared contracts liên quan DEV2. Không bắt đầu implementation thuộc Codex B; không lấy source từ worktree B làm current runtime DEV1.

Danh sách worktrees được khám phá lại bằng `git worktree list --porcelain` tại cả preflight và postflight, không dùng số lượng hoặc danh sách đường dẫn cố định. `evidence/PARALLEL_PREFLIGHT.json` và `evidence/PARALLEL_POSTFLIGHT.json` ghi exact path, HEAD, branch, index SHA256 và status từng worktree. Bản này chỉ đọc metadata Git ở các worktree ngoài DEV1 chính; không capture lại source của Codex B.

Không sửa/reset/clean/checkout/chuyển branch/đồng bộ/di chuyển/xóa worktree Codex B. Không ghi Git config hoặc index. Hoạt động đồng thời của B, nếu có, được ghi riêng; không nhận là thay đổi do COORD-FINAL-01 và không tuyên bố đã kiểm chứng implementation của B.

Trước tích hợp Codex B phải có bàn giao exact diff, base HEAD/branch, danh sách file và SHA256; review conflicts với toàn bộ local patches DEV1/shared contracts hiện tại; xin approval riêng cho phạm vi tích hợp cụ thể. Chỉ tích hợp và chạy verification phù hợp sau approval. Cùng HEAD không có nghĩa cùng working-tree source. Không tự động đồng bộ branch hoặc áp dụng diff của B.

Các kết luận về tìm kiếm ở worktrees trong bản capture trước chỉ là evidence lịch sử. Bản handoff này không khẳng định source của worktree B vẫn sạch hoặc không có publisher tại thời điểm B đang làm việc. Source/runtime được bàn giao vẫn là current DEV1 chính, đã đối chiếu hash trước/sau. Thay đổi source được dựa vào trong DEV1 chính phải dẫn tới `SNAPSHOT_INCONSISTENT`; thay đổi metadata của B chỉ được ghi nhận riêng khi không làm thay đổi source được dựa vào.

Không thay đổi approval boundary: business/architecture decisions đã chốt không tự cấp quyền áp dụng shared DDL/EF/DI/RBAC/coordinator. DEV2 có thể triển khai các thành phần nội bộ thuộc ownership theo contract; shared diff vẫn phải có file-specific review và approval riêng.
'@
foreach ($coordDoc in $coordDocs) {
    $coordText = Get-Content -LiteralPath (Join-Path $coordPrevious $coordDoc) -Raw -Encoding UTF8
    if ($coordDoc -eq 'DEV1_CURRENT_SOURCE_EVIDENCE_MANIFEST.md') {
        $coordCut = $coordText.IndexOf('## Program.cs')
        if ($coordCut -lt 0) { throw 'Source manifest boundary missing' }
        $coordHeader = @"
# Current source evidence — DEV1; parallel-rule revision

Worktree: $coordRoot. Branch: $($coordMainBefore.branch). HEAD: $($coordMainBefore.head).

Bản này được tạo trong thư mục suffix mới, giữ nguyên toàn bộ các gói trước. $($coordIds.Count) source identities C# và toàn bộ evidence trong gói trước đã đối chiếu hash trước khi sử dụng lại; không lấy source của Codex B. Số lượng worktrees không hardcode; exact inventory và status trước/sau nằm ngay trong ZIP, ở evidence/PARALLEL_PREFLIGHT.json và evidence/PARALLEL_POSTFLIGHT.json.

Baseline hiện tại chỉ bao gồm input src/docs/scripts/database/AGENTS của DEV1 chính, loại build outputs và các thư mục handoff. Toàn bộ artifacts Task82–86 nằm trong scope hash này; các gói handoff cũ có hash riêng để kiểm bảo toàn. Không build/test/SQL/HTTPS trong coordination task. Capture lịch sử của gói _01 được giữ nguyên ngoài bản mới; không dùng số đếm lịch sử của nó làm inventory hiện tại.

"@
        $coordText = $coordHeader + "`r`n" + $coordText.Substring($coordCut)
    }
    $coordText = $coordText.Replace('năm worktree đã đọc','các worktrees đã đọc trong capture trước (không phải rescan source Codex B hiện tại)')
    $coordText = $coordText.Replace('trong các worktrees đã đọc','trong các worktrees đã đọc tại capture trước; bản này chỉ xác minh lại source DEV1 chính')
    ($coordText.TrimEnd()+"`r`n`r`n"+$coordParallel+"`r`n") | Set-Content -LiteralPath (Join-Path $coordOutput $coordDoc) -Encoding UTF8
}
Write-CoordJson (Join-Path $coordOutput 'evidence/PARALLEL_PREFLIGHT.json') $coordPre
Write-CoordJson (Join-Path $coordOutput 'evidence/MAIN_INPUT_HASHES_PREFLIGHT.json') $coordInputsBefore
Write-CoordJson (Join-Path $coordOutput 'evidence/REVISION_PROVENANCE.json') ([ordered]@{task='COORD-FINAL-01';reason='User parallel-work amendment: Codex B independent ownership; dynamic pre/post worktrees; separate integration approval';previousPackage=$coordPrevious;previousZipSha256=(Get-FileHash -LiteralPath (Join-Path $coordPrevious 'DEV1_DEV2_FINAL_HANDOFF.zip')).Hash;sourceIdsRevalidated=$coordIds.Count;previousManifestEntriesVerified=$coordPriorManifest.files.Count;previousPackagesPreservedFiles=$coordPriorProtected.Count;sourceReusedOnlyAfterHashEquality=$true;outsideMainWorktreeSourceRecaptured=$false;taskDateLabel='20261104';actualCaptureUtc=$coordStarted.ToString('o');timezone='Asia/Saigon'})

$coordInputsAfter = @(Get-CoordMainInputs)
$coordInputMap = @{}
foreach ($coordFile in $coordInputsBefore) { $coordInputMap[$coordFile.path]=$coordFile.sha256 }
$coordChanged = @(); $coordAdded = @()
foreach ($coordFile in $coordInputsAfter) {
    if (!$coordInputMap.ContainsKey($coordFile.path)) { $coordAdded += $coordFile.path }
    elseif ($coordInputMap[$coordFile.path] -cne $coordFile.sha256) { $coordChanged += $coordFile.path }
}
$coordAfterPaths = @{}; foreach ($coordFile in $coordInputsAfter) { $coordAfterPaths[$coordFile.path]=$true }
$coordMissing = @($coordInputsBefore | Where-Object { !$coordAfterPaths.ContainsKey($_.path) } | ForEach-Object path)
$coordPriorChanged = @($coordPriorProtected | Where-Object { !(Test-Path -LiteralPath $_.path) -or (Get-FileHash -LiteralPath $_.path).Hash -cne $_.sha256 } | ForEach-Object path)
$coordPost = Get-CoordWorktrees
Write-CoordJson (Join-Path $coordOutput 'evidence/PARALLEL_POSTFLIGHT.json') $coordPost
$coordPreMap = @{}; foreach ($coordW in $coordPre.worktrees) { $coordPreMap[$coordW.path]=$coordW }
$coordComparisons = @()
foreach ($coordW in $coordPost.worktrees) {
    $coordBefore = $coordPreMap[$coordW.path]
    $coordComparisons += [ordered]@{path=$coordW.path;role=$coordW.role;previouslyPresent=($null -ne $coordBefore);headPreserved=($coordBefore.head -ceq $coordW.head);branchPreserved=($coordBefore.branch -ceq $coordW.branch);indexPreserved=($coordBefore.indexSha256 -ceq $coordW.indexSha256);statusSame=(($coordBefore.status -join "`n") -ceq ($coordW.status -join "`n"));preStatusLines=$coordBefore.statusLineCount;postStatusLines=$coordW.statusLineCount}
}
$coordPathsSame = ((@($coordPre.worktrees.path | Sort-Object) -join "`n") -ceq (@($coordPost.worktrees.path | Sort-Object) -join "`n"))
$coordMainComparison = @($coordComparisons | Where-Object role -eq 'CURRENT_DEV1_SOURCE')[0]
$coordMainStatusPre = @($coordMainBefore.status | Where-Object { $_ -notlike '*scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104*' })
$coordMainStatusPost = @(@($coordPost.worktrees | Where-Object role -eq 'CURRENT_DEV1_SOURCE')[0].status | Where-Object { $_ -notlike '*scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104*' })
$coordMainStatusSame = (($coordMainStatusPre -join "`n") -ceq ($coordMainStatusPost -join "`n"))
git --no-optional-locks -C $coordRoot diff --check
$coordDiffExit = $LASTEXITCODE
$coordConsistent = ($coordChanged.Count -eq 0 -and $coordAdded.Count -eq 0 -and $coordMissing.Count -eq 0 -and $coordPriorChanged.Count -eq 0 -and $coordPathsSame -and $coordMainComparison.headPreserved -and $coordMainComparison.branchPreserved -and $coordMainComparison.indexPreserved -and $coordMainStatusSame)
$coordParallelChanges = @($coordComparisons | Where-Object { $_.role -ne 'CURRENT_DEV1_SOURCE' -and (!$_.headPreserved -or !$_.branchPreserved -or !$_.indexPreserved -or !$_.statusSame) })
$coordOldValidation = Get-Content -LiteralPath (Join-Path $coordPrevious 'HANDOFF_VALIDATION.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$coordValidation = [ordered]@{task='COORD-FINAL-01';revision='PARALLEL_WORK_RULES';status=if($coordConsistent -and $coordDiffExit -eq 0){'DONE_CONSOLIDATED_HANDOFF_NOT_RUNTIME_ACCEPTANCE'}else{'SNAPSHOT_INCONSISTENT'};startedUtc=$coordStarted.ToString('o');recordedUtc=[DateTimeOffset]::UtcNow.ToString('o');elapsedSeconds=[Math]::Round(([DateTimeOffset]::UtcNow-$coordStarted).TotalSeconds,1);timezone='Asia/Saigon';sourceSnapshotConsistent=$coordConsistent;mainInputHashCount=$coordInputsBefore.Count;changedMainInputs=$coordChanged;newMainInputs=$coordAdded;missingMainInputs=$coordMissing;previousOutputFilesVerified=$coordPriorProtected.Count;previousOutputsChanged=$coordPriorChanged;currentMainCSharpIdentities=$coordIds.Count;worktreeCountPreflight=$coordPre.count;worktreeCountPostflight=$coordPost.count;worktreePathSetPreserved=$coordPathsSame;worktrees=$coordComparisons;mainStatusPreservedExcludingNewHandoff=$coordMainStatusSame;externalParallelMetadataChanges=$coordParallelChanges;outsideMainSourceRescanned=$false;parallelPolicy='Metadata only outside main; no Codex B implementation/integration; exact diff + conflict review + separate approval required';programIdentity=$coordOldValidation.programIdentity;sourceEditsByTask=0;programEditsByTask=0;databaseConnections=0;schemaEditsByTask=0;rbacEditsByTask=0;gitMutationsByTask=0;worktreeMutationsByTask=0;branchSyncs=0;build='NOT_RUN';frameworkTests='NOT_RUN';sql='NOT_RUN';https='NOT_RUN';diffCheckExit=$coordDiffExit;progress=$coordOldValidation.progress;approvalBoundaries=$coordOldValidation.approvalBoundaries;limitations=$coordOldValidation.limitations;fullOriginalCoordinationElapsedSeconds=$coordOldValidation.elapsedSeconds;elapsedNote='Revision elapsed excludes earlier consolidation; historical receipts are not rerun';sixMainDocuments=$coordDocs}
Write-CoordJson (Join-Path $coordOutput 'HANDOFF_VALIDATION.json') $coordValidation
if (!$coordConsistent -or $coordDiffExit -ne 0) { throw ('SNAPSHOT_INCONSISTENT; see '+$coordOutput) }

$coordFiles = @($coordDocs | ForEach-Object { Get-Item -LiteralPath (Join-Path $coordOutput $_) }) + @(Get-ChildItem -LiteralPath (Join-Path $coordOutput 'evidence') -File) + @(Get-Item -LiteralPath (Join-Path $coordOutput 'HANDOFF_VALIDATION.json'))
$coordEntries = @()
foreach ($coordFile in $coordFiles) {
    if ($coordFile.Extension -cnotin @('.md','.json','.txt','.diff')) { throw 'Non-text archive entry rejected' }
    $coordText = [IO.File]::ReadAllText($coordFile.FullName)
    if ($coordText -match '(?i)(?:Data Source|Initial Catalog|Server)\s*=|-----BEGIN .*PRIVATE KEY|(?:password|access_token|refresh_token|api[_-]?key)"\s*:\s*"[^"\r\n]+"') { throw ('Unsafe archive text: '+$coordFile.Name) }
    $coordEntries += [ordered]@{path=$coordFile.FullName.Substring($coordOutput.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $coordFile.FullName).Hash;bytes=$coordFile.Length}
}
$coordManifest = [ordered]@{task='COORD-FINAL-01';revision='PARALLEL_WORK_RULES';snapshotStatus=$coordValidation.status;worktree=$coordRoot;branch=$coordMainBefore.branch;head=$coordMainBefore.head;programSha256=$coordPriorManifest.programSha256;sourceIdentityManifest='evidence/CURRENT_SOURCE_IDS.json';historicalIdentityManifest='evidence/HISTORICAL_SOURCE_IDS.json';files=$coordEntries;manifestSelfHashOmitted=$true;packagePurpose='SOURCE_EVIDENCE_AND_CONTRACTS_ONLY; NOT_APPROVAL_TO_APPLY_SHARED_DIFFS_OR_RUNTIME_ACCEPTANCE';excluded=@('Secrets/config values/credentials','Whole repository and binaries','Codex B source and implementation','Helper scripts and previous ZIP packages');archiveSafety='Allowlisted text; literal connection/credential/private-key scan passed';previousPackageUnchanged=$true}
Write-CoordJson (Join-Path $coordOutput 'HANDOFF_MANIFEST.json') $coordManifest
$coordFiles += Get-Item -LiteralPath (Join-Path $coordOutput 'HANDOFF_MANIFEST.json')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$coordZip = Join-Path $coordOutput 'DEV1_DEV2_FINAL_HANDOFF.zip'
$coordArchive = [IO.Compression.ZipFile]::Open($coordZip,[IO.Compression.ZipArchiveMode]::Create)
try { foreach ($coordFile in $coordFiles) { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($coordArchive,$coordFile.FullName,$coordFile.FullName.Substring($coordOutput.Length+1).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal) } } finally { $coordArchive.Dispose() }
$coordVerifyEntries = @($coordEntries) + @([ordered]@{path='HANDOFF_MANIFEST.json';sha256=(Get-FileHash -LiteralPath (Join-Path $coordOutput 'HANDOFF_MANIFEST.json')).Hash;bytes=(Get-Item -LiteralPath (Join-Path $coordOutput 'HANDOFF_MANIFEST.json')).Length})
$coordArchive = [IO.Compression.ZipFile]::OpenRead($coordZip)
$coordFailures = @(); $coordArchiveCount = $coordArchive.Entries.Count
try {
    foreach ($coordExpected in $coordVerifyEntries) {
        $coordEntry = $coordArchive.GetEntry($coordExpected.path)
        if (!$coordEntry -or $coordEntry.Length -ne $coordExpected.bytes) { $coordFailures += $coordExpected.path; continue }
        $coordStream=$coordEntry.Open(); $coordSha=[Security.Cryptography.SHA256]::Create()
        try { $coordHash=[BitConverter]::ToString($coordSha.ComputeHash($coordStream)).Replace('-','') } finally { $coordStream.Dispose(); $coordSha.Dispose() }
        if ($coordHash -cne $coordExpected.sha256) { $coordFailures += $coordExpected.path }
    }
} finally { $coordArchive.Dispose() }
$coordZipHash=(Get-FileHash -LiteralPath $coordZip).Hash
$coordZipPass=($coordFailures.Count -eq 0 -and $coordArchiveCount -eq $coordVerifyEntries.Count)
Write-CoordJson (Join-Path $coordOutput 'ZIP_VERIFICATION.json') ([ordered]@{recordedUtc=[DateTimeOffset]::UtcNow.ToString('o');archive=$coordZip;sha256=$coordZipHash;bytes=(Get-Item -LiteralPath $coordZip).Length;entries=$coordArchiveCount;entriesByteAndHashVerified=$coordVerifyEntries.Count;failures=$coordFailures;pass=$coordZipPass;manifestVerified=$true;sourceSnapshotConsistent=$coordConsistent;note='Outside ZIP to avoid recursive self hashing'})
"$coordZipHash  DEV1_DEV2_FINAL_HANDOFF.zip" | Set-Content -LiteralPath (Join-Path $coordOutput 'ZIP_SHA256.txt') -Encoding ASCII
if (!$coordZipPass) { throw 'ZIP verification failed' }
Write-Output "OUTPUT=$coordOutput"
Write-Output "STATUS=$($coordValidation.status) MAIN_INPUTS=$($coordInputsBefore.Count) SOURCE_IDS=$($coordIds.Count) OLD_OUTPUTS_PRESERVED=$($coordPriorProtected.Count)"
Write-Output "WORKTREES_PRE=$($coordPre.count) POST=$($coordPost.count) EXTERNAL_METADATA_CHANGES=$($coordParallelChanges.Count) DIFF_CHECK=$coordDiffExit"
Write-Output "ZIP_ENTRIES=$coordArchiveCount HASH_FAILURES=$($coordFailures.Count) SHA256=$coordZipHash ELAPSED_SECONDS=$($coordValidation.elapsedSeconds)"
