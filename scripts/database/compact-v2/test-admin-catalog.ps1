if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$a=$f.branchId
foreach($resource in @('departments','room-types')){
 $body=@{name="Admin $resource";description='Mo ta tieng Viet - phong va bo phan'}
 if($resource-eq 'room-types'){$body.capacity=2}
 $row=(Call POST "/api/v2/catalog/$resource`?branchId=$a" @{fields=$body} $admin).fields
 $id=if($resource-eq 'departments'){$row.departmentId}else{$row.roomTypeId}
 Check "$resource description saved and active default retained" ($row.description-eq $body.description -and $row.isActive)
 $stale=$row.rowVersion
 $row=(Call PUT "/api/v2/catalog/$resource/$id`?branchId=$a" @{rowVersion=$row.rowVersion;fields=@{isActive=$false}} $admin).fields
 Check "$resource inline deactivate only changes status" (-not $row.isActive -and $row.description-eq $body.description)
 $null=Call PUT "/api/v2/catalog/$resource/$id`?branchId=$a" @{rowVersion=$stale;fields=@{isActive=$true}} $admin 409
 $row=(Call PUT "/api/v2/catalog/$resource/$id`?branchId=$a" @{rowVersion=$row.rowVersion;fields=@{name="Edited $resource";description='Description edited'}} $admin).fields
 Check "$resource content edit preserves inactive state" (-not $row.isActive -and $row.description-eq 'Description edited')
 $null=Call PUT "/api/v2/catalog/$resource/$id`?branchId=$a" @{rowVersion=$row.rowVersion;fields=@{description=('x'*2001)}} $admin 400
 $null=Call PUT "/api/v2/catalog/$resource/$id`?branchId=$($f.otherBranchId)" @{rowVersion=$row.rowVersion;fields=@{isActive=$true}} $admin 404
 $row=(Call PUT "/api/v2/catalog/$resource/$id`?branchId=$a" @{rowVersion=$row.rowVersion;fields=@{isActive=$true}} $admin).fields
 Check "$resource reactivation and scoped/version/length guards" $row.isActive
}
$dept=Call GET "/api/v2/catalog/departments?branchId=$a" $null $admin
$existing=$dept.items|Where-Object {$_.fields.departmentId-eq $f.departmentId}|Select-Object -First 1
$null=Call PUT "/api/v2/catalog/departments/$($f.departmentId)?branchId=$a" @{rowVersion=$existing.fields.rowVersion;fields=@{isActive=$false}} $admin 409
Check 'Department with active memberships cannot be deactivated by toggle' $true
$permissions=Call GET '/api/v2/catalog/permissions?pageSize=100' $null $admin
$permission=$permissions.items[0].fields
$null=Call PUT "/api/v2/catalog/permissions/$($permission.permissionId)" @{rowVersion=$permission.rowVersion;fields=@{isActive=$false}} $admin 409
Check 'Permission assigned to active users cannot be switched off' $true
Check 'Catalog changes audited' ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_type IN ('Department','RoomType')").Rows[0].n-ge 8)
