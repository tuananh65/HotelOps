[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RepositoryRoot,[switch]$Execute)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path -LiteralPath $RepositoryRoot).Path
$source=Join-Path $repository 'scripts/verification/FS011'
$destination=Join-Path $repository 'scripts/verification/FS011Tester'
$sqlDestination=Join-Path $repository 'scripts/database/release/fs011tester-candidate'
$docsDestination=Join-Path $repository 'docs/fs011tester'
if(!$Execute){[ordered]@{mode='PLAN';database='HotelOps_Canonical_20261106_f11tester_Fresh';owner='DEV1_FS011Tester';existingDatabaseOverwrite=$false;faultModes='DENIED';requiresLocalSqlExpressAndTrustedCertificate=$true}|ConvertTo-Json;return}
foreach($target in @($destination,$sqlDestination,$docsDestination)){if(Test-Path -LiteralPath $target){throw 'Tester scaffold exists: reuse start/stop only; no overwrite/reset. Inspect locally.'}}
function Convert-Tester([string]$text){return $text.Replace('HotelOps_Canonical_20261106_f110001_Fresh','HotelOps_Canonical_20261106_f11tester_Fresh').Replace('FS011','FS011Tester').Replace('fs011','fs011tester')}
foreach($target in @($destination,$sqlDestination,$docsDestination)){[void](New-Item -ItemType Directory -Path $target);[IO.File]::WriteAllText((Join-Path $target '.gitignore'),"*`n")}
foreach($file in Get-ChildItem -LiteralPath $source -File){
 if($file.Name -in @('.gitignore','New-TesterTrial.ps1','Build-TestHandover.cjs','Prepare-Package.cjs','Validate-Zip.cjs','Open-TesterBrowser.cjs','Tester-Setup.ps1','Install-Candidate.ps1','Test-SchemaSafety.ps1','Test-RestrictedWriter.ps1','Test-NotificationProposal.ps1','Run-LiveSuite.ps1')){continue}
 $name=Convert-Tester $file.Name
 $text=Convert-Tester ([IO.File]::ReadAllText($file.FullName))
 if($name -eq 'Program.cs'){
  $anchor='if (mode == "credentials-ipc")'
  $gate='if (mode is not ("plan" or "gateway" or "credentials-ipc" or "schema" or "seedstatus" or "seed" or "lifecycle-inspect" or "candidate-inspect" or "candidate-deliver")) throw new InvalidOperationException("TESTER_FAULT_MODE_DENIED");'
  if(!$text.Contains($anchor)){throw 'Tester gate source anchor missing'}
  $text=$text.Replace($anchor,$gate+"`n"+$anchor)
  $text=$text.Replace('OperationalStatus = scope == branch && i == 1 ? "CLEANING" : "DIRTY", IsActive = true','OperationalStatus = scope == branch && i == 1 ? "CLEANING" : "DIRTY", IsActive = !(scope == branch && i == 25)')
 }
 [IO.File]::WriteAllText((Join-Path $destination $name),$text)
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $repository 'scripts/database/release/fs011-candidate') -File){if($file.Extension -ne '.sql'){continue};[IO.File]::WriteAllText((Join-Path $sqlDestination $file.Name),(Convert-Tester ([IO.File]::ReadAllText($file.FullName))))}
Copy-Item -LiteralPath (Join-Path $repository 'docs/fs011/SOURCE_MANIFEST_BASE.json') -Destination (Join-Path $docsDestination 'SOURCE_MANIFEST_BASE.json')
& (Join-Path $destination 'New-ApprovedTrial.ps1') -ExecuteApprovedCreate -AllowLocalSqlCertificateException
& (Join-Path $destination 'Install-Lifecycle.ps1')
& dotnet build (Join-Path $destination 'FS011Tester.Verification.csproj')
if($LASTEXITCODE -ne 0){throw 'Tester build failed'}
& dotnet (Join-Path $destination 'bin/Debug/net10.0/FS011Tester.Verification.dll') seed --approved-seed --local-sql-certificate-exception
if($LASTEXITCODE -ne 0){throw 'Tester seed failed; no reset/overwrite attempted'}
# Install schema only; do not run developer fault/negative suites during Tester setup.
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']='.\SQLEXPRESS';$builder['Initial Catalog']='HotelOps_Canonical_20261106_f11tester_Fresh';$builder['Integrated Security']=$true;$builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(60000)
try {
 $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
 foreach($name in @('02_NotificationProposal.sql','03_VerifyNotificationProposal.sql','07_ExecutableCandidate.sql')){
  foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $sqlDestination $name)),'(?im)^\s*GO\s*\r?$')){
   if(!$batch.Trim()){continue};$command=$connection.CreateCommand();$command.CommandText=$batch;$command.CommandTimeout=30
   try{[void]$command.ExecuteNonQueryAsync($budget.Token).GetAwaiter().GetResult()}finally{$command.Dispose()}
  }
 }
}finally{$connection.Dispose();$budget.Dispose()}
[ordered]@{status='TESTER_TRIAL_CREATED';owner='DEV1_FS011Tester';credentials='DPAPI CurrentUser only, never printed';faultModes='DENIED';oldTrialsWritten=$false}|ConvertTo-Json
