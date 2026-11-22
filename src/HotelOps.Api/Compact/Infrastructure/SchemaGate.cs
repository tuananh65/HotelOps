using HotelOps.Api.Compact.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Infrastructure;
public static class SchemaGate
{
    public static async Task Validate(HotelOpsCompactDbContext db,CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try {
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.CommandText="""
                SELECT CASE WHEN DB_NAME() LIKE 'HotelOpsCompact%'
                AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(nvarchar(20),value)='2.1.0')
                AND (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)=40
                AND (SELECT COUNT(*) FROM sys.foreign_keys)=160
                AND (SELECT COUNT(*) FROM sys.check_constraints)=82
                AND (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1)=27
                AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
                AND NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
                AND NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1)
                THEN 1 ELSE 0 END
                """;
            if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Compact v2 schema required; HTTP listener was not started. No schema changes were attempted.");
            // Compile an empty query for every scalar mapping, including all six views.
            foreach(var entity in db.Model.GetEntityTypes())
            {
                var isView=entity.GetViewName()!=null;
                var table=isView?entity.GetViewName()!:entity.GetTableName()!;
                var schema=(isView?entity.GetViewSchema():entity.GetSchema())??"dbo";
                var store=isView?Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.View(table,entity.GetViewSchema()):Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table,entity.GetSchema());
                var columns=entity.GetProperties().Select(p=>"["+p.GetColumnName(store)+"]");
                command.CommandText=$"SELECT TOP (0) {string.Join(',',columns)} FROM [{schema}].[{table}]";
                await using var reader=await command.ExecuteReaderAsync(ct);
            }
        } finally{await db.Database.CloseConnectionAsync();}
    }
}
