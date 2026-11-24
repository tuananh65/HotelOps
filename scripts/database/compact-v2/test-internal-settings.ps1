if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$a=$f.branchId
$null=Sql "UPDATE user_roles SET revoked_at=SYSUTCDATETIME() WHERE user_id=$($f.workerId) AND role_id=(SELECT role_id FROM roles WHERE code='DEPT_MANAGER')"
$body=@{name='Support request';description='Request description';defaultDepartmentId=$f.departmentId;isActive=$true}
$initial=Call GET "/api/v2/work/internal-request-types?branchId=$a" $null $admin
Check 'No configured types preserves existing branch behavior' (-not $initial.configured)
$row=Call POST "/api/v2/work/internal-request-types?branchId=$a" $body $admin
$id=$row.referenceId
$list=Call GET "/api/v2/work/internal-request-types?branchId=$a" $null $admin
Check 'Type description and default department saved' ($list.configured -and $list.items[0].description-eq $body.description -and $list.items[0].defaultDepartmentId-eq $f.departmentId)
$null=Call POST "/api/v2/work/internal-request-types?branchId=$a" $body $admin 409
$null=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$($f.otherBranchId)" $body $admin 403
$null=Call POST "/api/v2/work/internal-request-types?branchId=$a" $body $worker 403
Check 'Duplicate names, foreign branch and unauthorized writers denied' $true
$body.rowVersion=$row.rowVersion;$body.isActive=$false
$row=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin
$null=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin 409
Check 'Status uses optimistic concurrency' (-not $row.isActive)
$request=@{kind='INTERNAL_REQUEST';branchId=$a;title='Internal config verification';requesterDepartmentId=$f.departmentId;categoryId=$id}
$null=Call POST '/api/v2/work' $request $admin 400
$request.Remove('categoryId');$null=Call POST '/api/v2/work' $request $admin 400
Check 'Disabled and missing configured type cannot create a request' $true
$body.rowVersion=$row.rowVersion;$body.isActive=$true
$row=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin
$request.categoryId=$id
$created=(Call POST '/api/v2/work' $request $admin).fields
Check 'Request receives category and default department' ($created.categoryId-eq $id -and $created.departmentId-eq $f.departmentId)
$body.rowVersion=$row.rowVersion;$body.description=('x'*2001)
$null=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin 400
$body.description='Updated';$body.defaultDepartmentId=999999
$null=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin 400
Check 'Description length and department validation' $true
$body.defaultDepartmentId=$f.departmentId;$body.isActive=$false
$row=Call PUT "/api/v2/work/internal-request-types/$id`?branchId=$a" $body $admin
$saved=Call GET "/api/v2/work/$($created.workId)" $null $admin
Check 'Deactivating category keeps existing request' ($saved.work.fields.workId-eq $created.workId)
$room=Call PUT "/api/v2/work/room-issue-types?branchId=$a" @{kind='BROKEN';name='Broken';description='Physical inspection';isActive=$true} $admin
$types=Call GET "/api/v2/work/room-issue-types?branchId=$a" $null $admin
Check 'Room report description persists' ($types[0].description-eq 'Physical inspection')
$options=Call GET "/api/v2/workspace/options?branchId=$a" $null $admin
Check 'Private configuration excluded from global workspace references' (@($options.references|Where-Object {$_.code-like 'IR_TYPE_*' -or $_.code-like 'ROOM_ISSUE_*'}).Count-eq 0)
Check 'Internal configuration changes audited' ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_type='InternalRequestType'").Rows[0].n-ge 4)
$visible=Call GET "/api/v2/work/internal-request-types?branchId=$a" $null $worker
Check 'Staff cannot list disabled types' ($visible.configured -and @($visible.items).Count-eq 0)
$department=(Call POST "/api/v2/catalog/departments?branchId=$a" @{fields=@{name='Config default test'}} $admin).fields
$other=Call POST "/api/v2/work/internal-request-types?branchId=$a" @{name='Other support';defaultDepartmentId=$department.departmentId;isActive=$true} $admin
$null=Call PUT "/api/v2/catalog/departments/$($department.departmentId)?branchId=$a" @{rowVersion=$department.rowVersion;fields=@{isActive=$false}} $admin
$other=Call PUT "/api/v2/work/internal-request-types/$($other.referenceId)?branchId=$a" @{name=$other.name;defaultDepartmentId=$department.departmentId;isActive=$false;rowVersion=$other.rowVersion} $admin
Check 'Type can be disabled after default department deactivation' (-not $other.isActive)
