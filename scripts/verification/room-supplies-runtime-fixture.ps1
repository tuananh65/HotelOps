$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestWarehouse261124'
$backup=Get-Content "artifacts/workday/backup-$database.json" -Raw -Encoding utf8 | ConvertFrom-Json
if(-not $backup.verified -or $backup.database -ne $database){throw 'TEST backup required'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult();$command=$connection.CreateCommand();$command.CommandText=@'
SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestWarehouse261124' THROW 51997,'Isolated TEST only.',1;
BEGIN TRANSACTION;
DECLARE @branch bigint=(SELECT branch_id FROM branches WHERE code='P0_TEST_DVCT');
IF @branch IS NULL THROW 51997,'Existing P0 TEST fixture required.',1;
IF NOT EXISTS(SELECT 1 FROM room_types WHERE branch_id=@branch AND code='TEST_FILL_CAP4')
 INSERT room_types(branch_id,code,name,capacity) VALUES(@branch,'TEST_FILL_CAP4',N'TEST sức chứa 4 khách',4);
IF NOT EXISTS(SELECT 1 FROM rooms WHERE branch_id=@branch AND code='TEST_FILL_CAP4')
 INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status)
 SELECT @branch,r.location_id,t.room_type_id,r.department_id,'TEST_FILL_CAP4','DIRTY' FROM rooms r JOIN room_types t ON t.branch_id=@branch AND t.code='TEST_FILL_CAP4' WHERE r.branch_id=@branch AND r.code='P0_101';
COMMIT;
SELECT r.room_id roomId,r.branch_id branchId,r.code,rt.capacity FROM rooms r JOIN room_types rt ON rt.room_type_id=r.room_type_id WHERE r.branch_id=@branch AND r.code IN('P0_101','TEST_FILL_CAP4');
'@
 $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=,$row};$reader.Dispose()
 $rows | ConvertTo-Json | Set-Content artifacts/room-supplies/runtime-rooms.json -Encoding utf8
 $rows | ConvertTo-Json -Compress
}finally{$connection.Dispose()}
