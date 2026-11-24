$ErrorActionPreference='Stop'
if(Test-Path artifacts/maintenance-workspace/runtime-checkpoint.json){
 $completed=Get-Content artifacts/maintenance-workspace/runtime-checkpoint.json -Raw -Encoding utf8 | ConvertFrom-Json
 if($completed.shiftChecked){Write-Output 'Existing TEST checkpoint already verified Start Shift; attendance left unchanged.';exit 0}
}
$database='HotelOpsCompactWorkdayTestWarehouse261124'
$backup=Get-Content "artifacts/workday/backup-$database.json" -Raw -Encoding utf8 | ConvertFrom-Json
if(-not $backup.verified -or $backup.database -ne $database){throw 'TEST backup required'}
$fixture=Get-Content artifacts/p0-workflow/private/runtime-fixture.json -Raw -Encoding utf8 | ConvertFrom-Json
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult();$command=$connection.CreateCommand();$command.CommandText=@'
SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestWarehouse261124' THROW 51997,'Isolated TEST only.',1;
BEGIN TRANSACTION;
DECLARE @branch bigint=(SELECT branch_id FROM branches WHERE code='P0_TEST_DVCT');
IF @branch IS NULL THROW 51997,'TEST branch required.',1;
DECLARE @assignment bigint=(SELECT TOP(1) a.assignment_id FROM workday_assignments a JOIN workday_shifts s ON a.shift_id=s.shift_id
 WHERE a.user_id=@user AND s.branch_id=@branch AND a.work_date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) AND a.status IN('PLANNED','WORKING') ORDER BY a.assignment_id DESC);
IF @assignment IS NULL THROW 51997,'Current TEST maintenance shift required.',1;
UPDATE workday_assignments SET status='PLANNED',started_at=NULL,ended_at=NULL WHERE assignment_id=@assignment;
COMMIT;
SELECT @assignment assignmentId;
'@
 [void]$command.Parameters.AddWithValue('@user',[long]$fixture.actors.'DVCT.maintenance'.userId)
 $id=$command.ExecuteScalarAsync().GetAwaiter().GetResult()
 New-Item -ItemType Directory -Force artifacts/maintenance-workspace | Out-Null
 @{assignmentId=$id;database=$database}|ConvertTo-Json|Set-Content artifacts/maintenance-workspace/fixture.json -Encoding utf8
 Write-Output "Prepared isolated TEST maintenance shift as PLANNED; assignment $id. No Production/Develop attendance changed."
} finally {$connection.Dispose()}
