$ErrorActionPreference='Stop'
$coordRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$coordPre=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PREFLIGHT.json') -Raw | ConvertFrom-Json
$coordBaseline=@{};foreach($coordEntry in $coordPre.files){$coordBaseline[$coordEntry.path]=$coordEntry.sha256}
$coordEvidence=Join-Path $PSScriptRoot 'evidence'
New-Item -ItemType Directory -Path $coordEvidence | Out-Null
$coordHead=@{};foreach($coordLine in (git -C $coordRoot ls-tree -r HEAD src/HotelOps.Api)){if($coordLine -match '^\d+\s+blob\s+([a-f0-9]+)\t(.+)$'){$coordHead[$Matches[2]]=$Matches[1]}}
$coordIndex=@{};foreach($coordLine in (git -C $coordRoot ls-files --stage -- src/HotelOps.Api)){if($coordLine -match '^\d+\s+([a-f0-9]+)\s+0\t(.+)$'){$coordIndex[$Matches[2]]=$Matches[1]}}
$coordStatuses=@{};foreach($coordLine in $coordPre.worktrees[0].status){if($coordLine.Length -gt 3){$coordStatuses[$coordLine.Substring(3).Replace('\','/').Trim('"')]=$coordLine.Substring(0,2)}}
$coordCurrent=@(rg --files (Join-Path $coordRoot 'src/HotelOps.Api') -g '*.cs' -g '!**/bin/**' -g '!**/obj/**')
$coordIds=@();foreach($coordAbsolute in $coordCurrent){
 $coordItem=Get-Item -LiteralPath $coordAbsolute;$coordRelative=$coordItem.FullName.Substring($coordRoot.Length+1).Replace('\','/')
 $coordSha=(Get-FileHash -LiteralPath $coordAbsolute).Hash
 if(!$coordBaseline.ContainsKey($coordItem.FullName) -or $coordBaseline[$coordItem.FullName] -cne $coordSha){throw "SNAPSHOT_INCONSISTENT before capture: $coordRelative"}
 $coordIds+=[ordered]@{path=$coordRelative;absolutePath=$coordItem.FullName;sha256=$coordSha;headBlob=$coordHead[$coordRelative];indexBlob=$coordIndex[$coordRelative];workingTreeRawBlob=(git -C $coordRoot hash-object --no-filters -- $coordAbsolute);gitStatus=if($coordStatuses.ContainsKey($coordRelative)){$coordStatuses[$coordRelative]}else{'  '};lineCount=([IO.File]::ReadAllLines($coordAbsolute)).Length}
}
$coordIds | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $coordEvidence 'CURRENT_SOURCE_IDS.json') -Encoding UTF8
$coordProgram=$coordIds | Where-Object path -eq 'src/HotelOps.Api/Program.cs'
[ordered]@{worktree=$coordRoot;branch=$coordPre.worktrees[0].branch;head=$coordPre.worktrees[0].head;program=$coordProgram;workingTreeFilteredBlob=(git -C $coordRoot hash-object --path=src/HotelOps.Api/Program.cs src/HotelOps.Api/Program.cs);computedWorkingTreeBlobsAreNotWrittenToGit=$true} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $coordEvidence 'PROGRAM_IDENTITY.json') -Encoding UTF8
foreach($coordDiff in @(@('PROGRAM_HEAD_TO_INDEX.diff',@('diff','--cached','HEAD','--','src/HotelOps.Api/Program.cs')),@('PROGRAM_INDEX_TO_WORKTREE.diff',@('diff','--','src/HotelOps.Api/Program.cs')),@('PROGRAM_HEAD_TO_WORKTREE.diff',@('diff','HEAD','--','src/HotelOps.Api/Program.cs')))){
 $coordArgs=$coordDiff[1];$coordText=(@(git -C $coordRoot @coordArgs) -join "`n")
 if(!$coordText){$coordText='(empty diff)'}
 if($coordText -match '(?i)(Server|Data Source|Initial Catalog)\s*='){throw 'Connection configuration found in Program diff; redact before packaging'}
 [IO.File]::WriteAllText((Join-Path $coordEvidence $coordDiff[0]),$coordText+"`n")
}
function Export-CoordExcerpt([string]$Relative,[string]$Ranges){
 $coordAbsolute=Join-Path $coordRoot $Relative;if(!(Test-Path -LiteralPath $coordAbsolute)){return}
 $coordSha=(Get-FileHash -LiteralPath $coordAbsolute).Hash
 if($coordBaseline[$coordAbsolute] -cne $coordSha){throw "SNAPSHOT_INCONSISTENT: $Relative"}
 $coordLines=[IO.File]::ReadAllLines($coordAbsolute);$coordOutputLines=@("SOURCE: $Relative","SHA256: $coordSha","Ranges: $Ranges (original source line numbers)")
 foreach($coordRange in $Ranges.Split(',')){
  $coordBounds=$coordRange.Split('-');$coordFirst=[int]$coordBounds[0];$coordLast=[Math]::Min([int]$coordBounds[1],$coordLines.Length)
  for($coordN=$coordFirst;$coordN -le $coordLast;$coordN++){
   $coordValue=$coordLines[$coordN-1]
   if($coordValue -match '(?i)(Data Source|Initial Catalog|Server)\s*='){ $coordValue='[REDACTED literal connection configuration; consult source locally]' }
   $coordOutputLines+=('{0,5}: {1}' -f $coordN,$coordValue)
  }
  $coordOutputLines+=''
 }
 $coordOutputLines | Set-Content -LiteralPath (Join-Path $coordEvidence ($Relative.Replace('/','__')+'.lines.txt')) -Encoding UTF8
 if((Get-FileHash -LiteralPath $coordAbsolute).Hash -cne $coordSha){throw "SNAPSHOT_INCONSISTENT after read: $Relative"}
}
$coordSelections=@{
 'src/HotelOps.Api/Program.cs'='1-130';
 'src/HotelOps.Api/Data/HotelOpsDbContext.cs'='531-575,1712-1790,2176-2305';
 'src/HotelOps.Api/Controllers/WorkdaysController.cs'='1-100';
 'src/HotelOps.Api/Services/WorkTime/IWorkTimeTransactionParticipant.cs'='1-100';
 'src/HotelOps.Api/Services/WorkTime/SqlWorkTimeTransactionParticipant.cs'='1-275';
 'src/HotelOps.Api/Services/WorkTime/WorkdayService.cs'='1-170';
 'src/HotelOps.Api/Services/WorkTime/WorkdayPermissionResourceResolver.cs'='1-150';
 'src/HotelOps.Api/Services/WorkTime/OwnWorkdaySnapshotReader.cs'='1-130';
 'src/HotelOps.Api/Services/Users/UserEligibilityLock.cs'='1-100';
 'src/HotelOps.Api/Services/Users/GlobalAdminSerialization.cs'='17-65,87-135';
 'src/HotelOps.Api/Services/Users/Dev1DeactivationPredicates.cs'='1-100';
 'src/HotelOps.Api/Services/Users/UserAccountActivationService.cs'='25-100';
 'src/HotelOps.Api/Services/Authentication/SqlSessionService.cs'='1-140';
 'src/HotelOps.Api/Security/Authorization/CurrentUserAccessor.cs'='1-100';
 'src/HotelOps.Api/Security/Authorization/PermissionService.cs'='1-130';
 'src/HotelOps.Api/Security/Authorization/PermissionDataSource.cs'='1-100';
 'src/HotelOps.Api/Security/Authorization/PermissionAuthorizationRegistration.cs'='1-110';
 'src/HotelOps.Api/Services/Housekeeping/CleaningMonitorService.cs'='1-165';
 'src/HotelOps.Api/Controllers/CleaningMonitorController.cs'='1-100';
 'src/HotelOps.Api/Services/HotelStructure/BranchAdministrationService.cs'='1-220';
 'src/HotelOps.Api/Services/HotelStructure/BranchDeactivationDependencyReader.cs'='1-120';
 'src/HotelOps.Api/Services/HotelStructure/HotelStructureCreationService.cs'='35-115';
 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistoryBoundary.cs'='15-90';
 'src/HotelOps.Api/Services/HotelStructure/RoomTypeDirectoryService.cs'='95-153';
 'src/HotelOps.Api/Services/Incidents/IncidentService.cs'='20-43,112-144,225-337';
 'src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Commands.cs'='1-150';
 'src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Create.cs'='1-100';
 'src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Workflow.cs'='1-120';
 'src/HotelOps.Api/Services/Inventory/InventoryService.cs'='58-135';
 'src/HotelOps.Api/Services/Inventory/InventoryService.Masters.cs'='64-105';
 'src/HotelOps.Api/Services/Inventory/InventoryService.Documents.cs'='1-24,77-101,152-205,226-256';
 'src/HotelOps.Api/Services/Inventory/InventoryService.ReceiptIntegration.cs'='1-170';
 'src/HotelOps.Api/Services/MaterialRequests/MaterialRequestService.cs'='1-185';
 'src/HotelOps.Api/Services/MaterialRequests/MaterialRequestService.Commands.cs'='12-63,86-105,150-194';
 'src/HotelOps.Api/Services/Replenishments/ReplenishmentService.cs'='14-23,46-152';
 'src/HotelOps.Api/Services/Assets/AssetService.Location.cs'='10-90';
 'src/HotelOps.Api/Controllers/IncidentsController.cs'='1-130';
 'src/HotelOps.Api/Entities/Notification.cs'='1-100';
 'src/HotelOps.Api/Entities/NotificationRecipient.cs'='1-100';
 'src/HotelOps.Api/Entities/Room.cs'='1-100';
 'src/HotelOps.Api/Entities/RoomBlock.cs'='1-100';
 'src/HotelOps.Api/Entities/RoomInspection.cs'='1-100';
 'src/HotelOps.Api/Entities/CleaningTask.cs'='1-95';
 'src/HotelOps.Api/Entities/CleaningRework.cs'='1-90';
 'src/HotelOps.Api/Entities/CleaningProgressLog.cs'='1-70';
 'src/HotelOps.Api/Entities/WorkInterval.cs'='1-100';
 'src/HotelOps.Api/Entities/RoomStatusHistory.cs'='1-100';
 'scripts/verification/Task77/proposal/01_WorkTime.forward.sql'='1-220';
 'scripts/database/release/01_HotelOps_Full_Schema.sql'='889-940,1168-1208,1928-1943,1997-2005,2247-2255,3318-3358,3375-3402,3439-3462'
}
foreach($coordSelection in $coordSelections.GetEnumerator()){Export-CoordExcerpt $coordSelection.Key $coordSelection.Value}
$coordHistorical=@('src/HotelOps.Api/Services/Housekeeping/HousekeepingAssignmentsService.cs','src/HotelOps.Api/Controllers/HousekeepingWorkflowController.cs','src/HotelOps.Api/Services/Housekeeping/InspectionExecutionService.cs','src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs','scripts/database/release/housekeeping/01_Housekeeping.sql')
$coordHistoricalIds=@();foreach($coordPath in $coordHistorical){
 $coordRef='cf8fb020362f8f64192e7f2f14a10547cebe5b91:'+ $coordPath
 $coordBlob=(git -C $coordRoot rev-parse $coordRef);$coordContent=@(git -C $coordRoot show $coordRef)
 if($LASTEXITCODE -ne 0){throw "Historical source missing: $coordRef"}
 $coordHistoricalIds+=[ordered]@{ref=$coordRef;blob=$coordBlob;lineCount=$coordContent.Count;currentPathExists=(Test-Path -LiteralPath (Join-Path $coordRoot $coordPath));classification='HISTORICAL_ONLY_NOT_RUNTIME'}
 $coordOut=@("HISTORICAL ONLY: $coordRef","Git blob: $coordBlob")
 for($coordI=0;$coordI -lt $coordContent.Count;$coordI++){$coordOut+=('{0,5}: {1}' -f ($coordI+1),$coordContent[$coordI])}
 $coordOut | Set-Content -LiteralPath (Join-Path $coordEvidence ('HISTORICAL__'+$coordPath.Replace('/','__')+'.lines.txt')) -Encoding UTF8
}
$coordHistoricalIds | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $coordEvidence 'HISTORICAL_SOURCE_IDS.json') -Encoding UTF8
rg -n 'SaveChanges|BeginTransaction|CommitAsync|\.Commit\(|Rollback|Savepoint|TransactionScope|GetDbConnection|CreateCommand|SqlConnection|UseTransaction|GetDbTransaction|ExecuteSql|ExecuteUpdate|ExecuteDelete|AddInterceptors' (Join-Path $coordRoot 'src/HotelOps.Api') -g '*.cs' -g '!**/bin/**' -g '!**/obj/**' | Set-Content -LiteralPath (Join-Path $coordEvidence 'CURRENT_COMMIT_PATHWAYS.txt') -Encoding UTF8
Write-Output "CAPTURED current C# identities=$($coordIds.Count), curated excerpts=$($coordSelections.Count), historical files=$($coordHistoricalIds.Count). No Git objects written; no SQL/build executed."
