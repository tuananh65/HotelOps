# Runs after the full HK Staff workflow on the same fresh isolated fixture only.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$headB=(Call POST '/api/v2/auth/login' @{username='qa_hks_b';password=$password}).accessToken
$me=Call GET '/api/v2/auth/me' $null $head
Check 'HKS baseline is exactly eleven grants with attendance OWN' (@($me.grants).Count-eq 11 -and @($me.grants|Where-Object {$_.code-eq 'ATTENDANCE_RECORD' -and $_.dataScope-eq 'OWN'}).Count-eq 1)
foreach($entry in @(@{token=$head;own=$a;foreign=$b},@{token=$headB;own=$b;foreign=$a})){
 foreach($path in @('insights/housekeeping','work/housekeepers','work/inspection-queue','work/defects','work/cleaning-rooms','rooms','templates','workday')){
  $null=Call GET "/api/v2/$path`?branchId=$($entry.own)&date=$today" $null $entry.token
  $null=Call GET "/api/v2/$path`?branchId=$($entry.foreign)&date=$today" $null $entry.token 403
 }
}
Check 'both HKS branches enforce all new read endpoints and existing Workday/template scope' $true
$null=Call GET "/api/v2/rooms/$($room.roomId)/workflow" $null $headB 403
$flow=Call GET "/api/v2/rooms/$($room.roomId)/workflow" $null $head
Check 'room workflow returns current turnover and excludes previous inspection cycle' ($flow.turnoverNo-eq $next.turnoverNo -and @($flow.cleaning).Count-eq 1 -and @($flow.inspections).Count-eq 0)
$staff=Call GET "/api/v2/work/housekeepers?branchId=$a&date=$today" $null $head
$person=@($staff.items|Where-Object userId -eq $hkId)[0]
Check 'staff candidates and actual shifts use assignment API without User Management' ($person.open-eq 1 -and $person.completed-ge 2 -and $person.shifts[0].status-eq 'ENDED' -and @($staff.items|Where-Object userId -eq $foreignId).Count-eq 0)
$dashboard=Call GET "/api/v2/insights/housekeeping?branchId=$a" $null $head
Check 'HK dashboard uses actual current cleaning and unresolved room incident counts' ($dashboard.metrics.unassigned-eq 0 -and $dashboard.metrics.incidents-eq 1 -and @($dashboard.workload).Count-eq 1)
$detailTemplate=Call GET "/api/v2/templates/checklists/$($quality.checklistTemplateId)" $null $head
$null=Call GET "/api/v2/templates/checklists/$($quality.checklistTemplateId)" $null $headB 403
$version=(Call POST '/api/v2/templates/checklists' @{branchId=$a;code=$quality.code;name='Inspection version two';kind='INSPECTION';items=@(@{description='New requirement';isRequired=$true;weight=2})} $head).fields
$old=Call GET "/api/v2/work/$($review.workId)" $null $head
$null=Call POST '/api/v2/templates/checklists' @{branchId=$a;code=$quality.code;name='Invalid kind change';kind='CLEANING';items=@(@{description='Invalid';isRequired=$true;weight=1})} $head 409
Check 'checklist versions preserve published template and historical inspection snapshots' ($version.versionNo-eq 2 -and $detailTemplate.items[0].fields.description-eq 'Surfaces inspected' -and $old.checklist[0].fields.descriptionSnapshot-eq 'Surfaces inspected')
foreach($verb in @('check-in','check-out','move','block','unblock')){$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $head 403}
foreach($path in @('/api/v2/users',"/api/v2/catalog/roles?branchId=$a","/api/v2/insights/maintenance?branchId=$a")){$null=Call GET $path $null $head 403}
$null=Call POST '/api/v2/users' @{employeeCode='HKS_FORBIDDEN';username='hks_forbidden';fullName='Denied';password=$password} $head 403
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='ISSUE'} $head 403
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$peerId;workDate=$today;rowVersion=$shiftVersion.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $head 403
$maintenance=(Call POST '/api/v2/work' @{branchId=$a;kind='MAINTENANCE';title='Technical review denied'} $admin).fields
$null=WorkAction $maintenance verify $head @{} 403
Check 'HKS cannot mutate occupancy/block, users/roles, stock, weekly schedules or maintenance review' $true
$null=WorkAction $next accept $head @{} 403
$null=WorkAction $next start $head @{} 403
Check 'supervisor assignment authority does not grant cleaning execution' $true
# Complete the second cycle and verify block precedence at inspection pass.
$clean=$next
foreach($verb in @('accept','start','complete')){$clean=(WorkAction $clean $verb $hkA).fields}
$inspection=NewInspection 'Second cycle first inspection'
$finding=(Call POST "/api/v2/work/$($inspection.workId)/findings" @{rowVersion=$inspection.rowVersion;description='First correction in second cycle'} $head).fields
$inspection=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).work.fields
$item=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).checklist[0].fields
$null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='FAIL'} $head
$inspection=(WorkAction $inspection complete $head).fields
$inspection=(WorkAction $inspection fail $head).fields
function SubmitCorrection($title){
 $r=(Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title=$title;roomId=$room.roomId;parentWorkId=$clean.workId} $head).fields
 $null=Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title='Concurrent duplicate';roomId=$room.roomId;parentWorkId=$clean.workId} $head 409
 $r=(WorkAction $r assign $head @{userId=$hkId}).fields
 foreach($verb in @('accept','start','complete')){$r=(WorkAction $r $verb $hkA).fields}
 return $r
}
$correction=SubmitCorrection 'First correction'
$null=Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title='Premature extra correction';roomId=$room.roomId;parentWorkId=$clean.workId} $head 409
$null=Call POST "/api/v2/work/$($inspection.workId)/findings/$($finding.findingId)/resolve" @{rowVersion=$finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();relatedId=$correction.workId;reason='First correction verified independently'} $head
$inspection=NewInspection 'Reinspection failed again'
$finding=(Call POST "/api/v2/work/$($inspection.workId)/findings" @{rowVersion=$inspection.rowVersion;description='Second correction after failed reinspection'} $head).fields
$inspection=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).work.fields
$item=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).checklist[0].fields
$null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='FAIL'} $head
$inspection=(WorkAction $inspection complete $head).fields
$inspection=(WorkAction $inspection fail $head).fields
$waiting=Call GET "/api/v2/work/inspection-queue?branchId=$a" $null $head
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title='Bypass second correction';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$quality.checklistTemplateId} $head 409
Check 'failed reinspection removes outdated rework submission from inspection intake' ($waiting.total-eq 0)
$correction=SubmitCorrection 'Second correction'
$null=Call POST "/api/v2/work/$($inspection.workId)/findings/$($finding.findingId)/resolve" @{rowVersion=$finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();relatedId=$correction.workId;reason='Second correction verified independently'} $head
Check 'failed reinspection admits next correction, prevents parallel duplicates and preserves prior reviews' ((Call GET "/api/v2/work/$($clean.workId)" $null $head).reviews.Count-eq 2)
$inspection=NewInspection 'Blocked room inspection'
$item=(Call GET "/api/v2/work/$($inspection.workId)" $null $head).checklist[0].fields
$null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $head
$inspection=(WorkAction $inspection complete $head).fields
$currentRoom=@(Call GET "/api/v2/rooms?branchId=$a" $null $head)|Where-Object roomId -eq $room.roomId
$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/block" @{rowVersion=$currentRoom.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Independent active room block'} $admin
$inspection=(WorkAction $inspection pass $head).fields
$currentRoom=@(Call GET "/api/v2/rooms?branchId=$a" $null $head)|Where-Object roomId -eq $room.roomId
$null=WorkAction $inspection pass $head @{} 403
Check 'inspection may pass while effective operational block still prevents room READY; duplicate pass rejected' ($inspection.status-eq 'PASSED' -and $currentRoom.readiness-eq 'BLOCKED' -and $currentRoom.isBlocked)
Check 'room DTO keeps cleaning and inspection distinct from block readiness and occupancy' ($currentRoom.cleaningStatus-eq 'COMPLETED' -and $currentRoom.inspectionStatus-eq 'PASSED' -and -not $currentRoom.isOccupied)
$inbox=Call GET "/api/v2/notifications?branchId=$a" $null $head
Check 'supervisor notification endpoint only returns recipient-owned items' (@($inbox.items|Where-Object {$_.fields.recipientUserId-ne $headId}).Count-eq 0)
