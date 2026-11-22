param([string]$Database='HotelOpsCompactDB',[string]$Server)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$'){throw 'Invalid database scope.'}
& "$PSScriptRoot/Backup-Workday.ps1" -Database $Database -Server $Server
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
if($Server){$builder['Data Source']=$Server}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open();$tx=$connection.BeginTransaction()
try {
 $cmd=$connection.CreateCommand();$cmd.Transaction=$tx;$cmd.CommandTimeout=120
 foreach($batch in [regex]::Split([IO.File]::ReadAllText("$PSScriptRoot/06_Workday.sql"),'(?im)^\s*GO\s*\r?$')){
  if($batch.Trim()){$cmd.CommandText=$batch;[void]$cmd.ExecuteNonQuery()}
 }
 $cmd.CommandText='SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0';if([int]$cmd.ExecuteScalar() -ne 40){throw 'Table budget violated.'}
 $tx.Commit();Write-Output "PASS: $Database upgraded to 2.1.0; 40 tables."
}catch{$tx.Rollback();throw}finally{$connection.Dispose()}
