-- UC139 V1: explicit employee membership and durable notification intent. No membership/grant seed.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51840,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51840,'ApplyChanges must be 0 or 1.',1;
IF OBJECT_ID('dbo.workdays','U') IS NULL OR COL_LENGTH('dbo.workdays','unscheduled_reason')<>2000
 THROW 51840,'Workday persistence prerequisite missing.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:UnscheduledWorkday:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51840,'Migration lock unavailable.',1;
-- Approved feature permission only; never create role permissions or user grants.
IF EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='WORKLOAD_VIEW'
 AND (module_code<>'TASK' OR resource_code<>'workload' OR action_code<>'view'))
 THROW 51840,'Existing WORKLOAD_VIEW metadata conflicts; review without overwriting.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_code='WORKLOAD_VIEW')
 INSERT dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
 VALUES('WORKLOAD_VIEW',N'View workload','TASK','workload','view',N'UC139: scoped Department Head workday review; grants are provisioned separately.');
IF OBJECT_ID('dbo.employee_department_memberships','U') IS NULL
 CREATE TABLE dbo.employee_department_memberships(
 user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 department_id bigint NOT NULL REFERENCES dbo.departments(department_id),
 granted_at datetime2(7) NOT NULL,
 revoked_at datetime2(7) NULL,
 recorded_by bigint NOT NULL REFERENCES dbo.users(user_id),
 provenance nvarchar(250) NOT NULL,
 row_version rowversion NOT NULL,
 CONSTRAINT PK_employee_department_memberships PRIMARY KEY(user_id,branch_id,department_id),
 CONSTRAINT CK_employee_membership_time CHECK(revoked_at IS NULL OR revoked_at>=granted_at),
 CONSTRAINT CK_employee_membership_provenance CHECK(LEN(LTRIM(RTRIM(provenance)))>0));
IF OBJECT_ID('dbo.unscheduled_workday_notifications','U') IS NULL
 CREATE TABLE dbo.unscheduled_workday_notifications(
 workday_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.workdays(workday_id),
 status varchar(20) NOT NULL,
 created_at datetime2(7) NOT NULL,
 last_attempt_at datetime2(7) NULL,
 notification_id bigint NULL REFERENCES dbo.notifications(notification_id),
 CONSTRAINT CK_unscheduled_notice_status CHECK(status IN('PENDING','NO_RECIPIENT','DELIVERED')));
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
 (OBJECT_ID('dbo.employee_department_memberships'),OBJECT_ID('dbo.unscheduled_workday_notifications'))
 AND is_disabled=0 AND is_not_trusted=0)<>6
 OR ISNULL(COL_LENGTH('dbo.employee_department_memberships','provenance'),-1)<>500
 OR ISNULL(COL_LENGTH('dbo.employee_department_memberships','row_version'),-1)<>8
 THROW 51840,'Membership/intent schema mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UnscheduledWorkday.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.UnscheduledWorkday.V1',@value=N'1';
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
