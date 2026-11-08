-- PROVISIONAL_NOT_ACCEPTED. New FS018 isolated trial only; no shared database execution.
SET XACT_ABORT ON;
IF DB_NAME()<>N'HotelOps_Canonical_FS018_Cancellation_V1_Fresh'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME()
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS018.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS018')
 THROW 51983,'FS018 isolated trial required',1;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS018.Cancellation') THROW 51983,'No overwrite',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.notification_publications DROP CONSTRAINT CK_fs016_publication_event;
ALTER TABLE dbo.notification_publications ADD CONSTRAINT CK_fs018_publication_event CHECK(event_type IN
 ('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED','REWORK_SUBMITTED','CLEANING_TASK_CANCELLED'));
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FS018.Cancellation',@value=N'PROVISIONAL_NOT_ACCEPTED';
COMMIT;
