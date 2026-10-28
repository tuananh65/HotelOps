/* Task26 trial-only forward patch. Do not run against HotelOpsDB. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()=N'HotelOpsDB'
   OR DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%[_]Fresh'
   OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                 AND name=N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)=N'Task17')
    THROW 51970,'Independent verified personnel trial required.',1;
IF OBJECT_ID(N'dbo.users',N'U') IS NULL
    THROW 51971,'Existing users table required.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users') AND name=N'row_version')
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users')
                  AND name=N'row_version' AND system_type_id=189 AND is_nullable=0)
       OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                     AND name=N'HotelOps.UserProfile.RowVersion' AND CONVERT(nvarchar(128),value)=N'Task26')
        THROW 51972,'Existing row_version is not this verified patch.',1;
END
ELSE
BEGIN
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users') AND system_type_id=189)
        THROW 51973,'Another rowversion column already exists.',1;
    IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UserProfile.RowVersion')
        THROW 51974,'Unexpected patch ownership marker.',1;
    ALTER TABLE dbo.users ADD row_version rowversion NOT NULL;
    EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserProfile.RowVersion',@value=N'Task26';
END;
COMMIT TRANSACTION;
