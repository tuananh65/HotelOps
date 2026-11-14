-- Restore only the captured, owner-reviewed prior trigger. Never synthesize its body.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR DB_NAME()<>N'HotelOps_ENVNOTIF001_Trial_20261109'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.FE04.ApprovedTrial')),N'')<>N'FE04-P0-APPROVED-SCHEMA'
 OR @@TRANCOUNT<>0 THROW 51962,'Dedicated exact-target schema session required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaLeaseEndUtc'
 AND TRY_CONVERT(datetimeoffset,value)>SYSUTCDATETIME())
 THROW 51962,'Active notification trial lease required.',1;
-- Parameters are supplied from the protected owner capture.
IF @PriorTrigger IS NULL OR @PriorSha IS NULL OR @InstalledSha IS NULL
 OR HASHBYTES('SHA2_256',CONVERT(varbinary(max),@PriorTrigger))<>@PriorSha
 OR @InstalledSha<>ISNULL((SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)) FROM sys.sql_modules
 WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source')),0x)
 THROW 51962,'Missing, altered capture or changed installed trigger; no overwrite.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.NotificationReconciliation'
 AND CONVERT(nvarchar(128),value)=N'FE04-P0-FS013-N01-CANDIDATE-V1') THROW 51962,'Exact reconciliation marker required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:NotificationSchema002',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
IF @lock<0 THROW 51962,'Another schema runner holds the lease.',1;
IF EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK)
 WHERE event_type IN('inspection.failed','cleaning.rework.requested','REWORK_SUBMITTED'))
 OR EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK)
 GROUP BY actor_user_id,operation_key HAVING COUNT(*)>1)
 THROW 51962,'Rollback would strand typed/multi-event history. Preserve data; owner reconciliation required.',1;
DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
CREATE UNIQUE INDEX UX_fs009_publication_operation ON dbo.notification_publications(actor_user_id,operation_key);
-- Preflight excludes object permissions/properties/signatures; transactional DROP/CREATE
-- restores captured text without rewriting CREATE to ALTER.
DROP TRIGGER dbo.TR_fs009_publication_source;
EXEC sys.sp_executesql @PriorTrigger;
IF @PriorSha<>(SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),definition)) FROM sys.sql_modules
 WHERE object_id=OBJECT_ID('dbo.TR_fs009_publication_source'))
 THROW 51962,'Prior trigger restore hash mismatch.',1;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FE04.NotificationReconciliation';
COMMIT;
