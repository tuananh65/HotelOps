param([ValidateSet('Inspect')][string]$Action='Inspect')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$database='HotelOpsCompactWorkdayTestV22611250010'
$fixture=Get-Content (Join-Path $root 'artifacts/restructure-v2/housekeeping-private/fixture.json') -Raw -Encoding utf8|ConvertFrom-Json
if($fixture.database -ne $database -or !$env:HOTELOPS_V2_RUN_ID -or $fixture.runId -ne $env:HOTELOPS_V2_RUN_ID){throw 'Owned synthetic fixture required.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60
 $cmd.CommandText="SELECT COUNT(*) FROM branches WHERE branch_id=@b AND code=@prefix"
 [void]$cmd.Parameters.AddWithValue('@b',[long]$fixture.branch);[void]$cmd.Parameters.AddWithValue('@prefix',[string]$fixture.prefix)
 if($cmd.ExecuteScalarAsync().GetAwaiter().GetResult() -ne 1){throw 'Synthetic branch mismatch.'}
 $snapshot=@{}
 # Snapshot every branch-scoped business table: failed requests cannot leave hidden workflow or ledger writes.
 $cmd.CommandText="SELECT t.name FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE c.name='branch_id' AND t.name NOT IN ('user_sessions') ORDER BY t.name"
 $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$tables=@()
 try {while($reader.ReadAsync().GetAwaiter().GetResult()){$tables+=,[string]$reader.GetValue(0)}} finally {$reader.Dispose()}
 foreach($table in $tables){
  $cmd.CommandText="SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID(@table) ORDER BY ic.key_ordinal"
  [void]$cmd.Parameters.AddWithValue('@table','dbo.'+$table)
  $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$keys=@()
  try {while($reader.ReadAsync().GetAwaiter().GetResult()){$keys+=,('['+([string]$reader.GetValue(0)).Replace(']',']]')+']')}} finally {$reader.Dispose();$cmd.Parameters.RemoveAt('@table')}
  if($keys.Count -eq 0){throw 'Snapshot table has no stable primary key.'}
  $cmd.CommandText='SELECT * FROM dbo.['+$table.Replace(']',']]')+'] WHERE branch_id=@b ORDER BY '+($keys -join ',')+' FOR JSON PATH,INCLUDE_NULL_VALUES'
  $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$json=''
  try {while($reader.ReadAsync().GetAwaiter().GetResult()){$json+=[string]$reader.GetValue(0)}} finally {$reader.Dispose()}
  # Compare full data without emitting document content, account details or token material.
  $hash=[Security.Cryptography.SHA256]::Create()
  try {$snapshot[$table]=[BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($json))).Replace('-','')} finally {$hash.Dispose()}
 }
 $snapshot|ConvertTo-Json -Depth 4 -Compress
} finally {$connection.Dispose()}
