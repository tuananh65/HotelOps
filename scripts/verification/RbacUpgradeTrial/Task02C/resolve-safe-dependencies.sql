/* Task02C independent safe subset ONLY. Default rollback.
   Request 13, shifts 4/5, WH-FB stock, user accounts and departments remain unchanged.
   Department retirement must still pass the unchanged Task02B SQL 52005 guard.
*/
USE [HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282];
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
DECLARE @Commit bit=0;
IF DB_NAME()<>N'HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282'
   OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
    THROW 52100,'Wrong trial server/database',1;
IF @@TRANCOUNT<>0 THROW 52101,'Run outside an existing transaction',1;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;
    DECLARE @now datetime2(7)=SYSUTCDATETIME();
    DECLARE @Changes TABLE(entity_type nvarchar(150),entity_id nvarchar(100),branch_id bigint);
    IF EXISTS (SELECT 1 FROM sys.triggers WHERE is_disabled=0 AND parent_id IN
      (OBJECT_ID('dbo.warehouses'),OBJECT_ID('dbo.internal_request_types'),OBJECT_ID('dbo.work_categories'),OBJECT_ID('dbo.audit_logs')))
        THROW 52102,'Unexpected trigger: review side effects before writing',1;
    IF (SELECT COUNT(*) FROM dbo.warehouses WITH (UPDLOCK,HOLDLOCK)
        WHERE (warehouse_id=8 AND branch_id=2 AND department_id=10 AND warehouse_code='WH-SEC')
           OR (warehouse_id=9 AND branch_id=1 AND department_id=3 AND warehouse_code='WH-LAU'))<>2
        THROW 52103,'Warehouse identity mismatch',1;
    -- Never erase stock or invent a transfer target. Exact candidates have no balances or workflows.
    IF EXISTS (SELECT warehouse_id,item_id FROM dbo.stock_movements WITH (UPDLOCK,HOLDLOCK)
      WHERE warehouse_id IN (8,9) GROUP BY warehouse_id,item_id HAVING SUM(signed_base_qty)<>0)
      OR EXISTS (SELECT 1 FROM dbo.stock_documents WITH (UPDLOCK,HOLDLOCK) WHERE
        (source_warehouse_id IN (8,9) OR destination_warehouse_id IN (8,9)) AND status NOT IN ('POSTED','RECEIVED','REVERSED','CANCELLED'))
      OR EXISTS (SELECT 1 FROM dbo.material_requests WITH (UPDLOCK,HOLDLOCK) WHERE source_warehouse_id IN (8,9) AND status NOT IN ('COMPLETED','CANCELLED','REJECTED'))
      OR EXISTS (SELECT 1 FROM dbo.material_fulfillments WITH (UPDLOCK,HOLDLOCK) WHERE warehouse_id IN (8,9) AND status NOT IN ('RECEIVED','CANCELLED'))
      OR EXISTS (SELECT 1 FROM dbo.stocktakes WITH (UPDLOCK,HOLDLOCK) WHERE warehouse_id IN (8,9) AND status NOT IN ('COMPLETED','APPROVED','CANCELLED'))
        THROW 52104,'Candidate warehouse has balances/open workflow',1;
    IF (SELECT COUNT(*) FROM dbo.internal_request_types WITH (UPDLOCK,HOLDLOCK)
      WHERE (request_type_id=4 AND type_code='LAUNDRY' AND default_department_id=3)
         OR (request_type_id=5 AND type_code='SECURITY' AND default_department_id=5)
         OR (request_type_id=9 AND type_code='EVENT_SETUP' AND default_department_id=6))<>3
        THROW 52105,'Request type identity mismatch',1;
    IF EXISTS (SELECT 1 FROM dbo.internal_requests WITH (UPDLOCK,HOLDLOCK)
      WHERE request_type_id IN (4,5,9) AND status NOT IN ('COMPLETED','CLOSED','CANCELLED','REJECTED'))
        THROW 52106,'Candidate request type has open requests',1;
    IF (SELECT COUNT(*) FROM dbo.work_categories WITH (UPDLOCK,HOLDLOCK)
      WHERE (category_id=2 AND category_code='LAUNDRY' AND department_id=3)
         OR (category_id=10 AND category_code='SECURITY_PATROL' AND department_id=5)
         OR (category_id=11 AND category_code='FB_SERVICE' AND department_id=6))<>3
        THROW 52107,'Work category identity mismatch',1;
    IF EXISTS (SELECT 1 FROM dbo.operational_tasks WITH (UPDLOCK,HOLDLOCK)
      WHERE category_id IN (2,10,11) AND status NOT IN ('COMPLETED','CANCELLED'))
        THROW 52108,'Candidate category has open tasks',1;

    UPDATE dbo.warehouses SET is_active=0
      OUTPUT N'warehouses',CONVERT(nvarchar(100),inserted.warehouse_id),inserted.branch_id INTO @Changes
      WHERE warehouse_id IN (8,9) AND is_active=1;
    UPDATE dbo.internal_request_types SET is_active=0
      OUTPUT N'internal_request_types',CONVERT(nvarchar(100),inserted.request_type_id),NULL INTO @Changes
      WHERE request_type_id IN (4,5,9) AND is_active=1;
    UPDATE dbo.work_categories SET is_active=0
      OUTPUT N'work_categories',CONVERT(nvarchar(100),inserted.category_id),NULL INTO @Changes
      WHERE category_id IN (2,10,11) AND is_active=1;
    -- Null actor denotes a database trial maintenance operation; no human identity is impersonated.
    INSERT INTO dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)
    SELECT branch_id,NULL,entity_type,entity_id,'UPDATE',N'{"is_active":true}',
      N'{"is_active":false,"reason":"Task02C approved legacy sample retirement; no open dependent workflow","task":"Task02C"}',
      N'["is_active"]',@now FROM @Changes;
    SELECT entity_type,entity_id,branch_id FROM @Changes ORDER BY entity_type,entity_id;
    IF @Commit=1 COMMIT TRANSACTION;
    ELSE ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
