param([string]$Server='.\SQLEXPRESS',[int]$Port=5187,[switch]$Extended,[switch]$DepartmentHead,[switch]$OperationsManager,[switch]$HousekeepingStaff,[switch]$HousekeepingSupervisor,[switch]$Storekeeper,[switch]$MaintenanceLead,[switch]$MaintenanceStaff,[switch]$Owner,[switch]$UxRefinements,[switch]$CleaningOnShift,[switch]$ReservationShifts,[switch]$WorkdayLimits,[switch]$AttendanceSummary,[switch]$PaperInspection,[switch]$ReceptionAccess,[switch]$CleaningDeadline,[switch]$RoomRemediation,[switch]$AdminCatalog,[switch]$InternalSettings)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$database='HotelOpsCompactApiTest'+[Guid]::NewGuid().ToString('N').Substring(0,12)
$artifacts=Join-Path $root 'artifacts/compact-v2/backend'
if($UxRefinements){$artifacts=Join-Path $artifacts ('ux-'+$database)}
[void][IO.Directory]::CreateDirectory($artifacts)
$password='Test!'+[Guid]::NewGuid().ToString('N')
$api=$null;$connection=$null;$results=New-Object 'System.Collections.Generic.List[object]'
$envNames=@('ConnectionStrings__HotelOpsDb','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','Compact__WorkerEnabled','Compact__AutoEndShiftsEnabled','Compact__Bootstrap__Username','Compact__Bootstrap__Password','Compact__EvidenceDirectory')
$saved=@{};foreach($name in $envNames){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
function Check($name,$condition){if(-not $condition){throw "FAIL: $name"};$results.Add(@{name=$name;passed=$true});Write-Output "PASS: $name"}
function Sql($query){$cmd=$connection.CreateCommand();$cmd.CommandText=$query;$cmd.CommandTimeout=60;$table=New-Object Data.DataTable;$reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Dispose();return ,$table}
function Call($method,$path,$body=$null,$token=$null,$expected=200){
    $headers=@{};if($token){$headers.Authorization='Bearer '+$token}
    $args=@{Uri="http://127.0.0.1:$Port$path";Method=$method;Headers=$headers;UseBasicParsing=$true}
    if($null-ne $body){$args.Body=ConvertTo-Json -InputObject $body -Depth 20 -Compress;$args.ContentType='application/json'}
    try{$response=Invoke-WebRequest @args;$status=[int]$response.StatusCode;$content=$response.Content}
    catch{if(-not $_.Exception.Response){throw};$status=[int]$_.Exception.Response.StatusCode;$reader=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream());$content=$reader.ReadToEnd();$reader.Dispose()}
    if($status-ne $expected){throw "$method $path expected $expected, got $status : $content"}
    if($content){return ($content|ConvertFrom-Json)}
}
function WorkAction($work,$action,$token,$extra=@{},$expected=200){$body=@{rowVersion=$work.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Integration verification reason'};if($action-eq 'assign' -and $work.kind-in @('CLEANING','REWORK')){$body.durationMinutes=30};foreach($key in $extra.Keys){$body[$key]=$extra[$key]};return (Call POST "/api/v2/work/$($work.workId)/actions/$action" $body $token $expected)}
function DocAction($doc,$action,$token,$expected=200){return (Call POST "/api/v2/documents/$($doc.documentId)/actions/$action" @{rowVersion=$doc.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Integration verification reason'} $token $expected)}
try{
    if([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $Port){throw "Port $Port is already in use."}
    & (Join-Path $PSScriptRoot 'Install-Level2.ps1') -Server $Server -Database $database
    & (Join-Path $PSScriptRoot 'Upgrade-Workday.ps1') -Server $Server -Database $database
    if($PaperInspection -or $CleaningDeadline -or $RoomRemediation){& (Join-Path $PSScriptRoot 'Enable-PaperInspection.ps1') -Server $Server -Database $database}
    if($RoomRemediation){& (Join-Path $PSScriptRoot 'Enable-RoomRemediation.ps1') -Server $Server -Database $database}
    if($ReceptionAccess -or $ReservationShifts -or $HousekeepingStaff -or $HousekeepingSupervisor){& (Join-Path $PSScriptRoot 'Enable-RoomReservations.ps1') -Server $Server -Database $database}
    $env:Compact__AutoEndShiftsEnabled='false'
    if($UxRefinements -or $WorkdayLimits -or $AttendanceSummary){& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database}
    $env:ConnectionStrings__HotelOpsDb="Server=$Server;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS="http://127.0.0.1:$Port";$env:Compact__WorkerEnabled='false'
    $env:Compact__EvidenceDirectory=Join-Path $artifacts $database
    $env:Compact__Bootstrap__Username='qa_admin';$env:Compact__Bootstrap__Password=$password
    $dll=Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll'
    $bootstrap=Start-Process dotnet -ArgumentList @($dll,'--bootstrap-admin') -WorkingDirectory $root -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $artifacts 'bootstrap.log') -RedirectStandardError (Join-Path $artifacts 'bootstrap-error.log')
    if($bootstrap.ExitCode-ne 0){throw 'Bootstrap failed; inspect bootstrap-error.log'}
    $env:Compact__Bootstrap__Password=$null
    $connection=New-Object System.Data.SqlClient.SqlConnection $env:ConnectionStrings__HotelOpsDb;$connection.Open()
    # Synthetic identities and grants exist only in this newly-created isolated test database.
    $fixture=Sql @'
SET NOCOUNT ON;
DECLARE @a bigint=(SELECT user_id FROM users WHERE username='qa_admin'),@b bigint,@branch bigint,@other bigint,@dept bigint,@role bigint,@reception bigint,@head bigint,@store bigint;
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status) SELECT 'QA_WORKER','qa_worker',N'QA Worker',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=@a; SET @b=SCOPE_IDENTITY();
INSERT branches(code,name) VALUES('QA',N'Test branch');SET @branch=SCOPE_IDENTITY();
INSERT branches(code,name) VALUES('OTHER',N'Other branch');SET @other=SCOPE_IDENTITY();
INSERT departments(branch_id,code,name) VALUES(@branch,'HK',N'Housekeeping');SET @dept=SCOPE_IDENTITY();
INSERT memberships(user_id,branch_id,kind,recorded_by) VALUES(@a,@branch,'BRANCH_ACCESS',@a),(@b,@branch,'BRANCH_ACCESS',@a);
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by) VALUES(@a,@branch,@dept,'EMPLOYMENT',@a),(@b,@branch,@dept,'EMPLOYMENT',@a),(@a,@branch,@dept,'DEPARTMENT_AUTHORITY',@a),(@b,@branch,@dept,'DEPARTMENT_AUTHORITY',@a);
INSERT roles(code,name) VALUES('QA_ALL',N'Synthetic test role');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'BRANCH' FROM permissions;
INSERT user_roles(user_id,role_id,branch_id,assigned_by) VALUES(@a,@role,@branch,@a),(@b,@role,@branch,@a);
INSERT roles(code,name) VALUES('QA_GLOBAL',N'Synthetic test catalog administrator');
DECLARE @global bigint=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @global,permission_id,'ALL' FROM permissions;
INSERT user_roles(user_id,role_id,assigned_by) VALUES(@a,@global,@a);
INSERT roles(code,name) VALUES('RECEPTION',N'Reception');SET @reception=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @reception,permission_id,'BRANCH' FROM permissions WHERE code IN ('ROOM_VIEW','ROOM_CHECKIN','ROOM_CHECKOUT','ROOM_OCCUPANCY_MOVE');
INSERT roles(code,name) VALUES('DEPT_MANAGER',N'Department Head');SET @head=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @head,permission_id,'BRANCH' FROM permissions WHERE code IN ('MR_APPROVE_L1','MR_APPROVE_FALLBACK');
INSERT roles(code,name) VALUES('STOREKEEPER',N'Storekeeper');SET @store=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @store,permission_id,'BRANCH' FROM permissions WHERE code IN ('STOCK_ISSUE_POST','MR_FULFILL');
INSERT user_roles(user_id,role_id,branch_id,assigned_by) VALUES(@a,@reception,@branch,@a),(@a,@head,@branch,@a),(@b,@head,@branch,@a),(@a,@store,@branch,@a),(@b,@store,@branch,@a);
SELECT @a AS adminId,@b AS workerId,@branch AS branchId,@other AS otherBranchId,@dept AS departmentId;
'@
    $f=$fixture.Rows[0]
    $api=Start-Process dotnet -ArgumentList @($dll) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'api.log') -RedirectStandardError (Join-Path $artifacts 'api-error.log')
    $ready=$false;for($i=0;$i-lt 80;$i++){try{$health=Call GET '/health';$ready=$true;break}catch{if($api.HasExited){throw 'API exited; inspect api-error.log'};Start-Sleep -Milliseconds 250}}
    Check 'startup schema v2.1 gate' ($ready-and $health.tables-eq 40)
    $null=Call GET "/api/v2/work?branchId=$($f.branchId)&kind=OPERATIONAL" $null $null 401
    Check 'anonymous requests denied' $true
    $admin=(Call POST '/api/v2/auth/login' @{username='qa_admin';password=$password}).accessToken
    $worker=(Call POST '/api/v2/auth/login' @{username='qa_worker';password=$password}).accessToken
    if($InternalSettings){
        . (Join-Path $PSScriptRoot 'test-internal-settings.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'internal-settings-verification.json') -Encoding UTF8
        return
    }
    if($AdminCatalog){
        . (Join-Path $PSScriptRoot 'test-admin-catalog.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'admin-catalog-verification.json') -Encoding UTF8
        return
    }
    if($ReceptionAccess){
        . (Join-Path $PSScriptRoot 'test-reception-access.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'reception-access-verification.json') -Encoding UTF8
        return
    }
    if($PaperInspection -or $CleaningDeadline -or $RoomRemediation){
        . (Join-Path $PSScriptRoot 'test-cleaning-on-shift.ps1')
        . (Join-Path $PSScriptRoot 'test-paper-inspection.ps1')
        if($RoomRemediation){. (Join-Path $PSScriptRoot 'test-room-remediation.ps1')}
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'paper-inspection-verification.json') -Encoding UTF8
        return
    }
    if($AttendanceSummary){
        . (Join-Path $PSScriptRoot 'test-attendance-summary.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'attendance-summary-verification.json') -Encoding UTF8
        return
    }
    if($WorkdayLimits){
        . (Join-Path $PSScriptRoot 'test-workday-limits.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'workday-limits-verification.json') -Encoding UTF8
        return
    }
    if($ReservationShifts){
        . (Join-Path $PSScriptRoot 'test-reservation-shifts.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'reservation-shifts-verification.json') -Encoding UTF8
        return
    }
    if($CleaningOnShift){
        . (Join-Path $PSScriptRoot 'test-cleaning-on-shift.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'cleaning-on-shift-verification.json') -Encoding UTF8
        return
    }
    if($UxRefinements){
        . (Join-Path $PSScriptRoot 'test-ux-refinements.ps1')
        return
    }
    if($Owner){
        & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database
        . (Join-Path $PSScriptRoot 'test-owner.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'owner-verification.json') -Encoding UTF8
        return
    }
    if($MaintenanceStaff){
        & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database
        . (Join-Path $PSScriptRoot 'test-maintenance-staff.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'maintenance-staff-verification.json') -Encoding UTF8
        return
    }
    if($MaintenanceLead){
        & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database
        . (Join-Path $PSScriptRoot 'test-maintenance-lead.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'maintenance-lead-verification.json') -Encoding UTF8
        return
    }
    if($Storekeeper){
        & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database
        . (Join-Path $PSScriptRoot 'test-storekeeper.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'storekeeper-verification.json') -Encoding UTF8
        return
    }
    if($HousekeepingStaff -or $HousekeepingSupervisor){
        & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Server $Server -Database $database
        . (Join-Path $PSScriptRoot 'test-housekeeping-staff.ps1')
        if($HousekeepingSupervisor){
            . (Join-Path $PSScriptRoot 'test-housekeeping-supervisor.ps1')
            @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'housekeeping-supervisor-verification.json') -Encoding UTF8
        }
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'housekeeping-staff-verification.json') -Encoding UTF8
        return
    }
    if($OperationsManager){
        . (Join-Path $PSScriptRoot 'test-operations-manager.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'operations-manager-verification.json') -Encoding UTF8
        return
    }
    if($DepartmentHead){
        . (Join-Path $PSScriptRoot 'test-department-head.ps1')
        @{database=$database;passed=$results.Count;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'department-head-verification.json') -Encoding UTF8
        return
    }
    $null=Call GET "/api/v2/work?branchId=$($f.otherBranchId)&kind=OPERATIONAL" $null $worker 403
    Check 'cross-branch access denied' $true
    $null=Call GET '/api/rooms' $null $admin 404
    $null=Call GET '/api/v2/catalog/shifts' $null $admin 404
    Check 'legacy controllers and removed shift catalog unavailable' $true
    $task=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='OPERATIONAL';title='Integration task';departmentId=$f.departmentId} $admin).fields
    $task=(WorkAction $task assign $admin @{userId=$f.workerId}).fields
    $stale=$task
    $task=(WorkAction $task accept $worker).fields
    $null=WorkAction $stale start $worker @{} 409
    Check 'stale row version rejected' $true
    $task=(WorkAction $task start $worker).fields
    $task=(WorkAction $task pause $worker).fields
    $task=(WorkAction $task resume $worker).fields
    $task=(WorkAction $task complete $worker).fields
    $null=WorkAction $task verify $worker @{} 403
    $task=(WorkAction $task verify $admin).fields
    Check 'task accept/start/pause/resume/complete/independent verify' ($task.status-eq 'CLOSED')
    $time=(Sql "SELECT COUNT(*) n FROM work_intervals WHERE work_id=$($task.workId) AND closed_at IS NOT NULL").Rows[0].n
    Check 'two task intervals without workdays' ($time-eq 2)
    $request=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='INTERNAL_REQUEST';title='Internal request';departmentId=$f.departmentId;requesterDepartmentId=$f.departmentId} $admin).fields
    $request=(WorkAction $request submit $admin).fields
    $request=(WorkAction $request route $admin @{relatedId=$f.departmentId}).fields
    $request=(WorkAction $request assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$request=(WorkAction $request $action $worker).fields}
    $request=(WorkAction $request verify $admin).fields
    Check 'internal request route/process/requester confirmation' ($request.status-eq 'CLOSED')
    $maintenance=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='MAINTENANCE';title='Preventive maintenance';departmentId=$f.departmentId} $admin).fields
    $maintenance=(WorkAction $maintenance assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$maintenance=(WorkAction $maintenance $action $worker).fields}
    $null=WorkAction $maintenance verify $worker @{} 403
    $maintenance=(WorkAction $maintenance verify $admin).fields
    Check 'maintenance completion requires independent verification' ($maintenance.status-eq 'CLOSED')
    $incident=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='INCIDENT';title='Incident resolution';departmentId=$f.departmentId} $admin).fields
    $incident=(WorkAction $incident assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$incident=(WorkAction $incident $action $worker).fields}
    $incident=(WorkAction $incident verify $admin).fields
    Check 'incident execution and independent closure' ($incident.status-eq 'CLOSED')
    $handover=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='OPERATIONAL';title='Backlog handover'} $admin).fields
    $handover=(WorkAction $handover assign $admin @{userId=$f.workerId}).fields
    $handover=(WorkAction $handover accept $worker).fields
    $handover=(WorkAction $handover start $worker).fields
    $handover=(WorkAction $handover handover-offer $worker @{userId=$f.adminId}).fields
    $offer=(Call GET "/api/v2/work/$($handover.workId)" $null $admin).events|Where-Object {$_.fields.eventType-eq 'HANDOVER_OFFERED'}|Select-Object -First 1
    $handover=(WorkAction $handover handover-accept $admin @{relatedId=$offer.fields.workEventId}).fields
    Check 'backlog acceptance closes prior assignee interval' ($handover.status-eq 'PAUSED')
    $handover=(WorkAction $handover cancel $admin).fields
    $type=(Call POST "/api/v2/catalog/room-types?branchId=$($f.branchId)" @{fields=@{code='STD';name='Standard';capacity=2}} $admin).fields
    $location=(Call POST "/api/v2/catalog/locations?branchId=$($f.branchId)" @{fields=@{code='F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
    $room=(Call POST "/api/v2/catalog/rooms?branchId=$($f.branchId)" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$location.locationId;departmentId=$f.departmentId}} $admin).fields
    $null=Call POST "/api/v2/rooms/$($room.roomId)/actions/check-in" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin 409
    Check 'dirty room cannot check in' $true
    $checklist=(Call POST '/api/v2/templates/checklists' @{branchId=$f.branchId;code='QC';name='Inspection';kind='INSPECTION';items=@(@{description='Clean surfaces';isRequired=$true;weight=1})} $admin).fields
    $clean=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='CLEANING';title='Clean room';roomId=$room.roomId} $admin).fields
    $clean=(WorkAction $clean assign $admin @{userId=$f.workerId}).fields
    foreach($action in @('accept','start','complete')){$clean=(WorkAction $clean $action $worker).fields}
    $inspection=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='INSPECTION';title='Inspect room';roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$checklist.checklistTemplateId} $admin).fields
    $null=WorkAction $inspection assign $admin @{userId=$f.workerId} 409
    $inspection=(WorkAction $inspection assign $admin @{userId=$f.adminId}).fields
    foreach($action in @('accept','start')){$inspection=(WorkAction $inspection $action $admin).fields}
    $detail=Call GET "/api/v2/work/$($inspection.workId)" $null $admin
    $item=$detail.checklist[0].fields
    $null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($item.workChecklistItemId)" @{rowVersion=$item.rowVersion;outcome='PASS'} $admin
    foreach($action in @('complete','pass')){$inspection=(WorkAction $inspection $action $admin).fields}
    $rooms=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $admin;$room=$rooms|Where-Object roomId -eq $room.roomId
    Check 'inspection pass derives READY and separates inspector' ($room.readiness-eq 'READY'-and $room.operationalStatus-eq 'READY')
    $null=Sql "DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='RECEPTION' AND p.code='ROOM_CHECKIN';"
    $null=Call POST "/api/v2/rooms/$($room.roomId)/actions/check-in" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin 403
    $null=Sql "INSERT role_permissions(role_id,permission_id,data_scope) SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p WHERE r.code='RECEPTION' AND p.code='ROOM_CHECKIN';"
    Check 'Reception cannot borrow check-in permission from another role' $true
    $arrival=Call POST "/api/v2/rooms/$($room.roomId)/actions/check-in" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
    $departure=Call POST "/api/v2/rooms/$($room.roomId)/actions/check-out" @{rowVersion=$arrival.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin
    Check 'check-in/check-out invalidate old turnover evidence' ($departure.room.fields.operationalStatus-eq 'DIRTY'-and $departure.room.fields.turnoverNo-gt $room.turnoverNo-and $departure.readiness.fields.readiness-eq 'PENDING_CLEANING')
    $unit=(Call POST '/api/v2/catalog/references' @{fields=@{kind='UOM';code='EA';name='Each'}} $admin).fields
    $stockItem=(Call POST '/api/v2/catalog/items' @{fields=@{code='SOAP';name='Soap';baseUnitId=$unit.referenceId}} $admin).fields
    $warehouse=(Call POST "/api/v2/catalog/warehouses?branchId=$($f.branchId)" @{fields=@{code='MAIN';name='Main';departmentId=$f.departmentId}} $admin).fields
    $receipt=(Call POST '/api/v2/documents' @{branchId=$f.branchId;kind='RECEIPT';destinationWarehouseId=$warehouse.warehouseId} $admin).fields
    $receipt=(Call POST "/api/v2/documents/$($receipt.documentId)/lines" @{rowVersion=$receipt.rowVersion;itemId=$stockItem.itemId;unitId=$unit.referenceId;quantity=10} $admin).document.fields
    $receipt=(DocAction $receipt post $admin).fields
    $mr=(Call POST '/api/v2/documents' @{branchId=$f.branchId;kind='MATERIAL_REQUEST';departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $worker).fields
    $line=Call POST "/api/v2/documents/$($mr.documentId)/lines" @{rowVersion=$mr.rowVersion;itemId=$stockItem.itemId;unitId=$unit.referenceId;quantity=5} $worker;$mr=$line.document.fields;$sourceLine=$line.line.fields
    $mr=(DocAction $mr submit $worker).fields
    $null=DocAction $mr approve $worker 403
    $mr=(DocAction $mr approve $admin).fields
    $issue=(Call POST '/api/v2/documents' @{branchId=$f.branchId;kind='ISSUE';sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$mr.documentId} $worker).fields
    $issue=(Call POST "/api/v2/documents/$($issue.documentId)/lines" @{rowVersion=$issue.rowVersion;itemId=$stockItem.itemId;unitId=$unit.referenceId;quantity=3;sourceLineId=$sourceLine.lineId} $worker).document.fields
    $null=Sql "DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='STOREKEEPER' AND p.code='MR_FULFILL';"
    $null=DocAction $issue post $worker 403
    $null=Sql "INSERT role_permissions(role_id,permission_id,data_scope) SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p WHERE r.code='STOREKEEPER' AND p.code='MR_FULFILL';"
    Check 'Storekeeper cannot borrow fulfillment permission from another role' $true
    $null=DocAction $issue post $admin 409
    $issue=(DocAction $issue post $worker).fields
    $detail=Call GET "/api/v2/documents/$($mr.documentId)" $null $admin
    Check 'partial material fulfillment and independent approval/issue' ($detail.fulfillment[0].fields.missingBaseQty-eq 2)
    $balance=(Sql "SELECT SUM(signed_base_qty) n FROM stock_movements WHERE item_id=$($stockItem.itemId)").Rows[0].n
    Check 'ledger balance equals received minus issued' ($balance-eq 7)
    $null=DocAction $issue post $worker 409
    Check 'duplicate posting rejected' $true
    $notifications=Call GET '/api/v2/notifications' $null $worker
    Check 'transactional notifications delivered' ($notifications.items.Count-gt 0)
    $null=Call GET "/api/v2/insights/dashboard?branchId=$($f.branchId)" $null $admin
    $null=Call GET "/api/v2/insights/reports/tasks?branchId=$($f.branchId)" $null $admin
    $null=Call GET "/api/v2/insights/audit?branchId=$($f.branchId)" $null $admin
    Check 'dashboard reports audit run against v2' $true
    $asset=(Call POST "/api/v2/catalog/assets?branchId=$($f.branchId)" @{fields=@{assetCode='QA-ASSET';name='Vacuum';conditionCode='GOOD';locationId=$location.locationId;departmentId=$f.departmentId}} $admin).fields
    $asset=(Call POST "/api/v2/assets/$($asset.assetId)/actions/handover" @{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Handover to test worker';userId=$f.workerId} $admin).fields
    $asset=(Call POST "/api/v2/assets/$($asset.assetId)/actions/accept" @{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Accept asset responsibility'} $worker).fields
    Check 'asset handover recipient acknowledgment' ($asset.custodianUserId-eq $f.workerId)
    Add-Type -AssemblyName System.Net.Http
    $client=New-Object Net.Http.HttpClient
    $client.DefaultRequestHeaders.Authorization=New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$admin)
    $form=New-Object Net.Http.MultipartFormDataContent
    $form.Add((New-Object Net.Http.StringContent([string]$f.branchId)),'branchId')
    $form.Add((New-Object Net.Http.StringContent([string]$asset.assetId)),'assetId')
    $png=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j5xkAAAAASUVORK5CYII=')
    $bytes=New-Object Net.Http.ByteArrayContent(,$png)
    $form.Add($bytes,'file','evidence.png')
    $upload=$client.PostAsync("http://127.0.0.1:$Port/api/v2/evidence",$form).GetAwaiter().GetResult()
    $uploadBody=$upload.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if([int]$upload.StatusCode-ne 200){throw "Evidence upload failed: $uploadBody"}
    $evidence=($uploadBody|ConvertFrom-Json).fields
    $download=$client.GetByteArrayAsync("http://127.0.0.1:$Port/api/v2/evidence/$($evidence.attachmentId)").GetAwaiter().GetResult()
    Check 'evidence upload/download preserves bytes and hides storage key' ([Convert]::ToBase64String($download)-eq [Convert]::ToBase64String($png)-and -not $evidence.storageKey)
    $form.Dispose();$client.Dispose()
    $schedule=(Call POST '/api/v2/templates/schedules' @{branchId=$f.branchId;code='QA-DAILY';kind='MAINTENANCE';title='Daily inspection';frequency='DAILY';frequencyInterval=1;nextDueAt=[DateTime]::UtcNow.AddDays(1).ToString('o')} $admin).fields
    $schedule=(Call POST "/api/v2/templates/schedules/$($schedule.workTemplateId)/disable" @{rowVersion=$schedule.rowVersion} $admin).fields
    Check 'recurring template create/disable' (-not $schedule.isActive)
    $newUser=(Call POST '/api/v2/users' @{employeeCode='QA_TEMP';username='qa_temporary';fullName='Temporary user';password=$password} $admin).fields
    $temporary=Call POST '/api/v2/auth/login' @{username='qa_temporary';password=$password}
    $null=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $temporary.accessToken 403
    $null=Call POST '/api/v2/auth/password' @{currentPassword=$password;newPassword=$password+'Changed'} $temporary.accessToken
    $null=Call GET '/api/v2/auth/me' $null $temporary.accessToken 401
    Check 'temporary credentials restricted and password change revokes session' ($temporary.requiresPasswordChange-and -not $newUser.passwordHash)
    $adminRole=(Sql 'SELECT ur.user_role_id,ur.row_version FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE r.code=''SYS_ADMIN''').Rows[0]
    $null=Call POST "/api/v2/users/role-assignments/$($adminRole.user_role_id)/revoke" @{rowVersion=[Convert]::ToBase64String($adminRole.row_version)} $admin 409
    Check 'last administrator protected' $true
    if($Extended){. (Join-Path $PSScriptRoot 'Test-Functions.ps1')}
    $null=Call POST '/api/v2/auth/logout' @{} $worker
    $null=Call GET '/api/v2/auth/me' $null $worker 401
    Check 'revoked bearer session denied' $true
    $counts=(Sql 'SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) fks').Rows[0]
    Check 'schema unchanged at 40 tables and 160 FKs' ($counts.tables-eq 40-and $counts.fks-eq 160)
    $failed=@($results|Where-Object {-not $_.passed}).Count
    @{database=$database;passed=@($results|Where-Object passed).Count;failed=$failed;extended=[bool]$Extended;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'verification.json') -Encoding UTF8
    if($failed-gt 0){throw "$failed functional groups failed; see verification.json for exact cases."}
}finally{
    if($Owner-and $connection-and $connection.State-eq 'Open'){
        try{$cleanup=$connection.CreateCommand();$cleanup.CommandText='UPDATE user_sessions SET revoked_at=SYSUTCDATETIME() WHERE revoked_at IS NULL';$null=$cleanup.ExecuteNonQuery()}
        catch{Write-Warning 'Fixture session cleanup failed; fixture API will still be stopped.'}
    }
    if($api-and -not $api.HasExited){Stop-Process -Id $api.Id -Force}
    if($connection){$connection.Dispose()}
    foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$saved[$name])}
    # The generated database remains isolated for diagnostics. No legacy or primary Compact rows are changed.
    Write-Output "Isolated verification database: $database"
}
