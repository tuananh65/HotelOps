$ErrorActionPreference='Stop'
& "$PSScriptRoot/Install-Lifecycle.ps1"
& "$PSScriptRoot/Test-NotificationProposal.ps1"
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='HotelOps_Canonical_20261106_f130001_Fresh';$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(120000)
try{$c.OpenAsync($budget.Token).GetAwaiter().GetResult();foreach($file in @('07_ExecutableCandidate.sql','10_SqlWriteBoundary.sql','11_CleaningExecution.sql')){foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PWD ('scripts/database/release/fs013-candidate/'+$file))),'(?im)^\s*GO\s*\r?$')){if(!$batch.Trim()){continue};$cmd=$c.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=$batch;try{[void]$cmd.ExecuteNonQueryAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}}};'FS013_ASSIGNMENT_AND_EXECUTION_INSTALLED_PROVISIONAL'}finally{$c.Dispose();$budget.Dispose()}