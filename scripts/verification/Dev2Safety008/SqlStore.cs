using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using HotelOps.Api.Data;

namespace Safety008;

internal sealed class SqlStore(SqlConnection connection,Durable journal)
{
    internal const string Database="HotelOps_DEV2_FullStack_Trial";
    internal static readonly Guid DatabaseGuid=Guid.Parse("6e68f686-d7c1-4002-94d9-a58fa73175da");
    internal const string LeaseResource="HotelOps:DEV2:FixtureHarness";
    internal SqlConnection Connection=>connection;
    internal static string Quote(string s)=>"["+s.Replace("]","]]",StringComparison.Ordinal)+"]";
    private static string TableName(string s)=>string.Join('.',s.Split('.').Select(Quote));
    internal async Task Identity(SqlTransaction? tx=null)
    {
        var rows=await Query("SELECT DB_NAME() db,CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) machine,CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')) instance,CONVERT(nvarchar(36),database_guid) guid FROM sys.database_recovery_status WHERE database_id=DB_ID()",tx);
        if(rows.Count!=1||rows[0]["db"]!=Database||rows[0]["machine"]!="KIENPC"||rows[0]["instance"]!=null||!Guid.TryParseExact(rows[0]["guid"],"D",out var guid)||guid!=DatabaseGuid)throw new InvalidOperationException("EXACT_TRIAL_IDENTITY_REQUIRED");
    }
    internal async Task<List<Dictionary<string,string?>>> Query(string sql,SqlTransaction? tx=null,Dictionary<string,object?>? parameters=null,CancellationToken cancellationToken=default)
    {
        using var cmd=connection.CreateCommand();cmd.CommandText=sql;cmd.Transaction=tx;cmd.CommandTimeout=15;
        if(parameters!=null)foreach(var (name,value) in parameters)cmd.Parameters.AddWithValue(name,value??DBNull.Value);
        await using var reader=await cmd.ExecuteReaderAsync(cancellationToken);var rows=new List<Dictionary<string,string?>>();
        while(await reader.ReadAsync(cancellationToken)){
            if(rows.Count>=100000)throw new InvalidOperationException("ROW_LIMIT_NO_PARTIAL_PROOF");
            var r=new Dictionary<string,string?>();for(var i=0;i<reader.FieldCount;i++)r.Add(reader.GetName(i),reader.IsDBNull(i)?null:Convert.ToString(reader.GetValue(i),CultureInfo.InvariantCulture));rows.Add(r);
        }return rows;
    }
    internal async Task AcquireLease()
    {
        await Identity();var r=await Query("DECLARE @n int; EXEC @n=sys.sp_getapplock @Resource=N'HotelOps:DEV2:FixtureHarness',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; SELECT @n result;");
        if(int.Parse(r.Single()["result"]!,CultureInfo.InvariantCulture)<0)throw new InvalidOperationException("LEASE_UNAVAILABLE");
        await journal.Event("LEASE_ACQUIRED",new{resource=LeaseResource,serverProcessId=connection.ServerProcessId});
    }
    internal async Task RequireOwnLease()
    {
        var r=await Query("SELECT APPLOCK_MODE(N'public',N'HotelOps:DEV2:FixtureHarness',N'Session') mode");
        if(r.Single()["mode"]!="Exclusive")throw new InvalidOperationException("EXCLUSIVE_SESSION_LEASE_NOT_HELD");
    }
    internal async Task ReleaseLease()
    {
        var r=await Query("DECLARE @n int; EXEC @n=sys.sp_releaseapplock @Resource=N'HotelOps:DEV2:FixtureHarness',@LockOwner=N'Session'; SELECT @n result;");
        if(int.Parse(r.Single()["result"]!,CultureInfo.InvariantCulture)<0)throw new InvalidOperationException("LEASE_RELEASE_UNCONFIRMED");
        var mode=(await Query("SELECT APPLOCK_MODE(N'public',N'HotelOps:DEV2:FixtureHarness',N'Session') mode")).Single()["mode"];
        if(mode!="NoLock")throw new InvalidOperationException("LEASE_STILL_HELD");await journal.Event("LEASE_RELEASE_VERIFIED",new{resource=LeaseResource});
    }
    internal async Task Quiet(SqlTransaction? tx=null,bool requireNoOtherSessions=true)
    {
        var permission=await Query("SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin')=1 OR (HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER STATE')=1 AND (CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))<16 OR HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER PERFORMANCE STATE')=1)) THEN 1 ELSE 0 END allowed",tx);
        if(permission.Single()["allowed"]!="1")throw new InvalidOperationException("GLOBAL_DMV_VISIBILITY_REQUIRED");
        var activity=await Query("SELECT s.session_id,s.open_transaction_count,r.command FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id WHERE s.session_id<>@@SPID AND s.is_user_process=1 AND (s.database_id=DB_ID() OR r.database_id=DB_ID() OR EXISTS(SELECT 1 FROM sys.dm_tran_locks l WHERE l.request_session_id=s.session_id AND l.resource_database_id=DB_ID()))",tx);
        if(activity.Any(r=>requireNoOtherSessions||r["command"]!=null||r["open_transaction_count"]!="0"))throw new InvalidOperationException("FOREIGN_TRIAL_SESSION_OR_WRITER");
        var locks=await Query("SELECT request_session_id FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND request_session_id<>@@SPID AND (resource_type='APPLICATION' OR request_mode IN('X','IX','SIX','U','SIU','UIX','BU'))",tx);
        if(locks.Count>0)throw new InvalidOperationException("FOREIGN_TRIAL_LOCK_OWNER");
        var transactions=await Query("SELECT dt.transaction_id FROM sys.dm_tran_database_transactions dt LEFT JOIN sys.dm_tran_session_transactions st ON st.transaction_id=dt.transaction_id WHERE dt.database_id=DB_ID() AND (st.session_id IS NULL OR st.session_id<>@@SPID)",tx);
        if(transactions.Count>0)throw new InvalidOperationException("FOREIGN_OR_UNKNOWN_TRIAL_TRANSACTION");
    }
    internal async Task<Snapshot> Capture(SqlTransaction? tx=null,bool exclusive=false)
    {
        await Identity(tx);
        var columns=await Query("SELECT SCHEMA_NAME(t.schema_id)+'.'+t.name tbl,c.name col,ty.name typ,c.column_id ord,c.is_nullable nullable,c.max_length len,c.precision prec,c.scale scale,c.is_computed computed,c.collation_name coll,COALESCE(ic.key_ordinal,0) pk,dc.definition def FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1 LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.is_ms_shipped=0 ORDER BY tbl,c.column_id",tx);
        var fks=await Query("SELECT CONVERT(varchar(20),f.object_id) id,f.name name,SCHEMA_NAME(c.schema_id)+'.'+c.name child,SCHEMA_NAME(p.schema_id)+'.'+p.name parent,cc.name cc,pc.name pc,k.constraint_column_id ord,f.is_disabled disabled,f.is_not_trusted untrusted,f.delete_referential_action_desc del FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON k.constraint_object_id=f.object_id JOIN sys.tables c ON c.object_id=f.parent_object_id JOIN sys.tables p ON p.object_id=f.referenced_object_id JOIN sys.columns cc ON cc.object_id=k.parent_object_id AND cc.column_id=k.parent_column_id JOIN sys.columns pc ON pc.object_id=k.referenced_object_id AND pc.column_id=k.referenced_column_id ORDER BY f.object_id,k.constraint_column_id",tx);
        var checks=await Query("SELECT SCHEMA_NAME(t.schema_id)+'.'+t.name tbl,c.name name,c.definition def,c.is_disabled disabled,c.is_not_trusted untrusted FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id ORDER BY tbl,c.name",tx);
        if(checks.Any(c=>c["disabled"]=="True"||c["untrusted"]=="True"))throw new InvalidOperationException("UNTRUSTED_CHECK_CONSTRAINT");
        var triggers=await Query("SELECT SCHEMA_NAME(t.schema_id)+'.'+t.name tbl,tr.name name,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(tr.object_id))),2) hash FROM sys.triggers tr JOIN sys.tables t ON t.object_id=tr.parent_id WHERE tr.is_disabled=0 ORDER BY tbl,tr.name",tx);
        var schemaHash=Durable.Hash(JsonSerializer.Serialize(new{columns,fks,checks,triggers}));
        var tables=columns.GroupBy(c=>c["tbl"]!).Select(g=>new Table(g.Key,g.Select(c=>new Column(c["col"]!,c["typ"]!,int.Parse(c["pk"]!,CultureInfo.InvariantCulture))).ToArray())).OrderBy(t=>t.Name,StringComparer.Ordinal).ToArray();
        var foreign=fks.GroupBy(f=>f["id"]!).Select(g=>{var r=g.First();return new ForeignKey(r["name"]!,r["child"]!,r["parent"]!,g.Select(f=>f["cc"]!).ToArray(),g.Select(f=>f["pc"]!).ToArray(),r["disabled"]=="True",r["untrusted"]=="True",r["del"]!);}).ToArray();
        var rows=new Dictionary<string,Row>();
        foreach(var table in tables){
            var selected=table.Columns.Where(c=>table.Pk.Contains(c.Name)||foreign.Any(f=>f.Child==table.Name&&f.ChildColumns.Contains(c.Name)||f.Parent==table.Name&&f.ParentColumns.Contains(c.Name))||c.Name.EndsWith("_id",StringComparison.Ordinal)||c.Name.EndsWith("_by",StringComparison.Ordinal)||c.Name.EndsWith("_type",StringComparison.Ordinal)||new[]{"action","group_code","value_code","permission_code","module_code","resource_code","data_scope"}.Contains(c.Name)).ToArray();
            if(selected.Any(c=>System.Text.RegularExpressions.Regex.IsMatch(c.Name,"password|secret|token|session_key",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))throw new InvalidOperationException("SENSITIVE_PK_OR_REFERENCE_UNSUPPORTED");
            var projections=selected.Select(c=>SqlValue(c)+" AS "+Quote(c.Name)).ToList();
            projections.Add("CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT a.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))),2) AS __hash");
            var values=await Query("SELECT "+string.Join(',',projections)+" FROM "+TableName(table.Name)+" a"+(exclusive?" WITH (TABLOCKX,HOLDLOCK)":""),tx);
            if(table.Pk.Length==0&&values.Count>0)throw new InvalidOperationException("UNKEYED_TABLE_NO_COMPLETE_PROOF");
            foreach(var value in values){var pk=new SortedDictionary<string,string?>(StringComparer.Ordinal);foreach(var c in table.Pk){if(value[c]==null)throw new InvalidOperationException("NULL_PK");pk.Add(c,value[c]);}var hash=value["__hash"]!;value.Remove("__hash");var row=new Row(table.Name,pk,value,hash);rows.Add(row.Key,row);}
        }
        return new(schemaHash,tables,foreign,rows,triggers.Select(t=>t["tbl"]+":"+t["name"]).ToArray());
    }
    private static string SqlValue(Column c)
    {
        var column="a."+Quote(c.Name);
        if(c.PrimaryOrdinal>0&&!new[]{"bigint","int","smallint","tinyint","uniqueidentifier","varchar","nvarchar","char","nchar","binary","varbinary"}.Contains(c.Type))throw new InvalidOperationException("UNSUPPORTED_PK_ENCODING");
        return c.Type is "binary" or "varbinary" or "timestamp" or "rowversion"?"CONVERT(varchar(max),"+column+",2)":c.Type=="uniqueidentifier"?"LOWER(CONVERT(nvarchar(36),"+column+"))":"CONVERT(nvarchar(max),"+column+",126)";
    }
    internal async Task ValidateEf(HotelOpsDbContext context,Snapshot snapshot)
    {
        await Task.CompletedTask;
        foreach(var entity in context.Model.GetEntityTypes()){
            var table=entity.GetSchema()??"dbo";var name=entity.GetTableName();if(name==null)continue;var sql=snapshot.Tables.SingleOrDefault(t=>t.Name==table+"."+name)??throw new InvalidOperationException("EF_TABLE_MISSING");
            var store=StoreObjectIdentifier.Table(name,entity.GetSchema());var pk=entity.FindPrimaryKey()?.Properties.Select(p=>p.GetColumnName(store)!).ToArray()??[];
            if(!pk.SequenceEqual(sql.Pk))throw new InvalidOperationException("EF_PK_MISMATCH");
            foreach(var fk in entity.GetForeignKeys()){
                var parent=fk.PrincipalEntityType;var parentName=parent.GetTableName()!;var parentSchema=parent.GetSchema()??"dbo";var ps=StoreObjectIdentifier.Table(parentName,parent.GetSchema());
                var cc=fk.Properties.Select(p=>p.GetColumnName(store)!).ToArray();var pc=fk.PrincipalKey.Properties.Select(p=>p.GetColumnName(ps)!).ToArray();
                if(!snapshot.ForeignKeys.Any(f=>f.Child==sql.Name&&f.Parent==parentSchema+"."+parentName&&f.ChildColumns.SequenceEqual(cc)&&f.ParentColumns.SequenceEqual(pc)&&!f.Disabled&&!f.Untrusted))throw new InvalidOperationException("EF_FK_MISMATCH");
            }
        }
    }
    internal async Task DeleteExact(Row row,Table table,SqlTransaction tx,CancellationToken cancellationToken=default)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;cmd.CommandTimeout=15;
        cmd.CommandText="DELETE FROM "+TableName(row.Table)+" WHERE "+string.Join(" AND ",table.Pk.Select((c,i)=>SqlValue(table.Columns.Single(x=>x.Name==c)).Replace("a.","",StringComparison.Ordinal)+"=@p"+i));
        for(var i=0;i<table.Pk.Length;i++)cmd.Parameters.AddWithValue("@p"+i,row.Pk[table.Pk[i]]!);
        if(await cmd.ExecuteNonQueryAsync(cancellationToken)!=1)throw new InvalidOperationException("EXACT_PK_DELETE_ROWCOUNT_MISMATCH");
    }
}
