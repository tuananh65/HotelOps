param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Trial,
 [ValidateSet('LinkedWork','Withdrawal','IncidentRead','IncidentLink','Intake','Handover','CompletionWork','Completion','CompletionUi','Escalation','EscalationUi','HandoverDelivery','OwnTimeLog','ReportActors','TaskExecution')][string]$Workflow='LinkedWork')
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
 $capabilities=if($Workflow -eq 'TaskExecution'){@('TaskEntry','TaskSubmission','TaskVerification','TaskDecline')}elseif($Workflow -in @('Escalation','EscalationUi')){@('InternalRequestCompletion','InternalRequestEscalation','InternalRequestEscalation','NotificationAcknowledgment')}elseif($Workflow -in @('Completion','CompletionUi')){@('InternalRequestCleaningLinks','InternalRequestIncidentLinks','InternalRequestHandover','InternalRequestCompletion','InternalRequestCompletion')}elseif($Workflow -eq 'CompletionWork'){@('InternalRequestCleaningLinks','InternalRequestIncidentLinks','InternalRequestHandover')}elseif($Workflow -in @('Handover','HandoverDelivery')){@('InternalRequestHandover','InternalRequestHandover')}elseif($Workflow -eq 'Intake'){@('InternalRequestIntake','InternalRequestIntake')}elseif($Workflow -eq 'IncidentLink'){@('InternalRequestIncidentLinks','InternalRequestIncidentLinks')}elseif($Workflow -in @('IncidentRead','OwnTimeLog','ReportActors')){@()}elseif($Workflow -eq 'Withdrawal'){@('InternalRequestWithdrawal','InternalRequestWithdrawal')}else{@('TaskEntry','TaskSubmission','TaskVerification','TaskDecline','TaskPlanning','InternalRequestCleaningLinks','InternalRequestCleaningLinks')}
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
 if($Workflow -eq 'ReportActors') {
  # Commit only in the verified disposable clone; whole-clone cleanup preserves immutable attribution.
  $sql=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CanonicalDatabase/ModelProbe/SprintCleanerAttribution.sql'))
  foreach($literal in @('HotelOps_DEV3_142A_20261114_cc78c13f','1D5A9B53-7D19-435A-96AB-9FDFF447B6E0','cc78c13ffc124efcad67b3d804baa84c')) {
   if(!$sql.Contains($literal)){throw 'Missing cleaner attribution identity guard'}
  }
  $sql=$sql.Replace('HotelOps_DEV3_142A_20261114_cc78c13f',$receipt.database).Replace('1D5A9B53-7D19-435A-96AB-9FDFF447B6E0',$receipt.databaseGuid).Replace('cc78c13ffc124efcad67b3d804baa84c',$receipt.run)
  [IO.File]::WriteAllText("$prefix-cleaner.sql",("BEGIN TRANSACTION;`n"+$sql+"`nCOMMIT;"),[Text.UTF8Encoding]::new($false))
  sqlcmd -S localhost -d $receipt.database -E -C -b -i "$prefix-cleaner.sql" -o "$prefix-cleaner.txt"
  if($LASTEXITCODE -ne 0){throw 'Clone cleaner attribution installation failed'}
 }
 $env:ConnectionStrings__HotelOpsDb="Server=localhost;Database=$($receipt.database);Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
 $env:CanonicalFrontend__Root=Join-Path $workspace 'frontend/dist'
 $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='https://localhost:7142'
 $env:InspectionNotifications__Enabled='false';$env:TaskNotifications__Enabled='false';$env:InternalRequestNotifications__Enabled='false'
 $env:DEV3_VERIFY_REPORT_ACTORS=if($Workflow -eq 'ReportActors'){'1'}else{'0'}
 $env:DEV3_VERIFY_OWN_TIME_LOG=if($Workflow -eq 'OwnTimeLog'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_WITHDRAWAL=if($Workflow -eq 'Withdrawal'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_INTAKE=if($Workflow -eq 'Intake'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_COMPLETION_UI=if($Workflow -eq 'CompletionUi'){'1'}else{'0'}
 if($Workflow -eq 'CompletionUi'){$env:InternalRequestNotifications__Enabled='true';$env:InternalRequestNotifications__PollSeconds='2'}
 $env:DEV3_VERIFY_IR_ESCALATION_UI=if($Workflow -eq 'EscalationUi'){'1'}else{'0'}
 if($Workflow -eq 'EscalationUi'){$env:InternalRequestNotifications__Enabled='true';$env:InternalRequestNotifications__PollSeconds='2'}
 $env:DEV3_VERIFY_IR_ESCALATION=if($Workflow -eq 'Escalation'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_COMPLETION=if($Workflow -eq 'Completion'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_COMPLETION_WORK=if($Workflow -eq 'CompletionWork'){'1'}else{'0'}
 $env:DEV3_VERIFY_IR_HANDOVER_DELIVERY=if($Workflow -eq 'HandoverDelivery'){'1'}else{'0'}
 if($Workflow -eq 'HandoverDelivery'){$env:InternalRequestNotifications__Enabled='true';$env:InternalRequestNotifications__PollSeconds='2'}
 $env:DEV3_VERIFY_IR_HANDOVER=if($Workflow -in @('Handover','HandoverDelivery')){'1'}else{'0'}
 if($Workflow -eq 'Intake'){
  $env:InternalRequestNotifications__Enabled='true';$env:InternalRequestNotifications__PollSeconds='60'
  $evidenceRoot=[IO.Path]::GetFullPath("$prefix-evidence")
  if(Test-Path -LiteralPath $evidenceRoot){throw 'Preserve existing evidence directory'}
  $env:Evidence__PrivateRoot=$evidenceRoot
 }
 $env:DEV3_VERIFY_INCIDENT_LINK=if($Workflow -eq 'IncidentLink'){'1'}else{'0'}
 $env:DEV3_VERIFY_INCIDENT_READ=if($Workflow -in @('IncidentRead','IncidentLink')){'1'}else{'0'}
 $hostProcess=Start-Process dotnet -ArgumentList 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll' -WindowStyle Hidden -PassThru -RedirectStandardOutput "$prefix-host.log" -RedirectStandardError "$prefix-host-error.log"
 $ready=$false
 for($attempt=0;$attempt -lt 30;$attempt++){
  if($hostProcess.HasExited){throw 'Owned API exited before readiness'}
  try{$response=Invoke-WebRequest https://localhost:7142/api/auth/csrf -UseBasicParsing -TimeoutSec 3;if($response.StatusCode -eq 200){$ready=$true;break}}catch{Start-Sleep -Milliseconds 400}
 }
 if(!$ready){throw 'Owned HTTPS host unavailable'}
 $probeMode=if($Workflow -eq 'TaskExecution'){'--sprint-task-decline'}else{'--sprint-linked-task-creation'}
 dotnet scripts/verification/CanonicalDatabase/ModelProbe/bin/Debug/net10.0/ModelProbe.dll $probeMode $receiptPath "$prefix-runtime.json" > "$prefix-probe.log" 2>&1
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
 if($Workflow -eq 'Intake' -and $evidenceRoot -and (Test-Path -LiteralPath $evidenceRoot)){
  $expected=[IO.Path]::GetFullPath((Join-Path $workspace "artifacts/$Trial-evidence"))
  $resolved=(Resolve-Path -LiteralPath $evidenceRoot).Path
  if($resolved -cne $expected -or !$resolved.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar)){throw 'Evidence cleanup path mismatch'}
  Remove-Item -LiteralPath $resolved -Recurse -Force
  [IO.File]::WriteAllText("$prefix-evidence-cleanup.json",'{"ownedEvidenceRemoved":true}',[Text.UTF8Encoding]::new($false))
 }
}
