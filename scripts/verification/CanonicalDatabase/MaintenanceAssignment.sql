-- UC053 immutable assignment receipt. Disposable only.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51848,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51848,'ApplyChanges must be 0 or 1.',1;


BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceAssignment:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51848,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.maintenance_assignment_receipts','U') IS NULL
BEGIN
 CREATE TABLE dbo.maintenance_assignment_receipts(
 audit_id bigint NOT NULL CONSTRAINT PK_maintenance_assignment_receipts PRIMARY KEY,
 work_id bigint NOT NULL,incident_id bigint NOT NULL,branch_id bigint NOT NULL,actor_id bigint NOT NULL,
 previous_assignee_id bigint NULL,assignee_id bigint NOT NULL,occurred_at datetime2(7) NOT NULL,incident_version binary(8) NOT NULL,
 CONSTRAINT UQ_maintenance_assignment_version UNIQUE(incident_id,incident_version),
 CONSTRAINT FK_maintenance_assignment_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id),
 CONSTRAINT FK_maintenance_assignment_work FOREIGN KEY(work_id) REFERENCES dbo.maintenance_works(maintenance_work_id),
 CONSTRAINT FK_maintenance_assignment_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
 CONSTRAINT FK_maintenance_assignment_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
 CONSTRAINT FK_maintenance_assignment_actor FOREIGN KEY(actor_id) REFERENCES dbo.users(user_id),
 CONSTRAINT FK_maintenance_assignment_previous FOREIGN KEY(previous_assignee_id) REFERENCES dbo.users(user_id),
 CONSTRAINT FK_maintenance_assignment_assignee FOREIGN KEY(assignee_id) REFERENCES dbo.users(user_id),
 CONSTRAINT CK_maintenance_assignment_change CHECK(previous_assignee_id IS NULL OR previous_assignee_id<>assignee_id));
 EXEC(N'CREATE TRIGGER dbo.TR_maintenance_assignment_immutable ON dbo.maintenance_assignment_receipts AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51848,''Assignment receipts are immutable; reviewed archive policy required.'',1; END');
END;
IF ISNULL(COL_LENGTH('dbo.maintenance_assignment_receipts','incident_version'),-1)<>8
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND
   ((name IN('audit_id','work_id','incident_id','branch_id','actor_id','assignee_id') AND system_type_id=127 AND is_nullable=0)
    OR (name='previous_assignee_id' AND system_type_id=127 AND is_nullable=1)
    OR (name='incident_version' AND system_type_id=173 AND max_length=8 AND is_nullable=0)
    OR (name='occurred_at' AND system_type_id=42 AND scale=7 AND is_nullable=0)))<>9
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>7
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_assignment_immutable') AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND name='UQ_maintenance_assignment_version' AND is_unique=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND name='CK_maintenance_assignment_change' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51848,'Assignment receipt schema mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceAssignment.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaintenanceAssignment.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.MaintenanceAssignment.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51848,'Conflicting assignment capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
