-- PROVISIONAL_NOT_ACCEPTED. Exact disposable target only, no event catalog table exists in canonical SQL.
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f110001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS011.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS011') THROW 51909,'FS011 isolated target required',1;
IF EXISTS(SELECT 1 FROM dbo.notification_operation_reservations) OR EXISTS(SELECT 1 FROM dbo.notification_publications) OR EXISTS(SELECT 1 FROM dbo.notification_delivery_intents) THROW 51917,'Populated evidence preserved: reconcile or roll forward; no destructive recovery',1;
DROP TRIGGER IF EXISTS dbo.TR_fs009_publication_source;
DROP TRIGGER IF EXISTS dbo.TR_fs009_operation_immutable;
DROP TRIGGER IF EXISTS dbo.TR_fs009_publication_immutable;
DROP PROCEDURE IF EXISTS dbo.fs009_verify_assignment_guard;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation') DROP INDEX UX_fs009_publication_operation ON dbo.notification_publications;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS011.ExecutableCandidate') EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FS011.ExecutableCandidate';