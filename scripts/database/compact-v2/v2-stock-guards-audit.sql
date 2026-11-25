SET NOCOUNT ON;
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.inventory_documents');
SELECT t.name,m.definition FROM sys.triggers t JOIN sys.sql_modules m ON t.object_id=m.object_id
WHERE t.parent_id=OBJECT_ID('dbo.stock_movements');
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.staff_supply_ledger');
