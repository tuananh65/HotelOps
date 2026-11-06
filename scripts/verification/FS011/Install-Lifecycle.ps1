$ErrorActionPreference='Stop'
$target='HotelOps_Canonical_20261106_f110001_Fresh'
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']=$target;$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
$c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
$ct=New-Object Threading.CancellationTokenSource;$ct.CancelAfter(60000)
try{$c.OpenAsync($ct.Token).GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandText="SELECT CASE WHEN DB_NAME()=N'HotelOps_Canonical_20261106_f110001_Fresh' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))=N'SQLEXPRESS' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS011.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS011') THEN 1 ELSE 0 END";if([int]$cmd.ExecuteScalarAsync($ct.Token).GetAwaiter().GetResult() -ne 1){throw 'TARGET_IDENTITY_MISMATCH'};$cmd.Dispose()
foreach($file in @('01_Install.sql','05_VerifyLifecycle.sql')){$cmd=$c.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=[IO.File]::ReadAllText((Join-Path $PWD ('scripts/database/release/fs011-candidate/'+$file)));[void]$cmd.ExecuteNonQueryAsync($ct.Token).GetAwaiter().GetResult();$cmd.Dispose()}
[ordered]@{result='LIFECYCLE_INSTALLED';target=$target;owner='DEV1_FS011';existingFS004Changed=$false}|ConvertTo-Json
}finally{$c.Dispose();$ct.Dispose()}
