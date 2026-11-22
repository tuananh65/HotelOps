Feature 'FE-01 user role membership lifecycle' {
    $u=(Call POST '/api/v2/users' @{employeeCode='FUNCTIONAL_USER';username='functional_user';fullName='Functional user';password=$password} $admin).fields
    $login=Call POST '/api/v2/auth/login' @{username='functional_user';password=$password}
    $null=Call POST '/api/v2/auth/password' @{currentPassword=$password;newPassword=$password+'New'} $login.accessToken
    $token=(Call POST '/api/v2/auth/login' @{username='functional_user';password=$password+'New'}).accessToken
    $membership=(Call POST "/api/v2/users/$($u.userId)/memberships" @{branchId=$f.branchId;kind='BRANCH_ACCESS';isPrimary=$true} $admin).fields
    $role=(Call POST '/api/v2/catalog/roles' @{fields=@{code='FUNCTIONAL_READER';name='Test scoped reader'}} $admin).fields
    $permissionId=[long](Sql "SELECT permission_id FROM permissions WHERE code='ROOM_VIEW'").Rows[0].permission_id
    $null=Call PUT "/api/v2/users/roles/$($role.roleId)/permissions" @(@{permissionId=$permissionId;dataScope='BRANCH'}) $admin
    $assignment=(Call POST "/api/v2/users/$($u.userId)/roles" @{roleId=$role.roleId;branchId=$f.branchId} $admin).fields
    $null=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $token
    $null=Call GET "/api/v2/rooms?branchId=$($f.otherBranchId)" $null $token 403
    Check 'FE-01 grant membership and branch role permits only its branch' $true
    $me=Call GET '/api/v2/auth/me' $null $token
    Check 'FE-01 profile hides password and security stamp' (-not $me.user.fields.passwordHash -and -not $me.user.fields.securityStamp)
    $null=Call PUT "/api/v2/users/roles/$($role.roleId)/permissions" @(@{permissionId=$permissionId;dataScope='DEPARTMENT'}) $admin 400
    $null=Call PUT "/api/v2/users/roles/$($role.roleId)/permissions" @(@{permissionId=$permissionId;dataScope='BRANCH'},@{permissionId=$permissionId;dataScope='BRANCH'}) $admin 400
    Check 'FE-01 invalid scope and duplicate permission rejected' $true
    $null=Call POST "/api/v2/users/role-assignments/$($assignment.userRoleId)/revoke" @{rowVersion=$assignment.rowVersion} $admin
    $null=Call GET "/api/v2/rooms?branchId=$($f.branchId)" $null $token 403
    Check 'FE-01 role revocation immediately removes access' $true
    $revoked=(Call POST "/api/v2/users/memberships/$($membership.membershipId)/revoke" @{rowVersion=$membership.rowVersion} $admin).fields
    $null=Call POST "/api/v2/users/$($u.userId)/roles" @{roleId=$role.roleId;branchId=$f.branchId} $admin 409
    Check 'FE-01 revoked membership prevents scoped assignment' ($null-ne $revoked.validTo)
    $u=((Call GET '/api/v2/users' $null $admin)|Where-Object {$_.fields.userId-eq $u.userId}).fields
    $u=(Call POST "/api/v2/users/$($u.userId)/deactivate" @{rowVersion=$u.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Functional deactivate account'} $admin).fields
    $null=Call GET '/api/v2/auth/me' $null $token 401
    $null=Call POST '/api/v2/auth/login' @{username='functional_user';password=$password+'New'} $null 401
    $u=(Call POST "/api/v2/users/$($u.userId)/activate" @{rowVersion=$u.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Functional activate account'} $admin).fields
    $token=(Call POST '/api/v2/auth/login' @{username='functional_user';password=$password+'New'}).accessToken
    Check 'FE-01 deactivate revokes session and activate permits login' ($u.accountStatus-eq 'ACTIVE')
    for($attempt=1;$attempt-le 5;$attempt++){$null=Call POST '/api/v2/auth/login' @{username='functional_user';password='WrongPassword!'} $null 401}
    $reply=Call POST '/api/v2/auth/login' @{username='functional_user';password=$password+'New'} $null 423
    Check 'FE-01 five failed attempts trigger account lock' ($reply.title-eq 'ACCOUNT_LOCKED')
    $null=Call POST '/api/v2/auth/login' @{username='unknown_functional';password='WrongPassword!'} $null 401
    $null=Call POST '/api/v2/users' @{employeeCode='BAD';username='';fullName='';password='short'} $admin 400
    Check 'FE-01 unknown login and invalid user DTO rejected' $true
}
Feature 'FE-02 master catalog validation and lifecycle' {
    $loc=(Call POST "/api/v2/catalog/locations?branchId=$($f.branchId)" @{fields=@{code='FUNCTIONAL_AREA';name='Test area';kind='AREA';parentLocationId=$location.locationId}} $admin).fields
    $stale=$loc.rowVersion
    $loc=(Call PUT "/api/v2/catalog/locations/$($loc.locationId)?branchId=$($f.branchId)" @{rowVersion=$loc.rowVersion;fields=@{name='Renamed area'}} $admin).fields
    $null=Call PUT "/api/v2/catalog/locations/$($loc.locationId)?branchId=$($f.branchId)" @{rowVersion=$stale;fields=@{name='Stale write'}} $admin 409
    Check 'FE-02 catalog update and stale version guard' ($loc.name-eq 'Renamed area')
    $loc=(Call PUT "/api/v2/catalog/locations/$($loc.locationId)?branchId=$($f.branchId)" @{rowVersion=$loc.rowVersion;fields=@{isActive=$false}} $admin).fields
    Check 'FE-02 unused location deactivates' (-not $loc.isActive)
    $null=Call PUT "/api/v2/catalog/locations/$($location.locationId)?branchId=$($f.branchId)" @{rowVersion=$location.rowVersion;fields=@{isActive=$false}} $admin 409
    $null=Call POST "/api/v2/catalog/rooms?branchId=$($f.otherBranchId)" @{fields=@{code='WRONG';roomTypeId=$type.roomTypeId;locationId=$location.locationId}} $admin 409
    $null=Call POST '/api/v2/catalog/permissions' @{fields=@{code='UNKNOWN'}} $admin 405
    $null=Call PUT "/api/v2/catalog/rooms/$($room.roomId)?branchId=$($f.branchId)" @{rowVersion=$departure.room.fields.rowVersion;fields=@{operationalStatus='READY'}} $admin 400
    Check 'FE-02 dependencies branch FKs read-only permissions and room status allowlist enforced' $true
    foreach($resource in @('branches','references','items','roles','permissions')){$page=Call GET "/api/v2/catalog/$resource" $null $admin;Check "FE-02 list $resource" ($page.total-gt 0)}
    foreach($resource in @('departments','locations','room-types','rooms','warehouses','assets')){$page=Call GET "/api/v2/catalog/$resource`?branchId=$($f.branchId)" $null $admin;Check "FE-02 list $resource" ($page.total-gt 0)}
}
Feature 'FE-07 assets move reject retire and evidence' {
    $a=(Call POST "/api/v2/catalog/assets?branchId=$($f.branchId)" @{fields=@{assetCode='FUNCTIONAL-ASSET';name='Functional asset';conditionCode='GOOD';locationId=$location.locationId}} $admin).fields
    $a=(Call POST "/api/v2/assets/$($a.assetId)/actions/move" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Move asset into test room';roomId=$room.roomId} $admin).fields
    Check 'FE-07 asset moves from location to room' ($a.roomId-eq $room.roomId -and $null-eq $a.locationId)
    $a=(Call POST "/api/v2/assets/$($a.assetId)/actions/condition" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Record detected equipment fault';condition='FAULTY'} $admin).fields
    $reply=Call POST "/api/v2/assets/$($a.assetId)/actions/condition" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Attempt unverified repair';condition='GOOD'} $admin 409
    Check 'FE-07 faulty asset requires verified repair' ($reply.title-eq 'VERIFIED_REPAIR_REQUIRED')
    $a=(Call POST "/api/v2/assets/$($a.assetId)/actions/handover" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Offer test asset handover';userId=$f.workerId} $admin).fields
    $null=Call POST "/api/v2/assets/$($a.assetId)/actions/accept" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Sender must not accept offer'} $admin 403
    $a=(Call POST "/api/v2/assets/$($a.assetId)/actions/reject" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Recipient declines asset handover'} $worker).fields
    Check 'FE-07 rejected handover preserves custodian' ($null-eq $a.custodianUserId)
    $a=(Call POST "/api/v2/assets/$($a.assetId)/actions/retire" @{rowVersion=$a.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Retire unused faulty test asset'} $admin).fields
    $detail=Call GET "/api/v2/assets/$($a.assetId)" $null $admin
    Check 'FE-07 retirement retains complete asset history' (-not $a.isActive -and $detail.history.Count-ge 5)
    $null=Call POST "/api/v2/evidence/$($evidence.attachmentId)/invalidate" @{rowVersion=$evidence.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Invalidate synthetic evidence'} $admin
    $null=Call GET "/api/v2/evidence/$($evidence.attachmentId)" $null $admin 404
    Check 'FE-07 invalidated evidence cannot be downloaded' $true
}
Feature 'FE-14 notifications and FE-15 reports' {
    $inbox=Call GET '/api/v2/notifications' $null $worker
    $n=$inbox.items|Where-Object {$_.fields.requiresAcknowledgment}|Select-Object -First 1
    $read=Call POST "/api/v2/notifications/$($n.fields.notificationId)/read" @{} $worker
    $ack=Call POST "/api/v2/notifications/$($n.fields.notificationId)/acknowledge" @{} $worker
    $null=Call POST "/api/v2/notifications/$($n.fields.notificationId)/read" @{} $admin 404
    $row=(Sql "SELECT read_at,acknowledged_at FROM notifications WHERE notification_id=$($n.fields.notificationId)").Rows[0]
    Check 'FE-14 recipient-only read and explicit acknowledgment persisted' ($row.read_at-is [DateTime] -and $row.acknowledged_at-is [DateTime])
    foreach($report in @('tasks','cleaning','incidents','maintenance','requests','stock','documents')){
        $rows=Call GET "/api/v2/insights/reports/$report`?branchId=$($f.branchId)" $null $admin
        Check "FE-15 $report report returns populated data" (@($rows).Count-gt 0)
    }
    $null=Call GET "/api/v2/insights/reports/tasks?branchId=$($f.branchId)&from=2020-01-01&to=2026-01-01" $null $admin 400
    $null=Call GET "/api/v2/insights/reports/tasks?branchId=$($f.branchId)&from=2026-12-31&to=2026-01-01" $null $admin 400
    $null=Call GET "/api/v2/insights/reports/unknown?branchId=$($f.branchId)" $null $admin 404
    Check 'FE-15 invalid report names and date ranges rejected' $true
    $audit=Call GET "/api/v2/insights/audit?branchId=$($f.branchId)" $null $admin
    Check 'FE-15 audit contains actor action and entity without secrets' ($audit.Count-gt 0 -and $null-ne $audit[0].actorUserId -and -not ($audit|ConvertTo-Json -Depth 5).Contains('passwordHash'))
}
