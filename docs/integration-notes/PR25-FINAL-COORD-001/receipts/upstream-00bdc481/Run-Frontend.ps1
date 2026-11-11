# Isolated, exact indexed frontend sources. Existing node_modules are read only.
$ErrorActionPreference='Stop'
$workspace=(Get-Location).Path
$receiptRoot=Join-Path $workspace 'docs/integration-notes/PR25-FINAL-COORD-001/receipts/upstream-00bdc481'
$temporaryRoot=Join-Path $workspace ('bin/PR25-UC138-'+[Guid]::NewGuid().ToString('N'))
$dependencies='C:\Users\Admin\Documents\GitHub\HotelOps_DEV2_FullStack_Integration\frontend\node_modules'
$node='C:\Program Files\nodejs\node.exe'
New-Item -ItemType Directory -Force -Path $temporaryRoot | Out-Null
$files=@(git ls-tree -r --name-only MERGE_HEAD frontend/src/auth frontend/src/workday)
$files+=@('frontend/package.json','frontend/tests/shift-plan-api.test.mjs','frontend/tests/secure-shift-plan-browser.test.mjs','frontend/tests/shift-plan-preview.test.mjs','frontend/tests/shift-plan-preview-browser.test.mjs')
$files+=@(git ls-tree -r --name-only MERGE_HEAD frontend | Where-Object {$_ -match '^frontend/tsconfig[^/]*\.json$'})
$inputs=@()
foreach($file in $files) {
 $target=Join-Path $temporaryRoot $file.Substring('frontend/'.Length)
 New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName='git';$start.Arguments="show :$file";$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.CreateNoWindow=$true
 $process=New-Object Diagnostics.Process;$process.StartInfo=$start;[void]$process.Start()
 $stream=[IO.File]::Create($target)
 try{$process.StandardOutput.BaseStream.CopyTo($stream)}finally{$stream.Dispose()}
 $process.WaitForExit();if($process.ExitCode -ne 0){throw "Cannot export indexed source $file"};$process.Dispose()
 $inputs+=[ordered]@{path=$file;gitBlob=(git rev-parse ":$file");sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash.ToLowerInvariant()}
}
New-Item -ItemType Junction -Path (Join-Path $temporaryRoot 'node_modules') -Target $dependencies | Out-Null
$nodeVersion=& $node --version
$versions=[ordered]@{}
foreach($package in @('esbuild','@playwright/test','react','react-dom')){$versions[$package]=(Get-Content -Raw -LiteralPath (Join-Path $dependencies "$package/package.json")|ConvertFrom-Json).version}
[ordered]@{sourceTree='85bd4cfa8816736231449abeb985a5b573fb79ab';temporaryRoot=$temporaryRoot;node=$node;nodeVersion=$nodeVersion;dependencyDirectory=$dependencies;dependencyVersions=$versions;indexedFiles=$inputs;qualification='Only indexed source copies, no installs, no modification of original worktree/dependencies, browser fixtures intercept requests; production routes not launched'}|ConvertTo-Json -Depth 8|Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/frontend_inputs.json"
Push-Location $temporaryRoot
try {
 foreach($file in @('shift-plan-api.test.mjs','secure-shift-plan-browser.test.mjs','shift-plan-preview.test.mjs','shift-plan-preview-browser.test.mjs')) {
  $name=$file.Replace('.test.mjs','');$output="$receiptRoot/frontend-$name.txt"
  & $node --test "tests/$file" *> $output
  $commandExit=$LASTEXITCODE
  [ordered]@{command="`"$node`" --test tests/$file";workingDirectory=$temporaryRoot;sourceTree='85bd4cfa8816736231449abeb985a5b573fb79ab';exitCode=$commandExit;output=$output.Substring($workspace.Length+1).Replace('\','/');sqlExecution='NOT_RUN';httpExecution='offline browser interception/mock fetch only'}|ConvertTo-Json|Set-Content -Encoding utf8 -LiteralPath "$receiptRoot/frontend-$name.command.json"
  Write-Output "FRONTEND=$name EXIT=$commandExit";Get-Content -LiteralPath $output -Tail 10
 }
} finally {Pop-Location}
