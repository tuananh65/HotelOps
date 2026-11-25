SET NOCOUNT ON;
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.asset_events');
SELECT name,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.asset_events');
SELECT COL_NAME(parent_object_id,parent_column_id) column_name,OBJECT_NAME(referenced_object_id) target
FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID('dbo.asset_events');
