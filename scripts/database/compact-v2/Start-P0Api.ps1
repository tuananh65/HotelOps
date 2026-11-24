param([int]$Port=5774)
$ErrorActionPreference='Stop'
if($Port -lt 1024 -or $Port -in @(5080,5769,5186,5173)){throw 'Dedicated P0 TEST port required'}
if(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue){throw 'Port already in use; existing process left unchanged'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']='HotelOpsCompactWorkdayTestP0261124'
$env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Compact__WorkerEnabled='false'
$env:Compact__CleaningOverdueEnabled='false'
$env:Compact__AutoEndShiftsEnabled='false'
Set-Item -LiteralPath 'Env:Logging__LogLevel__Microsoft.EntityFrameworkCore' -Value 'Warning'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $root 'artifacts/p0-workflow';[void][IO.Directory]::CreateDirectory($out)
$build=Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0'
$runtime=Join-Path $out ('api-runtime-'+(Get-Date -Format 'yyyyMMddHHmmssfff'))
[void][IO.Directory]::CreateDirectory($runtime)
Copy-Item -Path (Join-Path $build '*') -Destination $runtime -Recurse
$binary=Join-Path $runtime 'HotelOps.Api.dll'
$process=Start-Process dotnet -ArgumentList @($binary,'--urls',"http://127.0.0.1:$Port") -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out "api-$Port.log") -RedirectStandardError (Join-Path $out "api-$Port.err.log")
@{pid=$process.Id;port=$Port;database=$builder.InitialCatalog;binary=$binary;ownedBy='p0-integration'}|ConvertTo-Json|Set-Content (Join-Path $out "api-$Port-process.json")
$ready=$false
for($i=0;$i -lt 100;$i++){
 try{$health=Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 1;if($health.schema -eq '3.0.0'){$ready=$true;break}}catch{}
 if($process.HasExited){throw 'P0 API exited; inspect dedicated TEST logs'}
 Start-Sleep -Milliseconds 250
}
if(!$ready){Stop-Process -Id $process.Id -Force;throw 'P0 API timeout; owned process stopped'}
Write-Output "P0 TEST API ready: PID $($process.Id), port $Port, schema 3.0.0"
