-- JOB01 / GBR-158: one open violation episode per owning work record.
-- Install only through the guarded development installer, after copy-only backup.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51973,'Installer transaction required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01'
 OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E')
 THROW 51973,'Confirmed development identity required.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:WorkAlerts:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51973,'Work alert schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkAlerts.V1')
BEGIN
 IF OBJECT_ID('dbo.work_alert_episodes') IS NOT NULL
  THROW 51973,'Unexpected work alert storage; preserve existing data.',1;
 CREATE TABLE dbo.work_alert_episodes(
  episode_id bigint IDENTITY NOT NULL CONSTRAINT PK_work_alert_episodes PRIMARY KEY,
  source_type varchar(32) NOT NULL,source_id bigint NOT NULL,branch_id bigint NOT NULL,
  opened_at datetime2(7) NOT NULL,last_observed_at datetime2(7) NOT NULL,closed_at datetime2(7) NULL,
  initial_condition varchar(16) NOT NULL,current_condition varchar(16) NOT NULL,
  due_at datetime2(7) NOT NULL,allowed_from datetime2(7) NULL,
  CONSTRAINT FK_work_alert_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT CK_work_alert_source CHECK(source_type IN('CLEANING','REWORK','INCIDENT','MAINTENANCE_OCCURRENCE','TASK','INTERNAL_REQUEST') AND source_id>0),
  CONSTRAINT CK_work_alert_condition CHECK(initial_condition IN('AT_RISK','OVERDUE') AND current_condition IN('AT_RISK','OVERDUE','CLEAR')),
  CONSTRAINT CK_work_alert_time CHECK(last_observed_at>=opened_at AND (allowed_from IS NULL OR due_at>allowed_from)
   AND ((closed_at IS NULL AND current_condition<>'CLEAR') OR (closed_at IS NOT NULL AND closed_at>=opened_at AND closed_at=last_observed_at AND current_condition='CLEAR')))
 );
 CREATE UNIQUE INDEX UX_work_alert_open ON dbo.work_alert_episodes(source_type,source_id) WHERE closed_at IS NULL;
 CREATE INDEX IX_work_alert_delivery ON dbo.work_alert_episodes(closed_at,episode_id) INCLUDE(branch_id,opened_at);
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.WorkAlerts.V1',@value=1;
END;
IF COL_LENGTH('dbo.work_alert_episodes','episode_id')<>8
 OR COL_LENGTH('dbo.work_alert_episodes','source_type')<>32
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_alert_episodes') AND name='UX_work_alert_open' AND is_unique=1 AND has_filter=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.work_alert_episodes') AND name='FK_work_alert_branch' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
 THROW 51973,'Work alert storage is incompatible.',1;
