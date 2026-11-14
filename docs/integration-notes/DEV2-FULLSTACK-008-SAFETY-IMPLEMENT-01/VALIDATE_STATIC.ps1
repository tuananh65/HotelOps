$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $PSScriptRoot 'receipts/STATIC_VALIDATION.json'
if(Test-Path -LiteralPath $out){throw 'No overwrite of static receipt'}
$tokens=$null;$errors=$null
[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'RUN_WINDOWS_SAFETY_ACCEPTANCE.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){$errors|ForEach-Object {$_.Message};throw 'Runner parse failed'}
$nodeCommand=Get-Command node.exe -CommandType Application -ErrorAction SilentlyContinue
$node=if($nodeCommand){$nodeCommand.Source}else{Join-Path $env:ProgramFiles 'nodejs/node.exe'}
if(!(Test-Path -LiteralPath $node -PathType Leaf)){throw 'Installed Node executable not found'}
$node=& $node -p 'process.execPath';if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $node -PathType Leaf)){throw 'Actual Node executable resolution failed'}
foreach($file in @('scripts/verification/Dev2Safety008/browser/child.mjs','scripts/verification/Dev2Safety008/browser/protocol.mjs','docs/integration-notes/DEV2-FULLSTACK-008-SAFETY-IMPLEMENT-01/review-candidate.cjs','docs/integration-notes/DEV2-FULLSTACK-008-SAFETY-IMPLEMENT-01/seal-candidate.cjs')){
    & $node --check (Join-Path $repo $file)
    if($LASTEXITCODE -ne 0){throw 'Node syntax failed'}
}
$template=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AUTHORIZATION_TEMPLATE.json')))
if($template.Approved -ne $false -or $template.AllowFixtureCreate -ne $false -or $template.AllowExactPkCleanup -ne $false){throw 'Template must not approve execution'}
$data=@{status='PASS_STATIC_ONLY';powershellParseErrors=0;nodeSyntaxFiles=4;authorityTemplateApproved=$false;sqlConnections=0;runtimeStarts=0;utc=[DateTime]::UtcNow.ToString('o')}
[IO.File]::WriteAllText($out,($data|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
$data|ConvertTo-Json -Depth 10
