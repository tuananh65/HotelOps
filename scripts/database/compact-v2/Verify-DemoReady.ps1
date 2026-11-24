param([string]$Database='HotelOpsCompactDB')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact(DB|WorkdayTestUx[A-Za-z0-9_]*)$'){throw 'Unexpected database scope.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandText=@'
SELECT (SELECT COUNT(*) FROM rooms) roomCount,
 (SELECT COUNT(*) FROM v_room_readiness WHERE readiness='READY' AND is_occupied=0) readyCount,
 (SELECT COUNT(*) FROM work_items WHERE title LIKE '[[]DEMO]%' OR work_no LIKE 'DEMO%' OR description LIKE 'DEMO %') demoWorks,
 (SELECT COUNT(*) FROM inventory_documents WHERE document_no LIKE 'DEMO%' OR reason LIKE 'DEMO %') demoDocuments,
 (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=1 AND parent_class=1) disabledTriggers,
 (SELECT COUNT(*) FROM v_stock_balances WHERE base_qty<0) negativeBalances,
 (SELECT COUNT(*) FROM users) users,
 (SELECT COUNT(*) FROM role_permissions) permissionGrants,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM role_permissions) grantChecksum,
 (SELECT COUNT(*) FROM user_roles) roleAssignments,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM user_roles) assignmentChecksum,
 (SELECT COUNT(*) FROM assets) assets,
 (SELECT COUNT(*) FROM audit_logs WHERE action='RESET_DEMO_READY') resetAudits,
 (SELECT COUNT(*) FROM room_events WHERE event_type='ADMIN_BASELINE_RESET') baselineEvents;
'@
 $table=New-Object Data.DataTable;$reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Dispose()
 $r=$table.Rows[0]
 if($r.roomCount -ne $r.readyCount -or $r.demoWorks -ne 0 -or $r.demoDocuments -ne 0 -or $r.disabledTriggers -ne 0 -or $r.negativeBalances -ne 0){throw 'Reset verification failed.'}
 $r|Select-Object roomCount,readyCount,demoWorks,demoDocuments,disabledTriggers,negativeBalances,users,permissionGrants,grantChecksum,roleAssignments,assignmentChecksum,assets,resetAudits,baselineEvents|ConvertTo-Json
} finally {$connection.Dispose()}
