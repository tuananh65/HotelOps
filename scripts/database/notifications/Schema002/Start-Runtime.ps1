#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$RuntimeDirectory,
 [Parameter(Mandatory=$true)][string]$ExistingPfx,[Parameter(Mandatory=$true)][string]$ExistingPfxCredential)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Safety.ps1')
$m=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$null=Assert-NotificationTrialManifest $m ([Security.Principal.WindowsIdentity]::GetCurrent().Name)
& (Join-Path $PSScriptRoot 'Install.ps1') -Stage Verify -Manifest $Manifest -EvidenceDirectory (Join-Path (Split-Path -Parent $Manifest) 'reconciliation')
if(Get-NetTCPConnection -LocalPort 7029 -State Listen -ErrorAction SilentlyContinue){throw 'Port 7029 occupied: never replace another runtime.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$binaryDir=Join-Path $repo 'src/HotelOps.Api/bin/Debug/net10.0'
$binary=Join-Path $binaryDir 'HotelOps.Api.dll'
if([Diagnostics.FileVersionInfo]::GetVersionInfo($binary).ProductVersion -cne '1.0.0+e76d6fc2ad9915bf03bd5640a4ee6f63c1806550'){
 throw 'Use exact accepted consumer binary for this SQL-only change.'
}
$null=New-Item -ItemType Directory -Path $RuntimeDirectory
Copy-Item -Path (Join-Path $binaryDir '*') -Destination $RuntimeDirectory -Recurse
$cs=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$cs['Data Source']='tcp:DESKTOP-JI8U50T,1433';$cs['Initial Catalog']=$m.database
$cs['Integrated Security']=$true;$cs['Encrypt']=$true;$cs['TrustServerCertificate']=$false
$settings=@{
 'ConnectionStrings__HotelOpsDb'=$cs.ConnectionString;
 'ASPNETCORE_ENVIRONMENT'='Development';'ASPNETCORE_URLS'='https://localhost:7029';
 'Kestrel__Certificates__Default__Path'=$ExistingPfx;
 'Kestrel__Certificates__Default__AllowInvalid'='false';
 'NotificationDelivery__Enabled'='false';
 'CleaningAssignment__Candidate'='OFF';'CleaningClosure__Candidate'='OFF';
 'CleaningRework__Candidate'='OFF';'CleaningCancellation__Candidate'='OFF';
 'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command'='Warning'
}
$credential=Import-Clixml -LiteralPath $ExistingPfxCredential
$settings['Kestrel__Certificates__Default__Password']=$credential.GetNetworkCredential().Password
$saved=@{}
try{
 foreach($name in $settings.Keys){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process');[Environment]::SetEnvironmentVariable($name,$settings[$name],'Process')}
 $p=Start-Process -FilePath 'dotnet' -ArgumentList @('"'+(Join-Path $RuntimeDirectory 'HotelOps.Api.dll')+'"') -WorkingDirectory $RuntimeDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $RuntimeDirectory 'stdout.log') -RedirectStandardError (Join-Path $RuntimeDirectory 'stderr.log')
 [pscustomobject]@{pid=$p.Id;endpoint='https://localhost:7029';database=$m.database;database_guid=$m.database_guid;
  lease_end_utc=$m.lease_end_utc;binary_source_commit='e76d6fc2ad9915bf03bd5640a4ee6f63c1806550';
  producers_enabled=$false;workers_enabled=$false;public_cleaning_candidates='OFF';health='PENDING'} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RuntimeDirectory 'runtime.json') -Encoding UTF8
}finally{
 foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}
 $settings.Clear();$credential=$null
}

