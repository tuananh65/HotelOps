-- FS015 PROVISIONAL V1. Same notification publisher/ledger, new isolated trial only.
SET XACT_ABORT ON;
IF DB_NAME()<>N'HotelOps_Canonical_FS015_Cleaning_V1_Fresh'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME()
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS015.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS015')
 THROW 51980,'FS015 isolated trial required',1;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.notification_publications','occurrence_key') IS NOT NULL THROW 51980,'No overwrite of closure schema',1;
DECLARE @eventCheck sysname=(SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications') AND definition LIKE '%CLEANING_TASK_ASSIGNED%');
IF @eventCheck IS NULL THROW 51980,'Existing candidate publication contract required',1;
DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.notification_publications DROP CONSTRAINT '+QUOTENAME(@eventCheck);
EXEC(@ddl);
ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs015_publication_event CHECK(event_type IN('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED'));
DECLARE @assignmentUnique sysname=(SELECT k.name FROM sys.key_constraints k WHERE k.parent_object_id=OBJECT_ID('dbo.notification_publications') AND k.type='UQ'
 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id AND c.name='assignment_id'));
IF @assignmentUnique IS NULL THROW 51980,'Assignment publication unique key required',1;
SET @ddl=N'ALTER TABLE dbo.notification_publications DROP CONSTRAINT '+QUOTENAME(@assignmentUnique);
EXEC(@ddl);
CREATE UNIQUE INDEX UX_fs015_assignment_publication ON dbo.notification_publications(assignment_id) WHERE event_type='CLEANING_TASK_ASSIGNED';
ALTER TABLE dbo.notification_publications ADD occurrence_key varchar(120) NULL;
EXEC(N'CREATE UNIQUE INDEX UX_fs015_occurrence ON dbo.notification_publications(occurrence_key) WHERE occurrence_key IS NOT NULL;');
EXEC(N'ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs015_occurrence CHECK(event_type=''CLEANING_TASK_ASSIGNED'' OR occurrence_key IS NOT NULL);');
ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT CK_fs013_readiness;
ALTER TABLE dbo.room_readiness_events ADD CONSTRAINT CK_fs015_readiness CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS','PENDING_INSPECTION'));
-- Completion actor/time is durable in operation result + task audit + completion progress log.
-- Reuse FS014 blocker state; no additional blocker table, roles or permission grants.
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS015.Closure',@value=N'PROVISIONAL_V1';
COMMIT;
