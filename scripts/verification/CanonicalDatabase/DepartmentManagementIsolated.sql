SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51893,'Exact owned disposable database required.',1;
IF ISNULL(COL_LENGTH('dbo.users','user_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','department_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','branch_id'),0)<>8
 THROW 51893,'Department authority baseline incompatible.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.department_management_access','U') IS NULL
BEGIN
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.departments') AND name='UX_departments_authority_pair')
  THROW 51893,'Unexpected pre-existing authority index; preserve and review.',1;
 -- Existing department key stays unchanged. This alternate key permits a composite
 -- FK that makes a cross-branch department mapping physically impossible.
 CREATE UNIQUE INDEX UX_departments_authority_pair ON dbo.departments(department_id,branch_id);
 CREATE TABLE dbo.department_management_access(
  user_id bigint NOT NULL,
  branch_id bigint NOT NULL,
  department_id bigint NOT NULL,
  granted_at datetime2(7) NOT NULL,
  revoked_at datetime2(7) NULL,
  revision rowversion NOT NULL,
  CONSTRAINT PK_department_management_access PRIMARY KEY(user_id,branch_id,department_id),
  CONSTRAINT FK_department_management_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_department_management_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
  CONSTRAINT CK_department_management_dates CHECK(revoked_at IS NULL OR revoked_at>=granted_at)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.DepartmentAuthority.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.department_management_access','revision'),0)<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
   AND name IN('FK_department_management_user','FK_department_management_department')
   AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>2
 THROW 51893,'Existing department authority contract differs; preserve and review.',1;
-- No inferred mappings, default grants, production seed, or receipt modifications.
COMMIT;
