# Prepare canonical indexed files only; do not launch Node/esbuild/Playwright here.
$ErrorActionPreference='Stop'
$workspace=(Get-Location).Path
$evidence='docs/integration-notes/PR25-FINAL-COORD-001/receipts/uc139-70e1d4ee'
if((git rev-parse MERGE_HEAD) -ne '70e1d4ee2c4b63b14543f90c1e315bec39585d80'){throw 'Wrong integration parent'}
$scratch=Join-Path $workspace ('bin/PR25-UC139-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $scratch|Out-Null
$files=@(git ls-tree -r --name-only MERGE_HEAD frontend/src/auth frontend/src/workday)
$files+=@('frontend/package.json','frontend/tests/workday-fixtures.mjs','frontend/tests/scheduled-workday-browser.test.mjs')
$files+=@(git ls-tree -r --name-only MERGE_HEAD frontend|Where-Object{$_ -match '^frontend/tsconfig[^/]*\.json$'})
$records=@()
foreach($path in $files){
 $target=Join-Path $scratch $path.Substring('frontend/'.Length)
 New-Item -ItemType Directory -Force -Path (Split-Path $target)|Out-Null
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName='git';$start.Arguments="show :$path";$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.CreateNoWindow=$true
 $process=New-Object Diagnostics.Process;$process.StartInfo=$start;[void]$process.Start()
 $stream=[IO.File]::Create($target);try{$process.StandardOutput.BaseStream.CopyTo($stream)}finally{$stream.Dispose()}
 $process.WaitForExit();if($process.ExitCode -ne 0){throw 'Indexed export failed'};$process.Dispose()
 $expected=git rev-parse ":$path";$actual=git hash-object --no-filters -- $target
 if($actual -ne $expected -or $expected -ne (git rev-parse "MERGE_HEAD:$path")){throw "Scratch input mismatch: $path"}
 $records+=[ordered]@{path=$path;indexGitBlob=$expected;scratchGitBlob=$actual;sha256Worktree=(Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash.ToLowerInvariant()}
}
$dependencies='C:\Users\Admin\Documents\GitHub\HotelOps_DEV2_FullStack_Integration\frontend\node_modules'
if(!(Test-Path -LiteralPath "$dependencies/esbuild/package.json") -or !(Test-Path -LiteralPath "$dependencies/@playwright/test/package.json")){throw 'Existing host dependencies unavailable; do not install automatically'}
New-Item -ItemType Junction -Path (Join-Path $scratch 'node_modules') -Target $dependencies|Out-Null
[ordered]@{sourceTree='b3d8dfe61a528c68002ce6d725d47840604d2e27';integrationParent='70e1d4ee2c4b63b14543f90c1e315bec39585d80';scratchDirectory=$scratch;dependencyDirectory=$dependencies;node='C:/Program Files/nodejs/node.exe';test='tests/scheduled-workday-browser.test.mjs';indexedFiles=$records;inputMismatchCount=0;frontendExecution='NOT_RUN_SANDBOX_KNOWN_ESBUILD_RESTRICTION';qualification='Dedicated ignored scratch; exact indexed bytes and assertions; external dependencies read only; no package installation or original worktree edits'}|ConvertTo-Json -Depth 8|Set-Content -Encoding utf8 -LiteralPath "$evidence/frontend_inputs.json"
Write-Output "HOST_FRONTEND_DIRECTORY=$scratch"
Write-Output "INPUTS_VERIFIED=$($records.Count); FRONTEND_EXECUTION=HOST_PENDING"
