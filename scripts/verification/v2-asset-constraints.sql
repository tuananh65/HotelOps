SET NOCOUNT ON;
SELECT OBJECT_NAME(parent_object_id) table_name,name,definition FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID('dbo.assets'),OBJECT_ID('dbo.asset_events'),OBJECT_ID('dbo.inventory_documents'),OBJECT_ID('dbo.inventory_document_lines'));
SELECT name,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE parent_id IN(OBJECT_ID('dbo.assets'),OBJECT_ID('dbo.inventory_documents'),OBJECT_ID('dbo.stock_movements'));
SELECT condition_code,COUNT(*) n FROM assets GROUP BY condition_code;
SELECT * FROM item_lifecycle_profiles WHERE item_id=222;
