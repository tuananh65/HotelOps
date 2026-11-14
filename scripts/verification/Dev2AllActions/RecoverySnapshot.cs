using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal sealed class RecoverySnapshot
{
    internal sealed class Table(string schema,string name)
    {
        internal string Id=>schema+"."+name;internal string Sql=>Quote(schema)+"."+Quote(name);
        internal List<string> Columns=[];internal List<string> PrimaryKey=[];internal List<Row> Rows=[];
    }
    internal sealed class Row(Table table,Dictionary<string,object?> values)
    {
        internal Table Table{get;}=table;internal Dictionary<string,object?> Values{get;}=values;
        internal string Key=>Table.Id+":"+Tuple(Table.PrimaryKey.Select(c=>Values[c]));
        internal string Hash=>HashText(Tuple(Table.Columns.Select(c=>Values[c])));
    }
    internal sealed record ForeignKey(string Name,string Child,string Parent,string[] ChildColumns,string[] ParentColumns);
    internal Dictionary<string,Table> Tables=new(StringComparer.OrdinalIgnoreCase);internal List<ForeignKey> Keys=[];
    internal static string Quote(string value)=>"["+value.Replace("]","]]")+"]";
    internal static string Tuple(IEnumerable<object?> values)=>JsonSerializer.Serialize(values.ToArray());
    internal static string HashText(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static async Task<RecoverySnapshot> Read(SqlConnection connection,SqlTransaction tx,CancellationToken ct)
    {
        var result=new RecoverySnapshot();
        await using(var cmd=new SqlCommand("SELECT s.name,t.name,c.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.column_id",connection,tx))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))
        {var id=reader.GetString(0)+"."+reader.GetString(1);if(!result.Tables.TryGetValue(id,out var table))result.Tables[id]=table=new(reader.GetString(0),reader.GetString(1));table.Columns.Add(reader.GetString(2));}
        await using(var cmd=new SqlCommand("SELECT s.name,t.name,c.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1 JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id ORDER BY s.name,t.name,ic.key_ordinal",connection,tx))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))result.Tables[reader.GetString(0)+"."+reader.GetString(1)].PrimaryKey.Add(reader.GetString(2));
        var keyRows=new List<(string name,string child,string parent,string childColumn,string parentColumn)>();
        await using(var cmd=new SqlCommand("SELECT fk.name,SCHEMA_NAME(ct.schema_id)+'.'+ct.name,SCHEMA_NAME(pt.schema_id)+'.'+pt.name,cc.name,pc.name FROM sys.foreign_keys fk JOIN sys.foreign_key_columns k ON k.constraint_object_id=fk.object_id JOIN sys.tables ct ON ct.object_id=fk.parent_object_id JOIN sys.tables pt ON pt.object_id=fk.referenced_object_id JOIN sys.columns cc ON cc.object_id=k.parent_object_id AND cc.column_id=k.parent_column_id JOIN sys.columns pc ON pc.object_id=k.referenced_object_id AND pc.column_id=k.referenced_column_id ORDER BY fk.object_id,k.constraint_column_id",connection,tx))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))keyRows.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4)));
        result.Keys=keyRows.GroupBy(x=>(x.name,x.child,x.parent)).Select(g=>new ForeignKey(g.Key.name,g.Key.child,g.Key.parent,g.Select(x=>x.childColumn).ToArray(),g.Select(x=>x.parentColumn).ToArray())).ToList();
        foreach(var table in result.Tables.Values.OrderBy(x=>x.Id,StringComparer.Ordinal))
        {
            // Trial-only, short, exclusive serializable snapshot: prevents unrelated writes racing proof/deletion.
            await using var cmd=new SqlCommand("SELECT "+string.Join(",",table.Columns.Select(Quote))+" FROM "+table.Sql+" WITH(TABLOCKX,HOLDLOCK)",connection,tx){CommandTimeout=30};
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)){var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<table.Columns.Count;i++)values[table.Columns[i]]=reader.IsDBNull(i)?null:reader.GetValue(i);table.Rows.Add(new(table,values));}
        }
        return result;
    }
    internal IEnumerable<(Row child,Row parent)> Edges()
    {
        foreach(var fk in Keys)
        {
            var index=Tables[fk.Parent].Rows.ToDictionary(r=>Tuple(fk.ParentColumns.Select(c=>r.Values[c])),StringComparer.Ordinal);
            foreach(var child in Tables[fk.Child].Rows)
            {
                var values=fk.ChildColumns.Select(c=>child.Values[c]).ToArray();if(values.Any(x=>x is null))continue;
                if(index.TryGetValue(Tuple(values),out var parent))yield return(child,parent);
            }
        }
    }
    internal string PreservedHash(ISet<string> deletedKeys)=>HashText(string.Join("\n",Tables.Values.OrderBy(t=>t.Id,StringComparer.Ordinal).Select(t=>t.Id+":"+string.Join(",",t.Rows.Where(r=>t.PrimaryKey.Count==0||!deletedKeys.Contains(r.Key)).Select(r=>r.Hash).Order(StringComparer.Ordinal)))));
    internal void ValidateOwnedForeignKeys(ISet<Row> owned)
    {
        foreach(var fk in Keys)foreach(var child in Tables[fk.Child].Rows.Where(owned.Contains))
        {
            var values=fk.ChildColumns.Select(c=>child.Values[c]).ToArray();if(values.Any(x=>x is null))continue;
            var key=Tuple(values);if(Tables[fk.Parent].Rows.Count(p=>Tuple(fk.ParentColumns.Select(c=>p.Values[c]))==key)!=1)throw new AcceptanceFailure("RECOVERY_OWNED_ROW_MISSING_PRINCIPAL");
        }
    }
    internal static List<Row> DeleteOrder(ISet<Row> owned,IEnumerable<(Row child,Row parent)> allEdges)
    {
        var edges=allEdges.Where(e=>owned.Contains(e.parent)).ToArray();
        if(edges.Any(e=>!owned.Contains(e.child)))throw new AcceptanceFailure("RECOVERY_EXTERNAL_REFERENCE_BLOCKER");
        var pending=owned.ToHashSet();var order=new List<Row>();
        while(pending.Count>0){var referenced=edges.Where(e=>pending.Contains(e.child)&&pending.Contains(e.parent)&&e.child!=e.parent).Select(e=>e.parent).ToHashSet();var leaves=pending.Where(r=>!referenced.Contains(r)).OrderBy(r=>r.Key,StringComparer.Ordinal).ToArray();if(leaves.Length==0)throw new AcceptanceFailure("RECOVERY_DEPENDENCY_CYCLE");foreach(var row in leaves){pending.Remove(row);order.Add(row);}}
        return order;
    }
    internal static async Task Delete(Row row,SqlConnection connection,SqlTransaction tx,CancellationToken ct)
    {
        if(row.Table.PrimaryKey.Count==0)throw new AcceptanceFailure("RECOVERY_DELETE_REQUIRES_PRIMARY_KEY");
        await using var cmd=new SqlCommand("DELETE FROM "+row.Table.Sql+" WHERE "+string.Join(" AND ",row.Table.PrimaryKey.Select((c,i)=>Quote(c)+"=@p"+i)),connection,tx);
        for(var i=0;i<row.Table.PrimaryKey.Count;i++)cmd.Parameters.AddWithValue("@p"+i,row.Values[row.Table.PrimaryKey[i]]??throw new AcceptanceFailure("RECOVERY_NULL_PRIMARY_KEY"));
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)throw new AcceptanceFailure("RECOVERY_DELETE_ROWCOUNT_MISMATCH");
    }
}
