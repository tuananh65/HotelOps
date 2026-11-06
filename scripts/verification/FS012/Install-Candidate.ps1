$ErrorActionPreference='Stop'
& "$PSScriptRoot/Install-Lifecycle.ps1"
& "$PSScriptRoot/Test-NotificationProposal.ps1"
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='HotelOps_Canonical_20261106_f120001_Fresh';$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$ct=New-Object Threading.CancellationTokenSource;$ct.CancelAfter(60000)
try{$c.OpenAsync($ct.Token).GetAwaiter().GetResult();foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PWD 'scripts/database/release/fs012-candidate/07_ExecutableCandidate.sql')),'(?im)^\s*GO\s*\r?$')){if(!$batch.Trim()){continue};$cmd=$c.CreateCommand();$cmd.CommandText=$batch;$cmd.CommandTimeout=30;try{[void]$cmd.ExecuteNonQueryAsync($ct.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}};Write-Output 'FS012_PROVISIONAL_CANDIDATE_INSTALLED'}finally{$c.Dispose();$ct.Dispose()}
$boundary=[IO.File]::ReadAllText((Join-Path $PWD 'scripts/database/release/fs012-candidate/10_SqlWriteBoundary.sql'))
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$ct=New-Object Threading.CancellationTokenSource;$ct.CancelAfter(60000)
try{$c.OpenAsync($ct.Token).GetAwaiter().GetResult();foreach($batch in [regex]::Split($boundary,'(?im)^\s*GO\s*\r?$')){if(!$batch.Trim()){continue};$cmd=$c.CreateCommand();$cmd.CommandText=$batch;try{[void]$cmd.ExecuteNonQueryAsync($ct.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}};'FS012_SQL_BOUNDARY_INSTALLED_NOLOGIN_PRINCIPAL'}finally{$c.Dispose();$ct.Dispose()}
