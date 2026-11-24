# Only invoked by Test-Backend on its newly created independent SQL database.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$a=$f.branchId;$b=$f.otherBranchId
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{name='Reception fixture';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{name='Reception floor';kind='FLOOR';floorNumber=1}} $admin).fields
$ids=@();foreach($code in @('R101','R102')){$ids+=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code=$code;roomTypeId=$type.roomTypeId;locationId=$floor.locationId;departmentId=$f.departmentId}} $admin).fields.roomId}
$null=Sql "INSERT room_events(branch_id,room_id,turnover_no,event_type,actor_user_id,operation_key) SELECT branch_id,room_id,turnover_no,'ADMIN_BASELINE_RESET',$($f.adminId),NEWID() FROM rooms WHERE branch_id=$a; UPDATE rooms SET operational_status='READY' WHERE branch_id=$a;"
function Room($id){return @(Call GET "/api/v2/rooms?branchId=$a" $null $admin)|Where-Object roomId -eq $id}
function RoomAct($id,$verb,$extra=@{},$expected=200){$body=@{rowVersion=(Room $id).rowVersion;operationKey=[Guid]::NewGuid().ToString()};foreach($k in $extra.Keys){$body[$k]=$extra[$k]};return Call POST "/api/v2/rooms/$id/actions/$verb" $body $admin $expected}
foreach($verb in @('reserve','cancel-reservation','check-in','check-out','move')){
 $e=RoomAct $ids[0] $verb @{} 403
 Check "no shift denies $verb at API" ($e.title-eq 'RECEPTION_NOT_ON_SHIFT')
}
Check 'read-only room capability off without attendance' (-not (Room $ids[0]).canOperate)
$slot=(Sql @"
UPDATE memberships SET valid_from=DATEADD(day,-7,SYSUTCDATETIME());
UPDATE user_roles SET assigned_at=DATEADD(day,-7,SYSUTCDATETIME());
DECLARE @today date=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT $a,$($f.departmentId),role_id,'Reception fixture',0,1440,5 FROM roles WHERE code='RECEPTION';
DECLARE @shift bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by) VALUES(@shift,$($f.adminId),@today,'PLANNED',$($f.adminId));
SELECT SCOPE_IDENTITY() assignmentId,@shift shiftId,@today today;
"@).Rows[0]
$today=([DateTime]$slot.today).ToString('yyyy-MM-dd');$assignment=[long]$slot.assignmentId;$shift=[long]$slot.shiftId
$null=RoomAct $ids[0] reserve @{} 403
Check 'planned shift is not started attendance' (-not (Room $ids[0]).canOperate)
$week=Call GET "/api/v2/workday?branchId=$a&date=$today" $null $admin
$row=$week.assignments|Where-Object assignmentId -eq $assignment
$null=Call POST "/api/v2/workday/assignments/$assignment/start" @{rowVersion=$row.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
Check 'actual start unlocks reception room capability' ((Room $ids[0]).canOperate)
$null=RoomAct $ids[0] reserve
$null=RoomAct $ids[0] move @{reason='Move unused reservation';targetRoomId=$ids[1];targetRowVersion=(Room $ids[1]).rowVersion}
Check 'unused source ready and destination reserved' ((Room $ids[0]).operationalStatus-eq 'READY' -and (Room $ids[1]).isReserved)
$null=RoomAct $ids[1] cancel-reservation
$null=RoomAct $ids[0] reserve
$null=RoomAct $ids[0] check-in
$null=RoomAct $ids[0] move @{reason='Move occupied room';targetRoomId=$ids[1];targetRowVersion=(Room $ids[1]).rowVersion}
Check 'used source dirty and destination occupied' ((Room $ids[0]).operationalStatus-eq 'DIRTY' -and (Room $ids[1]).isOccupied)
$null=RoomAct $ids[1] check-out
Check 'checkout still requires cleaning' ((Room $ids[1]).operationalStatus-eq 'DIRTY')
# Changing fixture scheduling demonstrates the API does not depend on worker cleanup.
$null=Sql "UPDATE workday_assignments SET work_date=DATEADD(day,-1,work_date),started_at=DATEADD(day,-1,started_at) WHERE assignment_id=$assignment"
$e=RoomAct $ids[0] reserve @{} 403
Check 'stale WORKING yesterday denied before expiry worker' ($e.title-eq 'RECEPTION_NOT_ON_SHIFT')
$null=Sql "UPDATE workday_assignments SET work_date=CONVERT(date,'$today'),started_at=SYSUTCDATETIME() WHERE assignment_id=$assignment; UPDATE workday_shifts SET start_minute=CASE WHEN DATEPART(hour,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time')<12 THEN 720 ELSE 0 END,end_minute=CASE WHEN DATEPART(hour,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time')<12 THEN 1440 ELSE 720 END WHERE shift_id=$shift"
$null=RoomAct $ids[0] reserve @{} 403
Check 'WORKING outside scheduled hours denied' (-not (Room $ids[0]).canOperate)
$null=Sql "UPDATE workday_shifts SET start_minute=0,end_minute=1440 WHERE shift_id=$shift; UPDATE workday_assignments SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE assignment_id=$assignment"
$null=RoomAct $ids[0] reserve @{} 403
Check 'ended shift cannot change rooms' $true
$null=RoomAct $ids[0] block @{reason='OM block not reception attendance'}
Check 'independent management block permission unchanged' ((Room $ids[0]).isBlocked)
# Employees with BRANCH IR_VIEW must not get branch-wide access by changing mine.
$null=Sql "DELETE memberships WHERE user_id=$($f.workerId) AND kind='DEPARTMENT_AUTHORITY'; DELETE ur FROM user_roles ur JOIN roles r ON ur.role_id=r.role_id WHERE ur.user_id=$($f.workerId) AND r.code IN ('DEPT_MANAGER','STOREKEEPER');"
$own=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Employee authored';departmentId=$f.departmentId} $worker).fields
$other=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Manager private';departmentId=$f.departmentId} $admin).fields
$assigned=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Employee recipient';departmentId=$f.departmentId} $admin).fields
$assigned=(WorkAction $assigned submit $admin).fields
$assigned=(WorkAction $assigned route $admin @{relatedId=$f.departmentId}).fields
$null=Call POST "/api/v2/work/$($assigned.workId)/actions/assign" @{rowVersion=$assigned.rowVersion;operationKey=[Guid]::NewGuid().ToString();userId=$f.workerId} $admin
foreach($tail in @('mine=false','mine=true','mine=false&historyMine=false')){
 $list=Call GET "/api/v2/work?branchId=$a&kind=INTERNAL_REQUEST&$tail" $null $worker
 $found=@($list.items|ForEach-Object {$_.fields.workId})
 Check "IR ownership enforced for $tail" ($found.Count-eq 2 -and $found-contains $own.workId -and $found-contains $assigned.workId -and $found-notcontains $other.workId)
}
$null=Call GET "/api/v2/work/$($other.workId)" $null $worker 403
$null=Call GET "/api/v2/work/$($other.workId)/related" $null $worker 403
Check 'direct detail and related request ID cannot bypass visibility' $true
$null=Call GET "/api/v2/work/$($own.workId)" $null $worker
$null=Call GET "/api/v2/work/$($assigned.workId)" $null $worker
Check 'creator and recipient can read their request details' $true
$all=Call GET "/api/v2/work?branchId=$a&kind=INTERNAL_REQUEST&mine=false" $null $admin
Check 'department coordinator keeps authorized queue' ($all.total-eq 3)
$null=Call GET "/api/v2/work?branchId=$b&kind=INTERNAL_REQUEST" $null $worker 403
Check 'IR branch permission remains required' $true
$null=Sql "UPDATE work_assignments SET assignment_status='COMPLETED',ended_at=SYSUTCDATETIME() WHERE work_id=$($assigned.workId)"
$list=Call GET "/api/v2/work?branchId=$a&kind=INTERNAL_REQUEST&mine=true" $null $worker
$null=Call GET "/api/v2/work/$($assigned.workId)" $null $worker
Check 'completed recipient retains personal request history' (@($list.items|Where-Object {$_.fields.workId-eq $assigned.workId}).Count-eq 1)
$report=Call GET "/api/v2/insights/reports/requests?branchId=$a" $null $worker
Check 'reports apply same employee IR ownership' (@($report|Where-Object workId -eq $other.workId).Count-eq 0 -and @($report).Count-eq 2)
$null=Sql "UPDATE work_items SET due_at=DATEADD(hour,-1,SYSUTCDATETIME()) WHERE kind='INTERNAL_REQUEST'"
$dashboard=Call GET "/api/v2/insights/dashboard?branchId=$a" $null $worker
Check 'dashboard attention cannot leak other employee requests' (@($dashboard.attention|Where-Object workId -eq $other.workId).Count-eq 0)
$overview=Call GET "/api/v2/insights/owner?branchId=$a" $null $worker
Check 'alternate overview also preserves IR ownership' (@($overview.attention|Where-Object workId -eq $other.workId).Count-eq 0)
$null=Sql "UPDATE work_items SET status='CANCELLED' WHERE work_id=$($assigned.workId); UPDATE work_assignments SET assignment_status='CANCELLED' WHERE work_id=$($assigned.workId)"
$null=Call GET "/api/v2/work/$($assigned.workId)" $null $worker
$list=Call GET "/api/v2/work?branchId=$a&kind=INTERNAL_REQUEST&mine=true" $null $worker
Check 'cancelled request remains in recipient history' (@($list.items|Where-Object {$_.fields.workId-eq $assigned.workId}).Count-eq 1)
