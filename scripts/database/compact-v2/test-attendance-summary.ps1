if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated fixture required'}
$a=$f.branchId;$b=$f.otherBranchId;$uid=$f.workerId
$local=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').Date
$today=$local.ToString('yyyy-MM-dd')
$seed=Sql @"
SET NOCOUNT ON;
UPDATE memberships SET valid_from=DATEADD(day,-40,SYSUTCDATETIME());
DECLARE @role bigint,@shift bigint,@otherShift bigint,@otherDept bigint;
INSERT roles(code,name) VALUES('HOUSEKEEPING_SUPERVISOR','Supervisor');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'OWN' FROM permissions WHERE code='ATTENDANCE_RECORD';
DELETE user_roles WHERE user_id=$uid;
INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at) VALUES($uid,@role,$a,$($f.adminId),DATEADD(day,-40,SYSUTCDATETIME()));
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity) VALUES($a,$($f.departmentId),@role,'Supervisor day',0,1440,10);SET @shift=SCOPE_IDENTITY();
INSERT departments(branch_id,code,name) VALUES($b,'HK','Other HK');SET @otherDept=SCOPE_IDENTITY();
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity) VALUES($b,@otherDept,@role,'Other',0,1440,10);SET @otherShift=SCOPE_IDENTITY();
DECLARE @start datetime2=DATEADD(hour,-7,CAST('$today' AS datetime2));
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,ended_at,created_by)
VALUES(@shift,$uid,'$today','ENDED',@start,DATEADD(hour,2,@start),$($f.adminId)),
(@shift,$($f.adminId),'$today','ENDED',@start,DATEADD(hour,7,@start),$($f.adminId)),
(@otherShift,$uid,'$today','ENDED',@start,DATEADD(hour,6,@start),$($f.adminId));
SELECT @role role,@shift shift;
"@
$sf=$seed.Rows[0]
$summary=Call GET "/api/v2/workday/personal-summary?branchId=$a" $null $worker
Check 'OWN summary excludes other employee and other branch' ($summary.periods[0].completedShifts-eq 1 -and $summary.periods[0].workedSeconds-eq 7200 -and $summary.periods[1].workedHours-eq 2)
Check 'summary uses branch timezone and Monday week' ($summary.timezone-eq 'Asia/Ho_Chi_Minh' -and ([DateTime]$summary.periods[0].from).DayOfWeek-eq 'Monday')
$spoof=Call GET "/api/v2/workday/personal-summary?branchId=$a&userId=$($f.adminId)" $null $worker
Check 'userId parameter cannot impersonate another employee' ($spoof.periods[0].workedSeconds-eq 7200)
$null=Call GET "/api/v2/workday/personal-summary?branchId=$b" $null $worker 403
Check 'summary rejects other branch' $true
# Separate same-day shift allows actual start/end without editing the historical row.
$seed=Sql @"
SET NOCOUNT ON;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity) VALUES($a,$($f.departmentId),$($sf.role),'Current supervisor',0,1439,10);
DECLARE @shift bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by) VALUES(@shift,$uid,'$today','PLANNED',$($f.adminId));
SELECT SCOPE_IDENTITY() assignment;
"@
$aid=[long]$seed.Rows[0].assignment
$week=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $worker
Check 'supervisor sees personal calendar without SHIFT_MANAGE' (-not $week.canManage -and @($week.assignments|Where-Object userId -ne $uid).Count-eq 0)
$row=$week.assignments|Where-Object assignmentId -eq $aid
$started=(Call POST "/api/v2/workday/assignments/$aid/start" @{rowVersion=$row.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker).fields
Check 'supervisor starts own assigned shift' ($started.status-eq 'WORKING')
$summary=Call GET "/api/v2/workday/personal-summary?branchId=$a" $null $worker
Check 'working shift appears separately without projected hours' ($summary.periods[0].workingShifts-eq 1 -and $summary.periods[0].completedShifts-eq 1 -and $summary.periods[0].workedSeconds-eq 7200)
$ended=(Call POST "/api/v2/workday/assignments/$aid/end" @{rowVersion=$started.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker).fields
Check 'supervisor ends own shift' ($ended.status-eq 'ENDED')
$summary=Call GET "/api/v2/workday/personal-summary?branchId=$a" $null $worker
Check 'ended shift immediately updates statistics' ($summary.periods[0].workingShifts-eq 0 -and $summary.periods[0].completedShifts-eq 2 -and $summary.periods[0].workedSeconds-ge 7200)
$other=(Sql "SELECT assignment_id,CONVERT(varchar(max),row_version,1) version FROM workday_assignments WHERE user_id=$($f.adminId)").Rows[0]
$null=Call POST "/api/v2/workday/assignments/$($other.assignment_id)/start" @{rowVersion=$ended.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker 403
Check 'supervisor cannot start another employee shift' $true
# Remove only the synthetic fixture grant: SHIFT_MANAGE alone must not confer personal attendance statistics.
$null=Sql "DELETE role_permissions WHERE role_id=$($sf.role); INSERT role_permissions(role_id,permission_id,data_scope) SELECT $($sf.role),permission_id,'BRANCH' FROM permissions WHERE code='SHIFT_MANAGE'"
$null=Call GET "/api/v2/workday/personal-summary?branchId=$a" $null $worker 403
Check 'attendance permission required even for scheduler' $true
