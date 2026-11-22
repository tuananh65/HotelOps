param([string]$Database='HotelOpsCompactDB',[switch]$Apply)
$ErrorActionPreference='Stop'
if($Database -notin @('HotelOpsCompactDB','HotelOpsCompactWorkdayTestV2Data20261122')){throw 'Unexpected database.'}
if($Apply){& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=Get-Content -LiteralPath (Join-Path $PSScriptRoot '08_V2DataNormalization.sql') -Raw -Encoding UTF8
 [void]$cmd.Parameters.Add('@Apply',[System.Data.SqlDbType]::Bit);$cmd.Parameters['@Apply'].Value=[bool]$Apply
 $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sets=@()
 do{
  $rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$rows+=[pscustomobject]$row};if($rows.Count){$sets+=,@($rows)}
 }while($reader.NextResultAsync().GetAwaiter().GetResult())
 $reader.Dispose();$sets | ConvertTo-Json -Depth 8
}finally{$c.Dispose()}
