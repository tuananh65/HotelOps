# Invoked inside Test-Backend.ps1; every write targets a fresh, backed-up isolated database.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$actors=Sql (Get-Content (Join-Path $PSScriptRoot 'housekeeping-staff-fixture.sql') -Encoding UTF8 -Raw)
$hkA=(Call POST '/api/v2/auth/login' @{username='qa_hk_a';password=$password}).accessToken
$hkB=(Call POST '/api/v2/auth/login' @{username='qa_hk_b';password=$password}).accessToken
$peer=(Call POST '/api/v2/auth/login' @{username='qa_hk_peer';password=$password}).accessToken
$head=(Call POST '/api/v2/auth/login' @{username='qa_hks_a';password=$password}).accessToken
$noDefect=(Call POST '/api/v2/auth/login' @{username='qa_inspect_no_defect';password=$password}).accessToken
$a=$f.branchId;$b=$f.otherBranchId;$dept=$f.departmentId
$hkId=($actors.Rows|Where-Object username -eq 'qa_hk_a').user_id
$peerId=($actors.Rows|Where-Object username -eq 'qa_hk_peer').user_id
$foreignId=($actors.Rows|Where-Object username -eq 'qa_hk_b').user_id
$headId=($actors.Rows|Where-Object username -eq 'qa_hks_a').user_id
# Current creation contract requires both creator and assignee to be actually on shift.
$null=Sql @"
DECLARE @now datetime2=SYSUTCDATETIME(),@day date=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
DECLARE @minute int=DATEPART(hour,@now AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time')*60+DATEPART(minute,@now AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time');
UPDATE workday_shifts SET start_minute=CASE WHEN @minute>0 THEN @minute-1 ELSE 0 END,end_minute=CASE WHEN @minute+479>1440 THEN 1440 ELSE @minute+479 END WHERE name=N'Housekeeping morning';
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT d.branch_id,d.department_id,r.role_id,N'Supervisor fixture',0,1440,4 FROM departments d CROSS JOIN roles r WHERE d.code='HK' AND r.code='HOUSEKEEPING_SUPERVISOR';
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by)
SELECT s.shift_id,u.user_id,@day,'WORKING',DATEADD(second,-1,@now),$($f.adminId) FROM user_roles u JOIN workday_shifts s ON s.role_id=u.role_id AND s.branch_id=u.branch_id;
"@
Add-Type -AssemblyName System.Net.Http
function UploadEvidence($workId,$token,$expected=200){
 $client=New-Object Net.Http.HttpClient
 $client.DefaultRequestHeaders.Authorization=New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$token)
 $multipart=New-Object Net.Http.MultipartFormDataContent
 try {
  $multipart.Add((New-Object Net.Http.StringContent([string]$a)),'branchId')
  $multipart.Add((New-Object Net.Http.StringContent([string]$workId)),'workId')
  $bytes=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=')
  $content=New-Object Net.Http.ByteArrayContent(,$bytes);$content.Headers.ContentType=New-Object Net.Http.Headers.MediaTypeHeaderValue('image/png')
  $multipart.Add($content,'file','housekeeping.png')
  $response=$client.PostAsync("http://127.0.0.1:$Port/api/v2/evidence",$multipart).GetAwaiter().GetResult()
  $json=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  if([int]$response.StatusCode-ne $expected){throw "Evidence upload expected $expected got $($response.StatusCode): $json"}
  return $json|ConvertFrom-Json
 }finally{$multipart.Dispose();$client.Dispose()}
}
$me=Call GET '/api/v2/auth/me' $null $hkA
Check 'HK Staff has exactly baseline 13 grants; attendance OWN' (@($me.grants).Count-eq 13 -and @($me.grants|Where-Object {$_.code-eq 'ATTENDANCE_RECORD' -and $_.dataScope-eq 'OWN'}).Count-eq 1)
foreach($entry in @(@{token=$hkA;own=$a;foreign=$b},@{token=$hkB;own=$b;foreign=$a})){
 foreach($path in @('rooms','insights/personal','workspace/options','workday')){
  $null=Call GET "/api/v2/$path`?branchId=$($entry.own)&date=2026-11-23" $null $entry.token
  $null=Call GET "/api/v2/$path`?branchId=$($entry.foreign)&date=2026-11-23" $null $entry.token 403
 }
 foreach($kind in @('CLEANING','REWORK','OPERATIONAL','INTERNAL_REQUEST','INCIDENT')){
  $null=Call GET "/api/v2/work?branchId=$($entry.foreign)&kind=$kind" $null $entry.token 403
 }
}
Check 'both branches enforce read scope including personal dashboard and Workday' $true
foreach($path in @('/api/v2/users',"/api/v2/catalog/roles?branchId=$a","/api/v2/templates?branchId=$a","/api/v2/insights/dashboard?branchId=$a","/api/v2/insights/stock?branchId=$a")){$null=Call GET $path $null $hkA 403}
foreach($kind in @('CLEANING','REWORK','INSPECTION','MAINTENANCE','OPERATIONAL')){$null=Call POST '/api/v2/work' @{branchId=$a;kind=$kind;title='Forbidden specialist creation'} $hkA 403}
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='ISSUE'} $hkA 403
Check 'no admin, templates, branch dashboard, stock, assignment or specialist creation privileges' $true
$null=Call POST '/api/v2/users' @{branchId=$a;username='qa_forbidden';fullName='Forbidden user';password=$password} $hkA 403
$null=Call POST "/api/v2/catalog/roles?branchId=$a" @{fields=@{code='FORBIDDEN';name='Forbidden role'}} $hkA 403
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='HK-STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='HK-F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields
foreach($verb in @('check-in','check-out','move','block','unblock')){$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA 403}
Check 'staff cannot change occupancy, READY or room blocks' $true
$template=(Call POST '/api/v2/templates/checklists' @{branchId=$a;code='HK-CLEAN';name='Cleaning';kind='CLEANING';items=@(@{description='Clean surfaces';isRequired=$true;weight=1})} $head).fields
$quality=(Call POST '/api/v2/templates/checklists' @{branchId=$a;code='HK-QUALITY';name='Inspection';kind='INSPECTION';items=@(@{description='Surfaces inspected';isRequired=$true;weight=1})} $head).fields
$pending=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&search=101" $null $head
Check 'supervisor sees pending rooms without an existing cleaning request' ($pending.total-eq 1 -and $pending.items[0].roomId-eq $room.roomId -and $pending.items[0].readiness-eq 'PENDING_CLEANING')
$null=Call GET "/api/v2/work/cleaning-rooms?branchId=$b" $null $head 403
$null=Call GET "/api/v2/work/cleaning-rooms?branchId=$a" $null $hkA 403
$empty=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&search=not-a-room" $null $head
Check 'pending-room search and assignment/branch permissions enforced' ($empty.total-eq 0)
$selectedRoomVersion=$pending.items[0].rowVersion
$blocked=Call POST "/api/v2/rooms/$($room.roomId)/actions/block" @{rowVersion=$selectedRoomVersion;operationKey=[Guid]::NewGuid().ToString();reason='Fixture cleaning block'} $admin
$notEligible=Call GET "/api/v2/work/cleaning-rooms?branchId=$a" $null $head
$null=Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Blocked selection';roomId=$room.roomId;roomRowVersion=$blocked.room.fields.rowVersion;assigneeUserId=$hkId} $head 409
$unblocked=Call POST "/api/v2/rooms/$($room.roomId)/actions/unblock" @{rowVersion=$blocked.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Fixture unblock room'} $admin
Check 'blocked rooms are excluded and cannot be requested through the pending-room flow' ($notEligible.total-eq 0)
$null=Sql "UPDATE rooms SET code=code WHERE room_id=$($room.roomId)"
$null=Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Stale room selection';roomId=$room.roomId;roomRowVersion=$selectedRoomVersion;assigneeUserId=$hkId} $head 409
$pending=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&roomId=$($room.roomId)" $null $head
$clean=(Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Cleaning room 101';roomId=$room.roomId;roomRowVersion=$pending.items[0].rowVersion;checklistTemplateId=$template.checklistTemplateId;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o');assigneeUserId=$hkId} $head).fields
$pending=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&search=101" $null $head
$assigned=Call GET "/api/v2/work/cleaning-queue?branchId=$a&queue=doing&search=101" $null $head
Check 'created request leaves pending rooms and appears in assigned queue' ($pending.total-eq 0 -and $assigned.total-eq 1 -and $assigned.items[0].workId-eq $clean.workId)
$null=Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Duplicate cleaning';roomId=$room.roomId;assigneeUserId=$hkId} $head 409
$null=WorkAction $clean assign $head @{userId=$foreignId} 409
$null=Sql "UPDATE users SET account_status='INACTIVE' WHERE user_id=$peerId"
$null=WorkAction $clean assign $head @{userId=$peerId} 409
$null=Sql "UPDATE users SET account_status='ACTIVE' WHERE user_id=$peerId"
$null=WorkAction $clean assign $hkA @{userId=$peerId} 403
foreach($verb in @('accept','start','progress','complete')){$null=WorkAction $clean $verb $peer @{progress=50} 403}
$item=(Call GET "/api/v2/work/$($clean.workId)" $null $hkA).checklist[0].fields
$null=Call PUT "/api/v2/work/$($clean.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $peer 409
$null=WorkAction $clean complete $hkA @{} 409
Check 'assignment cannot cross branch; other staff cannot execute; duplicate/invalid completion denied' $true
$own=Call GET "/api/v2/work?branchId=$a&kind=CLEANING&assignedToMe=true&search=101&sort=due" $null $hkA
$other=Call GET "/api/v2/work?branchId=$a&kind=CLEANING&assignedToMe=true" $null $peer
$dashboard=Call GET "/api/v2/insights/personal?branchId=$a" $null $hkA
$peerDashboard=Call GET "/api/v2/insights/personal?branchId=$a" $null $peer
Check 'personal list and KPI exclude another employee and include assigned work/overdue' ($own.total-eq 1 -and $other.total-eq 0 -and $dashboard.metrics.assignedToday-eq 1 -and $dashboard.metrics.overdue-eq 1 -and $peerDashboard.metrics.assignedToday-eq 0)
$stale=$clean;$clean=(WorkAction $clean accept $hkA).fields
$null=WorkAction $stale start $hkA @{} 409
$clean=(WorkAction $clean start $hkA).fields
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title='Too early inspection';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$quality.checklistTemplateId} $head 409
$parallel=(Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title='Timer conflict verification';departmentId=$dept} $admin).fields
$parallel=(WorkAction $parallel assign $admin @{userId=$hkId}).fields
$parallel=(WorkAction $parallel accept $hkA).fields
$null=WorkAction $parallel start $hkA @{} 409
Check 'cannot inspect before cleaning completion or start overlapping work timer' $true
$null=WorkAction $clean progress $hkA @{progress=45} 403
$clean=(WorkAction $clean pause $hkA).fields
$clean=(WorkAction $clean resume $hkA).fields
$null=UploadEvidence $clean.workId $peer 403
$proof=(UploadEvidence $clean.workId $hkA).fields
$proofDetail=Call GET "/api/v2/work/$($clean.workId)" $null $hkA
$null=Call GET "/api/v2/evidence/$($proof.attachmentId)" $null $hkB 403
Check 'evidence upload requires assignment and detail exposes safe file metadata only' ($proofDetail.attachments.Count-eq 1 -and -not ($proofDetail.attachments[0].PSObject.Properties.Name -contains 'storageKey'))
$item=(Call GET "/api/v2/work/$($clean.workId)" $null $hkA).checklist[0].fields
$null=Call PUT "/api/v2/work/$($clean.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $peer 403
$checked=(Call PUT "/api/v2/work/$($clean.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS';note='Cleaned by assigned staff'} $hkA).fields
$null=Call PUT "/api/v2/work/$($clean.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='FAIL'} $hkA 409
$clean=(WorkAction $clean complete $hkA).fields
Check 'accept/start/pause/resume/complete retain legacy checklist history and reject manual progress' ($clean.status-eq 'PENDING_INSPECTION' -and $checked.note-eq 'Cleaned by assigned staff')
$waiting=Call GET "/api/v2/work/inspection-queue?branchId=$a&search=101" $null $head
Check 'supervisor inspection intake includes submitted cleaning root' ($waiting.total-eq 1 -and $waiting.items[0].workId-eq $clean.workId)
$null=WorkAction $clean assign $head @{userId=$peerId} 409
$null=Call GET "/api/v2/work/$($clean.workId)" $null $hkB 403
$state=@(Call GET "/api/v2/rooms?branchId=$a" $null $hkA)|Where-Object roomId -eq $room.roomId
Check 'cleaning completed waits for inspection and does not make room READY' ($state.readiness-ne 'READY')
function NewInspection($title){
 $i=(Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title=$title;roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$quality.checklistTemplateId} $head).fields
 $i=(WorkAction $i assign $head @{userId=$headId}).fields
 foreach($verb in @('accept','start')){$i=(WorkAction $i $verb $head).fields}
 return $i
}
$inspection=NewInspection 'First quality inspection'
$waiting=Call GET "/api/v2/work/inspection-queue?branchId=$a" $null $head
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title='Duplicate inspection';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$quality.checklistTemplateId} $head 409
Check 'active inspection removes root from waiting queue and duplicate creation is rejected' ($waiting.total-eq 0)
foreach($verb in @('start','pass','fail')){$null=WorkAction $inspection $verb $hkA @{} 403}
$qualityItem=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).checklist[0].fields
$null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($qualityItem.workChecklistItemId)" @{rowVersion=$qualityItem.rowVersion;outcome='FAIL'} $head
$finding=(Call POST "/api/v2/work/$($inspection.workId)/findings" @{rowVersion=$inspection.rowVersion;description='Surface requires cleaning again'} $head).fields
$inspection=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).work.fields
$inspection=(WorkAction $inspection complete $head).fields
$inspection=(WorkAction $inspection fail $head).fields
$defects=Call GET "/api/v2/work/defects?branchId=$a&status=OPEN&search=Surface" $null $head
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title='Bypass rework';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$quality.checklistTemplateId} $head 409
Check 'failed inspection exposes open defects and requires submitted rework before reinspection' ($defects.total-eq 1)
$detail=Call GET "/api/v2/work/$($clean.workId)" $null $hkA
Check 'staff sees failure summary through cleaning, cannot read/modify Inspection directly' ($detail.work.fields.status-eq 'REWORK_REQUIRED' -and $detail.reviews[0].result-eq 'FAIL' -and $detail.reviews[0].findings[0].description-eq 'Surface requires cleaning again')
$null=Call GET "/api/v2/work/$($inspection.workId)" $null $hkA 403
$rework=(Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title='Correct surface';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$template.checklistTemplateId} $head).fields
$rework=(WorkAction $rework assign $head @{userId=$hkId}).fields
$null=WorkAction $rework accept $peer @{} 403
foreach($verb in @('accept','start')){$rework=(WorkAction $rework $verb $hkA).fields}
$item=(Call GET "/api/v2/work/$($rework.workId)" $null $hkA).checklist[0].fields
$null=Call PUT "/api/v2/work/$($rework.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $hkA
$rework=(WorkAction $rework complete $hkA).fields
$waiting=Call GET "/api/v2/work/inspection-queue?branchId=$a" $null $head
Check 'submitted rework reopens inspection intake without making room READY' ($waiting.total-eq 1)
$state=@(Call GET "/api/v2/rooms?branchId=$a" $null $hkA)|Where-Object roomId -eq $room.roomId
Check 'rework is assigned execution and submission does not make room READY' ($rework.status-eq 'PENDING_REINSPECTION' -and $state.readiness-ne 'READY')
$null=Call POST "/api/v2/work/$($inspection.workId)/findings/$($finding.findingId)/resolve" @{rowVersion=$finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();relatedId=$rework.workId;reason='Staff cannot self review'} $hkA 403
$null=Call POST "/api/v2/work/$($inspection.workId)/findings/$($finding.findingId)/resolve" @{rowVersion=$finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();relatedId=$rework.workId;reason='Inspection alone does not grant defect resolution'} $noDefect 403
Check 'defect resolution requires DEFECT_RESOLVE in addition to inspection authority' $true
$null=Call POST "/api/v2/work/$($inspection.workId)/findings/$($finding.findingId)/resolve" @{rowVersion=$finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();relatedId=$rework.workId;reason='Supervisor verified correction'} $head
$review=NewInspection 'Independent reinspection'
$item=(Call GET "/api/v2/work/$($review.workId)" $null $head).checklist[0].fields
$null=Call PUT "/api/v2/work/$($review.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $head
$review=(WorkAction $review complete $head).fields
$review=(WorkAction $review pass $head).fields
$state=@(Call GET "/api/v2/rooms?branchId=$a" $null $hkA)|Where-Object roomId -eq $room.roomId
$history=Call GET "/api/v2/work?branchId=$a&kind=CLEANING&assignedToMe=true" $null $hkA
$dashboard=Call GET "/api/v2/insights/personal?branchId=$a" $null $hkA
Check 'supervisor reinspection passes to READY; completed staff history and original failure preserved' ($state.readiness-eq 'READY' -and $history.total-eq 1 -and $dashboard.metrics.completedToday-eq 2 -and (Call GET "/api/v2/work/$($clean.workId)" $null $hkA).reviews.Count-eq 2)
$task=(Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title='Restock trolley';departmentId=$dept} $admin).fields
$task=(WorkAction $task assign $admin @{userId=$hkId}).fields
$null=WorkAction $task start $peer @{} 403
foreach($verb in @('accept','start','complete')){$task=(WorkAction $task $verb $hkA).fields}
$null=WorkAction $task verify $hkA @{} 403
Check 'assigned operational task completes to verification; staff cannot self verify' ($task.status-eq 'PENDING_VERIFICATION')
$request=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Need room support';departmentId=$dept;requesterDepartmentId=$dept} $hkA).fields
$request=(WorkAction $request submit $hkA).fields
foreach($verb in @('route','assign','cancel','progress')){$null=WorkAction $request $verb $peer @{userId=$peerId;relatedId=$dept;progress=20} 403}
$caps=(Call GET "/api/v2/work/$($request.workId)" $null $peer).capabilities
Check 'IR_PROCESS alone cannot coordinate/cancel another request; capability agrees' (-not $caps.canCoordinateRequest -and -not $caps.canHandleRequest)
$request=(WorkAction $request route $admin @{relatedId=$dept}).fields
$request=(WorkAction $request assign $admin @{userId=$hkId}).fields
foreach($verb in @('accept','start','complete')){$request=(WorkAction $request $verb $hkA).fields}
Check 'assigned internal request processes through existing handler workflow' ($request.status-eq 'COMPLETED')
$incident=(Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='Broken light';roomId=$room.roomId} $hkA).fields
$null=UploadEvidence $incident.workId $hkA
$null=Call POST '/api/v2/work' @{branchId=$b;kind='INCIDENT';title='Foreign incident'} $hkA 403
foreach($verb in @('triage','assign','verify')){$null=WorkAction $incident $verb $hkA @{} 403}
Check 'incident creation in branch works without triage/maintenance authority' ($incident.kind-eq 'INCIDENT')
$unit=(Call POST "/api/v2/catalog/references?branchId=$a" @{fields=@{kind='UOM';code='HK-EA';name='Piece'}} $admin).fields
$item=(Call POST "/api/v2/catalog/items?branchId=$a" @{fields=@{code='HK-TOWEL';name='Towel';baseUnitId=$unit.referenceId}} $admin).fields
$material=(Call POST '/api/v2/documents' @{branchId=$a;kind='MATERIAL_REQUEST';departmentId=$dept;reason='Supplies for housekeeping'} $hkA).fields
$null=Call POST "/api/v2/documents/$($material.documentId)/lines" @{rowVersion=$material.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2} $peer 403
$null=DocAction $material cancel $peer 403
$material=(Call POST "/api/v2/documents/$($material.documentId)/lines" @{rowVersion=$material.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2} $hkA).document.fields
$material=(DocAction $material submit $hkA).fields
$null=DocAction $material submit $peer 403
foreach($verb in @('approve','post','close')){$null=DocAction $material $verb $hkA 403}
$null=Call GET "/api/v2/documents/$($material.documentId)" $null $hkB 403
Check 'material request create/line/submit works; no approve, stock post, close or cross-branch read' ($material.status-eq 'PENDING_APPROVAL')
$today=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').ToString('yyyy-MM-dd')
$shift=(Sql "SELECT shift_id FROM workday_shifts WHERE branch_id=$a AND name=N'Housekeeping morning'").Rows[0].shift_id
$shiftVersion=(Call GET "/api/v2/workday?branchId=$a&date=$today" $null $admin).shifts|Where-Object shiftId -eq $shift
$slotRow=(Sql "UPDATE workday_assignments SET status='PLANNED',started_at=NULL WHERE shift_id=$shift AND user_id=$hkId AND work_date='$today'; SELECT assignment_id,row_version FROM workday_assignments WHERE shift_id=$shift AND user_id=$hkId AND work_date='$today'").Rows[0]
$slot=@{fields=@{assignmentId=$slotRow.assignment_id;rowVersion=[Convert]::ToBase64String($slotRow.row_version)}}
$null=Call POST "/api/v2/workday/assignments/$($slot.fields.assignmentId)/end" @{rowVersion=$slot.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA 409
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$peerId;workDate=$today;rowVersion=$shiftVersion.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA 403
$null=Call POST "/api/v2/workday/assignments/$($slot.fields.assignmentId)/start" @{rowVersion=$slot.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $peer 403
$working=(Call POST "/api/v2/workday/assignments/$($slot.fields.assignmentId)/start" @{rowVersion=$slot.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA).fields
$null=Call POST "/api/v2/workday/assignments/$($slot.fields.assignmentId)/end" @{rowVersion=$slot.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA 409
$ended=(Call POST "/api/v2/workday/assignments/$($slot.fields.assignmentId)/end" @{rowVersion=$working.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA).fields
$dashboard=Call GET "/api/v2/insights/personal?branchId=$a" $null $hkA
Check 'own shift start/end and dashboard reflect actual Workday; cannot schedule or start others' ($ended.status-eq 'ENDED' -and $dashboard.shifts[0].status-eq 'ENDED')
$future=[DateTime]::Parse($today).AddDays(1).ToString('yyyy-MM-dd')
$futureSlot=(Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$hkId;workDate=$future;rowVersion=$shiftVersion.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin).fields
$null=Call POST "/api/v2/workday/assignments/$($futureSlot.assignmentId)/start" @{rowVersion=$futureSlot.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $hkA 409
Check 'future shift cannot start early and upcoming shift is backed by actual assignment' $true
$inbox=Call GET "/api/v2/notifications?branchId=$a" $null $hkA
$notification=$inbox.items[0].fields
Check 'notifications are personal and branch scoped' (@($inbox.items).Count-gt 0 -and @($inbox.items|Where-Object {$_.fields.recipientUserId-ne $hkId -or $_.fields.branchId-ne $a}).Count-eq 0)
$null=Call POST "/api/v2/notifications/$($notification.notificationId)/read" @{} $peer 404
$read=(Call POST "/api/v2/notifications/$($notification.notificationId)/read" @{} $hkA).fields
Check 'only recipient can mark notification read' ($null-ne $read.readAt)
$null=Call GET "/api/v2/work?branchId=$a&kind=CLEANING&sort=invalid" $null $hkA 400
$null=Call GET "/api/v2/work?branchId=$a&kind=CLEANING&from=2026-12-01&to=2026-11-01" $null $hkA 400
Check 'invalid sort/date filters rejected rather than silently returning misleading results' $true
$roomState=@(Call GET "/api/v2/rooms?branchId=$a" $null $hkA)|Where-Object roomId -eq $room.roomId
$notEligible=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&roomId=$($room.roomId)" $null $head
Check 'inspected READY room is not offered as needing a new cleaning request' ($notEligible.total-eq 0)
$reserved=(Call POST "/api/v2/rooms/$($room.roomId)/actions/reserve" @{rowVersion=$roomState.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin).room.fields
$occupied=(Call POST "/api/v2/rooms/$($room.roomId)/actions/check-in" @{rowVersion=$reserved.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin).room.fields
$notEligible=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&roomId=$($room.roomId)" $null $head
Check 'occupied room is not offered to supervisor for checkout cleaning' ($notEligible.total-eq 0)
$departure=Call POST "/api/v2/rooms/$($room.roomId)/actions/check-out" @{rowVersion=$occupied.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
$nextRooms=Call GET "/api/v2/work/cleaning-rooms?branchId=$a&roomId=$($room.roomId)" $null $head
Check 'Reception checkout room appears in supervisor pending-room list' ($nextRooms.total-eq 1)
$null=Sql "UPDATE workday_assignments SET status='WORKING',ended_at=NULL WHERE assignment_id=$($slot.fields.assignmentId)"
$next=(Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Next checkout turnover';roomId=$room.roomId;roomRowVersion=$nextRooms.items[0].rowVersion;assigneeUserId=$hkId} $head).fields
Check 'Reception checkout creates next dirty cycle; supervisor can assign next cleaning without reusing old inspection' ($next.turnoverNo-gt $clean.turnoverNo -and $departure.readiness.fields.readiness-ne 'READY')
