$ErrorActionPreference='Stop'
$coordRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$coordStarted=[DateTimeOffset]::UtcNow
$coordName='CODEX_B_UC013_READONLY_HANDOFF_20261105'
$coordOut=Join-Path $PSScriptRoot $coordName
$coordSuffix=1
while(Test-Path -LiteralPath $coordOut){$coordOut=Join-Path $PSScriptRoot ($coordName+('_'+$coordSuffix));$coordSuffix++}
function Save-CoordJson($coordPath,$coordValue){$coordValue|ConvertTo-Json -Depth 18|Set-Content -LiteralPath $coordPath -Encoding UTF8}
function Get-CoordTrees {
 $coordRaw=@(git --no-optional-locks -C $coordRoot worktree list --porcelain)
 if($LASTEXITCODE -ne 0){throw 'Worktree discovery failed'}
 $coordResult=@();$coordW=$null
 foreach($coordLine in $coordRaw){
  if($coordLine.StartsWith('worktree ')){if($null -ne $coordW){$coordResult+=$coordW};$coordW=[ordered]@{path=$coordLine.Substring(9);head=$null;branch=$null;flags=@()}}
  elseif($coordLine.StartsWith('HEAD ')){$coordW.head=$coordLine.Substring(5)}
  elseif($coordLine.StartsWith('branch ')){$coordW.branch=$coordLine.Substring(7).Replace('refs/heads/','')}
  elseif($coordLine -and $null -ne $coordW){$coordW.flags+=$coordLine}
 }
 if($null -ne $coordW){$coordResult+=$coordW}
 foreach($coordW in $coordResult){
  $coordTrust='safe.directory='+$coordW.path
  $coordIndex=git --no-optional-locks -c $coordTrust -C $coordW.path rev-parse --path-format=absolute --git-path index
  if($LASTEXITCODE -ne 0){throw 'Cannot inspect worktree index'}
  $coordW.indexSha256=if(Test-Path -LiteralPath $coordIndex){(Get-FileHash -LiteralPath $coordIndex).Hash}else{$null}
  $coordW.status=@(git --no-optional-locks -c $coordTrust -C $coordW.path status --porcelain=v1 -uall)
  if($LASTEXITCODE -ne 0){throw 'Cannot inspect worktree status'}
 }
 [ordered]@{recordedUtc=[DateTimeOffset]::UtcNow.ToString('o');count=$coordResult.Count;discovery='git worktree list --porcelain (dynamic)';worktrees=$coordResult}
}
function Get-CoordHashes {
 Push-Location $coordRoot
 try{
  $coordPaths=@(rg --files --hidden --no-ignore src docs scripts database -g '!**/bin/**' -g '!**/obj/**' -g '!**/node_modules/**' -g '!**/artifacts/**' -g '!**/packages/**')+@('AGENTS.md')
  if($LASTEXITCODE -ne 0){throw 'Input scan failed'}
  foreach($coordPath in ($coordPaths|Sort-Object -Unique)){
   $coordFile=Get-Item -LiteralPath $coordPath
   if($coordFile.FullName.StartsWith($coordOut+'\',[StringComparison]::OrdinalIgnoreCase)){continue}
   [ordered]@{path=$coordPath.Replace('\','/');sha256=(Get-FileHash -LiteralPath $coordFile.FullName).Hash}
  }
 }finally{Pop-Location}
}
$coordPre=Get-CoordTrees
$coordBefore=@(Get-CoordHashes)
$coordLedgerPath='scripts/verification/Task80/UC_LEDGER_TASK80.json'
$coordLedger=Get-Content -LiteralPath (Join-Path $coordRoot $coordLedgerPath) -Raw -Encoding UTF8|ConvertFrom-Json
$coordPlanPath='scripts/verification/DEV1_21_DAY_DELIVERY_PLAN.md'
$coordPlan=Get-Content -LiteralPath (Join-Path $coordRoot $coordPlanPath) -Raw -Encoding UTF8
$coordSectionMap=@{}
$coordSections=[regex]::Matches($coordPlan,'(?ms)^## (\d+)\.([^\r\n]*)\r?\n(.*?)(?=^## \d+\.|\z)')
$coordSnapshot='cf8fb020362f8f64192e7f2f14a10547cebe5b91'
foreach($coordSection in $coordSections){
 $coordNumber=$coordSection.Groups[1].Value
 $coordRefs=@()
 foreach($coordMatch in [regex]::Matches($coordSection.Value,'\[[^\]]*\]\(([^)\s]+\.json)\)')){
  $coordRefFull=[IO.Path]::GetFullPath((Join-Path (Join-Path $coordRoot 'scripts/verification') $coordMatch.Groups[1].Value))
  if(!$coordRefFull.StartsWith($coordRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Reference outside repo'}
  $coordRef=$coordRefFull.Substring($coordRoot.Length+1).Replace('\','/')
  $coordTreeLine=git --no-optional-locks -C $coordRoot ls-tree $coordSnapshot -- $coordRef
  if($LASTEXITCODE -ne 0){throw 'Cannot inspect historical reference'}
  $coordExistsInGit=($coordTreeLine -match '^\d+ blob ([a-f0-9]{40})\s')
  $coordBlob=if($coordExistsInGit){$matches[1]}else{$null}
  $coordRefs+=[ordered]@{path=$coordRef;existsInMain=(Test-Path -LiteralPath $coordRefFull);historicalCommit=if($coordExistsInGit){$coordSnapshot}else{$null};historicalBlob=if($coordExistsInGit){$coordBlob}else{$null};relationship='Linked receipt in historical plan section; may cover multiple UCs/regressions, not independently re-executed'}
 }
 $coordSectionMap[$coordNumber]=$coordRefs
}
$coordRows=@()
foreach($coordRow in $coordLedger.rows){
 $coordSection=[regex]::Match($coordRow.evidenceSummary,'§\s*(\d+)')
 $coordSectionSource='LEDGER_EVIDENCE_SUMMARY'
 if(!$coordSection.Success){
  $coordPlanRow=[regex]::Match($coordPlan,'(?m)^\|\s*'+[regex]::Escape($coordRow.uc)+'\s*\|[^\r\n]*$')
  if($coordPlanRow.Success){$coordSection=[regex]::Match($coordPlanRow.Value,'§\s*(\d+)');$coordSectionSource='MATCHING_UC_ROW_IN_DELIVERY_PLAN'}
 }
 $coordRefs=@();if($coordSection.Success -and $coordSectionMap.ContainsKey($coordSection.Groups[1].Value)){$coordRefs=@($coordSectionMap[$coordSection.Groups[1].Value])}
 $coordRows+=[ordered]@{ucId=$coordRow.uc;title=$coordRow.title;status=$coordRow.classification;pointWeight=$coordRow.weight;creditedPoints=$coordRow.verifiedPoints;sourceLedger=$coordLedgerPath;sourceEvidenceSummary=$coordRow.evidenceSummary;planSection=if($coordSection.Success){[int]$coordSection.Groups[1].Value}else{$null};planSectionReferenceSource=if($coordSection.Success){$coordSectionSource}else{$null};receiptReferences=$coordRefs;receiptReferenceStatus=if($coordRefs.Count){'HISTORICAL_REFERENCES_RESOLVED_NOT_RERUN'}else{'PENDING_EVIDENCE_NO_EXACT_RECEIPT_PATH_IN_LEDGER_ROW'};runtimeAcceptanceThisTask='NOT_RUN'}
}
$coordTotal=0;$coordCredit=0;$coordComplete=0
foreach($coordRow in $coordRows){$coordTotal+=$coordRow.pointWeight;$coordCredit+=$coordRow.creditedPoints;if($coordRow.status -like 'COMPLETE_VERIFIED*'){$coordComplete++}}
$coordUc013=@($coordRows|Where-Object ucId -eq 'UC-013')[0]
if($coordRows.Count -ne 75 -or $coordTotal -ne 256 -or $coordCredit -ne 60 -or $coordComplete -ne 26){throw 'Unexpected ledger totals; review before exporting'}
[void](New-Item -ItemType Directory -Path $coordOut)
Save-CoordJson (Join-Path $coordOut 'PREFLIGHT.json') $coordPre
Save-CoordJson (Join-Path $coordOut 'PROTECTED_INPUT_HASHES.json') $coordBefore
Save-CoordJson (Join-Path $coordOut 'DEV1_LEDGER_EXPORT.json') ([ordered]@{source=$coordLedgerPath;sourceSha256=(Get-FileHash -LiteralPath (Join-Path $coordRoot $coordLedgerPath)).Hash;corroboration=@('scripts/verification/Task83/LEDGER_RECONCILIATION.json','scripts/verification/Task84/LEDGER_CHECK.json','scripts/verification/Task85/PhaseB/TASK85B_RUNTIME_PROGRESS.md','scripts/verification/Task86/TASK86_DEV1_PROGRESS.md');ucCount=$coordRows.Count;weightTotal=$coordTotal;creditedTotal=$coordCredit;completeCount=$coordComplete;percent=100*$coordCredit/$coordTotal;ledgerModified=$false;uc013=$coordUc013;requestedUncreditedConfirmation='CONTRADICTED_BY_LEDGER_UC013_ALREADY_CREDITED_2';receiptSemantics='Exact paths resolved from linked historical plan sections. Empty references explicitly mean missing per-row receipt provenance, not failed acceptance or no historical credit. Runtime coverage is not inferred from ledger status.';rows=$coordRows})
$coordRows|ForEach-Object{[pscustomobject]@{ucId=$_.ucId;title=$_.title;status=$_.status;pointWeight=$_.pointWeight;creditedPoints=$_.creditedPoints;receiptReferences=($_.receiptReferences|ConvertTo-Json -Compress -Depth 7);receiptReferenceStatus=$_.receiptReferenceStatus;sourceLedger=$_.sourceLedger;sourceEvidenceSummary=$_.sourceEvidenceSummary}}|Export-Csv -LiteralPath (Join-Path $coordOut 'DEV1_LEDGER_EXPORT.csv') -NoTypeInformation -Encoding UTF8

$coordSourcePaths=@(
 'src/HotelOps.Api/DTOs/HotelStructure/BranchStructureDtos.cs',
 'src/HotelOps.Api/Security/Authorization/CurrentUserAccessor.cs',
 'src/HotelOps.Api/Security/Authorization/PermissionService.cs',
 'src/HotelOps.Api/Security/Authorization/PermissionDataSource.cs',
 'src/HotelOps.Api/Security/Authorization/PermissionAuthorizationHandler.cs',
 'src/HotelOps.Api/Security/Authorization/IPermissionService.cs',
 'src/HotelOps.Api/Services/Users/UserEligibilityLock.cs',
 'src/HotelOps.Api/Services/Authentication/SqlSessionService.cs',
 'src/HotelOps.Api/Services/HotelStructure/RoomTypeDirectoryService.cs',
 'src/HotelOps.Api/Controllers/RoomTypeDirectoryController.cs',
 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistoryBoundary.cs',
 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistorySchema.cs',
 'src/HotelOps.Api/Services/HotelStructure/RoomTypeDirectorySchema.cs',
 'src/HotelOps.Api/Services/HotelStructure/BranchAdministrationService.cs',
 'src/HotelOps.Api/Services/HotelStructure/HotelStructureCreationService.cs',
 'src/HotelOps.Api/Data/HotelOpsDbContext.cs','src/HotelOps.Api/Program.cs',
 'src/HotelOps.Api/Entities/Branch.cs','src/HotelOps.Api/Entities/Floor.cs','src/HotelOps.Api/Entities/Area.cs','src/HotelOps.Api/Entities/Room.cs','src/HotelOps.Api/Entities/Department.cs'
)
$coordSourceInventory=@()
foreach($coordPath in $coordSourcePaths){
 $coordFull=Join-Path $coordRoot $coordPath;$coordLines=Get-Content -LiteralPath $coordFull -Encoding UTF8
 $coordSymbols=@()
 for($coordLineN=0;$coordLineN -lt $coordLines.Count;$coordLineN++){
  if($coordLines[$coordLineN] -match '^\s*(public|private|internal|protected)\s+.*\b(class|interface|record)\b|^\s*(public|private|internal|protected)\s+(?:(?:static|async|sealed|override)\s+)*(?:Task(?:<.*>)?|bool|void|string\[\]|\(string[^)]*\))\s+\w+\('){$coordSymbols+=[ordered]@{line=($coordLineN+1);signatureLine=$coordLines[$coordLineN].Trim()}}
 }
 $coordSourceInventory+=[ordered]@{path=$coordPath;absolutePath=$coordFull;sha256=(Get-FileHash -LiteralPath $coordFull).Hash;lineCount=$coordLines.Count;gitStatus=@(git --no-optional-locks -C $coordRoot status --porcelain=v1 -- $coordPath);symbols=$coordSymbols;sourceTransferred=$false}
}
Save-CoordJson (Join-Path $coordOut 'REUSABLE_SOURCE_INVENTORY.json') $coordSourceInventory
$coordHistoricalInventory=@()
foreach($coordPath in @('src/HotelOps.Api/Services/HotelStructure/BranchStructureService.cs','src/HotelOps.Api/Controllers/BranchStructureController.cs','scripts/verification/HotelStructure/TASK64_UC013_IMPLEMENTATION.md','scripts/verification/HotelStructure/task64-accepted-final2.local.json')){
 $coordBlob=git --no-optional-locks -C $coordRoot rev-parse --verify ($coordSnapshot+':'+$coordPath)
 if($LASTEXITCODE -ne 0){throw 'Missing expected historical UC013 evidence'}
 $coordHistoricalLines=@(git --no-optional-locks -C $coordRoot show ($coordSnapshot+':'+$coordPath))
 $coordHistoricalSymbols=@();for($coordLineN=0;$coordLineN -lt $coordHistoricalLines.Count;$coordLineN++){if($coordHistoricalLines[$coordLineN] -match 'class |public .*ReadAsync|public .*Get|async Task.*Authorize|MASTER_DATA_MANAGE|ROOM_VIEW'){$coordHistoricalSymbols+=[ordered]@{line=($coordLineN+1);signatureLine=$coordHistoricalLines[$coordLineN].Trim()}}}
 $coordHistoricalInventory+=[ordered]@{path=$coordPath;commit=$coordSnapshot;gitBlob=$coordBlob;existsInCurrentMain=(Test-Path -LiteralPath (Join-Path $coordRoot $coordPath));symbols=$coordHistoricalSymbols;scope='IMMUTABLE_HISTORICAL_ONLY_NOT_RESTORED'}
 if($coordPath.EndsWith('task64-accepted-final2.local.json')){
  $coordReceipt=($coordHistoricalLines -join "`n")|ConvertFrom-Json
  Save-CoordJson (Join-Path $coordOut 'UC013_HISTORICAL_RECEIPT_SUMMARY.json') ([ordered]@{commit=$coordSnapshot;path=$coordPath;gitBlob=$coordBlob;structureReadChecks=$coordReceipt.structureReadChecks;structureReadWaitCount=@($coordReceipt.structureReadWaits).Count;structureReadFaults=$coordReceipt.structureReadFaults;http=$coordReceipt.http;rerun=$false;note='159 UC013 assertions is a historical receipt component, not new runtime acceptance. Combined receipt also contains UC014 checks; counts must not be double credited.'})
 }
}
Save-CoordJson (Join-Path $coordOut 'HISTORICAL_UC013_IDENTITIES.json') $coordHistoricalInventory
$coordCatalogPath='scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql'
$coordCatalog=Get-Content -LiteralPath (Join-Path $coordRoot $coordCatalogPath) -Encoding UTF8
$coordGrants=@();$coordPermissions=@()
for($coordLineN=0;$coordLineN -lt $coordCatalog.Count;$coordLineN++){
 $coordLine=$coordCatalog[$coordLineN].ToString()
 if($coordLine -match "^\('([^']+)','([^']+)','(ALL|BRANCH|OWN)'\)"){$coordGrants+=[ordered]@{role=$matches[1];permission=$matches[2];scope=$matches[3];line=$coordLineN+1}}
 if($coordLine -match "^\('(ROOM_VIEW|MASTER_DATA_MANAGE)',N'"){$coordPermissions+=[ordered]@{permission=$matches[1];line=$coordLineN+1;definition=$coordLine}}
}
Save-CoordJson (Join-Path $coordOut 'UC013_PERMISSION_EVIDENCE.json') ([ordered]@{catalogPath=$coordCatalogPath;catalogSha256=(Get-FileHash -LiteralPath (Join-Path $coordRoot $coordCatalogPath)).Hash;release='2026-10-21 catalog; 12 roles / 62 permissions / 140 grants per release assertions';parsedGrantCount=$coordGrants.Count;permissions=$coordPermissions;adminAndOmGrants=@($coordGrants|Where-Object{$_.role -in @('SYS_ADMIN','OPERATIONS_MANAGER')});relevantRoomAndMasterGrants=@($coordGrants|Where-Object{$_.permission -in @('ROOM_VIEW','MASTER_DATA_MANAGE')});uc013Binding='Task64 recorded developer-approved ASSUMED_V1: independent ROOM_VIEW basic physical read OR MASTER_DATA_MANAGE management read; no grant edits';dedicatedUc013Permission='NONE_FOUND';liveDatabaseGrants='PENDING_EVIDENCE_NOT_QUERIED';effectiveIndividualUserGrants='PENDING_EVIDENCE_NO_USER_OR_SQL_SESSION_EVALUATED';own='Not sufficient for branch read; requires verified ownership resource; no UC013 owner binding';noRoleNameBypass=$true})

$coordOwnership=@()
$coordRequirementsPath='docs/requirements/srs_sections/01_overall_requirements.md'
$coordDev2Features=@('FE-06','FE-07','FE-08','FE-09','FE-10','FE-14','FE-15')
foreach($coordLine in Get-Content -LiteralPath (Join-Path $coordRoot $coordRequirementsPath) -Encoding UTF8){
 if($coordLine -match '^\|\s*(UC-\d{3})\s*\|\s*([^|]+)\|\s*([^|]+)\|'){
  $coordId=$matches[1];$coordTitle=$matches[2].Trim();$coordFeature=$matches[3].Trim()
  $coordOwner=if($coordId -in @('UC-063','UC-064')){'SHARED_OWNER_PENDING_EXACT_ASSIGNMENT'}elseif($coordFeature -in $coordDev2Features){'DEV2_MODULE_OWNERSHIP_NOT_LIVE_TASK_ASSIGNMENT'}else{'DEV1_MODULE_OWNERSHIP'}
  $coordOwnership+=[ordered]@{ucId=$coordId;title=$coordTitle;feature=$coordFeature;moduleOwner=$coordOwner;exclusiveCodexB=($coordId -eq 'UC-013')}
 }
}
Save-CoordJson (Join-Path $coordOut 'UC_OWNERSHIP.json') ([ordered]@{source=$coordRequirementsPath;ownershipSource='AGENTS.md';codexBExclusive=@('UC-013');codexACurrentTask='COORD-FINAL-01 only; no UC implementation started';codexAContractDependencies=@('UC-012','UC-029','UC-037','UC-041','UC-044','UC-045','UC-046','UC-111','UC-138','UC-139');dev2CurrentConfirmedHandoff='Notification V1 B01/B02/C01; Maintenance/PM UC062 participant; Branch-active coordination in DEV2 writers. This is contract scope, not a verified live implementation assignment roster.';exactLiveDev2TaskRoster='PENDING_EVIDENCE';noAutomaticBranchSync=$true;codexBIntegrationGate='Exact diff + base HEAD + hashes + conflict review + separate approval';uc013Overlap='No UC013 implementation in Codex A. Already historically credited; recovery must not add new credit.';ucCatalog=$coordOwnership})
$coordMain=@($coordPre.worktrees|Where-Object{$_.path.Replace('/','\') -ieq $coordRoot})[0]
$coordCurrentContract=Join-Path $PSScriptRoot 'FINAL_DEV1_DEV2_HANDOFF_20261104_02'
$coordHelperTable=@()
foreach($coordItem in $coordSourceInventory){$coordHelperTable+='| '+$coordItem.path+' | '+$coordItem.sha256+' |'}
$coordReadme=@'
# Codex B — UC013 read-only handoff

Snapshot chỉ dành cho review. **UC013 được chủ dự án giao độc quyền Codex B; Codex A không implementation UC013, không chuyển uncommitted source hoặc áp dụng patch vào worktree B.**

## Kết luận quan trọng về điểm

**BLOCKED: không thể xác nhận “UC013 chưa được cộng điểm”.** Ledger Task80 hiện ghi UC-013 `COMPLETE_VERIFIED_V1`, weight **2**, credited **2**. Task75 reconciled ledger và Task86 progress corroborate baseline. Task64 §12 của delivery plan ghi rõ gain +2; số 0 ở bảng kế hoạch UC013 là estimated hours remaining, không phải credited points. Export giữ nguyên ledger, không trừ/cộng điểm hoặc thay classification. Tổng **26/75, 60/256 =23.4375%**. Công việc B hiện là recovery/reverification; không tính như một UC mới trừ khi có reconciliation được chủ dự án duyệt.

`DEV1_LEDGER_EXPORT.json` và CSV xuất đủ 75 UC, title/status/weight/credited/evidenceSummary/receiptReferences. Receipt paths lấy từ section được row ledger trỏ tới; có thể là batch/regression, không mặc định là full acceptance riêng của mọi UC trong section. Row không có exact receipt path được đánh `PENDING_EVIDENCE`, không tự tạo receipt. JSON giữ nguyên mọi status COMPLETE_VERIFIED và COMPLETE_VERIFIED_V1 theo nguồn. Ledger status là historical accepted progress, không chứng nhận current runtime tồn tại.

Historical Task64 receipt tìm được trực tiếp từ immutable snapshot cf8fb020362f8f64192e7f2f14a10547cebe5b91: `scripts/verification/HotelStructure/task64-accepted-final2.local.json`; 159 structure-read assertions, 11 recorded waits. Đây là receipt lịch sử, không chạy lại trong task này; exact blob/path nằm trong `UC013_HISTORICAL_RECEIPT_SUMMARY.json`. Không dùng assertions làm điểm mới.

## Quyền thực tế và semantics

**APPROVED (recorded Task64 V1 decision):** `ROOM_VIEW` độc lập cho basic physical Branch/Floor/Area/Room. `MASTER_DATA_MANAGE` độc lập cho management read gồm departments và Floor/Area descriptions. Không yêu cầu cả hai; không có permission riêng UC013, BRANCH_VIEW hoặc STRUCTURE_VIEW được tìm thấy trong catalog. Task64 report tại commit trên và current delivery plan §12 ghi developer approval ASSUMED_V1; bản tin approval gốc không nằm riêng trong snapshot. Các tài liệu HotelStructureRead còn ghi “pending mapping” là proposal cũ, không thay thế decision Task64.

| Actor/catalog role | Actual catalog grant | Hành vi V1 có điều kiện |
|---|---|---|
| Administrator / SYS_ADMIN | MASTER_DATA_MANAGE:ALL; không ROOM_VIEW | Management read qua effective ALL path hợp lệ; không bypass theo tên admin |
| Operations Manager / OPERATIONS_MANAGER | ROOM_VIEW:BRANCH; không MASTER_DATA_MANAGE | Basic physical read trong assigned active branch; không departments/descriptions nếu không có một management grant riêng hợp lệ |

Catalog và line numbers trong `UC013_PERMISSION_EVIDENCE.json`; numeric permission_id là DB-specific, không hardcode. **PENDING_EVIDENCE:** live SQL grants/effective permissions của từng user chưa query, không được nói mọi SYS_ADMIN/OM đều đang được phép. Chỉ SQL-backed evaluation của current authenticated user mới quyết định quyền request.

- ALL + assignment BranchId null có thể global; ALL bound branch chỉ hợp lệ trên đúng branch với active assignment/access, không thành global. EvaluateBranchAsync vẫn deny inactive target, kể cả ALL. **Ngoại lệ contract Task64 đã ghi rõ:** unbound global MASTER_DATA_MANAGE qua EvaluateGlobalAsync được đọc archived/inactive Branch; không gọi đây là Branch-policy bypass. Scoped/basic không được đọc inactive Branch. Khôi phục phải giữ đúng sự phân biệt này.
- BRANCH cần branch-bound assignment đúng target, active role/account, assignment dates hợp lệ và nonrevoked/current branch access. Không ghép grant role này với membership của role khác.
- OWN cần verified resource ownership qua resolver; không cấp branch tree chỉ vì membership/department/manager. UC013 không có OWN resolver binding được duyệt; fail closed. Unknown/DEPARTMENT scope không grant.
- PermissionService kiểm account/deleted, không employment. UserEligibilityLock và service boundary phải kiểm employment ACTIVE riêng. CurrentUserAccessor chỉ đọc request identity, không tự refresh SQL session. Session/account/permission phải recheck dưới transaction và trước trả dữ liệu, kể cả management-to-basic downgrade sau khi materialize management data.
- Không dùng authentication-only, role-name bypass, RoomTypeDirectory permission, query branchId hay DTO actor làm authority. Giữ branch SQL predicate và no-store; không chạy count/node lookup ngoài scope trước authorization.

**PENDING_EVIDENCE / approval nếu muốn mở rộng:** full draft cho OM xem departments hoặc scoped/basic inactive Branch vượt V1 cần quyết định cụ thể; task giao UC013 không tự cấp quyền/grant mới. Global management archived read đã thuộc Task64 V1. Inactive child trong active branch khác inactive Branch.

## Source/helper có thể dùng khi đã hiện diện trong branch của B

`REUSABLE_SOURCE_INVENTORY.json` có exact file/class/method declaration line, SHA256 và git status cho từng path. Một số signature nhiều dòng: field signatureLine là dòng khai báo bắt đầu, đọc tiếp tại source path. Không export implementation source sang B.

| Current helper | Method/lines | Cách dùng / giới hạn |
|---|---|---|
| DTOs/HotelStructure/BranchStructureDtos.cs | BranchStructureQuery.Validate 14; CollectionsFor 30; DTO declarations 34–40 | Node kind + typed IDs, branch không nodeId, child combinations, max100/default20 và overflow validation; DTO-only, không endpoint/service |
| Security/Authorization/PermissionService.cs | EvaluateGlobalAsync 9; EvaluateBranchAsync 12; EvaluateResourceAsync 16; EvaluateAsync 21 | Existing SQL-backed scope semantics; dùng interface đã có, không sửa evaluator/resolver cho UC013 |
| Security/Authorization/PermissionDataSource.cs | EfPermissionDataSource.IsActiveUserAsync 21; IsActiveBranchAsync 25; ReadPathsAsync 28 | Giữ từng grant gắn đúng assignment; không full session/employment validator |
| Security/Authorization/CurrentUserAccessor.cs | TryGetCurrentUser 8 | Server-side identity only, không client actor hoặc tự dựng session |
| Services/Users/UserEligibilityLock.cs | LockAsync 12; RequireEligible 31; RequireEligibleAsync 38 | Caller-owned transaction; sorted/distinct users, UPDLOCK/HOLDLOCK, fresh eligibility. Không tự commit |
| Services/Authentication/SqlSessionService.cs | ValidateSessionAsync 42; CheckAsync 48; RunAsync 104 | Read-only Validate path tham gia compatible caller tx; không gọi Touch để thay authentication check |
| Services/HotelStructure/RoomTypeDirectoryService.cs | private ReadAsync 106, local Gate 144 | Pattern initial/final session+permission, clean context, User-first/branch lock; private, feature-bound, không callable reusable general API. RoomTypes applock/schema gate không tự copy vào UC013 |
| Services/HotelStructure/TurnoverHistoryBoundary.cs | private ReadAsync 20, Gate 58 | Pattern only, CLEANING_VIEW và RoomTypes lock dành UC038; không cấp authority UC013 |
| Services/HotelStructure/TurnoverHistorySchema.cs | internal RoomsAsync 10 | Current legacy/V1 RoomType scope compatibility query; phụ thuộc RoomTypeDirectorySchema.ReadAsync. Không dùng thay authorization hoặc tự thay schema |
| Services/HotelStructure/BranchAdministrationService.cs | ReadAsync 53; Execute 111; Gate 139 | UC012 global MASTER_DATA_MANAGE + rowversion dependency; không phải UC013 branch read cho OM, không dùng làm drop-in |
| Services/HotelStructure/HotelStructureCreationService.cs | CreateFloorAsync 23; CreateAreaAsync 31; transaction 52 | Writer compatibility/reference only; UC013 không gọi writes |

Actual line index trong JSON là authoritative nếu bảng tóm tắt khác. Entities Branch/Floor/Area/Room/Department và DbContext mappings có hashes cùng inventory: Area.FloorId nullable, Room.FloorId required, Room.AreaId nullable; không invent unified floor_area table hoặc floor-parent-floor. Giữ same-branch predicates và reject corrupt/foreign parent links; DTO không expose EF entities.

**BLOCKED current runtime reuse:** BranchStructureService.cs và BranchStructureController.cs không có trong current DEV1 main; Program không đăng ký BranchStructureService. DTO còn. Historical service/controller tại immutable commit có exact Git blobs/method lines trong `HISTORICAL_UC013_IDENTITIES.json`; đây không phải code đã restore và không phải permission áp patch vào B. Historical service line55 còn gọi RoomTypeSchema.RoomsAsync, nhưng RoomTypeSchema class không có trong current main. Current TurnoverHistorySchema.RoomsAsync là dependency candidate để review, không tự động thay thế/restore. Codex B tự review branch của mình; Codex A không copy source/diff.

## Ownership và shared contracts

**APPROVED:** B độc quyền UC013 theo current user instruction. A chỉ COORD-FINAL-01; các UC liên quan handoff A là UC012/029/037/041/044/045/046/111/138/139 ở mức contract/dependency, không nhận implementation UC013. UC038/UC142 là recovery đã có receipt; không mở batch mới. Không coi danh sách UC dependencies là một allocation implementation mới.

DEV2 module ownership từ AGENTS: FE06/07/08/09/10/14/15. `UC_OWNERSHIP.json` xuất exact IDs/titles/features từ SRS; UC063/064 shared phải phối hợp. Current confirmed handoff gồm Notification V1, UC062 Maintenance/PM participant, Branch-active guards cho Incident/Maintenance/Inventory/MR/Replenishment. **PENDING_EVIDENCE:** không có live task-allocation roster của DEV2 để chứng nhận mọi UC thuộc module đang triển khai; module ownership không phải current task claim. Không có overlap UC013 implementation với A/DEV2 theo assignment đã xác nhận.

Shared baseline: **COORD-FINAL-01 / parallel revision _02**, không có unified semantic version release được xác minh. Exact ZIP/manifest hash và source SHA được ghi trong EVIDENCE_MANIFEST.json. Task64 ASSUMED_V1 là UC013 read contract; A1/B1.1 Workday approved/applied, B1.2 pending; Notification A01 ACCEPTED user-confirmed, A02 DEV2-reported structural, B01 architecture DESIGN ONLY, B01 executable/B02/C01 REVISE; Room exact shared diffs NOT APPROVED; global total lock order NOT SOURCE-VERIFIED.

| Thay đổi pending | Ảnh hưởng UC013 |
|---|---|
| BranchStructureService DI / restore vào main | BLOCKED áp dụng nếu chưa có exact diff + review conflicts + approval riêng; không kế thừa DI approval Task84/85/86 |
| Branch deactivation writer protocol (UC012) | Giữ SQL authority/retained read locks và archived global-management contract Task64; không bật Deactivate, không thay BranchAdministrationService để phục vụ UC013 |
| Branch rowversion Task86 trial | Không phải prerequisite tự động cho basic EF tree; không dùng private UC012 raw row_version query làm generic read; SQL rollout ngoài approval hiện tại |
| Room 3-dimension lifecycle/mapping migration | Chỉ đọc actual physical identity; không invent status/readiness/block/cycle hoặc thay EF mappings |
| Notification C01 / DbContext / Program shared diffs | Không implementation, giữ local registrations. Không mặc định mọi read cần notification/audit publication; UC013 no business mutation |
| OM departments/scoped inactive Branch/full SRS mở rộng | PENDING_EVIDENCE/approval specific nếu khác Task64 V1; global management archived read đã nằm trong V1, không tạo permission/grant mới |
| Source B vào main | Exact diff + base HEAD/hashes → conflict review → approval riêng → verification. Không auto sync hoặc checkout |

Build/test/HTTPS/SQL: **NOT_RUN**. Không kết nối DB. Đây là snapshot source/contract/ledger, không nghiệm thu UC013 runtime. Chỉ tạo artifact mới; HEAD/index/local patches và các worktrees được kiểm pre/post. Metadata B thay đổi đồng thời, nếu có, được báo riêng; không chứng nhận source B.
'@
$coordReadme=$coordReadme.Replace('@@UNUSED@@','')
($coordReadme+"`r`n`r`nCurrent main branch: "+$coordMain.branch+"; HEAD: "+$coordMain.head+".`r`n")|Set-Content -LiteralPath (Join-Path $coordOut 'CODEX_B_UC013_HANDOFF.md') -Encoding UTF8
$coordSourceManifestTable=@('# Current file SHA256 (methods/lines in REUSABLE_SOURCE_INVENTORY.json)','','| File | SHA256 |','|---|---|')+$coordHelperTable
$coordSourceManifestTable|Set-Content -LiteralPath (Join-Path $coordOut 'SOURCE_SHA256.md') -Encoding UTF8
$coordAfter=@(Get-CoordHashes);$coordPost=Get-CoordTrees
Save-CoordJson (Join-Path $coordOut 'POSTFLIGHT.json') $coordPost
$coordBeforeMap=@{};foreach($coordF in $coordBefore){$coordBeforeMap[$coordF.path]=$coordF.sha256}
$coordAfterMap=@{};foreach($coordF in $coordAfter){$coordAfterMap[$coordF.path]=$coordF.sha256}
$coordChanged=@($coordAfter|Where-Object{$coordBeforeMap.ContainsKey($_.path) -and $coordBeforeMap[$_.path] -cne $_.sha256}|ForEach-Object path)
$coordAdded=@($coordAfter|Where-Object{!$coordBeforeMap.ContainsKey($_.path)}|ForEach-Object path)
$coordMissing=@($coordBefore|Where-Object{!$coordAfterMap.ContainsKey($_.path)}|ForEach-Object path)
$coordTreeCompare=@()
foreach($coordW in $coordPost.worktrees){$coordOld=@($coordPre.worktrees|Where-Object path -eq $coordW.path)[0];$coordTreeCompare+=[ordered]@{path=$coordW.path;headPreserved=($coordOld.head -ceq $coordW.head);branchPreserved=($coordOld.branch -ceq $coordW.branch);indexPreserved=($coordOld.indexSha256 -ceq $coordW.indexSha256);statusSame=(($coordOld.status -join "`n") -ceq ($coordW.status -join "`n"))}}
$coordTreeSetSame=((@($coordPre.worktrees.path|Sort-Object)-join "`n") -ceq (@($coordPost.worktrees.path|Sort-Object)-join "`n"))
$coordMainPost=@($coordPost.worktrees|Where-Object path -eq $coordMain.path)[0]
$coordMainGit=@($coordTreeCompare|Where-Object path -eq $coordMain.path)[0]
$coordPreStatus=@($coordMain.status|Where-Object{$_ -notlike ('*scripts/verification/Coordination/'+[IO.Path]::GetFileName($coordOut)+'/*')})
$coordPostStatus=@($coordMainPost.status|Where-Object{$_ -notlike ('*scripts/verification/Coordination/'+[IO.Path]::GetFileName($coordOut)+'/*')})
$coordMainStatusSame=(($coordPreStatus -join "`n") -ceq ($coordPostStatus -join "`n"))
git --no-optional-locks -C $coordRoot diff --check
$coordDiffExit=$LASTEXITCODE
$coordConsistent=($coordChanged.Count -eq 0 -and $coordAdded.Count -eq 0 -and $coordMissing.Count -eq 0 -and $coordTreeSetSame -and $coordMainGit.headPreserved -and $coordMainGit.branchPreserved -and $coordMainGit.indexPreserved -and $coordMainStatusSame)
Save-CoordJson (Join-Path $coordOut 'VALIDATION.json') ([ordered]@{task='COORD-FINAL-01_CODEX_B_UC013_READONLY';status=if($coordConsistent -and $coordDiffExit -eq 0){'DONE_EVIDENCE_HANDOFF_WITH_EXPLICIT_CREDIT_CONTRADICTION'}else{'SNAPSHOT_INCONSISTENT'};startedUtc=$coordStarted.ToString('o');recordedUtc=[DateTimeOffset]::UtcNow.ToString('o');elapsedSeconds=[Math]::Round(([DateTimeOffset]::UtcNow-$coordStarted).TotalSeconds,1);protectedInputs=$coordBefore.Count;changed=$coordChanged;addedNonOutput=$coordAdded;missing=$coordMissing;sourceConsistent=$coordConsistent;branch=$coordMain.branch;head=$coordMain.head;worktreeCountPreflight=$coordPre.count;worktreeCountPostflight=$coordPost.count;worktreePathSetPreserved=$coordTreeSetSame;worktrees=$coordTreeCompare;mainStatusPreservedExceptNewArtifacts=$coordMainStatusSame;productionSourceEdits=0;databaseConnections=0;gitMutations=0;worktreeMutations=0;sourceTransfersToB=0;uc013Implemented=$false;ledgerModified=$false;uc013CreditedBefore=2;uc013CreditedAfter=2;pointsGained=0;ledgerRows=$coordRows.Count;ledgerWeight=$coordTotal;ledgerCredit=$coordCredit;rowsWithoutExactReceiptReferences=@($coordRows|Where-Object receiptReferenceStatus -like 'PENDING*'|ForEach-Object ucId);diffCheckExit=$coordDiffExit;build='NOT_RUN';frameworkTests='NOT_RUN';https='NOT_RUN';sql='NOT_RUN'})
if(!$coordConsistent -or $coordDiffExit -ne 0){throw 'Snapshot inconsistent; no final handoff claim'}
$coordContractFiles=@('DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md','DEV1_SHARED_APPROVAL_AND_ACCEPTANCE_MATRIX.md','HANDOFF_MANIFEST.json','DEV1_DEV2_FINAL_HANDOFF.zip')
$coordContracts=@();foreach($coordF in $coordContractFiles){$coordP=Join-Path $coordCurrentContract $coordF;$coordContracts+=[ordered]@{path=$coordP;sha256=(Get-FileHash -LiteralPath $coordP).Hash}}
$coordPackageFiles=@(Get-ChildItem -LiteralPath $coordOut -File)
$coordFiles=@();foreach($coordF in $coordPackageFiles){$coordFiles+=[ordered]@{path=$coordF.Name;sha256=(Get-FileHash -LiteralPath $coordF.FullName).Hash;bytes=$coordF.Length}}
Save-CoordJson (Join-Path $coordOut 'EVIDENCE_MANIFEST.json') ([ordered]@{task='COORD-FINAL-01_CODEX_B_UC013_READONLY';branch=$coordMain.branch;head=$coordMain.head;sharedContractPackage='COORD-FINAL-01 parallel revision _02';sharedContractHashes=$coordContracts;files=$coordFiles;selfHashExcluded=$true;implementationSourceIncluded=$false;uncommittedSourceTransferred=$false;priorHandoffPreserved=$true})
$coordPackageFiles+=Get-Item -LiteralPath (Join-Path $coordOut 'EVIDENCE_MANIFEST.json')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$coordZip=Join-Path $coordOut 'CODEX_B_UC013_READONLY_HANDOFF.zip'
$coordArchive=[IO.Compression.ZipFile]::Open($coordZip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($coordF in $coordPackageFiles){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($coordArchive,$coordF.FullName,$coordF.Name,[IO.Compression.CompressionLevel]::Optimal)}}finally{$coordArchive.Dispose()}
$coordArchive=[IO.Compression.ZipFile]::OpenRead($coordZip);$coordFailures=@()
try{foreach($coordF in $coordPackageFiles){$coordE=$coordArchive.GetEntry($coordF.Name);if(!$coordE -or $coordE.Length -ne $coordF.Length){$coordFailures+=$coordF.Name;continue};$coordStream=$coordE.Open();$coordSha=[Security.Cryptography.SHA256]::Create();try{$coordHash=[BitConverter]::ToString($coordSha.ComputeHash($coordStream)).Replace('-','')}finally{$coordStream.Dispose();$coordSha.Dispose()};if($coordHash -cne (Get-FileHash -LiteralPath $coordF.FullName).Hash){$coordFailures+=$coordF.Name}};$coordZipCount=$coordArchive.Entries.Count}finally{$coordArchive.Dispose()}
Save-CoordJson (Join-Path $coordOut 'ZIP_VERIFICATION.json') ([ordered]@{zip=$coordZip;sha256=(Get-FileHash -LiteralPath $coordZip).Hash;entries=$coordZipCount;verified=$coordPackageFiles.Count;failures=$coordFailures;pass=($coordFailures.Count -eq 0 -and $coordZipCount -eq $coordPackageFiles.Count)})
if($coordFailures.Count){throw 'ZIP verification failed'}
Write-Output "OUTPUT=$coordOut LEDGER_ROWS=$($coordRows.Count) POINTS=$coordCredit/$coordTotal UC013_CREDIT=$($coordUc013.creditedPoints)"
Write-Output "WORKTREES=$($coordPre.count)/$($coordPost.count) INPUTS=$($coordBefore.Count) CHANGED=$($coordChanged.Count) ADDED=$($coordAdded.Count) MISSING=$($coordMissing.Count) ZIP_ENTRIES=$coordZipCount"
