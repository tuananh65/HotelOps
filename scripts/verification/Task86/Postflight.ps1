param([Parameter(Mandatory=$true)][string]$Receipt)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Receipt){throw 'Unused receipt required'}
$task86Pre=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PREFLIGHT.json') -Raw | ConvertFrom-Json
$task86Approval=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'A_APPROVAL_APPLIED.json') -Raw | ConvertFrom-Json
$task86Mismatches=@();$task86Unchanged=0
foreach($task86File in $task86Pre.protectedFiles){
 if(!(Test-Path -LiteralPath $task86File.path)){$task86Mismatches+=$task86File.path;continue}
 $task86Hash=(Get-FileHash -LiteralPath $task86File.path).Hash
 $task86Mtime=(Get-Item -LiteralPath $task86File.path).LastWriteTimeUtc
 if($task86Hash -ceq $task86File.sha256 -and $task86Mtime -eq ([DateTimeOffset]::Parse($task86File.lastWriteUtc)).UtcDateTime){$task86Unchanged++}
 elseif($task86File.path -cne 'src\HotelOps.Api\Program.cs'){$task86Mismatches+=$task86File.path}
}
$task86Head=(git rev-parse HEAD).Trim();$task86Branch=(git branch --show-current).Trim()
$task86Index=(Get-FileHash -LiteralPath (git rev-parse --git-path index)).Hash
$task86Worktrees=@(git worktree list --porcelain)
$task86OriginalBlocks=($task86Pre.worktrees -join "`n").Trim() -split "`n`n"
$task86ActualBlocks=($task86Worktrees -join "`n").Trim() -split "`n`n"
$task86MissingOriginal=@($task86OriginalBlocks | Where-Object {$_ -cnotin $task86ActualBlocks})
$task86AddedBlocks=@($task86ActualBlocks | Where-Object {$_ -cnotin $task86OriginalBlocks})
$task86ProgramMatches=((Get-FileHash src/HotelOps.Api/Program.cs).Hash -ceq $task86Approval.programAfter)
git diff --check
$task86DiffExit=$LASTEXITCODE
$task86Result=[ordered]@{recordedUtc=[DateTime]::UtcNow.ToString('o');protectedCount=$task86Pre.protectedFiles.Count;unchangedHashAndMtime=$task86Unchanged;approvedProgramChangeMatchesAppliedReceipt=$task86ProgramMatches;unauthorizedMismatches=$task86Mismatches;head=$task86Head;headPreserved=($task86Head -ceq $task86Pre.head);branch=$task86Branch;branchPreserved=($task86Branch -ceq $task86Pre.branch);indexSha256=$task86Index;indexPreserved=($task86Index -ceq $task86Pre.indexSha256);worktreesPreserved=(($task86Worktrees -join "`n") -ceq ($task86Pre.worktrees -join "`n"));worktrees=$task86Worktrees;diffCheckExit=$task86DiffExit;status=@(git status --porcelain=v1 -uall)}
$task86Result.originalWorktreesPreserved=($task86MissingOriginal.Count -eq 0)
$task86Result.originalWorktreeCount=$task86OriginalBlocks.Count
$task86Result.currentWorktreeCount=$task86ActualBlocks.Count
$task86Result.addedWorktreeBlocks=$task86AddedBlocks
$task86Result.externalChangeNote='Task86 issued no branch/worktree mutation commands. Additional ai/codex-b worktree observed after earlier four-worktree postflight; original entries compared independently.'
$task86Result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $Receipt -Encoding UTF8
if($task86Mismatches.Count -or !$task86ProgramMatches -or !$task86Result.headPreserved -or !$task86Result.branchPreserved -or !$task86Result.indexPreserved -or !$task86Result.originalWorktreesPreserved -or $task86DiffExit){throw 'Postflight mismatch'}
Write-Output "PASS: $task86Unchanged unchanged files + one approved Program change; HEAD/index/branch/four original worktrees preserved; diff check PASS. Current worktree count=$($task86ActualBlocks.Count)."
