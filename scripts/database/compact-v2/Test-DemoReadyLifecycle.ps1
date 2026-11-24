param([string]$Database='HotelOpsCompactWorkdayTestUx20261123')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompactWorkdayTestUx[A-Za-z0-9_]+$'){throw 'Only isolated UX clone allowed.'}
& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=@'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @room bigint=(SELECT MIN(room_id) FROM rooms),@branch bigint,@actor bigint;
SELECT @branch=branch_id FROM rooms WHERE room_id=@room;
SELECT TOP(1) @actor=actor_user_id FROM room_events WHERE event_type='ADMIN_BASELINE_RESET';
IF NOT EXISTS(SELECT 1 FROM v_room_readiness WHERE room_id=@room AND readiness='READY') THROW 51500,'Baseline must be ready.',1;
INSERT room_blocks(branch_id,room_id,reason,blocked_by,blocked_at) VALUES(@branch,@room,N'Fixture lifecycle verification',@actor,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM v_room_readiness WHERE room_id=@room AND readiness='BLOCKED') THROW 51500,'Block must override reset baseline.',1;
UPDATE room_blocks SET released_at=SYSUTCDATETIME(),released_by=@actor WHERE room_id=@room AND released_at IS NULL;
UPDATE rooms SET operational_status='OCCUPIED' WHERE room_id=@room;
IF NOT EXISTS(SELECT 1 FROM v_room_readiness WHERE room_id=@room AND is_occupied=1) THROW 51500,'Check-in occupancy missing.',1;
UPDATE rooms SET operational_status='DIRTY',turnover_no=turnover_no+1,current_cleaning_work_id=NULL WHERE room_id=@room;
IF NOT EXISTS(SELECT 1 FROM v_room_readiness WHERE room_id=@room AND readiness='PENDING_CLEANING' AND is_occupied=0) THROW 51500,'Checkout must expire baseline and require cleaning.',1;
ROLLBACK;
SELECT 1 baselineReady,1 blockOverridesBaseline,1 occupancySeparate,1 checkoutRequiresCleaning,1 rolledBack;
'@
 $table=New-Object Data.DataTable;$reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Dispose()
 $table.Rows[0]|Select-Object baselineReady,blockOverridesBaseline,occupancySeparate,checkoutRequiresCleaning,rolledBack|ConvertTo-Json
} finally {$connection.Dispose()}
