using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

sealed partial class ReplenishmentSuite
{
    async Task VerifyCatalog()
    {
        await using var db = Db();
        var columns = await db.Database.SqlQueryRaw<CatalogColumn>("""
            SELECT OBJECT_NAME(c.object_id) TableName,c.name ColumnName,TYPE_NAME(c.user_type_id) SqlType,
            CONVERT(int,c.max_length) MaxLength,CONVERT(int,c.precision) Precision,CONVERT(int,c.scale) Scale,
            c.is_nullable Nullable,c.is_identity IdentityColumn
            FROM sys.columns c WHERE OBJECT_NAME(c.object_id) IN
            ('material_replenishments','material_replenishment_lines','material_replenishment_receipt_lines')
            """).ToListAsync();
        var keys = await db.Database.SqlQueryRaw<CatalogKey>("""
            SELECT OBJECT_NAME(i.object_id) TableName,i.name KeyName,c.name ColumnName,i.is_unique UniqueKey,i.is_primary_key PrimaryKey,
            CONVERT(int,ic.key_ordinal) Ordinal,COALESCE(i.filter_definition,'') Filter
            FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE ic.key_ordinal>0 AND OBJECT_NAME(i.object_id) LIKE 'material_replenishment%'
            """).ToListAsync();
        var fks = await db.Database.SqlQueryRaw<CatalogFk>("""
            SELECT fk.name ConstraintName,OBJECT_NAME(fk.parent_object_id) TableName,
            COL_NAME(fk.parent_object_id,fkc.parent_column_id) ColumnName,
            OBJECT_NAME(fk.referenced_object_id) TargetTable,COL_NAME(fk.referenced_object_id,fkc.referenced_column_id) TargetColumn,
            CONVERT(int,fkc.constraint_column_id) Ordinal,CONVERT(int,fk.delete_referential_action) DeleteAction,fk.is_disabled Disabled,fk.is_not_trusted Untrusted
            FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            WHERE OBJECT_NAME(fk.parent_object_id) LIKE 'material_replenishment%'
            """).ToListAsync();
        foreach (var entity in db.Model.GetEntityTypes().Where(x => columns.Any(c => c.TableName == x.GetTableName())))
        {
            var table = entity.GetTableName()!; var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var p in entity.GetProperties())
            {
                var col = columns.SingleOrDefault(x => x.TableName == table && x.ColumnName == p.GetColumnName(store));
                Check(col != null && !p.IsShadowProperty(), $"Unexpected/shadow column {table}.{p.Name}");
                Check(col!.Nullable == p.IsNullable, $"Nullability {table}.{p.Name}");
                var sqlType = p.GetColumnType()!.ToLowerInvariant();
                Check(sqlType.StartsWith(col.SqlType) || col.SqlType == "timestamp" && p.IsConcurrencyToken, $"SQL type {table}.{p.Name}");
                if (p.GetMaxLength() is int max) Check(col.MaxLength == max * (p.IsUnicode() == false ? 1 : 2), $"Length {table}.{p.Name}");
                if (col.SqlType == "decimal") Check(col.Precision == 18 && col.Scale == 3, $"Precision {table}.{p.Name}");
                if (col.IdentityColumn) Check(p.ValueGenerated == ValueGenerated.OnAdd, $"Identity {table}.{p.Name}");
                if (col.SqlType == "timestamp") Check(p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate, "SQL rowversion generation");
            }
            var pk = entity.FindPrimaryKey()!.Properties.Select(x => x.GetColumnName(store));
            Check(pk.SequenceEqual(keys.Where(x => x.TableName == table && x.PrimaryKey).OrderBy(x => x.Ordinal).Select(x => x.ColumnName)), "PK columns " + table);
            foreach (var key in entity.GetKeys())
                // Database First represents the named SQL UQ as an index; the principal key
                // introduced by HasPrincipalKey has an EF convention name, not the SQL UQ name.
                Check(keys.Where(x => x.TableName == table && x.UniqueKey).GroupBy(x => x.KeyName).Any(g => g.OrderBy(x => x.Ordinal).Select(x => x.ColumnName).SequenceEqual(key.Properties.Select(x => x.GetColumnName(store)))), "Alternate/primary key columns " + key.GetName());
            foreach (var index in entity.GetIndexes())
            {
                var name = index.GetDatabaseName()!;
                var actual = keys.Where(x => x.TableName == table && x.KeyName == name).OrderBy(x => x.Ordinal).ToArray();
                Check(actual.Length > 0 && actual.All(x => x.UniqueKey == index.IsUnique) && actual.Select(x => x.ColumnName).SequenceEqual(index.Properties.Select(x => x.GetColumnName(store))), "Index " + name);
                Check(actual[0].Filter == (index.GetFilter() ?? ""), "Index filter " + name);
            }
            foreach (var fk in entity.GetForeignKeys())
            {
                var actual = fks.Where(x => x.ConstraintName == fk.GetConstraintName()).OrderBy(x => x.Ordinal).ToArray();
                var targetStore = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema());
                Check(actual.Length == fk.Properties.Count && actual.All(x => x.TargetTable == fk.PrincipalEntityType.GetTableName()) && actual.Select(x => x.ColumnName).SequenceEqual(fk.Properties.Select(x => x.GetColumnName(store)))
                    && actual.Select(x => x.TargetColumn).SequenceEqual(fk.PrincipalKey.Properties.Select(x => x.GetColumnName(targetStore))), "FK " + fk.GetConstraintName());
                Check(fk.DependentToPrincipal != null, "FK navigation " + fk.GetConstraintName());
                Check(actual.All(x => x.DeleteAction == 0 && !x.Disabled && !x.Untrusted), "FK NO ACTION/trust " + fk.GetConstraintName());
            }
        }
        Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.check_constraints WHERE OBJECT_NAME(parent_object_id) LIKE 'material_replenishment%' AND (is_disabled=1 OR is_not_trusted=1)").SingleAsync() == 0, "Material CHECKs enabled and trusted");
        Console.WriteLine($"SQL catalog verified: {columns.Count} columns, primary keys, FKs, navigation metadata and FE10 indexes.");
    }
}
sealed record CatalogColumn(string TableName, string ColumnName, string SqlType, int MaxLength, int Precision, int Scale, bool Nullable, bool IdentityColumn);
sealed record CatalogKey(string TableName, string KeyName, string ColumnName, bool UniqueKey, bool PrimaryKey, int Ordinal, string Filter);
sealed record CatalogFk(string ConstraintName, string TableName, string ColumnName, string TargetTable, string TargetColumn, int Ordinal, int DeleteAction, bool Disabled, bool Untrusted);
