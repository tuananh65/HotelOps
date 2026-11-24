$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']='HotelOpsCompactDB'
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $queries=@{
 branches='SELECT branch_id,code,name,address,phone FROM branches';
 rooms='SELECT operational_status,readiness,COUNT(*) AS total FROM v_room_readiness GROUP BY operational_status,readiness';
 demoWork="SELECT work_id,kind,status,title,room_id,parent_work_id FROM work_items WHERE title LIKE '%DEMO%' OR work_no LIKE '%DEMO%' OR description LIKE '%DEMO%'";
 documents="SELECT document_id,kind,status,document_no,reason FROM inventory_documents WHERE document_no LIKE '%DEMO%' OR reason LIKE '%DEMO%'";
 openRoomWork="SELECT kind,status,COUNT(*) total FROM work_items WHERE room_id IS NOT NULL GROUP BY kind,status";
 blocks='SELECT room_id,source_work_id,reason FROM room_blocks WHERE released_at IS NULL';
 branchColumns="SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('branches')";
 }
 foreach($key in $queries.Keys){$cmd=$connection.CreateCommand();$cmd.CommandText=$queries[$key];$table=New-Object Data.DataTable;$reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Dispose();Write-Output $key;$table | Select-Object * -ExcludeProperty RowError,RowState,Table,ItemArray,HasErrors | ConvertTo-Json -Depth 3}
}finally{$connection.Dispose()}
