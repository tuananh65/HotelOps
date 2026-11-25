param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestTopup2611252300')][string]$Database)
$ErrorActionPreference='Stop'
if(!$Database){throw 'Explicit database required.'}
& "$PSScriptRoot/Backup-Workday.ps1" -Database $Database
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
 $db.OpenAsync().GetAwaiter().GetResult();$q=$db.CreateCommand();$q.CommandTimeout=180
 $q.CommandText=Get-Content "$PSScriptRoot/owner-stock-topup-261125.sql" -Raw -Encoding UTF8
 $reader=$q.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@()
 while($reader.Read()){$o=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$o[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=[pscustomobject]$o}
 $reader.Close();$rows|ConvertTo-Json -Depth 5|Set-Content "$PSScriptRoot/../../../artifacts/workday/topup-$Database.json" -Encoding UTF8
 Write-Output "PASS $Database receipt rows: $($rows.Count)"
} finally {$db.Dispose()}
