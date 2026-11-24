param([switch]$Apply)
$ErrorActionPreference='Stop'
if(!$Apply){throw 'Explicit -Apply required for synthetic TEST fixture'}
$database='HotelOpsCompactWorkdayTestP0261124'
$backup=Get-Content artifacts/workday/backup-HotelOpsCompactWorkdayTestP0261124.json -Raw|ConvertFrom-Json
if(!$backup.verified -or $backup.database -ne $database -or ([DateTime]::UtcNow-[DateTime]$backup.at).TotalHours -gt 2){throw 'Fresh verified TEST backup required'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$fixture=Get-Content artifacts/p0-workflow/private/runtime-fixture.json -Raw|ConvertFrom-Json
$branch=$fixture.branches|Where-Object code -eq 'P0_TEST_DVCT'
$staff=$fixture.actors.'DVCT.hkPeer';$supervisor=$fixture.actors.'DVCT.supervisor'
$cn=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$cn.Open()
try{
 $cmd=$cn.CreateCommand();$cmd.CommandTimeout=60
 foreach($entry in @{branch=$branch.branchId;staff=$staff.userId;supervisor=$supervisor.userId;oldDay=$staff.assignmentId}.GetEnumerator()){[void]$cmd.Parameters.AddWithValue('@'+$entry.Key,[long]$entry.Value)}
 $cmd.CommandText=@'
SET XACT_ABORT ON; SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestP0261124' THROW 51990,'TEST only',1;
IF NOT EXISTS(SELECT 1 FROM branches WHERE branch_id=@branch AND code='P0_TEST_DVCT') THROW 51990,'Synthetic branch only',1;
IF EXISTS(SELECT 1 FROM rooms WHERE branch_id=@branch AND code='P0_LEGACY') THROW 51990,'Fixture exists; do not recreate',1;
IF EXISTS(SELECT 1 FROM workday_assignments WHERE user_id=@staff AND status='WORKING') THROW 51990,'Staff must have no active shift',1;
DECLARE @cutover datetime2=(SELECT MIN(occurred_at) FROM audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED');
IF @cutover IS NULL THROW 51990,'Migration cutover required',1;
DECLARE @started datetime2=DATEADD(day,-1,@cutover),@day bigint,@room bigint,@work bigint;
BEGIN TRAN;
-- Simulated pre-migration attendance/task: NEW records only, explicitly labelled TEST.
-- No existing business history, stock, supply balances or reservations are changed.
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by,created_at)
 SELECT shift_id,@staff,CONVERT(date,DATEADD(hour,7,@started)),'WORKING',@started,@supervisor,@started FROM workday_assignments WHERE assignment_id=@oldDay AND user_id=@staff AND status='ENDED';
IF @@ROWCOUNT<>1 THROW 51990,'Ended synthetic staff attendance required',1;
SET @day=SCOPE_IDENTITY();
INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status,turnover_no)
 SELECT TOP(1) branch_id,location_id,room_type_id,department_id,'P0_LEGACY','DIRTY',1 FROM rooms WHERE branch_id=@branch AND code='P0_104';
IF @@ROWCOUNT<>1 THROW 51990,'Synthetic room template required',1;
SET @room=SCOPE_IDENTITY();
INSERT work_items(branch_id,work_no,kind,status,title,description,department_id,room_id,turnover_no,actual_start_at,created_by,created_at)
 SELECT @branch,CONVERT(varchar(36),NEWID()),'CLEANING','ON_HOLD',N'P0 TEST legacy recovery',N'Synthetic migration fixture; dates simulate pre-cutover work, not real hotel history',department_id,@room,1,DATEADD(minute,2,@started),@supervisor,@started FROM rooms WHERE room_id=@room;
SET @work=SCOPE_IDENTITY();
INSERT work_assignments(branch_id,work_id,assignee_user_id,assigned_by,assigned_at,accepted_at,reason)
 VALUES(@branch,@work,@staff,@supervisor,DATEADD(minute,1,@started),DATEADD(minute,2,@started),N'P0 TEST simulated legacy assignment');
UPDATE rooms SET current_cleaning_work_id=@work WHERE room_id=@room;
COMMIT;
SELECT @work workId,@room roomId,@day assignmentId,@staff userId,@branch branchId,@cutover cutover,
 'Synthetic TEST pre-cutover dates; no existing history altered' provenance FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
'@
 $r=$cmd.ExecuteReader();$json='';while($r.Read()){$json+=$r.GetString(0)};$r.Close()
 [IO.File]::WriteAllText((Join-Path (Get-Location) 'artifacts/p0-workflow/legacy-fixture.json'),$json)
 Write-Output 'Synthetic legacy TEST fixture created; no inventory changes.'
}finally{$cn.Dispose()}
