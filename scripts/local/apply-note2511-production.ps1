$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$database='HotelOpsCompactDB'
& "$root/scripts/database/compact-v2/Backup-Workday.ps1" -Database $database
$manifest=Get-Content "$root/artifacts/workday/backup-HotelOpsCompactDB.json" -Raw | ConvertFrom-Json
if(!$manifest.verified -or $manifest.database -ne $database -or [DateTime]::Parse($manifest.at).ToUniversalTime() -lt [DateTime]::UtcNow.AddMinutes(-15)){throw 'Fresh verified backup required.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
 $db.OpenAsync().GetAwaiter().GetResult()
 $cmd=$db.CreateCommand();$cmd.CommandTimeout=180
 $cmd.CommandText="SELECT CASE WHEN DB_NAME()='HotelOpsCompactDB' AND (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)=57 THEN 1 ELSE 0 END"
 if($cmd.ExecuteScalar() -ne 1){throw 'Official database/schema mismatch.'}
 $cmd.CommandText=@"
SELECT s.name schema_name,t.name table_name,SUM(p.rows) row_count
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1)
WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name
"@
 function Snapshot {
  $rows=@{};$reader=$cmd.ExecuteReader()
  try{while($reader.Read()){$rows["$($reader['schema_name']).$($reader['table_name'])"]=[long]$reader['row_count']}}finally{$reader.Close()}
  return $rows
 }
 $countSql=$cmd.CommandText;$before=Snapshot
 $cmd.CommandText=Get-Content "$root/scripts/database/compact-v2/note-25-11-refill-om.sql" -Raw -Encoding utf8
 [void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()
 $cmd.CommandText=$countSql;$after=Snapshot
 foreach($key in $before.Keys){if($key -notin @('dbo.audit_logs','dbo.notifications') -and $after[$key] -ne $before[$key]){throw "Unexpected row count change: $key"}}
 $cmd.CommandText="SELECT COUNT(*) FROM dbo.request_workflows WHERE request_type='CONSUMABLE_REFILL_REQUEST' AND status='PENDING_SUPERVISOR'"
 if($cmd.ExecuteScalar() -ne 0){throw 'Legacy pending refill remains.'}
 $cmd.CommandText="SELECT COUNT(*) FROM sys.check_constraints WHERE name='CK_request_workflow_status' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%PENDING_OPERATIONS_MANAGER%'"
 if($cmd.ExecuteScalar() -ne 1){throw 'Refill constraint verification failed.'}
 $report=@{database=$database;at=[DateTime]::UtcNow.ToString('o');backup=$manifest.backup;verifiedBackup=$true;tables=57;rowCountsPreserved=$true;before=$before;after=$after;legacyPending=0;constraintVerified=$true}
 $report | ConvertTo-Json -Depth 5 | Set-Content "$root/artifacts/local-runtime/note2511-production-database.json" -Encoding utf8
 Write-Output 'PASS: official Note25_11 patch; verified backup;57 tables; business row counts preserved; constraint trusted; no legacy pending refill.'
}finally{$db.Dispose()}
