SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51400,'Compact database required.',1;
IF COL_LENGTH('dbo.departments','description') IS NULL
 ALTER TABLE dbo.departments ADD description nvarchar(2000) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.departments') AND name='description' AND system_type_id=231 AND max_length=4000 AND is_nullable=1)
 THROW 51400,'Unexpected department description definition.',1;
