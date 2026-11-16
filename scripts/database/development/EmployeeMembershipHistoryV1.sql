-- Additive UC007 membership-period history. No grant/role seeds or existing row rewrites.
-- ApplyChanges=0 rolls back installation. After use, source rollback retains history.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51847,'Verified development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51847,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:EmployeeMembershipHistory:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51847,'Migration lock unavailable.',1;
IF ISNULL(COL_LENGTH('dbo.employee_department_memberships','row_version'),-1)<>8
 THROW 51847,'Employee membership prerequisite missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.EmployeeMembershipHistory.V1')
BEGIN
 IF OBJECT_ID('dbo.employee_department_membership_history','U') IS NOT NULL
  THROW 51847,'Unexpected existing history; preserve and review.',1;
 CREATE TABLE dbo.employee_department_membership_history(
  history_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_employee_membership_history PRIMARY KEY,
  user_id bigint NOT NULL,branch_id bigint NOT NULL,department_id bigint NOT NULL,
  granted_at datetime2(7) NOT NULL,revoked_at datetime2(7) NOT NULL,
  recorded_by bigint NOT NULL,provenance nvarchar(250) NOT NULL,
  source_version binary(8) NOT NULL,restored_by bigint NOT NULL,restored_at datetime2(7) NOT NULL,
  CONSTRAINT FK_employee_membership_history_membership FOREIGN KEY(user_id,branch_id,department_id)
    REFERENCES dbo.employee_department_memberships(user_id,branch_id,department_id),
  CONSTRAINT FK_employee_membership_history_recorder FOREIGN KEY(recorded_by) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_employee_membership_history_restorer FOREIGN KEY(restored_by) REFERENCES dbo.users(user_id),
  CONSTRAINT CK_employee_membership_history_time CHECK(revoked_at>=granted_at AND restored_at>=revoked_at),
  CONSTRAINT CK_employee_membership_history_provenance CHECK(LEN(LTRIM(RTRIM(provenance)))>0),
  CONSTRAINT UQ_employee_membership_history_version UNIQUE(user_id,branch_id,department_id,source_version));
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.EmployeeMembershipHistory.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.EmployeeMembershipHistory.V1' AND CONVERT(nvarchar(10),value)='1')
 OR ISNULL(COL_LENGTH('dbo.employee_department_membership_history','source_version'),-1)<>8
 OR ISNULL(COL_LENGTH('dbo.employee_department_membership_history','provenance'),-1)<>500
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.employee_department_membership_history') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.employee_department_membership_history') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.employee_department_membership_history') AND name='UQ_employee_membership_history_version' AND is_unique=1 AND is_disabled=0)
 THROW 51847,'Membership history schema mismatch.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
