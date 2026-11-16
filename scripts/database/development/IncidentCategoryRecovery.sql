-- Reviewed-source candidate: additive, no catalog codes or SLA durations invented.
-- Run with sqlcmd -b on the verified recovery clone ONLY. Default rolls back.
-- Set @Apply=1 only for installation; no receipt/business rows are changed.
SET XACT_ABORT ON;
SET NOCOUNT ON;
DECLARE @Apply bit = 0;
IF CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) <> N'meita\MSSQLSERVER01'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_Recovery_2b221f40'
 OR NOT EXISTS (SELECT 1 FROM sys.database_recovery_status
    WHERE database_id=DB_ID() AND database_guid='5270D07C-7C27-477B-A60E-86485ADD7FAD')
 THROW 51898, 'Exact verified disposable recovery database required.', 1;
IF OBJECT_ID(N'dbo.incidents',N'U') IS NULL OR OBJECT_ID(N'dbo.reference_values',N'U') IS NULL
 THROW 51898, 'Existing incidents/reference_values schema required.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentCategory:Install',
   @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
 IF @lock < 0 THROW 51898, 'Could not lock category installation.', 1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentCategory.V1')
 BEGIN
  IF COL_LENGTH(N'dbo.incidents',N'category_id') IS NOT NULL
    OR OBJECT_ID(N'dbo.FK_incidents_category') IS NOT NULL
   THROW 51898, 'Unexpected existing category storage; preserve and review.', 1;
  -- Dynamic batches avoid compiling column references before additive DDL executes.
  EXEC(N'ALTER TABLE dbo.incidents ADD category_id bigint NULL;');
  EXEC(N'ALTER TABLE dbo.incidents WITH CHECK ADD CONSTRAINT FK_incidents_category
    FOREIGN KEY(category_id) REFERENCES dbo.reference_values(reference_id);');
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentCategory.V1', @value=N'1';
 END;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentCategory.V1'
     AND CONVERT(nvarchar(20),value)=N'1')
   OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.incidents')
     AND name=N'category_id' AND system_type_id=127 AND is_nullable=1)
   OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys f
     JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
     WHERE f.name=N'FK_incidents_category' AND f.parent_object_id=OBJECT_ID(N'dbo.incidents')
       AND f.referenced_object_id=OBJECT_ID(N'dbo.reference_values')
       AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'category_id'
       AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'reference_id'
       AND f.is_disabled=0 AND f.is_not_trusted=0
       AND f.delete_referential_action=0 AND f.update_referential_action=0)
  THROW 51898, 'Category schema incompatible.', 1;
 -- Group and active-state validation occurs under command transaction locks.
 -- No triggers are added to the Database First incident table (EF OUTPUT compatibility).
 IF @Apply=1 COMMIT TRANSACTION;
 ELSE ROLLBACK TRANSACTION;
 SELECT @Apply AS Applied, N'IncidentCategory.V1' AS Capability;
END TRY
BEGIN CATCH
 IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
