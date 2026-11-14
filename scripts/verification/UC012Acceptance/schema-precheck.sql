SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches')
AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches') AND name=N'branch_code'
AND collation_name LIKE N'%[_]CI[_]%')
AND EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
WHERE i.object_id=OBJECT_ID(N'dbo.branches') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
AND k.key_ordinal=1 AND COL_NAME(k.object_id,k.column_id)=N'branch_code'
AND NOT EXISTS(SELECT 1 FROM sys.index_columns extra WHERE extra.object_id=i.object_id AND extra.index_id=i.index_id AND extra.key_ordinal>1))
THEN 1 ELSE 0 END) AS [Value]
