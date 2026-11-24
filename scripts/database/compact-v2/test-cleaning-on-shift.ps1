# Dot-sourced only by Test-Backend.ps1 against a newly generated fixture database.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$actors=Sql (Get-Content (Join-Path $PSScriptRoot 'housekeeping-staff-fixture.sql') -Encoding UTF8 -Raw)
$a=$f.branchId;$b=$f.otherBranchId
$hkId=($actors.Rows|Where-Object username -eq 'qa_hk_a').user_id
$peerId=($actors.Rows|Where-Object username -eq 'qa_hk_peer').user_id
$foreignId=($actors.Rows|Where-Object username -eq 'qa_hk_b').user_id
$headId=($actors.Rows|Where-Object username -eq 'qa_hks_a').user_id
$head=(Call POST '/api/v2/auth/login' @{username='qa_hks_a';password=$password}).accessToken
$hk=(Call POST '/api/v2/auth/login' @{username='qa_hk_a';password=$password}).accessToken
$headB=(Call POST '/api/v2/auth/login' @{username='qa_hks_b';password=$password}).accessToken
$null=Sql @"
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT d.branch_id,d.department_id,r.role_id,N'Fixture full-day',0,1440,10
FROM departments d CROSS JOIN roles r WHERE d.code='HK' AND r.code IN ('HK_STAFF','HOUSEKEEPING_SUPERVISOR');
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by)
SELECT s.shift_id,ur.user_id,CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time'),
CASE WHEN ur.user_id=$peerId THEN 'PLANNED' ELSE 'WORKING' END,
CASE WHEN ur.user_id=$peerId THEN NULL ELSE DATEADD(minute,-1,SYSUTCDATETIME()) END,$($f.adminId)
FROM user_roles ur JOIN workday_shifts s ON s.role_id=ur.role_id AND s.branch_id=ur.branch_id
WHERE s.name=N'Fixture full-day';
"@
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId;departmentId=$f.departmentId}} $admin).fields
function NewCleaningBody {
 $current=@(Call GET "/api/v2/rooms?branchId=$a" $null $head)|Where-Object roomId -eq $room.roomId
 return @{branchId=$a;kind='CLEANING';title='On-shift cleaning';roomId=$room.roomId;roomRowVersion=$current.rowVersion;assigneeUserId=$hkId;durationMinutes=30}
}
function RejectCreate($code,$expected=409,$body=$null,$token=$head){
 if($null-eq $body){$body=NewCleaningBody}
 $before=(Sql 'SELECT COUNT(*) n FROM work_items').Rows[0].n
 $errorResult=Call POST '/api/v2/work' $body $token $expected
 Check $code ($errorResult.title-eq $code -and (Sql 'SELECT COUNT(*) n FROM work_items').Rows[0].n-eq $before)
}
$options=Call GET "/api/v2/work/cleaning-creation-options?branchId=$a" $null $head
Check 'current supervisor sees only working HK staff in own branch' ($options.canCreate -and @($options.candidates).Count-eq 1 -and $options.candidates[0].userId-eq $hkId)
$null=Call GET "/api/v2/work/cleaning-creation-options?branchId=$b" $null $head 403
$other=Call GET "/api/v2/work/cleaning-creation-options?branchId=$b" $null $headB
Check 'other branch has only its own working staff' ($other.canCreate -and @($other.candidates).Count-eq 1 -and $other.candidates[0].userId-eq $foreignId)
$body=NewCleaningBody;$body.Remove('assigneeUserId');RejectCreate 'CLEANING_ASSIGNEE_REQUIRED' 400 $body
$body=NewCleaningBody;$body.assigneeUserId=$foreignId;RejectCreate 'ASSIGNEE_NOT_ACTIVE_IN_BRANCH' 409 $body
$body=NewCleaningBody;$body.assigneeUserId=$peerId;RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT' 409 $body
$body=NewCleaningBody;$body.assigneeUserId=$f.workerId;RejectCreate 'CLEANING_STAFF_REQUIRED' 409 $body
RejectCreate 'PERMISSION_DENIED' 403 (NewCleaningBody) $hk
$null=Sql "UPDATE workday_assignments SET status='PLANNED',started_at=NULL WHERE user_id=$headId"
$options=Call GET "/api/v2/work/cleaning-creation-options?branchId=$a" $null $head
Check 'planned creator cannot create before starting shift' (-not $options.canCreate)
RejectCreate 'CLEANING_CREATOR_NOT_ON_SHIFT' 403
$null=Sql "UPDATE workday_assignments SET status='WORKING',started_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE user_id=$headId"
# Off-duty transitions after the options GET must be rechecked at POST, without partial creation.
$null=Sql "UPDATE workday_assignments SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id=$hkId"
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$options=Call GET "/api/v2/work/cleaning-creation-options?branchId=$a" $null $head
Check 'ended staff absent from choices' (@($options.candidates).Count-eq 0)
$null=Sql "UPDATE workday_assignments SET status='WORKING',ended_at=NULL,work_date=DATEADD(day,-1,work_date) WHERE user_id=$hkId"
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$null=Sql "UPDATE workday_assignments SET work_date=DATEADD(day,2,work_date) WHERE user_id=$hkId"
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$null=Sql "UPDATE workday_assignments SET work_date=DATEADD(day,-1,work_date),started_at=DATEADD(hour,1,SYSUTCDATETIME()) WHERE user_id=$hkId"
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$null=Sql "UPDATE workday_assignments SET started_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE user_id=$hkId; UPDATE users SET account_status='INACTIVE' WHERE user_id=$hkId"
RejectCreate 'ASSIGNEE_NOT_ACTIVE_IN_BRANCH'
$null=Sql "UPDATE users SET account_status='ACTIVE' WHERE user_id=$hkId"
$null=Sql "UPDATE workday_shifts SET is_active=0 WHERE name=N'Fixture full-day' AND branch_id=$a AND role_id=(SELECT role_id FROM roles WHERE code='HK_STAFF')"
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$null=Sql "UPDATE workday_shifts SET is_active=1 WHERE name=N'Fixture full-day' AND branch_id=$a"
# Move the fixture shift outside the current local minute without relying on the test machine timezone.
$null=Sql @"
DECLARE @local datetime2=CONVERT(datetime2,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
DECLARE @minute int=DATEPART(hour,@local)*60+DATEPART(minute,@local);
UPDATE workday_shifts SET start_minute=CASE WHEN @minute<720 THEN 720 ELSE 0 END,end_minute=CASE WHEN @minute<720 THEN 1440 ELSE 720 END
WHERE name=N'Fixture full-day' AND branch_id=$a AND role_id=(SELECT role_id FROM roles WHERE code='HK_STAFF');
"@
RejectCreate 'CLEANING_ASSIGNEE_NOT_ON_SHIFT'
$null=Sql "UPDATE workday_shifts SET start_minute=0,end_minute=1440 WHERE name=N'Fixture full-day'"
$null=Sql "UPDATE workday_assignments SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id=$headId"
RejectCreate 'CLEANING_CREATOR_NOT_ON_SHIFT' 403
$null=Sql "UPDATE workday_assignments SET status='WORKING',ended_at=NULL WHERE user_id=$headId"
$stale=NewCleaningBody;$stale.roomRowVersion='AAAAAAAAAAA=';RejectCreate 'STALE_ROW_VERSION' 409 $stale
$body=NewCleaningBody;$body.departmentId=(Sql "SELECT department_id FROM departments WHERE branch_id=$b AND code='HK'").Rows[0].department_id
$clean=(Call POST '/api/v2/work' $body $head).fields
$detail=Call GET "/api/v2/work/$($clean.workId)" $null $head
Check 'create atomically assigns chosen housekeeper and infers own department' ($clean.status-eq 'ASSIGNED' -and $clean.departmentId-eq $f.departmentId -and $detail.assignments[0].fields.assigneeUserId-eq $hkId)
Check 'created and assigned histories retained' (@($detail.events|Where-Object {$_.fields.eventType-eq 'CREATED'}).Count-eq 1 -and @($detail.events|Where-Object {$_.fields.eventType-eq 'ASSIGN'}).Count-eq 1)
$notification=(Sql "SELECT COUNT(*) n FROM notifications WHERE recipient_user_id=$hkId AND work_id=$($clean.workId)").Rows[0].n
Check 'assignment notification emitted once' ($notification-eq 1)
$body=NewCleaningBody;$body.Remove('roomRowVersion');RejectCreate 'CLEANING_ALREADY_ACTIVE' 409 $body
$null=WorkAction $clean assign $head @{userId=$peerId} 409
if($CleaningDeadline){. (Join-Path $PSScriptRoot 'test-cleaning-deadline.ps1')}
$clean=(WorkAction $clean accept $hk).fields
$clean=(WorkAction $clean start $hk).fields
$clean=(WorkAction $clean complete $hk).fields
Check 'staff accepts starts and completes to pending inspection' ($clean.status-eq 'PENDING_INSPECTION')
$state=(Sql "SELECT readiness FROM v_room_readiness WHERE room_id=$($room.roomId)").Rows[0].readiness
Check 'staff completion does not bypass inspection into ready' ($state-ne 'READY')
