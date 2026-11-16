-- Approved development identity only. Additive UC025 / FE13 authority storage.
-- No inferred mappings, employee changes, roles or grants.
-- sqlcmd -v ApplyChanges=1 applies; 0 rolls back. Preserve provenance after use.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF N'$(ApplyChanges)' NOT IN (N'0',N'1') THROW 51982,'Explicit ApplyChanges required.',1;
IF @@TRANCOUNT<>0 THROW 51982,'Standalone installation required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
  AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51982,'Approved development database identity required.',1;
IF ISNULL(COL_LENGTH('dbo.users','user_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','department_id'),0)<>8
 OR ISNULL(COL_LENGTH('dbo.departments','branch_id'),0)<>8
 THROW 51982,'Department authority baseline incompatible.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:InternalRequest:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51982,'Schema lock unavailable.',1;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51982,'Catalog lock unavailable.',1;
 IF OBJECT_ID('dbo.department_management_access','U') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name IN('HotelOps.DepartmentAuthority.V1','HotelOps.DepartmentHeadTransfer.V1'))
   OR EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.departments') AND name='UX_departments_authority_pair')
   THROW 51982,'Unexpected partial authority installation; preserve and review.',1;
  CREATE UNIQUE INDEX UX_departments_authority_pair ON dbo.departments(department_id,branch_id);
  CREATE TABLE dbo.department_management_access(
   user_id bigint NOT NULL,
   branch_id bigint NOT NULL,
   department_id bigint NOT NULL,
   granted_at datetime2(7) NOT NULL,
   revoked_at datetime2(7) NULL,
   revision rowversion NOT NULL,
   authority_origin varchar(24) NOT NULL CONSTRAINT DF_department_authority_origin DEFAULT 'LEGACY_UNKNOWN',
   CONSTRAINT PK_department_management_access PRIMARY KEY(user_id,branch_id,department_id),
   CONSTRAINT FK_department_management_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
   CONSTRAINT FK_department_management_department FOREIGN KEY(department_id,branch_id) REFERENCES dbo.departments(department_id,branch_id),
   CONSTRAINT CK_department_management_dates CHECK(revoked_at IS NULL OR revoked_at>=granted_at),
   CONSTRAINT CK_department_authority_origin CHECK(authority_origin IN('LEGACY_UNKNOWN','INDEPENDENT','DESIGNATION','BOTH'))
  );
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.DepartmentAuthority.V1',@value=N'1';
 END;
 -- Existing authority tables must already match; never rewrite provenance.
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentAuthority.V1' AND CONVERT(nvarchar(10),value)='1')
  OR ISNULL(COL_LENGTH('dbo.department_management_access','revision'),0)<>8
  OR ISNULL(COL_LENGTH('dbo.department_management_access','authority_origin'),0)<>24
  OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
   AND name IN('FK_department_management_user','FK_department_management_department') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>2
  OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
   AND name IN('CK_department_management_dates','CK_department_authority_origin') AND is_disabled=0 AND is_not_trusted=0)<>2
  THROW 51982,'Authority/provenance contract differs; preserve and review.',1;
 IF COL_LENGTH('dbo.departments','head_transfer_version') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.departments') AND system_type_id=189)
   THROW 51982,'An alternate department rowversion requires review.',1;
  ALTER TABLE dbo.departments ADD head_transfer_version rowversion NOT NULL;
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.departments')
  AND name='head_transfer_version' AND system_type_id=189 AND is_nullable=0)
  THROW 51982,'Head concurrency contract differs.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentHeadTransfer.V1')
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.DepartmentHeadTransfer.V1',@value=N'1';
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.DepartmentHeadTransfer.V1' AND CONVERT(nvarchar(10),value)='1')
  THROW 51982,'Head transfer marker differs.',1;
 IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
-- On failure all schema changes roll back together. Application rollback must
-- retain these additive columns/tables once historical authority has been written.
