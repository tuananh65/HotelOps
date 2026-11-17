-- UC131 approved explicit room-department mapping. No historical inference or grants.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'$(ExpectedDatabase)' OR N'$(ExpectedDatabase)'<>N'HotelOpsDB'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
 AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51872,'Confirmed development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51872,'ApplyChanges must be 0 or 1.',1;

BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:RoomDepartments:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51874,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.room_department_mappings','U') IS NULL
BEGIN
 CREATE TABLE dbo.room_department_mappings(
  room_id bigint NOT NULL,department_id bigint NOT NULL,is_active bit NOT NULL,
  audit_id bigint NOT NULL,row_version rowversion NOT NULL,
  CONSTRAINT PK_room_department_mappings PRIMARY KEY(room_id,department_id),
  CONSTRAINT FK_room_department_room FOREIGN KEY(room_id) REFERENCES dbo.rooms(room_id),
  CONSTRAINT FK_room_department_department FOREIGN KEY(department_id) REFERENCES dbo.departments(department_id),
  CONSTRAINT FK_room_department_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id));
 CREATE INDEX IX_room_department_reverse ON dbo.room_department_mappings(department_id,is_active,room_id);
END;
IF COL_LENGTH('dbo.room_department_mappings','row_version')<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.room_department_mappings')
 AND name IN('FK_room_department_room','FK_room_department_department','FK_room_department_audit')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 THROW 51874,'Existing mapping schema mismatch; preserve and review.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_room_department_scope_v1 ON dbo.room_department_mappings AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d WHERE NOT EXISTS(SELECT 1 FROM inserted i WHERE i.room_id=d.room_id AND i.department_id=d.department_id))
  THROW 51874,''Preserve room-department history; deactivate mappings instead.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id
   JOIN dbo.departments d WITH(HOLDLOCK) ON d.department_id=i.department_id
   WHERE r.branch_id<>d.branch_id OR i.is_active=1 AND d.is_active=0)
  THROW 51874,''Room and active department must belong to the same branch.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RoomDepartments.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.RoomDepartments.V1',@value=N'1';
IF N'$(ApplyChanges)'='1' COMMIT; ELSE ROLLBACK;
