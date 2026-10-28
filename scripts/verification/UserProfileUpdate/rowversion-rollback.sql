/* Trial rollback only. Stop any future rowversion-dependent API before rollback.
   Dropping this token invalidates every issued ETag; reapply cannot preserve it. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()=N'HotelOpsDB'
   OR DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh'
   OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
    THROW 51970,'Independent verified personnel trial required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name=N'HotelOps.UserProfile.RowVersion' AND CONVERT(nvarchar(128),value)=N'Task26')
   OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users')
                 AND name=N'row_version' AND system_type_id=189 AND is_nullable=0)
    THROW 51975,'Owned rowversion patch is required for rollback.',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.users DROP COLUMN row_version;
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.UserProfile.RowVersion';
COMMIT TRANSACTION;
