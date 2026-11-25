param([ValidateSet('Setup','Inspect','SeedExpiredShift','ExpireActiveShift','InspectShift','Cleanup')][string]$Action)
$ErrorActionPreference='Stop'
$RunId=[string]$env:HOTELOPS_V2_RUN_ID
if(-not $RunId){throw 'Run identity required.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$database='HotelOpsCompactWorkdayTestV22611250010'
$directory=Join-Path $root 'artifacts/restructure-v2/housekeeping-private'
New-Item -ItemType Directory -Force $directory|Out-Null
$path=Join-Path $directory 'fixture.json'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
function Query([string]$sql,[hashtable]$parameters=@{}) {
 $cmd=$connection.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=60
 foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue('@'+$key,$parameters[$key])}
 $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@()
 try{while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$v=$reader.GetValue($i);$row[$reader.GetName($i)]=if($v -is [DBNull]){$null}else{$v}};$rows+=,$row}}finally{$reader.Dispose();$cmd.Dispose()};return ,$rows
}
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 if($Action -eq 'Setup'){
  if(Test-Path $path){throw 'Existing housekeeping fixture preserved; cleanup first.'}
  $backup=Get-Content (Join-Path $root ('artifacts/workday/backup-'+$database+'.json')) -Raw|ConvertFrom-Json
  if(!$backup.verified -or $backup.database -ne $database -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 15){throw 'Fresh verified TEST backup required.'}
  $prefix='V2HK-'+[Guid]::NewGuid().ToString('N').Substring(0,10)
  @{database=$database;prefix=$prefix;runId=$RunId}|ConvertTo-Json|Set-Content $path -Encoding utf8
  $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'v2-housekeeping-fixture.sql') -Raw -Encoding utf8
  [void]$cmd.Parameters.AddWithValue('@prefix',$prefix)
  $fixture=([string]$cmd.ExecuteScalarAsync().GetAwaiter().GetResult())|ConvertFrom-Json;$cmd.Dispose()
  $fixture|Add-Member database $database;$fixture|Add-Member prefix $prefix;$fixture|Add-Member runId $RunId
  $fixture|ConvertTo-Json -Depth 8|Set-Content $path -Encoding utf8
  foreach($actor in $fixture.actors){
   $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose();$sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
   [void](Query 'INSERT user_sessions(user_id,token_hash,security_stamp,expires_at) SELECT user_id,@hash,security_stamp,DATEADD(hour,2,SYSUTCDATETIME()) FROM users WHERE user_id=@u' @{hash=$hash;u=$actor.user})
   $actor|Add-Member token (([BitConverter]::ToString($bytes)).Replace('-',''))
   $fixture|ConvertTo-Json -Depth 8|Set-Content $path -Encoding utf8
  }
  '{"setup":true}';exit 0
 }
 if(!(Test-Path $path)){if($Action -eq 'Cleanup'){'{"cleaned":true}';exit 0};throw 'No fixture.'}
 $fixture=Get-Content $path -Raw|ConvertFrom-Json
 if([string]$fixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 if($fixture.database -ne $database -or $fixture.prefix -notmatch '^V2HK-[a-f0-9]{10}$'){throw 'Fixture identity guard.'}
 $parameters=@{prefix=$fixture.prefix;like=$fixture.prefix+'-%'}
 if($Action -eq 'ExpireActiveShift'){
  $seed=Query @'
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact V2 TEST required',1;
DECLARE @u bigint=(SELECT user_id FROM users WHERE username=@prefix+'-hk');
DECLARE @b bigint=(SELECT branch_id FROM branches WHERE code=@prefix AND is_active=1);
DECLARE @minute int=DATEPART(hour,DATEADD(hour,7,SYSUTCDATETIME()))*60+DATEPART(minute,DATEADD(hour,7,SYSUTCDATETIME()));
DECLARE @a bigint=(SELECT a.assignment_id FROM workday_assignments a JOIN workday_shifts s ON s.shift_id=a.shift_id WHERE a.user_id=@u AND s.branch_id=@b AND a.status='WORKING');
IF @a IS NULL OR @minute<2 OR @minute>1438 THROW 51999,'Owned working shift and safe daytime required',1;
-- Put the deadline in the near future; moving it behind AssignedAt would create
-- an invalid historical assignment and correctly fail the execution guard.
UPDATE workday_shifts SET end_minute=@minute+1 WHERE shift_id=(SELECT shift_id FROM workday_assignments WHERE assignment_id=@a);
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity,is_active)
 SELECT branch_id,department_id,role_id,'TEST next shift',@minute+1,1440,5,1 FROM workday_shifts WHERE shift_id=(SELECT shift_id FROM workday_assignments WHERE assignment_id=@a);
DECLARE @s bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by)
 VALUES(@s,@u,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'PLANNED',@u);
SELECT CONVERT(bigint,SCOPE_IDENTITY()) nextAssignmentId,@a assignmentId,
 CONVERT(varchar(33),DATEADD(hour,-7,DATEADD(minute,@minute+1,CONVERT(datetime2,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))))),126) expectedEndedAt;
COMMIT;
'@ $parameters
  $seed[0]|ConvertTo-Json -Compress;exit 0
 }
 if($Action -eq 'InspectShift'){
  $rows=Query @'
SELECT a.assignment_id assignmentId,a.status,CONVERT(varchar(33),a.started_at,126) startedAt,CONVERT(varchar(33),a.ended_at,126) endedAt,
 (SELECT COUNT(*) FROM audit_logs l WHERE l.entity_type='Workday' AND l.entity_key=CONVERT(varchar(30),a.assignment_id) AND l.action='AUTO_END') autoEndCount,
 (SELECT COUNT(*) FROM workday_events e WHERE e.assignment_id=a.assignment_id AND e.action='END') manualEndCount
FROM workday_assignments a JOIN users u ON u.user_id=a.user_id WHERE u.username=@prefix+'-hk' ORDER BY a.assignment_id;
'@ $parameters
  @{assignments=$rows}|ConvertTo-Json -Depth 4 -Compress;exit 0
 }
 if($Action -eq 'SeedExpiredShift'){
  $seed=Query @'
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact V2 TEST required',1;
DECLARE @u bigint=(SELECT user_id FROM users WHERE username=@prefix+'-hk');
DECLARE @b bigint=(SELECT branch_id FROM branches WHERE code=@prefix AND is_active=1);
DECLARE @s bigint=(SELECT TOP(1) s.shift_id FROM workday_shifts s JOIN roles r ON r.role_id=s.role_id WHERE s.branch_id=@b AND r.code='HK_STAFF' AND s.end_minute=1440);
DECLARE @day date=CONVERT(date,DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())));
IF @u IS NULL OR @b IS NULL OR @s IS NULL THROW 51999,'Owned TEST baseline required',1;
IF EXISTS(SELECT 1 FROM workday_assignments WHERE user_id=@u AND work_date=@day) THROW 51999,'Expired TEST shift already exists',1;
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by,started_at)
 VALUES(@s,@u,@day,'WORKING',@u,DATEADD(day,-1,SYSUTCDATETIME()));
SELECT CONVERT(bigint,SCOPE_IDENTITY()) assignmentId;
COMMIT;
'@ $parameters
  $seed[0]|ConvertTo-Json -Compress;exit 0
 }
 if($Action -eq 'Inspect'){
  $stock=Query 'SELECT i.item_id itemId,COALESCE(SUM(m.signed_base_qty),0) warehouseQuantity FROM inventory_items i LEFT JOIN stock_movements m ON m.item_id=i.item_id WHERE i.code LIKE @like GROUP BY i.item_id' $parameters
  $batches=Query 'SELECT b.batch_id batchId,b.item_id itemId,b.warehouse_quantity warehouseQuantity,b.expires_on expiryDate FROM stock_batches b JOIN inventory_items i ON i.item_id=b.item_id WHERE i.code LIKE @like' $parameters
  $held=Query 'SELECT h.balance_id balanceId,h.batch_id batchId,h.quantity FROM staff_batch_balances h JOIN stock_batches b ON b.batch_id=h.batch_id JOIN inventory_items i ON i.item_id=b.item_id WHERE i.code LIKE @like' $parameters
  $supplyLedger=Query 'SELECT l.* FROM staff_supply_ledger l JOIN staff_supply_balances b ON b.balance_id=l.balance_id JOIN inventory_items i ON i.item_id=b.item_id WHERE i.code LIKE @like ORDER BY l.ledger_id' $parameters
  $supplyDocuments=Query 'SELECT d.* FROM supply_stock_documents d JOIN staff_supply_allocations a ON a.allocation_id=d.allocation_id JOIN users u ON u.user_id=a.user_id WHERE u.username LIKE @like ORDER BY d.document_id' $parameters
  $movements=Query 'SELECT m.* FROM stock_movements m JOIN inventory_items i ON i.item_id=m.item_id WHERE i.code LIKE @like ORDER BY m.movement_id' $parameters
  @{stock=$stock;batches=$batches;held=$held;supplyLedger=$supplyLedger;supplyDocuments=$supplyDocuments;movements=$movements}|ConvertTo-Json -Depth 7 -Compress;exit 0
 }
 [void](Query @'
SET XACT_ABORT ON;BEGIN TRANSACTION;
UPDATE user_sessions SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE user_id IN(SELECT user_id FROM users WHERE username LIKE @like);
UPDATE user_roles SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE user_id IN(SELECT user_id FROM users WHERE username LIKE @like);
UPDATE memberships SET valid_to=SYSUTCDATETIME() WHERE user_id IN(SELECT user_id FROM users WHERE username LIKE @like) AND valid_to IS NULL;
UPDATE branches SET is_active=0 WHERE code=@prefix;
UPDATE inventory_items SET is_active=0 WHERE code LIKE @like;
UPDATE assets SET is_active=0 WHERE asset_code LIKE @like;
COMMIT;
'@ $parameters)
 Remove-Item -LiteralPath $path
 '{"cleaned":true,"historyRetained":true,"sessionsRevoked":true,"syntheticBranchInactive":true}'
} finally {$connection.Dispose()}
