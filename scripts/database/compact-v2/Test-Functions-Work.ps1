function TestRoom($code){return (Call POST "/api/v2/catalog/rooms?branchId=$($f.branchId)" @{fields=@{code=$code;roomTypeId=$type.roomTypeId;locationId=$location.locationId;departmentId=$f.departmentId}} $admin).fields}
function CleanRoom($testRoom){
    $w=NewWork CLEANING @{roomId=$testRoom.roomId}
    $w=(WorkAction $w assign $admin @{userId=$f.workerId}).fields
    foreach($a in @('accept','start','complete')){$w=(WorkAction $w $a $worker).fields};return $w
}
function InspectRoom($testRoom,$cleanWork,$pass=$true){
    $w=NewWork INSPECTION @{roomId=$testRoom.roomId;parentWorkId=$cleanWork.workId;checklistTemplateId=$checklist.checklistTemplateId}
    $w=(WorkAction $w assign $admin @{userId=$f.adminId}).fields
    foreach($a in @('accept','start')){$w=(WorkAction $w $a $admin).fields}
    $ci=(Call GET "/api/v2/work/$($w.workId)" $null $admin).checklist[0].fields
    $outcome='PASS';if(-not $pass){$outcome='FAIL'}
    $null=Call PUT "/api/v2/work/$($w.workId)/checklist/$($ci.workChecklistItemId)" @{rowVersion=$ci.rowVersion;outcome=$outcome} $admin
    $finding=$null
    if(-not $pass){
        $finding=(Call POST "/api/v2/work/$($w.workId)/findings" @{rowVersion=$w.rowVersion;description='Functional inspection defect';workChecklistItemId=$ci.workChecklistItemId} $admin).fields
        $w=(Call GET "/api/v2/work/$($w.workId)" $null $admin).work.fields
    }
    $w=(WorkAction $w complete $admin).fields
    $action='pass';if(-not $pass){$action='fail'}
    $w=(WorkAction $w $action $admin).fields
    return @{work=$w;finding=$finding;check=$ci}
}
Feature 'FE-04 FE-05 failed inspection rework and reinspection' {
    $testRoom=TestRoom 'F-REWORK';$cleanWork=CleanRoom $testRoom
    $failed=InspectRoom $testRoom $cleanWork $false
    $parent=(Call GET "/api/v2/work/$($cleanWork.workId)" $null $admin).work.fields
    Check 'FE-05 failed inspection creates an unresolved defect and rework required state' ($parent.status-eq 'REWORK_REQUIRED' -and $failed.finding.status-eq 'OPEN')
    $rw=NewWork REWORK @{roomId=$testRoom.roomId;parentWorkId=$cleanWork.workId}
    $rw=(WorkAction $rw assign $admin @{userId=$f.workerId}).fields
    foreach($a in @('accept','start','complete')){$rw=(WorkAction $rw $a $worker).fields}
    Check 'FE-04 completed rework waits for independent reinspection' ($rw.status-eq 'PENDING_REINSPECTION')
    $resolved=(Call POST "/api/v2/work/$($failed.work.workId)/findings/$($failed.finding.findingId)/resolve" @{rowVersion=$failed.finding.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Verified correction after rework';relatedId=$rw.workId} $admin).fields
    Check 'FE-05 independent inspector resolves defect against submitted rework' ($resolved.status-eq 'RESOLVED')
    $passed=InspectRoom $testRoom $cleanWork
    $rwNow=(Call GET "/api/v2/work/$($rw.workId)" $null $admin).work.fields
    $ready=(Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $admin)|Where-Object roomId -eq $testRoom.roomId
    Check 'FE-05 reinspection pass closes rework and makes room ready' ($rwNow.status-eq 'COMPLETED' -and $ready.readiness-eq 'READY')
    $null=Call POST "/api/v2/work/$($passed.work.workId)/findings" @{rowVersion=$passed.work.rowVersion;description='Attempt to alter final result'} $admin 409
    Check 'FE-05 passed inspection rejects new findings' $true
}
Feature 'FE-03 occupancy move block and unblock' {
    $sourceRoom=TestRoom 'F-MOVE-A';$targetRoom=TestRoom 'F-MOVE-B'
    foreach($r in @($sourceRoom,$targetRoom)){$cleanWork=CleanRoom $r;$null=InspectRoom $r $cleanWork}
    $rows=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $admin
    $sourceRoom=$rows|Where-Object roomId -eq $sourceRoom.roomId;$targetRoom=$rows|Where-Object roomId -eq $targetRoom.roomId
    $blocked=Call POST "/api/v2/rooms/$($targetRoom.roomId)/actions/block" @{rowVersion=$targetRoom.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Block ready room for functional test'} $admin
    $reply=Call POST "/api/v2/rooms/$($targetRoom.roomId)/actions/check-in" @{rowVersion=$blocked.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin 409
    Check 'FE-03 effective room block prevents arrival' ($reply.title-eq 'ROOM_NOT_READY')
    $unblocked=Call POST "/api/v2/rooms/$($targetRoom.roomId)/actions/unblock" @{rowVersion=$blocked.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Release block after functional check'} $admin
    Check 'FE-03 removing standalone block restores derived readiness' ($unblocked.readiness.fields.readiness-eq 'READY')
    $arrival=Call POST "/api/v2/rooms/$($sourceRoom.roomId)/actions/check-in" @{rowVersion=$sourceRoom.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
    $move=Call POST "/api/v2/rooms/$($sourceRoom.roomId)/actions/move" @{rowVersion=$arrival.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString();targetRoomId=$targetRoom.roomId;targetRowVersion=$unblocked.room.fields.rowVersion;reason='Move occupant to another ready room'} $admin
    $rows=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $admin
    $sourceNow=$rows|Where-Object roomId -eq $sourceRoom.roomId;$targetNow=$rows|Where-Object roomId -eq $targetRoom.roomId
    Check 'FE-03 occupancy move atomically dirties source and occupies target' ($sourceNow.operationalStatus-eq 'DIRTY' -and $sourceNow.turnoverNo-eq 1 -and $targetNow.operationalStatus-eq 'OCCUPIED')
    $history=Call GET "/api/v2/rooms/$($targetRoom.roomId)/history" $null $admin
    Check 'FE-03 room history contains inbound move and retained block history' (@($history.events|Where-Object {$_.fields.eventType-eq 'OCCUPANCY_MOVE_IN'}).Count-eq 1 -and $history.blocks.Count-eq 1)
}
Feature 'FE-11 FE-12 execution guard progress timer and handover rejection' {
    $w=NewWork;$other=NewWork
    $w=(WorkAction $w assign $admin @{userId=$f.workerId}).fields
    $reply=WorkAction $w start $worker @{} 409
    Check 'FE-11 assigned work requires acceptance before execution' ($reply.title-in @('INVALID_WORK_TRANSITION','ACCEPT_ASSIGNMENT_FIRST'))
    $w=(WorkAction $w accept $worker).fields;$w=(WorkAction $w start $worker).fields
    $other=(WorkAction $other assign $admin @{userId=$f.workerId}).fields;$other=(WorkAction $other accept $worker).fields
    $null=WorkAction $other start $worker @{} 409
    Check 'FE-12 one open timer per worker across different tasks' ((Sql "SELECT COUNT(*) n FROM work_intervals WHERE user_id=$($f.workerId) AND closed_at IS NULL").Rows[0].n-eq 1)
    $null=WorkAction $w progress $admin @{progress=20} 403
    $null=WorkAction $w progress $worker @{progress=101} 400
    $key=[Guid]::NewGuid().ToString()
    $w=(Call POST "/api/v2/work/$($w.workId)/actions/progress" @{rowVersion=$w.rowVersion;operationKey=$key;progress=40} $worker).fields
    $reply=Call POST "/api/v2/work/$($w.workId)/actions/progress" @{rowVersion=$w.rowVersion;operationKey=$key;progress=40} $worker 409
    Check 'FE-11 progress checks assignee range and operation idempotency' ($w.progressPercent-eq 40 -and $reply.title-eq 'OPERATION_ALREADY_APPLIED')
    $w=(WorkAction $w handover-offer $worker @{userId=$f.adminId}).fields
    $offer=(Call GET "/api/v2/work/$($w.workId)" $null $admin).events|Where-Object {$_.fields.eventType-eq 'HANDOVER_OFFERED'}|Select-Object -First 1
    $null=WorkAction $w handover-accept $worker @{relatedId=$offer.fields.workEventId} 403
    $w=(WorkAction $w handover-reject $admin @{relatedId=$offer.fields.workEventId}).fields
    $detail=Call GET "/api/v2/work/$($w.workId)" $null $admin
    $active=$detail.assignments|Where-Object {$_.fields.assignmentStatus-eq 'ACTIVE'}
    Check 'FE-11 rejected handover keeps original assignee and running timer' ($active.fields.assigneeUserId-eq $f.workerId -and $w.status-eq 'IN_PROGRESS')
    $null=WorkAction $w handover-accept $admin @{relatedId=$offer.fields.workEventId} 409
    Check 'FE-11 handover decision cannot be acknowledged twice' $true
    $w=(WorkAction $w cancel $admin).fields;$other=(WorkAction $other cancel $admin).fields
    Check 'FE-12 cancellation closes running timer' ((Sql "SELECT COUNT(*) n FROM work_intervals WHERE work_id=$($w.workId) AND closed_at IS NULL").Rows[0].n-eq 0)
    $candidates=Call GET "/api/v2/work/candidates?branchId=$($f.branchId)&kind=OPERATIONAL" $null $admin
    Check 'FE-11 candidates include eligible branch worker' (@($candidates|Where-Object userId -eq $f.workerId).Count-eq 1)
}
Feature 'FE-13 dependencies and reject internal request' {
    $a=NewWork;$b=NewWork
    $null=Call POST "/api/v2/work/$($a.workId)/links" @{rowVersion=$a.rowVersion;targetWorkId=$b.workId;relation='DEPENDS_ON'} $admin
    $reply=Call POST "/api/v2/work/$($b.workId)/links" @{rowVersion=$b.rowVersion;targetWorkId=$a.workId;relation='DEPENDS_ON'} $admin 400
    Check 'FE-13 circular work dependency rejected' ($reply.title-eq 'DEPENDENCY_CYCLE')
    $ir=NewWork INTERNAL_REQUEST @{requesterDepartmentId=$f.departmentId}
    $ir=(WorkAction $ir submit $admin).fields
    $null=WorkAction $ir reject $worker @{} 409
    Check 'FE-13 request must be routed before receiving department rejects it' $true
    $ir=(WorkAction $ir route $admin @{relatedId=$f.departmentId}).fields
    $ir=(WorkAction $ir reject $worker).fields
    Check 'FE-13 authorized receiving department can reject request' ($ir.status-eq 'REJECTED')
    $reply=WorkAction $ir start $worker @{} 403
    Check 'FE-13 unassigned handler cannot start a rejected request' ($reply.title-eq 'CURRENT_ASSIGNEE_REQUIRED')
    Check 'FE-13 rejected request cannot start execution' $true
}
Feature 'FE-06 incident triage escalation and linked maintenance closure' {
    # Fixed production references are immutable through the API. Seed only the isolated fixture.
    $priorityRow=(Sql "IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated database required',1; INSERT reference_values(kind,code,name) VALUES('PRIORITY','FUNCTIONAL_HIGH',N'High'); SELECT CONVERT(bigint,SCOPE_IDENTITY()) reference_id;").Rows[0]
    $priority=[pscustomobject]@{referenceId=$priorityRow.reference_id}
    $i=NewWork INCIDENT
    $null=WorkAction $i triage $admin @{priorityId=$priority.referenceId;dueAt=[DateTime]::UtcNow.AddDays(-1).ToString('o')} 400
    $i=(WorkAction $i triage $admin @{priorityId=$priority.referenceId;dueAt=[DateTime]::UtcNow.AddDays(1).ToString('o')}).fields
    $i=(WorkAction $i escalate $admin).fields
    Check 'FE-06 triage sets future deadline and escalation records event' ($i.priorityId-eq $priority.referenceId -and @((Call GET "/api/v2/work/$($i.workId)" $null $admin).events|Where-Object {$_.fields.eventType-eq 'ESCALATE'}).Count-eq 1)
    $m=NewWork MAINTENANCE @{parentWorkId=$i.workId}
    $i=(WorkAction $i assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$i=(WorkAction $i $action $worker).fields}
    $reply=WorkAction $i verify $admin @{} 409
    Check 'FE-06 incident cannot close before linked maintenance is verified' ($reply.title-eq 'MAINTENANCE_NOT_VERIFIED')
    $m=(WorkAction $m assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$m=(WorkAction $m $action $worker).fields}
    $m=(WorkAction $m verify $admin).fields;$i=(WorkAction $i verify $admin).fields
    Check 'FE-06 independent maintenance verification unlocks incident closure' ($m.status-eq 'CLOSED' -and $i.status-eq 'CLOSED')
    $i=(WorkAction $i reopen $admin).fields
    Check 'FE-06 reopen clears previous verification' ($i.status-eq 'OPEN' -and $null-eq $i.verifiedAt)
}
