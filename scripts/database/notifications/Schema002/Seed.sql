-- Synthetic personnel/master-data fixtures only. No tasks, assignments, inspection
-- records, sessions, work intervals, notification outbox, or copied real data.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'HotelOps_ENVNOTIF001_Trial_20261109'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaOwner' AND CONVERT(nvarchar(128),value)=N'DEV1 ENV-001 / DEV2')
 THROW 52001,'Exact owned trial required.',1;
IF @SupervisorHash IS NULL OR @StaffHash IS NULL OR LEN(@SupervisorHash)>255 OR LEN(@StaffHash)>255
 THROW 52002,'Existing PasswordCredentialService hashes are required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:NotificationSchema002:Runner',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
 IF @lock<0 THROW 52003,'Another fixture operation is running.',1;
 IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaSeed')
  THROW 52004,'Seed already installed; verify instead of replacing credentials.',1;
 IF EXISTS(SELECT 1 FROM dbo.users) OR EXISTS(SELECT 1 FROM dbo.rooms)
  THROW 52005,'Personnel/room fixtures require a pristine trial; do not overwrite data.',1;
 DECLARE @branch bigint=(SELECT branch_id FROM dbo.branches WHERE branch_code='BR001' AND is_active=1);
 DECLARE @department bigint=(SELECT department_id FROM dbo.departments WHERE branch_id=@branch AND department_code='HK' AND is_active=1);
 DECLARE @supervisorRole bigint=(SELECT role_id FROM dbo.roles WHERE role_code='HK_SUPERVISOR' AND is_active=1);
 DECLARE @staffRole bigint=(SELECT role_id FROM dbo.roles WHERE role_code='HK_STAFF' AND is_active=1);
 IF @branch IS NULL OR @department IS NULL OR @supervisorRole IS NULL OR @staffRole IS NULL
  THROW 52006,'Canonical branch/department/roles are missing.',1;
 IF (SELECT COUNT(*) FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE rp.role_id=@supervisorRole AND p.permission_code IN('CLEANING_ASSIGN','CLEANING_VIEW') AND rp.data_scope='BRANCH')<>2
 OR (SELECT COUNT(*) FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE rp.role_id=@staffRole AND p.permission_code IN('CLEANING_EXECUTE','CLEANING_VIEW') AND rp.data_scope='OWN')<>2
  THROW 52007,'Existing canonical FE04 permission grants do not match.',1;
 DECLARE @expires datetime2(7)=CONVERT(datetime2(7),(SELECT TRY_CONVERT(datetimeoffset(7),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaLeaseEndUtc'));
 IF @expires IS NULL OR @expires<=SYSUTCDATETIME() THROW 52008,'Lease missing or expired.',1;
 INSERT dbo.users(employee_code,username,email,password_hash,full_name,primary_branch_id,department_id,employment_status,account_status,created_at,failed_login_count)
 VALUES('ENVNOTIF001-SUP','envnotif001.supervisor','envnotif001.supervisor@example.invalid',@SupervisorHash,N'ENVNOTIF001 Supervisor',@branch,@department,'ACTIVE','ACTIVE',SYSUTCDATETIME(),0),
       ('ENVNOTIF001-STAFF','envnotif001.staff','envnotif001.staff@example.invalid',@StaffHash,N'ENVNOTIF001 Staff',@branch,@department,'ACTIVE','ACTIVE',SYSUTCDATETIME(),0);
 DECLARE @supervisor bigint=(SELECT user_id FROM dbo.users WHERE username='envnotif001.supervisor');
 DECLARE @staff bigint=(SELECT user_id FROM dbo.users WHERE username='envnotif001.staff');
 INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,expires_at,is_active)
 VALUES(@supervisor,@supervisorRole,@branch,SYSUTCDATETIME(),@expires,1),(@staff,@staffRole,@branch,SYSUTCDATETIME(),@expires,1);
 INSERT dbo.user_branch_access(user_id,branch_id,access_level,is_primary,granted_at)
 VALUES(@supervisor,@branch,'STANDARD',1,SYSUTCDATETIME()),(@staff,@branch,'STANDARD',1,SYSUTCDATETIME());
 INSERT dbo.floors(branch_id,floor_code,floor_name,floor_number,is_active)
 VALUES(@branch,'ENVNOTIF001',N'ENVNOTIF001 Trial Floor',1,1);
 DECLARE @floor bigint=CONVERT(bigint,SCOPE_IDENTITY());
 -- The existing schema explicitly permits a global legacy type (all V1 fields NULL).
 -- This fixture is isolated; it neither assigns an inspection checklist nor fakes one.
 INSERT dbo.room_types(type_code,type_name,standard_capacity,is_active)
 VALUES('ENVNOTIF001',N'ENVNOTIF001 Trial Legacy Type',2,1);
 DECLARE @type bigint=CONVERT(bigint,SCOPE_IDENTITY());
 INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active,created_at)
 VALUES(@branch,@floor,@type,'ENVNOTIF001-101','DIRTY',1,SYSUTCDATETIME());
 INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order)
 VALUES(@type,N'ENVNOTIF001 synthetic cleaning item',1,0);
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.NotificationSchemaSeed',@value=N'V1';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
