-- SQLCMD: caller must supply the verified development identity.
-- Additive only. Disable LowStockAlerts:Enabled to roll back application rollout;
-- retain episodes/audit after use. Failure rolls back the complete installation.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=CONVERT(uniqueidentifier,'$(ExpectedDatabaseGuid)'))
 THROW 51980,'Confirmed development database identity required.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:LowStockEpisodes:V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51980,'Low stock installation lock unavailable.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.LowStockEpisodes.V1')
 BEGIN
  IF OBJECT_ID('dbo.low_stock_episodes') IS NOT NULL THROW 51980,'Unversioned low stock storage requires review.',1;
  CREATE TABLE dbo.low_stock_episodes(
   episode_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_low_stock_episodes PRIMARY KEY,
   warehouse_id bigint NOT NULL, item_id bigint NOT NULL, branch_id bigint NOT NULL,
   opened_at datetime2(7) NOT NULL, last_observed_at datetime2(7) NOT NULL, closed_at datetime2(7) NULL,
   opening_quantity decimal(38,6) NOT NULL, minimum_quantity decimal(18,3) NOT NULL,
   open_audit_id bigint NOT NULL, close_audit_id bigint NULL,
   CONSTRAINT FK_low_stock_warehouse FOREIGN KEY(warehouse_id) REFERENCES dbo.warehouses(warehouse_id),
   CONSTRAINT FK_low_stock_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
   CONSTRAINT FK_low_stock_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
   CONSTRAINT FK_low_stock_open_audit FOREIGN KEY(open_audit_id) REFERENCES dbo.audit_logs(audit_id),
   CONSTRAINT FK_low_stock_close_audit FOREIGN KEY(close_audit_id) REFERENCES dbo.audit_logs(audit_id),
   CONSTRAINT CK_low_stock_quantity CHECK(minimum_quantity>=0 AND opening_quantity<minimum_quantity),
   CONSTRAINT CK_low_stock_time CHECK(last_observed_at>=opened_at AND (closed_at IS NULL OR closed_at>=opened_at)),
   CONSTRAINT CK_low_stock_closure CHECK((closed_at IS NULL AND close_audit_id IS NULL) OR (closed_at IS NOT NULL AND close_audit_id IS NOT NULL))
  );
  CREATE UNIQUE INDEX UX_low_stock_open ON dbo.low_stock_episodes(warehouse_id,item_id) WHERE closed_at IS NULL;
  CREATE INDEX IX_low_stock_delivery ON dbo.low_stock_episodes(closed_at,episode_id);
  EXEC(N'CREATE TRIGGER dbo.TR_low_stock_immutable ON dbo.low_stock_episodes AFTER UPDATE,DELETE AS
  BEGIN
   SET NOCOUNT ON;
   IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.episode_id=d.episode_id WHERE i.episode_id IS NULL
    OR d.closed_at IS NOT NULL OR i.warehouse_id<>d.warehouse_id OR i.item_id<>d.item_id OR i.branch_id<>d.branch_id
    OR i.opened_at<>d.opened_at OR i.open_audit_id<>d.open_audit_id OR i.opening_quantity<>d.opening_quantity
    OR i.minimum_quantity<>d.minimum_quantity OR i.last_observed_at<d.last_observed_at)
     THROW 51980,''Low stock episode history is immutable.'',1;
  END');
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.LowStockEpisodes.V1',@value=1;
 END;
 IF COL_LENGTH('dbo.low_stock_episodes','open_audit_id')<>8
  OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.low_stock_episodes') AND name='UX_low_stock_open' AND is_unique=1 AND is_disabled=0)
  OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.low_stock_episodes') AND (is_disabled=1 OR is_not_trusted=1 OR delete_referential_action<>0))
  OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.low_stock_episodes') AND name='TR_low_stock_immutable' AND is_disabled=0)
  THROW 51980,'Low stock episode storage incompatible.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
