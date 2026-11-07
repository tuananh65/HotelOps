$ErrorActionPreference='Stop'
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='HotelOps_Canonical_20261106_f130001_Fresh';$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(60000)
try{$c.OpenAsync($budget.Token).GetAwaiter().GetResult();foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PWD 'scripts/database/release/fs013-candidate/11_CleaningExecution.sql')),'(?im)^\s*GO\s*\r?$')){if(!$batch.Trim()){continue};$cmd=$c.CreateCommand();$cmd.CommandTimeout=30;$cmd.CommandText=$batch;try{[void]$cmd.ExecuteNonQueryAsync($budget.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}};'FS013_EXECUTION_INSTALLED'}finally{$c.Dispose();$budget.Dispose()}
