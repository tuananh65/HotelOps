-- CANDIDATE ONLY: owner must approve artifact, SQLCMD variable values and disposable lease before execution.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),N'')<>N'$(TrialServer)' OR DB_NAME()<>N'$(TrialDatabase)' OR DB_NAME() NOT LIKE N'HotelOps[_]UC139[_]Trial[_]%'
  OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=CONVERT(uniqueidentifier,'$(TrialDatabaseGuid)'))
  OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UC139.Trial' AND CONVERT(nvarchar(128),value)=N'$(LeaseId)')
  THROW 51890,'Disposable trial identity/owner marker required.',1;
IF SYSUTCDATETIME()<CONVERT(datetime2(7),'$(LeaseStartsUtc)',127) OR SYSUTCDATETIME()>=CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)
  OR DATEDIFF(minute,CONVERT(datetime2(7),'$(LeaseStartsUtc)',127),CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)) NOT BETWEEN 1 AND 480
  THROW 51891,'Fresh bounded trial lease required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UC139.SchemaCandidate' AND CONVERT(nvarchar(128),value)=N'$(LeaseId)') THROW 51893,'Not installed by this candidate lease.',1;
IF EXISTS(SELECT 1 FROM dbo.workdays) OR EXISTS(SELECT 1 FROM dbo.work_intervals) OR EXISTS(SELECT 1 FROM dbo.time_correction_requests) THROW 51894,'Nonempty trial: rollback refused; owner must dispose database separately.',1;
DROP TABLE dbo.time_correction_requests; DROP TABLE dbo.work_intervals; DROP TABLE dbo.workdays;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.UC139.SchemaCandidate';
COMMIT;
