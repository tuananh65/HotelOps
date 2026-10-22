using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

sealed partial class InventorySuite
{
    private async Task SchemaMetadata(HotelOpsDbContext db)
    {
        foreach (var type in new[] { typeof(StockReceiptLineDetail), typeof(StockReturnLineDetail), typeof(StockTransferReceipt), typeof(StockTransferReceiptLine) })
        {
            var entity = db.Model.FindEntityType(type)!; var table = entity.GetTableName()!; var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            using var command = db.Database.GetDbConnection().CreateCommand();
            var parameter = command.CreateParameter(); parameter.ParameterName = "@table"; parameter.Value = "dbo." + table; command.Parameters.Add(parameter);
            command.CommandText = """
                SELECT c.name,t.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity
                FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id
                WHERE c.object_id=OBJECT_ID(@table) ORDER BY c.column_id;
                """;
            var columns = new Dictionary<string, (string Type, bool Nullable, bool Identity)>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var sqlType = reader.GetString(1); var len = reader.GetInt16(2); var precision = reader.GetByte(3); var scale = reader.GetByte(4);
                    var storeType = sqlType switch
                    {
                        "nvarchar" or "nchar" => sqlType + "(" + (len == -1 ? "max" : (len / 2).ToString()) + ")",
                        "varchar" or "char" or "varbinary" => sqlType + "(" + (len == -1 ? "max" : len.ToString()) + ")",
                        "decimal" or "numeric" => $"decimal({precision},{scale})",
                        "datetime2" => scale == 7 ? "datetime2" : $"datetime2({scale})",
                        _ => sqlType
                    };
                    columns.Add(reader.GetString(0), (storeType, reader.GetBoolean(5), reader.GetBoolean(6)));
                }
            }
            Check(columns.Count == entity.GetProperties().Count(), "Column count mismatch: " + table);
            foreach (var property in entity.GetProperties())
            {
                var column = columns[property.GetColumnName(store)!]; var mapped = property.GetRelationalTypeMapping().StoreType.Replace(" ", "");
                Check(mapped == column.Type && property.IsNullable == column.Nullable, "SQL type/nullability mismatch: " + table + "." + property.Name);
                if (column.Identity) Check(property.ValueGenerated == ValueGenerated.OnAdd, "Identity metadata mismatch.");
            }
            command.CommandText = """
                SELECT i.name,c.name,ic.key_ordinal,i.is_primary_key,i.is_unique
                FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE i.object_id=OBJECT_ID(@table) AND ic.key_ordinal>0 ORDER BY i.name,ic.key_ordinal;
                """;
            var indexes = new Dictionary<string, (List<string> Columns, bool Primary, bool Unique)>();
            await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (!indexes.TryGetValue(name, out var value)) value = ([], reader.GetBoolean(3), reader.GetBoolean(4));
                value.Columns.Add(reader.GetString(1)); indexes[name] = value;
            }
            var pk = entity.FindPrimaryKey()!; var sqlPk = indexes.Values.Single(x => x.Primary);
            Check(pk.Properties.Select(p => p.GetColumnName(store)).SequenceEqual(sqlPk.Columns), "PK mismatch: " + table);
            foreach (var index in entity.GetIndexes())
            {
                var actual = indexes[index.GetDatabaseName()!];
                Check(index.IsUnique == actual.Unique && index.Properties.Select(p => p.GetColumnName(store)).SequenceEqual(actual.Columns), "Index mismatch: " + table);
            }
            Check(indexes.Count == entity.GetIndexes().Count() + 1, "Unexpected SQL index: " + table);
            command.CommandText = """
                SELECT f.name,c.name,rt.name,rc.name,f.delete_referential_action
                FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
                JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
                JOIN sys.tables rt ON rt.object_id=fc.referenced_object_id
                JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
                WHERE f.parent_object_id=OBJECT_ID(@table);
                """;
            var foreignKeys = new Dictionary<string, (string Column, string Target, string TargetColumn, int DeleteAction)>();
            await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync())
                foreignKeys.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetByte(4)));
            Check(foreignKeys.Count == entity.GetForeignKeys().Count(), "FK count mismatch.");
            foreach (var fk in entity.GetForeignKeys())
            {
                var sql = foreignKeys[fk.GetConstraintName()!]; var targetStore = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema());
                Check(sql.Column == fk.Properties.Single().GetColumnName(store) && sql.Target == fk.PrincipalEntityType.GetTableName() &&
                    sql.TargetColumn == fk.PrincipalKey.Properties.Single().GetColumnName(targetStore) && sql.DeleteAction == 0, "FK mismatch: " + table);
                Check(fk.DependentToPrincipal is not null && fk.PrincipalToDependent is not null, "Generated FK navigation missing.");
            }
        }
        foreach (var table in new[] { "inventory_items", "units_of_measure", "uom_conversions", "warehouses", "stock_thresholds", "stock_documents", "stocktakes" })
        {
            using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND name='row_version' AND system_type_id=189 AND is_nullable=0;";
            var p = command.CreateParameter(); p.ParameterName = "@table"; p.Value = "dbo." + table; command.Parameters.Add(p);
            Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "SQL rowversion metadata missing: " + table);
        }
        passed.Add("SQL catalog versus EF: every new column type/precision/nullability, identity, PK/FK/index/navigation and all seven rowversions");
    }
}
