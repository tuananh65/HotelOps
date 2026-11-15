$ErrorActionPreference='Stop'
if (Get-NetTCPConnection -LocalPort 7142,7143 -State Listen -ErrorAction SilentlyContinue) { throw 'Trial HTTPS port in use' }
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
try {
 $connection.Open();$query=$connection.CreateCommand()
 $query.CommandText="SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita' AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f' AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0') AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c') AND NOT EXISTS(SELECT 1 FROM dbo.users) AND NOT EXISTS(SELECT 1 FROM dbo.branches) THEN 1 ELSE 0 END"
 if ([int]$query.ExecuteScalar() -ne 1) { throw 'Exact empty disposable identity required' }
} finally { $connection.Dispose() }
$env:ConnectionStrings__HotelOpsDb='Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
$env:CanonicalFrontend__Root=Join-Path (Get-Location) 'frontend/dist'
$env:Evidence__PrivateRoot=Join-Path (Get-Location) 'artifacts/dev3-142a-rework-evidence'
$env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='https://localhost:7142'
$env:InspectionNotifications__Enabled='false';$env:TaskNotifications__Enabled='false'
$trial=1
while(Test-Path "artifacts/checkpoint76-trial$trial-runtime.json"){ $trial++ }
$prefix="artifacts/checkpoint76-trial$trial"
$hostProcess=Start-Process dotnet -ArgumentList 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$prefix-host.log" -RedirectStandardError "$prefix-host-error.log"
try {
 $ready=$false
 for($attempt=0;$attempt -lt 30;$attempt++){try { $response=Invoke-WebRequest https://localhost:7142/api/auth/csrf -UseBasicParsing; if($response.StatusCode -eq 200){$ready=$true;break} }catch{Start-Sleep -Milliseconds 400}}
 if(!$ready){throw 'Owned HTTPS host unavailable'}
 dotnet scripts/verification/CanonicalDatabase/ModelProbe/bin/Debug/net10.0/ModelProbe.dll --sprint-audit "$prefix-runtime.json" > "$prefix-probe.log" 2>&1
 if($LASTEXITCODE -ne 0){throw "Trial failed: inspect $prefix-runtime.json"}
} finally {
 $owned=Get-CimInstance Win32_Process -Filter "ProcessId=$($hostProcess.Id)"
 if($owned -and $owned.Name -eq 'dotnet.exe' -and $owned.CommandLine -match 'HotelOps.Api.dll'){Stop-Process -Id $hostProcess.Id;Wait-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue}
}
