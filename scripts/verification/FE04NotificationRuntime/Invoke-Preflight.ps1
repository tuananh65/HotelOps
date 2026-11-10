#requires -Version 5.1
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ApprovalPacket,
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string]$FixtureManifest,
 [switch]$Connect
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$dll=Join-Path $PSScriptRoot 'bin/Debug/net10.0/FE04NotificationRuntime.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Build the exact pinned launcher before execution.'}
# No security-policy write, User Secrets change, connection-string fallback or runtime deployment.
& dotnet $dll --check-approval $ApprovalPacket $SourceRoot $FixtureManifest
if($LASTEXITCODE -ne 0){throw 'Signed owner/source approval check failed; credential not read.'}
if(!$Connect){return}
$credentialPath=Join-Path $env:LOCALAPPDATA 'HotelOps/ENVNOTIF001-Trial001/local-secrets/SqlRuntime.credential.clixml'
# Owner-controlled handoff only; reject other writers and reparse-point substitution.
$allowedSids=@('S-1-5-18','S-1-5-32-544',[Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
foreach($path in @($credentialPath,(Split-Path $credentialPath))){
 $item=Get-Item -LiteralPath $path
 if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Protected credential reparse point rejected.'}
 $acl=Get-Acl -LiteralPath $path
 if($allowedSids -notcontains $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value){throw 'Credential owner rejected.'}
 foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
  if($rule.AccessControlType -eq 'Allow' -and $allowedSids -notcontains $rule.IdentityReference.Value -and
    ($rule.FileSystemRights -band ([Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Modify -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership))){throw 'Credential path is writable outside its owner boundary.'}
 }
}
$credential=Import-Clixml -LiteralPath $credentialPath
if($credential -isnot [Management.Automation.PSCredential] -or $credential.UserName -cne 'HotelOps_DEV2_FullStack_Runtime'){throw 'Exact restricted SQL credential required.'}
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='dotnet';$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardInput=$true
# Windows command-line quoting; paths cannot contain a double quote. Secrets are never arguments.
$values=@($dll,'--preflight',$ApprovalPacket,$SourceRoot,$FixtureManifest)
foreach($value in $values){if($value.Contains('"') -or $value.EndsWith('\')){throw 'Ambiguous launcher path rejected.'}}
$psi.Arguments=($values|ForEach-Object {'"'+$_+'"'}) -join ' '
$process=[Diagnostics.Process]::Start($psi)
try{
 $payload=@{user=$credential.UserName;password=$credential.GetNetworkCredential().Password}|ConvertTo-Json -Compress
 $process.StandardInput.WriteLine($payload);$process.StandardInput.Close();$payload=$null
 $process.WaitForExit()
 if($process.ExitCode -ne 0){throw 'Restricted preflight failed; no mutation or acceptance authorized.'}
}finally{$payload=$null;$credential=$null;$process.Dispose()}
