param([string]$Database='HotelOpsCompactWorkdayTest20261122',[int]$Port=5768)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$'){throw 'Compact database required.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString;$env:ASPNETCORE_ENVIRONMENT='Development';$env:Compact__WorkerEnabled='false'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'));$out=Join-Path $root 'artifacts/workday'
$process=Start-Process dotnet -ArgumentList @((Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll'),'--urls',"http://127.0.0.1:$Port") -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out "api-$Port.log") -RedirectStandardError (Join-Path $out "api-$Port.err.log")
@{pid=$process.Id;port=$Port;database=$Database;ownedBy='workday-integration'} | ConvertTo-Json | Set-Content (Join-Path $out "api-$Port-process.json")
$ready=$false
for($i=0;$i -lt 80;$i++){
 try{$health=Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 1;if($health.schema -eq '2.1.0'){$ready=$true;break}}catch{}
 if($process.HasExited){throw 'API exited; inspect the local error log.'}
 Start-Sleep -Milliseconds 250
}
if(-not $ready){Stop-Process -Id $process.Id -Force;throw 'API readiness timeout; owned process stopped.'}
Write-Output "API ready: process $($process.Id), port $Port, database $Database"
