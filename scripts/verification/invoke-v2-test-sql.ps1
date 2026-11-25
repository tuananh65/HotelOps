param([Parameter(Mandatory=$true)][string]$SqlFile,[string]$ResultName='sql-result')
$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestV22611250010'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$file=[IO.Path]::GetFullPath($SqlFile)
if(-not $file.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($file) -ne '.sql'){throw 'Only repository SQL files are accepted.'}
if($ResultName -notmatch '^[a-z0-9-]+$'){throw 'Invalid artifact name.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 $guard=$connection.CreateCommand();$guard.CommandText='SELECT DB_NAME()'
 if($guard.ExecuteScalar() -ne $database){throw 'Unexpected database.'}
 $sets=@()
 foreach($batch in [regex]::Split((Get-Content -LiteralPath $file -Raw -Encoding UTF8),'(?im)^\s*GO\s*$')){
  if(-not $batch.Trim()){continue}
  $command=$connection.CreateCommand();$command.CommandTimeout=120;$command.CommandText=$batch
  $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult()
  try {do {$rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$v=$reader.GetValue($i);$row[$reader.GetName($i)]=if($v -is [DBNull]){$null}else{$v}};$rows+=,$row};if($reader.FieldCount -gt 0){$sets+=,@($rows)}}while($reader.NextResultAsync().GetAwaiter().GetResult())}finally{$reader.Dispose();$command.Dispose()}
 }
 @{database=$database;at=[DateTime]::UtcNow.ToString('o');script=[IO.Path]::GetFileName($file);sets=$sets} | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $root "artifacts/restructure-v2/$ResultName.json") -Encoding UTF8
 Write-Output "PASS: $ResultName; database=$database; resultSets=$($sets.Count)"
}finally{$connection.Dispose()}
