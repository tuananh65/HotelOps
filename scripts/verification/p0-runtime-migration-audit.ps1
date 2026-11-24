param([ValidateSet('migration','alerts','workflow')][string]$Mode='migration',[long]$BranchId=0,[long]$ItemId=0,[long]$WorkId=0)
$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestP0261124'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $command=$connection.CreateCommand();$command.CommandTimeout=60
 foreach($entry in @{branch=$BranchId;item=$ItemId;work=$WorkId}.GetEnumerator()){
  [void]$command.Parameters.Add('@'+$entry.Key,[System.Data.SqlDbType]::BigInt);$command.Parameters['@'+$entry.Key].Value=$entry.Value
 }
 $command.CommandText=switch($Mode){
 'migration' {@'
IF DB_NAME()<>'HotelOpsCompactWorkdayTestP0261124' THROW 51990,'Exact isolated TEST database required.',1;
DECLARE @before nvarchar(max),@cutover datetime2;
SELECT @before=before_json,@cutover=occurred_at FROM dbo.audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED';
SELECT (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='P0_ROLE_MIGRATION' AND entity_key='261124-v1' AND action='APPLIED') migrationCount,
 (SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) activeRoleCount,
 (SELECT COUNT(*) FROM dbo.user_roles a JOIN dbo.roles r ON r.role_id=a.role_id WHERE r.code IN ('DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF') AND a.revoked_at IS NULL) activeLegacyAssignments,
 (SELECT COUNT(*) FROM OPENJSON(@before,'$.assignments') WITH(user_role_id bigint,user_id bigint) old WHERE NOT EXISTS(SELECT 1 FROM dbo.user_roles x WHERE x.user_role_id=old.user_role_id) OR NOT EXISTS(SELECT 1 FROM dbo.users u WHERE u.user_id=old.user_id)) missingHistoricalAssignmentsOrUsers,
 (SELECT COUNT(*) FROM OPENJSON(@before,'$.memberships') WITH(membership_id bigint) old WHERE NOT EXISTS(SELECT 1 FROM dbo.memberships m WHERE m.membership_id=old.membership_id)) missingHistoricalMemberships,
 (SELECT COUNT(*) FROM OPENJSON(@before,'$.schedules') WITH(assignment_id bigint) old WHERE NOT EXISTS(SELECT 1 FROM dbo.workday_assignments a WHERE a.assignment_id=old.assignment_id)) missingHistoricalSchedules,
 (SELECT COUNT(*) FROM dbo.user_sessions s WHERE s.created_at<=@cutover AND s.revoked_at IS NULL AND EXISTS(SELECT 1 FROM OPENJSON(@before,'$.assignments') WITH(user_id bigint) a WHERE a.user_id=s.user_id)) unrevokedPreMigrationSessions,
 (SELECT COUNT(*) FROM OPENJSON(@before,'$.assignments') WITH(user_id bigint)) historicalAssignmentCount,
 (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.revoked_at IS NULL AND r.is_active=1 AND ur.branch_id IS NOT NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND NOT EXISTS(SELECT 1 FROM dbo.memberships m WHERE m.user_id=ur.user_id AND m.branch_id=ur.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=SYSUTCDATETIME() AND (m.valid_to IS NULL OR m.valid_to>SYSUTCDATETIME()))) activeAssignmentsWithoutMembership
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
'@}
 'alerts' {@'
IF NOT EXISTS(SELECT 1 FROM dbo.branches WHERE branch_id=@branch AND code IN ('P0_TEST_DVBV','P0_TEST_DVCT')) THROW 51991,'Synthetic TEST branch required.',1;
SELECT p.minimum_qty minimumStock,p.low_stock_episode episode,
 (SELECT COUNT(*) FROM dbo.notifications n WHERE n.branch_id=@branch AND n.notification_type='LOW_STOCK' AND n.event_key=p.low_stock_episode AND n.expires_at IS NULL) activeNotifications,
 (SELECT COUNT(DISTINCT n.recipient_user_id) FROM dbo.notifications n WHERE n.branch_id=@branch AND n.notification_type='LOW_STOCK' AND n.event_key=p.low_stock_episode AND n.expires_at IS NULL) activeRecipients,
 (SELECT COUNT(*) FROM dbo.notifications n WHERE n.branch_id=@branch AND n.notification_type='LOW_STOCK' AND n.expires_at IS NOT NULL) resolvedNotifications
FROM dbo.inventory_policies p WHERE p.branch_id=@branch AND p.item_id=@item FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
'@}
 'workflow' {@'
IF NOT EXISTS(SELECT 1 FROM dbo.work_items w JOIN dbo.branches b ON b.branch_id=w.branch_id WHERE w.work_id=@work AND b.code IN ('P0_TEST_DVBV','P0_TEST_DVCT')) THROW 51991,'Synthetic TEST work required.',1;
SELECT (SELECT COUNT(*) FROM dbo.notifications WHERE work_id=@work AND notification_type='REQUEST_WORKFLOW') notifications,
 (SELECT COUNT(*) FROM dbo.request_workflows f JOIN dbo.branches b ON b.branch_id=f.branch_id WHERE b.code IN ('P0_TEST_DVBV','P0_TEST_DVCT') AND f.parent_request_work_id IS NOT NULL) linkedChildRequests
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
'@}
 }
 $reader=$command.ExecuteReader();$json='';while($reader.Read()){$json+=$reader.GetString(0)};$reader.Dispose()
 if(!$json){throw 'Audit returned no evidence'}
 Write-Output $json
} finally {$connection.Dispose()}
