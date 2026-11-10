#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$dll=Join-Path $PSScriptRoot 'bin/Debug/net10.0/FE04NotificationRuntime.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Build launcher first.'}
$errors=$null;$tokens=$null
[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Invoke-Preflight.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Launcher parser failed.'}
$plan=(& dotnet $dll --plan)|ConvertFrom-Json
if($LASTEXITCODE -ne 0 -or $plan.status -cne 'CONTRACT_PREPARED_NOT_ACCEPTED' -or $plan.sql -cne '15_NOT_RUN'){throw 'Plan must never report acceptance.'}
function Expect-Closed([string[]]$Values){
 $psi=New-Object Diagnostics.ProcessStartInfo
 $psi.FileName='dotnet';$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true
 $psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true;$psi.RedirectStandardInput=$true
 $psi.Arguments='"'+$dll+'" '+(($Values|ForEach-Object {'"'+$_+'"'}) -join ' ')
 $p=[Diagnostics.Process]::Start($psi)
 try{
  $p.StandardInput.Close();$out=$p.StandardOutput.ReadToEnd();$err=$p.StandardError.ReadToEnd();$p.WaitForExit()
  if($p.ExitCode -ne 3 -or $out.Contains('PASS') -or !$err.Contains('FE04_PREFLIGHT_BLOCKED')){throw 'Expected sanitized fail-closed refusal.'}
 }finally{$p.Dispose()}
}
Expect-Closed @('--run')
Expect-Closed @('--preflight','missing-owner-packet.json','.','missing-fixture.json')
Expect-Closed @('--check-approval','missing-owner-packet.json','.','missing-fixture.json')
[pscustomobject]@{status='PASS';checks=5;scope='PowerShell parse, offline plan, forbidden mutation CLI, missing preflight approval, missing approval packet';sqlConnections=0;sqlCases='15_NOT_RUN'}|ConvertTo-Json
$global:LASTEXITCODE=0
