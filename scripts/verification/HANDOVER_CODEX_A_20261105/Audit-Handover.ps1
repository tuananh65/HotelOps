param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Set-Location $root
function SaveJson($name,$value){ConvertTo-Json -InputObject $value -Depth 30 | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot $name)}
$trees=@()
foreach($line in (git worktree list --porcelain)){
 if($line -notlike 'worktree *'){continue};$p=$line.Substring(9)
 $gitArgs=@('-c',"safe.directory=$p",'-C',$p)
 $head=(& git @gitArgs rev-parse HEAD)
 $indexPath=(& git @gitArgs rev-parse --path-format=absolute --git-path index)
 $status=@(& git @gitArgs status --porcelain=v1 --untracked-files=all)
 $delta=@(& git @gitArgs diff --name-status)
 $untracked=@(& git @gitArgs ls-files --others --exclude-standard)
 $tracked=@(& git @gitArgs ls-files)
 $inventory=@(foreach($f in @($tracked+$untracked | Sort-Object -Unique)){
   $full=Join-Path $p $f
   if(Test-Path -LiteralPath $full -PathType Leaf){$i=Get-Item -LiteralPath $full;@{path=$f;bytes=$i.Length;sha256=(Get-FileHash -LiteralPath $full).Hash;tracked=($tracked -contains $f)}}
 })
 $trees+=@{path=$p;branch=(& git @gitArgs branch --show-current);head=$head;indexSha256=(Get-FileHash -LiteralPath $indexPath).Hash;status=$status;unstaged=$delta;staged=@(& git @gitArgs diff --cached --name-status);untracked=$untracked;files=$inventory;owner= $(if($p -like '*Codex-B'){'CODEX_B_READ_ONLY_NOT_OURS'}elseif($p -eq $root.Replace('\','/')){'CODEX_A_CURRENT'}else{'OTHER_WORKTREE_READ_ONLY_NO_OWNERSHIP_CLAIM'})}
}
SaveJson 'WORKTREE_INVENTORY.json' $trees
$upstream=git rev-parse --abbrev-ref '@{upstream}'
SaveJson 'GIT_AUDIT.json' @{root=$root;branch=(git branch --show-current);head=(git rev-parse HEAD);upstream=$upstream;upstreamRefSha=(git rev-parse '@{upstream}');aheadBehind=(git rev-list --left-right --count 'HEAD...@{upstream}');unpushedRelativeToUpstream=@(git log --format='%H %s' '@{upstream}..HEAD');allRefs=@(git for-each-ref '--format=%(refname) %(objectname) %(upstream)' refs/heads refs/remotes);worktreeCount=$trees.Count;staged=@(git diff --cached --name-status);unstaged=@(git diff --name-status);recordedUtc=[DateTime]::UtcNow.ToString('o')}
# Enumerate ignored evidence too; binaries/build output are classified, never staged by this script.
$physical=@(& rg --files --hidden --no-ignore scripts tests src docs -g '!**/bin/**' -g '!**/obj/**' -g '!**/artifacts/**' -g '!**/node_modules/**' -g '!**/.git/**' -g '!**/HANDOVER_CODEX_A_20261105/**')
$inventory=@(foreach($f in $physical){$i=Get-Item -LiteralPath $f;@{path=$f.Replace('\','/');bytes=$i.Length;sha256=(Get-FileHash -LiteralPath $f).Hash}})
SaveJson 'PHYSICAL_SOURCE_ARTIFACT_INVENTORY.json' $inventory
# Read SRS UC catalog, never infer feature from a UC title.
$catalog=@{}
foreach($line in (Get-Content docs/requirements/srs_sections/01_overall_requirements.md -Encoding utf8)){
 if($line -match '^\|\s*(UC-\d+)\s*\|\s*([^|]+)\|\s*([^|]+)\|'){$catalog[$Matches[1]]=@{title=$Matches[2].Trim();feature=$Matches[3].Trim()}}
}
function Owner($feature){
 $fs=@([regex]::Matches($feature,'FE-\d+') | ForEach-Object Value)
 $owners=@(foreach($f in $fs){switch -Regex ($f){'^FE-(03|04|05|11|13)$'{'DEV1'} '^FE-(01|02|12)$'{'DEV3'} '^FE-(06|07|08|09|10|14|15)$'{'DEV2'} default{'SHARED'}}}); $owners=@($owners | Sort-Object -Unique)
 if($owners.Count -eq 1){return $owners[0]};return 'SHARED'
}
$ledger=Get-Content scripts/verification/Task80/UC_LEDGER_TASK80.json -Raw -Encoding utf8 | ConvertFrom-Json
$rows=@(foreach($r in $ledger.rows){$c=$catalog[$r.uc];[pscustomobject]@{uc=$r.uc;title=$r.title;srsTitle=$c.title;feature=$c.feature;owner=(Owner $c.feature);historicalClassification=$r.classification;weight=$r.weight;creditedPoints=$r.verifiedPoints;evidence=$r.evidenceSummary;currentRuntimeAcceptance='NOT_REACCEPTED_BY_HANDOVER; consult current source inventory and UC status overrides'}})
SaveJson 'UC_LEDGER.json' @{source='Task80/UC_LEDGER_TASK80.json reconciled Task83/84/86/87';rows=$rows;completeCount=@($rows|Where-Object {$_.historicalClassification -like 'COMPLETE*'}).Count;creditedPoints=($rows|Measure-Object creditedPoints -Sum).Sum;totalWeight=($rows|Measure-Object weight -Sum).Sum;totalUCs=$rows.Count;srsSource='docs/requirements/srs_sections/01_overall_requirements.md';creditIsHistoricalNotCurrentRuntime=$true}
$rows | Select-Object uc,title,feature,owner,historicalClassification,weight,creditedPoints,currentRuntimeAcceptance | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $PSScriptRoot 'UC_LEDGER.csv')
$allCatalog=@(foreach($id in ($catalog.Keys|Sort-Object)){@{uc=$id;title=$catalog[$id].title;feature=$catalog[$id].feature;owner=(Owner $catalog[$id].feature);codexALedgerContains=(@($rows|Where-Object uc -eq $id).Count -gt 0)}})
SaveJson 'SRS_ALL_UC_OWNERSHIP.json' $allCatalog
Write-Output "Worktrees=$($trees.Count); files=$($inventory.Count); ledger=$($rows.Count); SRS catalog=$($catalog.Count)"


