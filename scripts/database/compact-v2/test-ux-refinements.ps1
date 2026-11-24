# Dot-sourced only by Test-Backend.ps1 -UxRefinements, after a verified fixture backup.
if($database -notmatch '^HotelOpsCompactApiTest[0-9a-f]{12}$' -or $connection.Database -ne $database){throw 'Fresh isolated API fixture required.'}
$ux=@{}
function UxAssert($condition,$message){if(-not $condition){throw $message}}
function UxCase($name,[scriptblock]$test){
    try{& $test;$results.Add(@{name=$name;passed=$true});Write-Output "PASS: $name"}
    catch{$message=$_.Exception.Message;$results.Add(@{name=$name;passed=$false;error=$message;cause=$_.Exception.GetBaseException().Message;line=$_.InvocationInfo.ScriptLineNumber});Write-Output "FAIL: $name : $message"}
}
function UxError($method,$path,$body,$token,$status,$code){
    $errorBody=Call $method $path $body $token $status
    if($code){UxAssert ($errorBody.title -eq $code) "Expected error $code; got $($errorBody | ConvertTo-Json -Compress -Depth 5)"}
}
function UxCounts {
    $row=(Sql 'SELECT (SELECT COUNT(*) FROM users) users,(SELECT COUNT(*) FROM memberships) memberships,(SELECT COUNT(*) FROM audit_logs) audits,(SELECT COUNT(*) FROM workday_assignments) assignments,(SELECT COUNT(*) FROM workday_events) events').Rows[0]
    return "$($row.users)/$($row.memberships)/$($row.audits)/$($row.assignments)/$($row.events)"
}
function UxAccount($username,$email=$null){return @{username=$username;fullName='Same Display Name';password='Valid1!x';branchId=[long]$f.branchId;departmentId=[long]$f.departmentId;email=$email;phone='0901234567'}}
function UxBatch($shift,$users,$dates){return @{shiftId=$shift.shiftId;userIds=@($users);workDates=@($dates);rowVersion=$shift.rowVersion;operationKey=[Guid]::NewGuid().ToString();holidaysAcknowledged=$true}}
function UxShift($name,$start,$end,$capacity=2){
    return @{branchId=[long]$f.branchId;departmentId=[long]$f.departmentId;roleId=$ux.role;name=$name;startMinute=$start;endMinute=$end;capacity=$capacity;isActive=$true;operationKey=[Guid]::NewGuid().ToString()}
}
function UxFingerprint {
    $paths=@(Get-ChildItem (Join-Path $root 'src/HotelOps.Api/Compact') -Recurse -File -Filter '*.cs')
    $paths+=Get-Item (Join-Path $PSScriptRoot 'Test-Backend.ps1'),(Join-Path $PSScriptRoot 'test-ux-refinements.ps1'),$dll
    return @($paths | Sort-Object FullName | ForEach-Object {@{path=$_.FullName.Substring($root.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
}
$beforeFingerprint=UxFingerprint
$beforeFingerprint|ConvertTo-Json -Depth 5|Set-Content (Join-Path $artifacts 'inputs-before.json') -Encoding UTF8

UxCase 'accounts: auto code, eight-character password, branch/employment and audit commit' {
    $ux.a=(Call POST '/api/v2/users' (UxAccount 'ux_account_a' 'ux-a@example.test') $admin).fields
    UxAssert ($ux.a.employeeCode -match '^NV\d{6,}$' -and -not $ux.a.passwordHash -and -not $ux.a.securityStamp) 'Generated code or secret filtering incorrect.'
    $m=(Sql "SELECT kind,is_primary,branch_id,department_id FROM memberships WHERE user_id=$($ux.a.userId)").Rows
    UxAssert ($m.Count -eq 2 -and @($m|Where-Object {$_.kind -eq 'BRANCH_ACCESS' -and $_.is_primary -and $_.branch_id -eq $f.branchId}).Count -eq 1 -and @($m|Where-Object {$_.kind -eq 'EMPLOYMENT' -and $_.department_id -eq $f.departmentId}).Count -eq 1) 'Memberships not committed correctly.'
    UxAssert ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_type='User' AND entity_key='$($ux.a.userId)' AND action='CREATE'").Rows[0].n -eq 1) 'Create audit absent.'
    UxAssert ((Sql "SELECT COUNT(*) n FROM user_roles WHERE user_id=$($ux.a.userId)").Rows[0].n -eq 0) 'Account creation silently grants a role.'
}
UxCase 'accounts: repeated full name allowed and auto codes unique' {
    $ux.b=(Call POST '/api/v2/users' (UxAccount 'ux_account_b' 'ux-b@example.test') $admin).fields
    UxAssert ($ux.b.fullName -eq $ux.a.fullName -and $ux.b.employeeCode -ne $ux.a.employeeCode) 'Same-name user rejected or code reused.'
}
UxCase 'accounts: simultaneous creates allocate distinct codes' {
    Add-Type -AssemblyName System.Net.Http
    $handler=New-Object Net.Http.HttpClientHandler
    $handler.UseProxy=$false
    $client=New-Object Net.Http.HttpClient($handler)
    $client.DefaultRequestHeaders.Authorization=New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$admin)
    $bodies=@();$responses=@()
    try{
        $tasks=@();foreach($name in @('ux_parallel_a','ux_parallel_b')){
            $body=New-Object Net.Http.StringContent((ConvertTo-Json (UxAccount $name) -Compress),[Text.Encoding]::UTF8,'application/json');$bodies+=,$body
            $tasks+=,$client.PostAsync("http://127.0.0.1:$Port/api/v2/users",$body)
        }
        $codes=@();foreach($task in $tasks){$response=$task.GetAwaiter().GetResult();$responses+=,$response;$content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult();UxAssert ([int]$response.StatusCode -eq 200) "Concurrent create returned $([int]$response.StatusCode): $content";$codes+=($content|ConvertFrom-Json).fields.employeeCode}
        UxAssert ($codes.Count -eq 2 -and $codes[0] -ne $codes[1]) 'Concurrent auto code collision.'
    }finally{foreach($response in $responses){$response.Dispose()};foreach($body in $bodies){$body.Dispose()};$client.Dispose()}
}
UxCase 'accounts: short and missing-complexity passwords rejected without writes' {
    $before=UxCounts
    foreach($candidate in @('Ab1!xyz','abcdefgh','Abcdefg1')){$body=UxAccount 'ux_invalid_password';$body.password=$candidate;UxError POST '/api/v2/users' $body $admin 400 $null}
    UxAssert ((UxCounts) -eq $before) 'Invalid password created data.'
}
UxCase 'accounts: duplicate username/email reject after trim with no partial writes' {
    $before=UxCounts
    $body=UxAccount ' ux_account_a ';UxError POST '/api/v2/users' $body $admin 409 'USERNAME_ALREADY_USED'
    $body=UxAccount 'ux_duplicate_email' 'ux-a@example.test';UxError POST '/api/v2/users' $body $admin 409 'EMAIL_ALREADY_USED'
    UxAssert ((UxCounts) -eq $before) 'Rejected duplicate left user/membership/audit rows.'
}
UxCase 'accounts: missing branch, invalid branch and foreign department rejected atomically' {
    $before=UxCounts
    $body=UxAccount 'ux_bad_branch';$body.Remove('branchId');UxError POST '/api/v2/users' $body $admin 400 $null
    $body=UxAccount 'ux_bad_branch';$body.branchId=[long]::MaxValue;UxError POST '/api/v2/users' $body $admin 400 'INVALID_ACCOUNT_BRANCH'
    $body=UxAccount 'ux_bad_department';$body.branchId=[long]$f.otherBranchId;UxError POST '/api/v2/users' $body $admin 400 'INVALID_ACCOUNT_DEPARTMENT'
    UxAssert ((UxCounts) -eq $before) 'Invalid branch/department left writes.'
}
UxCase 'accounts: branch-scoped manager cannot create global accounts' {
    UxError POST '/api/v2/users' (UxAccount 'ux_unauthorized') $worker 403 'PERMISSION_DENIED'
}
UxCase 'profile: password rotation accepts eight characters and revokes old session' {
    $login=Call POST '/api/v2/auth/login' @{username='ux_account_a';password='Valid1!x'}
    UxAssert $login.requiresPasswordChange 'New account not marked temporary.'
    UxError POST '/api/v2/auth/password' @{currentPassword='Valid1!x';newPassword='Valid1!x'} $login.accessToken 400 $null
    $null=Call POST '/api/v2/auth/password' @{currentPassword='Valid1!x';newPassword='Other2!x'} $login.accessToken
    UxError GET '/api/v2/auth/me' $null $login.accessToken 401 $null
    $ux.self=(Call POST '/api/v2/auth/login' @{username='ux_account_a';password='Other2!x'}).accessToken
}
UxCase 'profile: no admin grant required; extra identity/status/role fields cannot target another user' {
    $p=Call GET '/api/v2/auth/profile' $null $ux.self
    $other=(Sql "SELECT full_name,row_version FROM users WHERE user_id=$($ux.b.userId)").Rows[0]
    $body=@{rowVersion=$p.rowVersion;fullName='Updated Self';email='ux-a@example.test';phone='0907654321';userId=$ux.b.userId;accountStatus='INACTIVE';branchId=$f.otherBranchId;roleId=1}
    $ux.profile=Call PUT '/api/v2/auth/profile' $body $ux.self
    UxAssert ($ux.profile.userId -eq $ux.a.userId -and $ux.profile.fullName -eq 'Updated Self' -and $ux.profile.rowVersion -ne $p.rowVersion) 'Profile did not update actor with fresh version.'
    $unchanged=(Sql "SELECT full_name,row_version FROM users WHERE user_id=$($ux.b.userId)").Rows[0]
    UxAssert ($unchanged.full_name -eq $other.full_name -and [Convert]::ToBase64String($unchanged.row_version) -eq [Convert]::ToBase64String($other.row_version)) 'Other identity was modified.'
    UxAssert ((Sql "SELECT COUNT(*) n FROM users WHERE user_id=$($ux.a.userId) AND account_status='ACTIVE'").Rows[0].n -eq 1) 'Profile changed account status.'
    UxAssert ((Sql "SELECT COUNT(*) n FROM user_roles WHERE user_id=$($ux.a.userId)").Rows[0].n -eq 0) 'Profile granted privilege.'
    $before=UxCounts
    UxError PUT '/api/v2/auth/profile' $body $ux.self 409 'STALE_ROW_VERSION'
    UxAssert ((UxCounts) -eq $before) 'Stale profile wrote audit/data.'
}
UxCase 'profile: duplicate email rejected and committed profile preserved' {
    $before=UxCounts
    UxError PUT '/api/v2/auth/profile' @{rowVersion=$ux.profile.rowVersion;fullName='Must not persist';email='ux-b@example.test';phone=$null} $ux.self 409 'EMAIL_ALREADY_USED'
    $p=Call GET '/api/v2/auth/profile' $null $ux.self
    UxAssert ($p.fullName -eq 'Updated Self' -and $p.rowVersion -eq $ux.profile.rowVersion -and (UxCounts) -eq $before) 'Duplicate email partially persisted.'
}
UxCase 'catalog: automatic branch code and representative contact round trip' {
    $ux.branch=(Call POST '/api/v2/catalog/branches' @{fields=@{name='UX Contact Branch';address='Test address';phone='0901111111';representativeName='First Contact'}} $admin).fields
    UxAssert ($ux.branch.code -match '^BRA-' -and $ux.branch.timezone -eq 'Asia/Ho_Chi_Minh') 'Branch auto defaults missing.'
    $listed=(Call GET '/api/v2/catalog/branches?pageSize=100' $null $admin).items.fields|Where-Object branchId -eq $ux.branch.branchId
    UxAssert ($listed.representativeName -eq 'First Contact' -and $listed.phone -eq '0901111111') 'Branch contact did not round trip.'
    $ux.branch=(Call PUT "/api/v2/catalog/branches/$($ux.branch.branchId)" @{rowVersion=$listed.rowVersion;fields=@{representativeName='Second Contact'}} $admin).fields
    UxError PUT "/api/v2/catalog/branches/$($ux.branch.branchId)" @{rowVersion=$listed.rowVersion;fields=@{representativeName='Stale'}} $admin 409 'STALE_ROW_VERSION'
    $listed=(Call GET '/api/v2/catalog/branches?pageSize=100' $null $admin).items.fields|Where-Object branchId -eq $ux.branch.branchId
    UxAssert ($listed.representativeName -eq 'Second Contact') 'Contact edit/stale guard failed.'
    $refs=Call GET '/api/v2/catalog/references?pageSize=100' $null $admin
    UxAssert (@($refs.items.fields|Where-Object kind -eq 'BRANCH_CONTACT').Count -eq 0) 'Internal branch contact leaks into reference catalog.'
}
UxCase 'catalog: invalid contact rolls back an already-saved branch and audit' {
    $beforePhone=UxCounts
    UxError POST '/api/v2/catalog/branches' @{fields=@{name='UX Invalid Phone';phone='not-a-phone'}} $admin 400 'INVALID_PHONE'
    UxAssert ((UxCounts) -eq $beforePhone) 'Invalid branch phone left writes.'
    $before=UxCounts
    UxError POST '/api/v2/catalog/branches' @{fields=@{name='UX Rolled Back Branch';representativeName=42}} $admin 400 'INVALID_FIELD_VALUE'
    UxAssert ((Sql "SELECT COUNT(*) n FROM branches WHERE name='UX Rolled Back Branch'").Rows[0].n -eq 0 -and (UxCounts) -eq $before) 'Branch insert survived contact validation failure.'
}
UxCase 'catalog: scoped automatic codes and duplicate names respect branch' {
    $ux.dept=(Call POST "/api/v2/catalog/departments?branchId=$($f.branchId)" @{fields=@{name='UX Department'}} $admin).fields
    UxAssert ($ux.dept.code -match '^DEP-') 'Department code missing.'
    UxError POST "/api/v2/catalog/departments?branchId=$($f.branchId)" @{fields=@{name=' UX Department '}} $admin 409 'DUPLICATE_NAME'
    $other=(Call POST "/api/v2/catalog/departments?branchId=$($f.otherBranchId)" @{fields=@{name='UX Department'}} $admin).fields
    UxAssert ($other.branchId -eq $f.otherBranchId -and $other.code -ne $ux.dept.code) 'Duplicate-name scope or generated-code uniqueness incorrect.'
    UxError PUT "/api/v2/catalog/departments/$($ux.dept.departmentId)?branchId=$($f.otherBranchId)" @{rowVersion=$ux.dept.rowVersion;fields=@{name='Wrong branch'}} $admin 404 'BRANCH_MISMATCH'
    UxError POST "/api/v2/catalog/departments?branchId=$($f.otherBranchId)" @{fields=@{name='Unauthorized'}} $worker 403 'PERMISSION_DENIED'
}
UxCase 'catalog: active-user permission deactivation rejected without version change' {
    $p=(Sql "SELECT permission_id,row_version FROM permissions WHERE code='ROOM_VIEW'").Rows[0]
    UxError PUT "/api/v2/catalog/permissions/$($p.permission_id)" @{rowVersion=[Convert]::ToBase64String($p.row_version);fields=@{isActive=$false}} $admin 409 'PERMISSION_USED_BY_ACTIVE_USER'
    $after=(Sql "SELECT is_active,row_version FROM permissions WHERE permission_id=$($p.permission_id)").Rows[0]
    UxAssert ($after.is_active -and [Convert]::ToBase64String($after.row_version) -eq [Convert]::ToBase64String($p.row_version)) 'Permission guard changed data.'
}
UxCase 'assets: room/location labels, unplaced and retired assets remain branch scoped' {
    $type=(Call POST "/api/v2/catalog/room-types?branchId=$($f.branchId)" @{fields=@{name='UX Room Type';capacity=2}} $admin).fields
    $location=(Call POST "/api/v2/catalog/locations?branchId=$($f.branchId)" @{fields=@{name='UX Floor';kind='FLOOR';floorNumber=1}} $admin).fields
    $room=(Call POST "/api/v2/catalog/rooms?branchId=$($f.branchId)" @{fields=@{locationId=$location.locationId;roomTypeId=$type.roomTypeId;departmentId=$f.departmentId}} $admin).fields
    $a=(Call POST "/api/v2/catalog/assets?branchId=$($f.branchId)" @{fields=@{name='UX Room Asset';roomId=$room.roomId;conditionCode='GOOD'}} $admin).fields
    $located=(Call POST "/api/v2/catalog/assets?branchId=$($f.branchId)" @{fields=@{name='UX Located Asset';locationId=$location.locationId;conditionCode='GOOD'}} $admin).fields
    $b=(Call POST "/api/v2/catalog/assets?branchId=$($f.branchId)" @{fields=@{name='UX Unplaced';conditionCode='FAULTY'}} $admin).fields
    $c=(Call POST "/api/v2/catalog/assets?branchId=$($f.otherBranchId)" @{fields=@{name='UX Foreign';conditionCode='GOOD'}} $admin).fields
    $null=Call POST "/api/v2/assets/$($b.assetId)/actions/retire" @{rowVersion=$b.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Fixture retirement verification'} $admin
    $assets=@(Call GET "/api/v2/assets?branchId=$($f.branchId)" $null $worker)
    $placed=$assets|Where-Object assetId -eq $a.assetId;$retired=$assets|Where-Object assetId -eq $b.assetId;$locatedResult=$assets|Where-Object assetId -eq $located.assetId
    UxAssert ($a.assetCode -match '^ASS-' -and $placed.roomCode -eq $room.code -and $locatedResult.locationName -eq 'UX Floor' -and $null -ne $retired -and -not $retired.isActive -and @($assets|Where-Object branchId -ne $f.branchId).Count -eq 0) 'Asset projection lost location, retired rows or branch isolation.'
    UxError GET "/api/v2/assets?branchId=$($f.otherBranchId)" $null $worker 403 'PERMISSION_DENIED'
    UxError GET "/api/v2/assets/$($c.assetId)" $null $worker 403 'PERMISSION_DENIED'
}
foreach($period in @('day','week','month')){
    UxCase "dashboard: $period local calendar UTC boundaries and actual SQL counts" {
        $window=(Call GET "/api/v2/insights/dashboard?branchId=$($f.branchId)&taskPeriod=$period" $null $worker).taskWindow
        $local=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').Date
        $start=$local;$end=$local.AddDays(1)
        if($period -eq 'week'){$start=$local.AddDays(-(([int]$local.DayOfWeek+6)%7));$end=$start.AddDays(7)}
        if($period -eq 'month'){$start=Get-Date -Year $local.Year -Month $local.Month -Day 1 -Hour 0 -Minute 0 -Second 0;$start=$start.Date;$end=$start.AddMonths(1)}
        UxAssert ($window.fromDate -eq $start.ToString('yyyy-MM-dd') -and $window.toDate -eq $end.AddDays(-1).ToString('yyyy-MM-dd') -and ([DateTimeOffset]::Parse($window.fromUtc)).UtcDateTime -eq $start.AddHours(-7) -and ([DateTimeOffset]::Parse($window.toUtc)).UtcDateTime -eq $end.AddHours(-7)) 'Calendar metadata is not branch-local day/Monday-week/month.'
        $lo=$start.AddHours(-7);$hi=$end.AddHours(-7)
        foreach($stamp in @($lo.AddSeconds(-1),$lo,$hi.AddSeconds(-1),$hi)){
            $task=(Call POST '/api/v2/work' @{branchId=$f.branchId;kind='OPERATIONAL';title="UX $period boundary"} $admin).fields
            $null=Sql "UPDATE work_items SET created_at='$($stamp.ToString('yyyy-MM-ddTHH:mm:ss'))' WHERE work_id=$($task.workId)"
        }
        $dashboard=Call GET "/api/v2/insights/dashboard?branchId=$($f.branchId)&taskPeriod=$period" $null $worker
        $count=(Sql "SELECT COUNT(*) n FROM work_items WHERE branch_id=$($f.branchId) AND kind='OPERATIONAL' AND created_at>='$($lo.ToString('yyyy-MM-ddTHH:mm:ss'))' AND created_at<'$($hi.ToString('yyyy-MM-ddTHH:mm:ss'))'").Rows[0].n
        $actual=($dashboard.work|Measure-Object count -Sum).Sum
        UxAssert ($count -ge 2 -and $actual -eq $count) "Dashboard count $actual differs from SQL $count."
    }
}
UxCase 'dashboard: invalid period and foreign branch denied' {
    UxError GET "/api/v2/insights/dashboard?branchId=$($f.branchId)&taskPeriod=year" $null $worker 400 'INVALID_TASK_PERIOD'
    UxError GET "/api/v2/insights/dashboard?branchId=$($f.otherBranchId)&taskPeriod=day" $null $worker 403 'PERMISSION_DENIED'
}

# Only fixture identities receive synthetic execution grants. No production role matrix is changed.
$ux.role=[long](Sql "SELECT role_id FROM roles WHERE code='QA_ALL'").Rows[0].role_id
$null=Sql "INSERT user_roles(user_id,role_id,branch_id,assigned_by) SELECT user_id,$($ux.role),$($f.branchId),$($f.adminId) FROM users WHERE username IN ('ux_account_a','ux_account_b','ux_parallel_a','ux_parallel_b')"
$month=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').Date.AddMonths(1)
$first=Get-Date -Year $month.Year -Month $month.Month -Day 1 -Hour 0 -Minute 0 -Second 0
$ux.dates=@(0..([DateTime]::DaysInMonth($month.Year,$month.Month)-1)|ForEach-Object {$first.AddDays($_).ToString('yyyy-MM-dd')})
UxCase 'workday: configuration and shift creation use active branch department and execution role' {
    $config=Call GET "/api/v2/workday/configuration?branchId=$($f.branchId)" $null $worker
    UxAssert (@($config.roles|Where-Object roleId -eq $ux.role).Count -eq 1 -and @($config.departments|Where-Object departmentId -eq $f.departmentId).Count -eq 1) 'Configuration omitted eligible fixture role/department.'
    $ux.main=(Call POST '/api/v2/workday/shifts' (UxShift 'UX Main' 480 960) $worker).fields
    $ux.spare=(Call POST '/api/v2/workday/shifts' (UxShift 'UX Spare' 0 120 3) $worker).fields
    $body=UxShift 'UX Foreign Department' 120 240;$body.branchId=[long]$f.otherBranchId
    UxError POST '/api/v2/workday/shifts' $body $admin 400 'SHIFT_DEPARTMENT_INVALID'
    UxError GET "/api/v2/workday/configuration?branchId=$($f.otherBranchId)" $null $worker 403 'PERMISSION_DENIED'
}
UxCase 'workday: multi-person whole-month batch commits every assignment/event/audit' {
    $query=($ux.dates|ForEach-Object {"dates=$_"}) -join '&'
    $c=Call GET "/api/v2/workday/candidates/batch?shiftId=$($ux.main.shiftId)&$query" $null $worker
    UxAssert ($c.slots.Count -eq $ux.dates.Count -and @($c.users|Where-Object {$_.userId -in @($ux.a.userId,$ux.b.userId)}).Count -eq 2) 'Monthly candidate intersection missing users/dates.'
    $ux.batch=UxBatch $ux.main @($ux.a.userId,$ux.b.userId) $ux.dates
    $ux.assigned=Call POST '/api/v2/workday/assignments/batch' $ux.batch $worker
    $expected=2*$ux.dates.Count
    $counts=(Sql "SELECT (SELECT COUNT(*) FROM workday_assignments WHERE shift_id=$($ux.main.shiftId)) assignments,(SELECT COUNT(*) FROM workday_events e JOIN workday_assignments a ON a.assignment_id=e.assignment_id WHERE a.shift_id=$($ux.main.shiftId)) events,(SELECT COUNT(*) FROM audit_logs l JOIN workday_assignments a ON l.entity_key=CONVERT(nvarchar(150),a.assignment_id) WHERE l.entity_type='Workday' AND l.action='ASSIGN' AND a.shift_id=$($ux.main.shiftId)) audits").Rows[0]
    UxAssert ($ux.assigned.count -eq $expected -and $counts.assignments -eq $expected -and $counts.events -eq $expected -and $counts.audits -eq $expected) 'Monthly batch lost assignments, events or audit.'
    $c=Call GET "/api/v2/workday/candidates/batch?shiftId=$($ux.main.shiftId)&$query" $null $worker
    UxAssert (@($c.slots|Where-Object remaining -ne 0).Count -eq 0 -and @($c.users|Where-Object {$_.userId -in @($ux.a.userId,$ux.b.userId)}).Count -eq 0) 'Occupied users/capacity still offered.'
}
UxCase 'workday: batch replay and full capacity fail without new rows' {
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' $ux.batch $worker 409 'DUPLICATE_RECORD'
    $third=[long]$f.workerId
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.main @($third) @($ux.dates[0])) $worker 409 'SHIFT_CAPACITY_REACHED'
    UxAssert ((UxCounts) -eq $before) 'Rejected replay/capacity wrote data.'
}
UxCase 'workday: invalid late candidate and later full day cause no partial batch' {
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.spare @($ux.a.userId,[long]::MaxValue) @($ux.dates[0],$ux.dates[1])) $worker 409 'SHIFT_EMPLOYEE_NOT_ELIGIBLE'
    UxAssert ((UxCounts) -eq $before) 'Invalid candidate left partial assignment.'
    $small=(Call POST '/api/v2/workday/shifts' (UxShift 'UX One Place' 120 240 1) $worker).fields
    $null=Call POST '/api/v2/workday/assignments/batch' (UxBatch $small @($ux.a.userId) @($ux.dates[1])) $worker
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $small @($ux.b.userId) @($ux.dates[0],$ux.dates[1])) $worker 409 'SHIFT_CAPACITY_REACHED'
    UxAssert ((UxCounts) -eq $before -and (Sql "SELECT COUNT(*) n FROM workday_assignments WHERE shift_id=$($small.shiftId) AND work_date='$($ux.dates[0])'").Rows[0].n -eq 0) 'First day committed despite later capacity failure.'
}
UxCase 'workday: database event collision after assignment insert rolls entire transaction back' {
    $body=UxBatch $ux.spare @($ux.a.userId,$ux.b.userId) @($ux.dates[2])
    $ordered=@($body.userIds|Sort-Object);$key=[Guid]::Parse($body.operationKey).ToString('N')+':'+$ordered[1]+':'+$ux.dates[2]
    $sha=[Security.Cryptography.SHA256]::Create();try{$hash=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key))}finally{$sha.Dispose()}
    $child=New-Object Guid (,[byte[]]$hash[0..15])
    $assignment=$ux.assigned.assignments[0].fields.assignmentId
    $null=Sql "INSERT workday_events(assignment_id,actor_user_id,action,operation_key) VALUES($assignment,$($f.adminId),'ASSIGN','$child')"
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' $body $worker 409 'DUPLICATE_RECORD'
    UxAssert ((UxCounts) -eq $before -and (Sql "SELECT COUNT(*) n FROM workday_assignments WHERE shift_id=$($ux.spare.shiftId) AND work_date='$($ux.dates[2])'").Rows[0].n -eq 0) 'Database failure left assignments/events/audits committed.'
}
UxCase 'workday: branch authorization on candidates and batch follows actual shift' {
    $otherDept=(Sql "SELECT TOP 1 department_id FROM departments WHERE branch_id=$($f.otherBranchId)").Rows[0].department_id
    $body=UxShift 'UX Foreign Shift' 480 960;$body.branchId=[long]$f.otherBranchId;$body.departmentId=[long]$otherDept
    $foreign=(Call POST '/api/v2/workday/shifts' $body $admin).fields
    UxError GET "/api/v2/workday/candidates/batch?shiftId=$($foreign.shiftId)&dates=$($ux.dates[0])" $null $worker 403 'PERMISSION_DENIED'
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $foreign @($ux.a.userId) @($ux.dates[0])) $worker 403 'PERMISSION_DENIED'
    UxAssert ((UxCounts) -eq $before) 'Unauthorized branch batch left writes.'
}
UxCase 'workday: duplicate users/dates and cross-month selections rejected atomically' {
    $before=UxCounts
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.spare @($ux.a.userId,$ux.a.userId) @($ux.dates[0])) $worker 400 'SHIFT_EMPLOYEES_REQUIRED'
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.spare @($ux.a.userId) @($ux.dates[0],$ux.dates[0])) $worker 400 'SHIFT_DUPLICATE_DATES'
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.spare @($ux.a.userId) @($ux.dates[-1],$first.AddMonths(1).ToString('yyyy-MM-dd'))) $worker 400 'SHIFT_ONE_MONTH_REQUIRED'
    UxAssert ((UxCounts) -eq $before) 'Malformed batch left writes.'
}
UxCase 'workday: history prevents time changes, capacity shrink and deactivation' {
    $body=UxShift 'UX Main' 481 960;$body.rowVersion=$ux.main.rowVersion
    UxError PUT "/api/v2/workday/shifts/$($ux.main.shiftId)" $body $worker 409 'SHIFT_HISTORY_DEFINITION_LOCKED'
    $body.startMinute=480;$body.capacity=1;UxError PUT "/api/v2/workday/shifts/$($ux.main.shiftId)" $body $worker 409 'SHIFT_CAPACITY_BELOW_ASSIGNED'
    $body.capacity=2;$body.isActive=$false;UxError PUT "/api/v2/workday/shifts/$($ux.main.shiftId)" $body $worker 409 'SHIFT_HAS_OPEN_ASSIGNMENTS'
    $body=UxShift 'UX Spare Updated' 0 120 3;$body.rowVersion=$ux.spare.rowVersion
    $updated=(Call PUT "/api/v2/workday/shifts/$($ux.spare.shiftId)" $body $worker).fields
    UxError POST '/api/v2/workday/assignments/batch' (UxBatch $ux.spare @($ux.a.userId) @($ux.dates[0])) $worker 409 'STALE_ROW_VERSION'
    UxAssert ($updated.rowVersion -ne $ux.spare.rowVersion) 'Shift version did not advance.'
}
$afterFingerprint=UxFingerprint
$afterFingerprint|ConvertTo-Json -Depth 5|Set-Content (Join-Path $artifacts 'inputs-after.json') -Encoding UTF8
UxCase 'verification: tested source and assembly stayed unchanged during API run' {
    UxAssert (($beforeFingerprint|ConvertTo-Json -Depth 5 -Compress) -eq ($afterFingerprint|ConvertTo-Json -Depth 5 -Compress)) 'Source/assembly changed during verification; results apply to loaded binary only.'
}
$failed=@($results|Where-Object {-not $_.passed}).Count
@{database=$database;port=$Port;apiPid=$api.Id;passed=@($results|Where-Object passed).Count;failed=$failed;tests=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $artifacts 'ux-verification.json') -Encoding UTF8
Write-Output "UX evidence: $artifacts"
if($failed){throw "$failed UX checks failed; see ux-verification.json."}
