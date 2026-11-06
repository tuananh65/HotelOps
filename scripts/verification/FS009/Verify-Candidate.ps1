$ErrorActionPreference='Stop'
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='HotelOps_Canonical_20261106_230000_f090001_Fresh';$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$ct=New-Object Threading.CancellationTokenSource;$ct.CancelAfter(60000)
try{$c.OpenAsync($ct.Token).GetAwaiter().GetResult();foreach($name in @('03_VerifyNotificationProposal.sql','05_VerifyLifecycle.sql','08_VerifyCandidate.sql')){$cmd=$c.CreateCommand();$cmd.CommandTimeout=20;$cmd.CommandText=[IO.File]::ReadAllText((Join-Path $PWD ('scripts/database/release/fs009-candidate/'+$name)));try{[void]$cmd.ExecuteNonQueryAsync($ct.Token).GetAwaiter().GetResult()}finally{$cmd.Dispose()}};[ordered]@{status='PASS_READ_ONLY_VERIFY_SQL';target='HotelOps_Canonical_20261106_230000_f090001_Fresh';scripts=3;acceptedA01=$false;recoveryExecuted=$false}|ConvertTo-Json|Set-Content -Encoding utf8 docs/fs009/VERIFY_SQL_RESULTS.json}finally{$c.Dispose();$ct.Dispose()}
