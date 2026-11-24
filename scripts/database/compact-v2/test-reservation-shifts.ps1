# Invoked only inside the isolated Test-Backend fixture.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$a=$f.branchId;$b=$f.otherBranchId
$null=Sql "DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='QA_ALL' AND p.code IN ('SHIFT_MANAGE','AUDIT_VIEW')"
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$ids=@()
foreach($code in @('101','102','103','104')){
 $room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code=$code;roomTypeId=$type.roomTypeId;locationId=$floor.locationId;departmentId=$f.departmentId}} $admin).fields
 $ids+= $room.roomId
}
$null=Sql @"
INSERT room_events(branch_id,room_id,turnover_no,event_type,actor_user_id,operation_key)
SELECT branch_id,room_id,turnover_no,'ADMIN_BASELINE_RESET',$($f.adminId),NEWID() FROM rooms WHERE branch_id=$a;
UPDATE rooms SET operational_status='READY' WHERE branch_id=$a;
"@
function Room($id){return @(Call GET "/api/v2/rooms?branchId=$a" $null $admin)|Where-Object roomId -eq $id}
function RoomAct($id,$action,$extra=@{},$expected=200,$token=$admin){
 $body=@{rowVersion=(Room $id).rowVersion;operationKey=[Guid]::NewGuid().ToString()}
 foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
 return Call POST "/api/v2/rooms/$id/actions/$action" $body $token $expected
}
$null=RoomAct $ids[0] check-in @{} 409
Check 'arrival requires an existing reservation' ((Room $ids[0]).operationalStatus-eq 'READY')
$null=RoomAct $ids[0] reserve @{} 403 $worker
Check 'non-reception cannot reserve despite unrelated grants' $true
$null=Call GET "/api/v2/rooms?branchId=$b" $null $worker 403
Check 'branch grant cannot read another branch' $true
$initial=Room $ids[0]
$null=RoomAct $ids[0] reserve
Check 'reservation remains clean but is not occupied' ((Room $ids[0]).isReserved -and (Room $ids[0]).readiness-eq 'READY' -and -not (Room $ids[0]).isOccupied)
$null=RoomAct $ids[0] reserve @{} 409
$null=RoomAct $ids[0] check-in @{rowVersion=$initial.rowVersion} 409
Check 'duplicate reserve and stale version rejected' $true
$null=RoomAct $ids[0] cancel-reservation
Check 'cancel unused reservation returns ready without turnover' ((Room $ids[0]).operationalStatus-eq 'READY' -and (Room $ids[0]).turnoverNo-eq $initial.turnoverNo)
$null=RoomAct $ids[0] reserve
$null=RoomAct $ids[1] reserve
$null=RoomAct $ids[0] move @{reason='Fixture move reservation';targetRoomId=$ids[1];targetRowVersion=(Room $ids[1]).rowVersion} 409
Check 'cannot move into another reserved room; no partial mutation' ((Room $ids[0]).isReserved -and (Room $ids[1]).isReserved)
$null=RoomAct $ids[1] cancel-reservation
$null=RoomAct $ids[0] move @{reason='Fixture move reservation';targetRoomId=$ids[1];targetRowVersion='AAAAAAAAAAA='} 409
Check 'target stale version rejects whole transfer' ((Room $ids[0]).isReserved -and (Room $ids[1]).operationalStatus-eq 'READY')
$null=RoomAct $ids[0] move @{reason='Fixture move reservation';targetRoomId=$ids[1];targetRowVersion=(Room $ids[1]).rowVersion}
Check 'unused source ready and destination reserved after move' ((Room $ids[0]).operationalStatus-eq 'READY' -and (Room $ids[1]).isReserved -and (Room $ids[0]).turnoverNo-eq $initial.turnoverNo)
$h=Call GET "/api/v2/rooms/$($ids[0])/history" $null $admin
Check 'reservation transfer preserves history' (@($h.events|Where-Object {$_.fields.eventType-eq 'RESERVATION_MOVE_OUT'}).Count-eq 1)
$key=[Guid]::NewGuid().ToString()
$null=RoomAct $ids[1] check-in @{operationKey=$key}
Check 'guest arrival is occupied without a reason' ((Room $ids[1]).isOccupied)
$null=RoomAct $ids[1] check-in @{operationKey=$key} 409
Check 'retry cannot repeat room execution' $true
$before=Room $ids[1]
$null=RoomAct $ids[1] move @{reason='Fixture occupied transfer';targetRoomId=$ids[2];targetRowVersion=(Room $ids[2]).rowVersion}
Check 'used source dirty with next turnover; destination occupied' ((Room $ids[1]).operationalStatus-eq 'DIRTY' -and (Room $ids[1]).turnoverNo-eq ($before.turnoverNo+1) -and (Room $ids[2]).isOccupied)
Check 'used room cannot retain old ready evidence' ((Room $ids[1]).readiness-ne 'READY')
$null=RoomAct $ids[2] check-out
Check 'checkout without reason requires housekeeping' ((Room $ids[2]).operationalStatus-eq 'DIRTY' -and (Room $ids[2]).readiness-ne 'READY')
$null=RoomAct $ids[2] reserve @{} 409
Check 'dirty room cannot be booked until inspected' $true
$null=RoomAct $ids[3] block @{reason='Fixture operational block'}
$null=RoomAct $ids[3] reserve @{} 409
Check 'blocked room cannot be booked' $true

# WORKING yesterday, WORKING today, and PLANNED tomorrow. No primary changes.
$shiftFixture=Sql @"
SET NOCOUNT ON;
DECLARE @today date=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
UPDATE memberships SET valid_from=DATEADD(day,-7,SYSUTCDATETIME());
UPDATE user_roles SET assigned_at=DATEADD(day,-7,SYSUTCDATETIME());
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT $a,$($f.departmentId),role_id,'Fixture day',0,1440,10 FROM roles WHERE code='QA_ALL';
DECLARE @shift bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by)
VALUES(@shift,$($f.workerId),DATEADD(day,-1,@today),'WORKING',DATEADD(day,-1,SYSUTCDATETIME()),$($f.adminId));
DECLARE @expired bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by)
VALUES(@shift,$($f.adminId),@today,'WORKING',DATEADD(minute,-1,SYSUTCDATETIME()),$($f.adminId));
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by)
VALUES(@shift,$($f.workerId),DATEADD(day,1,@today),'PLANNED',$($f.adminId));
SELECT @today today,@expired expired,@shift shift;
"@
$sf=$shiftFixture.Rows[0];$today=([DateTime]$sf.today).ToString('yyyy-MM-dd')
$week=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $admin
$ended=(Sql "SELECT status,ended_at,started_at FROM workday_assignments WHERE assignment_id=$($sf.expired)").Rows[0]
$expected=[TimeZoneInfo]::ConvertTimeToUtc(([DateTime]$sf.today).Date,[TimeZoneInfo]::FindSystemTimeZoneById('SE Asia Standard Time'))
Check 'overdue working shift ends at scheduled boundary, not current clock' ($ended.status-eq 'ENDED' -and $ended.ended_at-eq $expected -and $ended.ended_at-ge $ended.started_at)
Check 'current working shift stays active' ($week.active -and $week.active.workDate-eq $today)
$null=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $admin
$audit=(Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_type='Workday' AND entity_key='$($sf.expired)' AND action='AUTO_END' AND actor_user_id IS NULL").Rows[0].n
Check 'auto expiry idempotent and attributed to system, not employee' ($audit-eq 1)
$history=Call GET "/api/v2/workday/assignments/$($sf.expired)/history" $null $worker
Check 'own shift history shows automatic end without AUDIT_VIEW' (@($history|Where-Object action -eq 'AUTO_END').Count-eq 1)
Check 'unstarted future shift remains planned' ((Sql "SELECT COUNT(*) n FROM workday_assignments WHERE user_id=$($f.workerId) AND status='PLANNED'").Rows[0].n-eq 1)
$null=Call POST "/api/v2/workday/assignments/$($week.active.assignmentId)/end" @{rowVersion=$week.active.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker 403
Check 'staff cannot end someone else shift' $true
$null=Call GET "/api/v2/workday/assignments/$($week.active.assignmentId)/history" $null $worker 403
Check 'staff cannot read another employee shift history' $true
$null=Call POST "/api/v2/workday/assignments/$($week.active.assignmentId)/end" @{rowVersion=$week.active.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
Check 'manual early end remains supported' ((Sql "SELECT status FROM workday_assignments WHERE assignment_id=$($week.active.assignmentId)").Rows[0].status-eq 'ENDED')
# Exercise nested transaction expiry on Start and End (same workday lock).
$null=Sql "DELETE workday_assignments WHERE status='PLANNED'; UPDATE workday_assignments SET work_date=CONVERT(date,'$today'),status='PLANNED',started_at=NULL,ended_at=NULL WHERE assignment_id=$($sf.expired)"
$mine=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $worker
$row=$mine.assignments|Where-Object assignmentId -eq $sf.expired
$started=(Call POST "/api/v2/workday/assignments/$($sf.expired)/start" @{rowVersion=$row.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker).fields
Check 'start current assigned shift succeeds' ($started.status-eq 'WORKING')
$null=Call POST "/api/v2/workday/assignments/$($sf.expired)/end" @{rowVersion=$row.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker 409
Check 'shift stale rowversion rejected' $true
$null=Call POST "/api/v2/workday/assignments/$($sf.expired)/end" @{rowVersion=$started.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker
$null=Sql @"
DECLARE @local datetime2=CONVERT(datetime2,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
DECLARE @minute int=DATEPART(hour,@local)*60+DATEPART(minute,@local);
UPDATE workday_shifts SET start_minute=CASE WHEN @minute<720 THEN 720 ELSE 0 END,end_minute=CASE WHEN @minute<720 THEN 1440 ELSE 720 END;
UPDATE workday_assignments SET status='PLANNED',started_at=NULL,ended_at=NULL WHERE assignment_id=$($sf.expired);
"@
$mine=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $worker
$row=$mine.assignments|Where-Object assignmentId -eq $sf.expired
$errorResult=Call POST "/api/v2/workday/assignments/$($sf.expired)/start" @{rowVersion=$row.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $worker 409
Check 'start outside scheduled time denied' ($errorResult.title-eq 'SHIFT_OUTSIDE_WORKING_HOURS')
# Run the actual hosted worker: no Workday request is made after restart.
$null=Sql "UPDATE workday_shifts SET start_minute=0,end_minute=1440; UPDATE workday_assignments SET work_date=DATEADD(day,-1,CONVERT(date,'$today')),status='WORKING',started_at=DATEADD(day,-1,SYSUTCDATETIME()),ended_at=NULL WHERE assignment_id=$($sf.expired)"
$beforeAudit=(Sql "SELECT COUNT(*) n FROM audit_logs WHERE action='AUTO_END'").Rows[0].n
Stop-Process -Id $api.Id -Force
$api.WaitForExit()
$env:Compact__AutoEndShiftsEnabled='true'
$api=Start-Process dotnet -ArgumentList @($dll) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'expiry-worker.log') -RedirectStandardError (Join-Path $artifacts 'expiry-worker-error.log')
for($attempt=0;$attempt-lt 40;$attempt++){
 if((Sql "SELECT status FROM workday_assignments WHERE assignment_id=$($sf.expired)").Rows[0].status-eq 'ENDED'){break}
 Start-Sleep -Milliseconds 250
}
Check 'background worker ends overdue shift without browser/API polling' ((Sql "SELECT status FROM workday_assignments WHERE assignment_id=$($sf.expired)").Rows[0].status-eq 'ENDED')
Check 'background worker creates one system audit' ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE action='AUTO_END'").Rows[0].n-eq ($beforeAudit+1))
