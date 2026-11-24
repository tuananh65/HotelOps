# Reuses the real on-shift setup and submitted cleaning in the independent test database.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
function PaperBody($work,$result='PASS',$note=$null,$resolve=$false){return @{rowVersion=$work.rowVersion;operationKey=[Guid]::NewGuid().ToString();result=$result;note=$note;resolveFindings=$resolve}}
function Review($body,$token=$head,$expected=200){return Call POST "/api/v2/work/$($clean.workId)/inspection-result" $body $token $expected}
$null=Review (PaperBody $clean) $hk 403
$null=Review (PaperBody $clean) $headB 403
$stale=PaperBody $clean;$stale.rowVersion='AAAAAAAAAAA=';$null=Review $stale $head 409
$null=Review (PaperBody $clean 'FAIL') $head 400
Check 'paper result enforces scope permission version and failure notes' $true
$failBody=PaperBody $clean 'FAIL' 'Bathroom floor still needs cleaning'
$failed=Review $failBody
$clean=$failed.work.fields
Check 'paper FAIL creates immutable result and rework-required parent' ($clean.status-eq 'REWORK_REQUIRED' -and $failed.inspection.fields.result-eq 'FAIL')
$null=Review $failBody $head 409
$null=Review (PaperBody $clean) $head 409
Check 'duplicate review and review before rework denied' $true
$null=Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title='Fix bathroom floor';parentWorkId=$clean.workId;roomId=$room.roomId} $hk 403
$rework=(Call POST '/api/v2/work' @{branchId=$a;kind='REWORK';title='Fix bathroom floor';parentWorkId=$clean.workId;roomId=$room.roomId} $head).fields
$rework=(WorkAction $rework assign $head @{userId=$hkId;durationMinutes=30}).fields
$null=Review (PaperBody $clean) $head 409
$peer=(Call POST '/api/v2/auth/login' @{username='qa_hk_peer';password=$password}).accessToken
$rework=(WorkAction $rework 'handover-offer' $hk @{userId=$peerId}).fields
$offer=(Call GET "/api/v2/work/$($rework.workId)" $null $hk).events|Where-Object {$_.fields.eventType-eq 'HANDOVER_OFFERED'}|Select-Object -First 1
$null=WorkAction $rework accept $peer @{reason=$null} 403
$rework=(WorkAction $rework accept $hk @{reason=$null}).fields
$rework=(WorkAction $rework start $hk @{reason=$null}).fields
$null=WorkAction $rework progress $hk @{progress=50} 403
$null=WorkAction $rework 'handover-offer' $hk @{userId=$peerId} 409
$null=WorkAction $rework 'handover-accept' $peer @{relatedId=$offer.fields.workEventId} 409
$rework=(WorkAction $rework pause $hk).fields
$null=WorkAction $rework 'handover-offer' $hk @{userId=$peerId} 409
$rework=(WorkAction $rework resume $hk).fields
$staleRework=$rework
$rework=(WorkAction $rework complete $hk @{reason=$null}).fields
$null=WorkAction $staleRework complete $hk @{reason=$null} 409
$null=WorkAction $rework 'handover-offer' $hk @{userId=$peerId} 409
Check 'reasonless execution works; progress and handover after start paused or submitted rejected' ($rework.status-eq 'PENDING_REINSPECTION')
$null=Review (PaperBody $clean) $head 409
# A fixture principal with inspection/read permission but no defect-resolution permission.
$null=Sql "INSERT role_permissions(role_id,permission_id,data_scope) SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p WHERE r.code='QA_INSPECT_NO_DEFECT' AND p.code='CLEANING_VIEW'"
$noDefect=(Call POST '/api/v2/auth/login' @{username='qa_inspect_no_defect';password=$password}).accessToken
$null=Review (PaperBody $clean 'PASS' 'All previously recorded defects are fixed' $true) $noDefect 403
Check 'paper reinspection cannot close findings without explicit confirmation and permission' $true
$body=PaperBody $clean 'PASS' 'All previously recorded defects are fixed' $true
$passed=Review $body
$clean=$passed.work.fields
$null=Review $body $head 409
$state=(Sql "SELECT readiness FROM v_room_readiness WHERE room_id=$($room.roomId)").Rows[0].readiness
$facts=(Sql "SELECT (SELECT COUNT(*) FROM work_checklist_items WHERE work_id=$($passed.inspection.fields.workId)) checks,(SELECT COUNT(*) FROM work_intervals WHERE work_id=$($passed.inspection.fields.workId)) timers,(SELECT COUNT(*) FROM inspection_findings WHERE inspection_work_id=$($failed.inspection.fields.workId) AND status='RESOLVED') resolved").Rows[0]
Check 'paper PASS completes rework and cleaning with READY and no fabricated checklist or inspection timer' ($clean.status-eq 'COMPLETED' -and $state-eq 'READY' -and $facts.checks-eq 0 -and $facts.timers-eq 0 -and $facts.resolved-eq 1)
$history=Call GET "/api/v2/work/housekeeping-history?branchId=$a" $null $head
Check 'daily history uses completion includes cleaning and rework once each' ($history.total-eq 2 -and @($history.items|Select-Object -ExpandProperty workId -Unique).Count-eq 2)
$null=Call GET "/api/v2/work/housekeeping-history?branchId=$a" $null $headB 403
$foreign=Call GET "/api/v2/work/housekeeping-history?branchId=$b" $null $headB
Check 'daily history respects both branches' ($foreign.total-eq 0)
$yesterday=([datetime]$history.date).AddDays(-1).ToString('yyyy-MM-dd')
$empty=Call GET "/api/v2/work/housekeeping-history?branchId=$a&date=$yesterday" $null $head
Check 'other completion day empty even when created day differs' ($empty.total-eq 0)
# Boundary fixtures: 17:00 UTC is next day in the branch timezone. No business writes on primary.
$null=Sql "UPDATE work_items SET created_at='2026-10-01',actual_start_at=NULL,completed_at='2026-11-22T17:00:00' WHERE work_id=$($clean.workId)"
$boundary=Call GET "/api/v2/work/housekeeping-history?branchId=$a&date=2026-11-23&search=On-shift" $null $head
$before=Call GET "/api/v2/work/housekeeping-history?branchId=$a&date=2026-11-22&search=On-shift" $null $head
Check 'history uses local midnight inclusive and excludes prior day; not created date' ($boundary.total-eq 1 -and $before.total-eq 0)
# Historical performer cannot inspect, even if their active assignment ended.
$room2=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='102';roomTypeId=$type.roomTypeId;locationId=$floor.locationId;departmentId=$f.departmentId}} $admin).fields
$clean2=(Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';durationMinutes=30;title='Independent review';roomId=$room2.roomId;assigneeUserId=$hkId} $head).fields
$clean2=(WorkAction $clean2 accept $hk @{reason=$null}).fields
$clean2=(WorkAction $clean2 start $hk @{reason=$null}).fields
$clean2=(WorkAction $clean2 complete $hk @{reason=$null}).fields
$null=Sql "INSERT work_assignments(branch_id,work_id,assignee_user_id,assigned_by,assigned_at,assignment_status,ended_at) VALUES($a,$($clean2.workId),$($f.adminId),$($f.adminId),DATEADD(minute,-2,SYSUTCDATETIME()),'REASSIGNED',DATEADD(minute,-1,SYSUTCDATETIME()))"
$null=Call POST "/api/v2/work/$($clean2.workId)/inspection-result" (PaperBody $clean2) $admin 403
Check 'historical performer cannot self-inspect with global permissions' $true
$guard=(Sql @"
BEGIN TRANSACTION;
BEGIN TRY
 INSERT work_items(branch_id,kind,status,work_no,title,room_id,parent_work_id,turnover_no,created_by)
 SELECT branch_id,'INSPECTION','ASSIGNED',CONVERT(varchar(36),NEWID()),N'No physical evidence',room_id,work_id,turnover_no,$headId FROM work_items WHERE work_id=$($clean2.workId);
 DECLARE @i bigint=SCOPE_IDENTITY();
 UPDATE work_items SET status='PASSED',result='PASS',verified_by=$headId,verified_at=SYSUTCDATETIME() WHERE work_id=@i;
 ROLLBACK; SELECT 0 blocked;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 SELECT CASE WHEN ERROR_NUMBER()=51212 THEN 1 ELSE 0 END blocked;
END CATCH;
"@).Rows[0].blocked
Check 'database still rejects PASS without digital checks or physical inspection evidence' ($guard-eq 1)
# Reuse an existing inspection without losing its version, assignment or genuine time history.
$legacyId=(Sql "INSERT work_items(branch_id,kind,status,work_no,title,room_id,parent_work_id,turnover_no,created_by) SELECT branch_id,'INSPECTION','ASSIGNED',CONVERT(varchar(36),NEWID()),N'Existing inspection',room_id,work_id,turnover_no,$headId FROM work_items WHERE work_id=$($clean2.workId); SELECT CONVERT(bigint,SCOPE_IDENTITY()) id").Rows[0].id
$legacy=(Call GET "/api/v2/work/$legacyId" $null $head).work.fields
$legacy=(WorkAction $legacy assign $head @{userId=$headId}).fields
$legacy=(WorkAction $legacy accept $head @{reason=$null}).fields
$legacy=(WorkAction $legacy start $head @{reason=$null}).fields
$null=Call POST "/api/v2/work/$($clean2.workId)/inspection-result" (PaperBody $clean2) $head 409
$legacyBody=PaperBody $clean2;$legacyBody.inspectionRowVersion='AAAAAAAAAAA='
$null=Call POST "/api/v2/work/$($clean2.workId)/inspection-result" $legacyBody $head 409
$legacyBody.inspectionRowVersion=$legacy.rowVersion
$null=Call POST "/api/v2/work/$($clean2.workId)/inspection-result" $legacyBody $noDefect 403
Check 'existing inspection requires its version and assigned inspector' $true
# Active block prevents room READY even when cleaning passed.
$null=Sql "INSERT room_blocks(branch_id,room_id,reason,blocked_at,blocked_by) VALUES($a,$($room2.roomId),N'Fixture maintenance block',SYSUTCDATETIME(),$($f.adminId))"
$passed2=Call POST "/api/v2/work/$($clean2.workId)/inspection-result" $legacyBody $head
$blocked=(Sql "SELECT r.operational_status,v.readiness FROM rooms r JOIN v_room_readiness v ON v.room_id=r.room_id WHERE r.room_id=$($room2.roomId)").Rows[0]
Check 'passing physical inspection never releases room block' ($passed2.work.fields.status-eq 'COMPLETED' -and $blocked.readiness-eq 'BLOCKED' -and $blocked.operational_status-ne 'READY')
Check 'existing inspection reused and time closed rather than duplicate result' ($passed2.inspection.fields.workId-eq $legacyId -and (Sql "SELECT COUNT(*) n FROM work_intervals WHERE work_id=$legacyId AND closed_at IS NULL").Rows[0].n-eq 0)
