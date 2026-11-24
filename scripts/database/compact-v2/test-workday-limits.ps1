if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated fixture required'}
$a=$f.branchId;$b=$f.otherBranchId;$uid=$f.workerId
$seed=Sql @"
SET NOCOUNT ON;
UPDATE memberships SET valid_from=DATEADD(day,-7,SYSUTCDATETIME());
UPDATE user_roles SET assigned_at=DATEADD(day,-7,SYSUTCDATETIME());
DECLARE @tech bigint,@dept bigint,@role bigint;
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT 'QA_TECH','qa_tech','QA Technician',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=$($f.adminId);SET @tech=SCOPE_IDENTITY();
INSERT departments(branch_id,code,name) VALUES($a,'ENG','Engineering');SET @dept=SCOPE_IDENTITY();
INSERT roles(code,name) VALUES('QA_TECH','Technician');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'OWN' FROM permissions WHERE code='ATTENDANCE_RECORD';
INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at) VALUES(@tech,@role,$a,$($f.adminId),DATEADD(day,-7,SYSUTCDATETIME()));
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,valid_from) VALUES(@tech,$a,@dept,'EMPLOYMENT',$($f.adminId),DATEADD(day,-7,SYSUTCDATETIME()));
INSERT memberships(user_id,branch_id,kind,recorded_by,valid_from) VALUES(@tech,$a,'BRANCH_ACCESS',$($f.adminId),DATEADD(day,-7,SYSUTCDATETIME()));
SELECT @tech tech,@dept dept,@role role,(SELECT role_id FROM roles WHERE code='QA_ALL') hkRole;
"@
$s=$seed.Rows[0]
$config=Call GET "/api/v2/workday/configuration?branchId=$a" $null $worker
Check 'configuration maps active department to its actual employee roles' (@($config.departmentRoles|Where-Object {$_.departmentId-eq $f.departmentId -and $_.roleId-eq $s.hkRole}).Count-eq 1 -and @($config.departmentRoles|Where-Object {$_.departmentId-eq $f.departmentId -and $_.roleId-eq $s.role}).Count-eq 0)
function ShiftBody($name,$start,$end){return @{branchId=$a;departmentId=$f.departmentId;roleId=$s.hkRole;name=$name;startMinute=$start;endMinute=$end;capacity=10;isActive=$true;operationKey=[Guid]::NewGuid().ToString()}}
$wrong=ShiftBody 'Invalid department role' 480 960;$wrong.roleId=$s.role
$err=Call POST '/api/v2/workday/shifts' $wrong $worker 400
Check 'backend rejects role from another department' ($err.title-eq 'SHIFT_ROLE_DEPARTMENT_MISMATCH')
$null=Call GET "/api/v2/workday/configuration?branchId=$b" $null $worker 403
Check 'configuration remains branch scoped' $true
$shift=(Call POST '/api/v2/workday/shifts' (ShiftBody 'Main' 480 960) $worker).fields
$extra=(Call POST '/api/v2/workday/shifts' (ShiftBody 'Extra' 960 1020) $worker).fields
$late=(Call POST '/api/v2/workday/shifts' (ShiftBody 'Late' 1020 1080) $worker).fields
$local=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').Date
$monday=$local.AddDays((8-[int]$local.DayOfWeek)%7);if($monday-le $local){$monday=$monday.AddDays(7)}
$dates=@(0..6|ForEach-Object {$monday.AddDays($_).ToString('yyyy-MM-dd')})
function Preview($sh,$days){$qs=($days|ForEach-Object {"dates=$_"})-join '&';return Call GET "/api/v2/workday/candidates/batch?shiftId=$($sh.shiftId)&$qs" $null $worker}
function Batch($sh,$days,$users=@($uid)){return @{shiftId=$sh.shiftId;rowVersion=$sh.rowVersion;operationKey=[Guid]::NewGuid().ToString();userIds=@($users);workDates=@($days);holidaysAcknowledged=$true}}
function Counts{return (Sql 'SELECT COUNT(*) n FROM workday_assignments').Rows[0].n}
$p=Preview $shift $dates[0..4];$u=$p.users|Where-Object userId -eq $uid
Check 'exact 8h/day and 40h/week has no warning' (@($u.hourWarnings).Count-eq 0)
$result=Call POST '/api/v2/workday/assignments/batch' (Batch $shift $dates[0..4]) $worker
Check 'five-day batch commits within limit without override' ($result.count-eq 5)
$p=Preview $shift @($dates[5]);$u=$p.users|Where-Object userId -eq $uid
Check 'sixth day warns weekly 48h with 40h already scheduled' (@($u.hourWarnings).Count-eq 1 -and $u.hourWarnings[0].totalMinutes-eq 2880 -and $u.hourWarnings[0].existingMinutes-eq 2400)
$body=Batch $shift @($dates[5]);$before=Counts
$err=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'missing overtime acknowledgement rejects without partial writes' ($err.title-eq 'SHIFT_HOURS_CONFIRMATION_REQUIRED' -and (Counts)-eq $before)
$body.hoursAcknowledgements=@{"$uid"='forged'};$null=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'forged hours acknowledgement rejected' ((Counts)-eq $before)
$body.hoursAcknowledgements=@{"$uid"=$u.hoursToken}
$result=Call POST '/api/v2/workday/assignments/batch' $body $worker
Check 'explicit current warning acknowledgement permits over-limit assignment' ($result.count-eq 1)
$aid=$result.assignments[0].fields.assignmentId
$audit=(Sql "SELECT (SELECT COUNT(*) FROM workday_events WHERE assignment_id=$aid AND reason IS NOT NULL) events,(SELECT COUNT(*) FROM audit_logs WHERE entity_type='Workday' AND entity_key='$aid' AND after_json LIKE '%hoursOverride%') audits").Rows[0]
Check 'override recorded in assignment history and audit' ($audit.events-eq 1 -and $audit.audits-eq 1)
$null=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'retry cannot duplicate accepted override' ((Counts)-eq ($before+1))
$p=Preview $extra @($dates[0]);$u=$p.users|Where-Object userId -eq $uid
Check 'additional nonoverlap shift warns daily and weekly totals' (@($u.hourWarnings).Count-eq 2 -and ($u.hourWarnings|Where-Object period -eq 'DAY').totalMinutes-eq 540)
# Single-assignment endpoint must not bypass the same policy.
$single=@{shiftId=$extra.shiftId;userId=$uid;workDate=$dates[0];rowVersion=$extra.rowVersion;operationKey=[Guid]::NewGuid().ToString()}
$err=Call POST '/api/v2/workday/assignments' $single $worker 409
Check 'single assignment cannot bypass hours confirmation' ($err.title-eq 'SHIFT_HOURS_CONFIRMATION_REQUIRED')
$single.hoursAcknowledgement=$u.hoursToken
$null=Call POST '/api/v2/workday/assignments' $single $worker
Check 'single assignment supports current reviewed override' $true
# A warning token must become stale if another allocation changes the week total.
$p=Preview $extra @($dates[1]);$oldToken=($p.users|Where-Object userId -eq $uid).hoursToken
$p=Preview $late @($dates[1]);$u=$p.users|Where-Object userId -eq $uid
$body=Batch $late @($dates[1]);$body.hoursAcknowledgements=@{"$uid"=$u.hoursToken};$null=Call POST '/api/v2/workday/assignments/batch' $body $worker
$before=Counts;$body=Batch $extra @($dates[1]);$body.hoursAcknowledgements=@{"$uid"=$oldToken}
$err=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'changed weekly schedule requires fresh warning acknowledgement' ($err.title-eq 'SHIFT_HOURS_CONFIRMATION_REQUIRED' -and (Counts)-eq $before)
# Cancellation removes hours; ended assignments still count. Mutations remain isolated fixture operations.
$null=Sql "UPDATE workday_assignments SET status='CANCELLED' WHERE assignment_id=$aid; UPDATE workday_assignments SET status='ENDED',started_at=DATEADD(day,-1,SYSUTCDATETIME()),ended_at=SYSUTCDATETIME() WHERE user_id=$uid AND work_date='$($dates[2])'"
$p=Preview $shift @($dates[6]);$u=$p.users|Where-Object userId -eq $uid
Check 'cancelled shifts excluded while ended shifts retained in weekly total' (($u.hourWarnings|Where-Object period -eq 'WEEK').existingMinutes-eq 2520)
# Whole batch itself can exceed weekly hours even with no prior schedule for the other employee.
$p=Preview $shift $dates;$adminCandidate=$p.users|Where-Object userId -eq $f.adminId
Check 'whole week preview includes all proposed hours for a new employee' (($adminCandidate.hourWarnings|Where-Object period -eq 'WEEK').totalMinutes-eq 3360)
$before=Counts;$body=Batch $shift $dates @($f.adminId)
$null=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'whole week overage cannot write before explicit confirmation' ((Counts)-eq $before)
# Dates spanning months in the same week are valid.
$boundary=$monday;while($boundary.Month-eq $boundary.AddDays(6).Month){$boundary=$boundary.AddDays(7)}
$weekDates=@(0..6|ForEach-Object {$boundary.AddDays($_).ToString('yyyy-MM-dd')})
$p=Preview $shift $weekDates
Check 'week selection crosses month boundary' ($p.slots.Count-eq 7)
# Check that confirming a warning cannot bypass an overlap.
$body=Batch $shift @($dates[0]);$body.hoursAcknowledgements=@{"$uid"=$oldToken}
$before=Counts;$null=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'confirmation never bypasses overlapping assignment' ((Counts)-eq $before)

# A multi-employee batch is all-or-nothing, including the last employee's confirmation.
$body=Batch $extra @($dates[3]) @($uid,$f.adminId);$before=Counts
$null=Call POST '/api/v2/workday/assignments/batch' $body $worker 409
Check 'multi-employee batch rolls back when any required confirmation is missing' ((Counts)-eq $before)

# The longest monthly batch has many daily and weekly warnings; each history event must fit its existing column.
$long=(Call POST '/api/v2/workday/shifts' (ShiftBody 'Long shift' 60 600) $worker).fields
$month=$monday.AddMonths(2);$first=[DateTime]::new($month.Year,$month.Month,1)
$monthDates=@(0..([DateTime]::DaysInMonth($month.Year,$month.Month)-1)|ForEach-Object {$first.AddDays($_).ToString('yyyy-MM-dd')})
$p=Preview $long $monthDates;$u=$p.users|Where-Object userId -eq $f.adminId
Check 'month preview includes every daily and weekly overtime warning' (@($u.hourWarnings|Where-Object period -eq 'DAY').Count-eq $monthDates.Count -and @($u.hourWarnings|Where-Object period -eq 'WEEK').Count-ge 3)
$body=Batch $long $monthDates @($f.adminId);$body.hoursAcknowledgements=@{"$($f.adminId)"=$u.hoursToken}
$result=Call POST '/api/v2/workday/assignments/batch' $body $worker
$history=(Sql "SELECT COUNT(*) n,MAX(LEN(e.reason)) maxlen FROM workday_events e JOIN workday_assignments a ON a.assignment_id=e.assignment_id WHERE a.shift_id=$($long.shiftId)").Rows[0]
Check 'confirmed monthly overtime commits all events without truncation' ($result.count-eq $monthDates.Count -and $history.n-eq $monthDates.Count -and $history.maxlen-lt 2000)
