SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51973,'Installer transaction required.',1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita\MSSQLSERVER01' OR DB_NAME()<>N'HotelOpsDB'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E') THROW 51973,'Confirmed development identity required.',1;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:WorkAlerts:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51973,'Work alert schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkAlerts.V1')
 THROW 51973,'V1 storage required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkAlerts.V2')
BEGIN
 -- Never assign an invented cycle to an existing alert. The staged V1 worker
 -- has not been enabled; refuse upgrade if an operator has populated it.
 IF EXISTS(SELECT 1 FROM dbo.work_alert_episodes) THROW 51973,'Existing alert episodes require explicit provenance migration.',1;
 ALTER TABLE dbo.work_alert_episodes ADD cycle_id bigint NOT NULL CONSTRAINT DF_work_alert_cycle DEFAULT(0),
   metric varchar(16) NOT NULL CONSTRAINT DF_work_alert_metric DEFAULT('DUE');
 EXEC(N'ALTER TABLE dbo.work_alert_episodes ADD CONSTRAINT CK_work_alert_cycle CHECK(cycle_id>=0 AND metric IN(''DUE'',''RESPONSE'',''RESOLUTION''))');
 DROP INDEX UX_work_alert_open ON dbo.work_alert_episodes;
 EXEC(N'CREATE UNIQUE INDEX UX_work_alert_open ON dbo.work_alert_episodes(source_type,source_id,cycle_id,metric,initial_condition) WHERE closed_at IS NULL');
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.WorkAlerts.V2',@value=2;
END;
IF COL_LENGTH('dbo.work_alert_episodes','cycle_id')<>8 OR COL_LENGTH('dbo.work_alert_episodes','metric')<>16
 THROW 51973,'Cycle-aware alert storage incompatible.',1;
