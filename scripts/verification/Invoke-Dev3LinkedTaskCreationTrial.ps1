param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Trial,
 [ValidateSet('LinkedWork','Withdrawal','IncidentRead','IncidentLink')][string]$Workflow='LinkedWork')
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $workspace
if(Get-NetTCPConnection -LocalPort 7142,7143 -State Listen -ErrorAction SilentlyContinue){throw 'Trial HTTPS port in use'}
$prefix=Join-Path $workspace "artifacts/$Trial"
$receiptPath="$prefix-clone.json"
if(Test-Path -LiteralPath $receiptPath){throw 'Preserve existing clone receipt'}
$hostProcess=$null
try {
 & (Join-Path $PSScriptRoot 'Invoke-Dev3CriteriaClone.ps1') -Mode Create -ReceiptPath $receiptPath
 $receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
 if($receipt.status -cne 'CLONE_READY' -or $receipt.source -cne 'HotelOps_DEV3_142A_20261114_cc78c13f' -or
   $receipt.run -cnotmatch '^[a-f0-9]{32}$' -or $receipt.database -cne ('HotelOps_DEV3_142A_CriteriaTrial_'+$receipt.run.Substring(0,8)) -or
   [Guid]$receipt.databaseGuid -eq [Guid]::Empty){throw 'Verified disposable clone receipt required'}
 # Adapt only identity literals in previously reviewed scripts to the independently
 # verified new clone. The original disposable source schema remains unchanged.
 $capabilities=if($Workflow -eq 'IncidentLink'){@('InternalRequestIncidentLinks','InternalRequestIncidentLinks')}elseif($Workflow -eq 'IncidentRead'){@()}elseif($Workflow -eq 'Withdrawal'){@('InternalRequestWithdrawal','InternalRequestWithdrawal')}else{@('TaskEntry','TaskSubmission','TaskVerification','TaskDecline','TaskPlanning','InternalRequestCleaningLinks','InternalRequestCleaningLinks')}
 foreach($cap in $capabilities) {
  $sourcePath=Join-Path $PSScriptRoot "CanonicalDatabase/${cap}Isolated.sql"
  $sql=[IO.File]::ReadAllText($sourcePath)
  foreach($literal in @('HotelOps_DEV3_142A_20261114_cc78c13f','1D5A9B53-7D19-435A-96AB-9FDFF447B6E0','cc78c13ffc124efcad67b3d804baa84c')) {
   if(!$sql.Contains($literal)){throw "Missing identity guard in $cap"}
  }
  $sql=$sql.Replace('HotelOps_DEV3_142A_20261114_cc78c13f',$receipt.database).Replace('1D5A9B53-7D19-435A-96AB-9FDFF447B6E0',$receipt.databaseGuid).Replace('cc78c13ffc124efcad67b3d804baa84c',$receipt.run)
  $scriptPath="$prefix-$cap.sql";[IO.File]::WriteAllText($scriptPath,$sql,[Text.UTF8Encoding]::new($false))
  sqlcmd -S localhost -d $receipt.database -E -C -b -i $scriptPath -o "$prefix-$cap.txt"
  if($LASTEXITCODE -ne 0){throw "Clone capability installation failed: $cap"}
 }
 $env:ConnectionStrings__HotelOpsDb="Server=localhost;Database=$($receipt.database);Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
 $env:CanonicalFrontend__Root=Join-Path $workspace 'frontend/dist'
 $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='https://localhost:7142'
 $env:InspectionNotifications__Enabled='false';$env:TaskNotifications__Enabled='false';$env:InternalRequestNotifications__Enabled='false'
 $env:DEV3_VERIFY_IR_WITHDRAWAL=if($Workflow -eq 'Withdrawal'){'1'}else{'0'}
 $env:DEV3_VERIFY_INCIDENT_LINK=if($Workflow -eq 'IncidentLink'){'1'}else{'0'}
 $env:DEV3_VERIFY_INCIDENT_READ=if($Workflow -in @('IncidentRead','IncidentLink')){'1'}else{'0'}
 $hostProcess=Start-Process dotnet -ArgumentList 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$prefix-host.log" -RedirectStandardError "$prefix-host-error.log"
 $ready=$false
 for($attempt=0;$attempt -lt 30;$attempt++){
  if($hostProcess.HasExited){throw 'Owned API exited before readiness'}
  try{$response=Invoke-WebRequest https://localhost:7142/api/auth/csrf -UseBasicParsing -TimeoutSec 3;if($response.StatusCode -eq 200){$ready=$true;break}}catch{Start-Sleep -Milliseconds 400}
 }
 if(!$ready){throw 'Owned HTTPS host unavailable'}
 dotnet scripts/verification/CanonicalDatabase/ModelProbe/bin/Debug/net10.0/ModelProbe.dll --sprint-linked-task-creation $receiptPath "$prefix-runtime.json" > "$prefix-probe.log" 2>&1
 if($LASTEXITCODE -ne 0){throw "Linked creation trial failed: $prefix-runtime.json"}
} finally {
 if($hostProcess){
  $owned=Get-CimInstance Win32_Process -Filter "ProcessId=$($hostProcess.Id)"
  if($owned -and $owned.Name -eq 'dotnet.exe' -and $owned.CommandLine -match 'HotelOps.Api.dll'){
   Stop-Process -Id $hostProcess.Id;Wait-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue
  }
 }
 if(Test-Path -LiteralPath $receiptPath){
  # Release only this runner's local ADO.NET pools before guarded clone cleanup.
  [System.Data.SqlClient.SqlConnection]::ClearAllPools()
  $cleanupReceipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
  if($cleanupReceipt.status -ceq 'CLONE_READY'){
   & (Join-Path $PSScriptRoot 'Invoke-Dev3CriteriaClone.ps1') -Mode Cleanup -ReceiptPath $receiptPath
  }else{Write-Warning 'Clone preparation incomplete; preserve receipt for identity-checked recovery.'}
 }
}
