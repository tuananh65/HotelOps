param($clone)
$id=Identity $clone
if($id.database -eq 'HotelOpsDB' -or $id.database -notmatch '^HotelOps_RBAC03A_Rehearsal_20261021_[a-f0-9]{12}$'){throw 'Verification DBCC is clone-only'}
$seed=Get-Content -Raw -Encoding UTF8 (Join-Path $release '03_HotelOps_RBAC_Catalog_Seed.sql')
$specs=@(
@{name='roles';start='INSERT INTO @SeedRoles';end='DECLARE @SeedPermissions';columns='role_code,role_name,description,is_system_role,is_active';actual='SELECT role_code,role_name,description,is_system_role,is_active FROM dbo.roles';count=12},
@{name='permissions';start='INSERT INTO @SeedPermissions';end='DECLARE @SeedGrants';columns='permission_code,permission_name,module_code,resource_code,action_code,description';actual='SELECT permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions';count=62},
@{name='grants';start='INSERT INTO @SeedGrants';end='    IF (SELECT COUNT(*) FROM @SeedRoles)';columns='role_code,permission_code,data_scope';actual='SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id';count=140}
)
foreach($s in $specs){$part=$seed.Substring($seed.IndexOf($s.start));$part=$part.Substring(0,$part.IndexOf($s.end));$tuples=[regex]::Matches($part,'(?m)^\(.+\)(?=[,;]\r?$)');if($tuples.Count -ne $s.count){throw 'Seed parser count mismatch'};$values=($tuples|ForEach-Object {$_.Value}) -join ',';$r=(Query $clone ("WITH expected AS (SELECT * FROM (VALUES $values) x($($s.columns))),actual AS ($($s.actual)) SELECT (SELECT COUNT(*) FROM (SELECT * FROM expected EXCEPT SELECT * FROM actual) x) missing,(SELECT COUNT(*) FROM (SELECT * FROM actual EXCEPT SELECT * FROM expected) x) extra")).Tables[0].Rows[0];if($r.missing -ne 0 -or $r.extra -ne 0){throw 'Exact catalog comparison failed'};Write-Output ('PASS: bidirectional exact '+$s.name+' metadata/scope comparison')}
$bad=(Query $clone @"
SELECT 'BAD_DH' issue WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.is_active=1)<>2
OR (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.is_active=1 AND ((ur.user_id=4 AND ur.branch_id=1) OR (ur.user_id=19 AND ur.branch_id=2)))<>2
UNION ALL SELECT 'BAD_OM' WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='OPERATIONS_MANAGER' AND ur.is_active=1)<>1
OR NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.user_id=1 AND ur.branch_id=1 AND ur.is_active=1 AND r.role_code='OPERATIONS_MANAGER')
OR EXISTS (SELECT 1 FROM dbo.user_branch_access WHERE user_id=1 AND branch_id<>1 AND access_level='MANAGER' AND revoked_at IS NULL)
UNION ALL SELECT 'BAD_STOREKEEPER' WHERE NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='STOREKEEPER' AND ur.user_id=5 AND ur.branch_id=1 AND ur.is_active=1)
OR EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.is_active=1 AND ((ur.user_id=5 AND r.role_code='DEPT_MANAGER') OR (ur.user_id=19 AND r.role_code='STOREKEEPER')))
UNION ALL SELECT 'BAD_CONSTRAINT_INDEX' WHERE EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
OR EXISTS (SELECT 1 FROM sys.indexes WHERE is_unique=1 AND is_disabled=1)
"@).Tables[0]
if($bad.Rows.Count){throw 'Personnel/constraint assertions failed'}
Write-Output 'PASS: fixed OM/DH/Storekeeper, enabled/trusted FK/CHECK and unique indexes'
foreach($sql in @('DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS,NO_INFOMSGS','DBCC CHECKDB WITH NO_INFOMSGS,ALL_ERRORMSGS')){$ds=Query $clone $sql;foreach($t in $ds.Tables){if($t.Rows.Count){throw 'Integrity findings'}};Write-Output ('PASS: '+$sql)}
Print-Results (Query $clone (Get-Content -Raw -Encoding UTF8 (Join-Path $release '04_HotelOps_Verification_ReadOnly.sql')))
