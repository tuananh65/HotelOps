-- REVIEW CANDIDATE ONLY. Empty, owned disposable trial tables only; never discard time history.
SET XACT_ABORT ON;
IF COALESCE(TRY_CONVERT(int, SESSION_CONTEXT(N'HotelOps.Task77Approved')), 0) <> 1
    THROW 51770, 'Task77 shared schema approval is required.', 1;
IF LEN(DB_NAME()) <> 49 OR LEFT(DB_NAME(),19) <> N'HotelOps_Canonical_'
    OR SUBSTRING(DB_NAME(),20,8) LIKE N'%[^0-9]%' OR SUBSTRING(DB_NAME(),28,1) <> N'_'
    OR SUBSTRING(DB_NAME(),29,6) LIKE N'%[^0-9]%' OR SUBSTRING(DB_NAME(),35,1) <> N'_'
    OR SUBSTRING(DB_NAME(),36,8) COLLATE Latin1_General_100_BIN2 LIKE N'%[^a-f0-9]%'
    OR RIGHT(DB_NAME(),6) <> N'_Fresh'
    OR NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0
        AND name = N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value) = N'Task17')
    OR NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0
        AND name = N'HotelOps.WorkTime.Contract' AND CONVERT(nvarchar(128),value) = N'Task77-v1')
    THROW 51774, 'Owned disposable Task77 contract required.', 1;
IF @@TRANCOUNT <> 0 THROW 51772, 'Schema runner must not have an ambient transaction.', 1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.workdays WITH(TABLOCKX,HOLDLOCK))
    OR EXISTS(SELECT 1 FROM dbo.work_intervals WITH(TABLOCKX,HOLDLOCK))
    OR EXISTS(SELECT 1 FROM dbo.time_correction_requests WITH(TABLOCKX,HOLDLOCK))
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51775, 'Time history exists; rollback refused without deleting data.', 1;
END;
DROP TABLE dbo.time_correction_requests;
DROP TABLE dbo.work_intervals;
DROP TABLE dbo.workdays;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.WorkTime.Contract';
COMMIT TRANSACTION;
