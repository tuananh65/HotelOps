using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal sealed class FailureDiagnostics
{
    public string Phase {get;init;}="";
    public string? FixtureStage {get;init;}
    public string? SaveChangesBoundary {get;init;}
    public string ExceptionType {get;init;}="";
    public string? InnerExceptionType {get;init;}
    public string[] ExceptionChain {get;init;}=[];
    public object[] SqlErrors {get;init;}=[];
    public string[] EntityTypes {get;init;}=[];
    public string[] AffectedTables {get;init;}=[];
    public string[] ConstraintNames {get;init;}=[];
    public string[] PendingEntityTypes {get;init;}=[];
    public bool TransactionCommitted {get;set;}
    public string TransactionScope {get;}="FIXTURE_CREATION";
    public bool CleanupCompleted {get;set;}
    public string FailureCategory {get;init;}="UNCLASSIFIED";
    public string? FailedOperation {get;init;}
    public string? ValidationCategory {get;init;}
    public string DetailAvailability {get;init;}="No raw exception messages, entity values or HTTP bodies are persisted.";

    internal static FailureDiagnostics Capture(Exception error,string phase,string? stage,string? boundary,
        HotelOpsDbContext? db,string output,IEnumerable<string>? pending=null)
    {
        var chain=new List<Exception>();for(Exception? e=error;e is not null&&chain.Count<12;e=e.InnerException)chain.Add(e);
        var operation=chain.OfType<PreconditionStepFailure>().FirstOrDefault();
        var update=chain.OfType<DbUpdateException>().FirstOrDefault();
        var entries=update?.Entries.ToArray()??[];
        var tables=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(db is not null)foreach(var entity in db.Model.GetEntityTypes())if(entity.GetTableName() is string name)tables.Add((entity.GetSchema()??"dbo")+"."+name);
        foreach(var entry in entries)if(entry.Metadata.GetTableName() is string name)tables.Add((entry.Metadata.GetSchema()??"dbo")+"."+name);
        var constraints=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Only names from independently captured schema metadata may leave a SQL message.
        var snapshot=Path.Combine(output,"SQL_SCHEMA_SNAPSHOT.json");
        try
        {
            if(File.Exists(snapshot))
            {
            using var document=JsonDocument.Parse(File.ReadAllText(snapshot));
            foreach(var group in new[]{"checks","foreignKeys","indexes"})
                if(document.RootElement.TryGetProperty(group,out var rows))foreach(var row in rows.EnumerateArray())
                {
                    foreach(var key in new[]{"name","index_name"})if(row.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString() is string name)constraints.Add(name);
                    if(row.TryGetProperty("table_name",out var table)&&table.ValueKind==JsonValueKind.String)tables.Add("dbo."+table.GetString());
                }
            }
        }
        catch(Exception metadataError) when(metadataError is JsonException or IOException or UnauthorizedAccessException)
        { /* Provider numbers and EF metadata remain available; never replace the original failure. */ }
        var sql=chain.OfType<SqlException>().FirstOrDefault();var foundTables=new HashSet<string>();var foundConstraints=new HashSet<string>();
        var errors=new List<object>();
        if(sql is not null)foreach(SqlError item in sql.Errors)
        {
            var names=ExtractKnownIdentifiers(item.Message,tables,constraints);
            foundTables.UnionWith(names.tables);foundConstraints.UnionWith(names.constraints);
            errors.Add(new{number=item.Number,state=item.State,severity=item.Class,line=item.LineNumber});
        }
        foreach(var entry in entries)if(entry.Metadata.GetTableName() is string table)foundTables.Add((entry.Metadata.GetSchema()??"dbo")+"."+table);
        return new(){Phase=phase,FixtureStage=stage,SaveChangesBoundary=boundary,ExceptionType=error.GetType().Name,
            InnerExceptionType=error.InnerException?.GetType().Name,ExceptionChain=chain.Select(e=>e.GetType().Name).ToArray(),
            SqlErrors=errors.ToArray(),EntityTypes=entries.Select(e=>e.Metadata.ClrType.Name).Distinct().Order().ToArray(),
            PendingEntityTypes=(pending??[]).Distinct().Order().ToArray(),AffectedTables=foundTables.Order().ToArray(),ConstraintNames=foundConstraints.Order().ToArray(),
            FailedOperation=operation?.Step,ValidationCategory=operation?.Category,
            FailureCategory=sql is null?operation is not null?operation.Category:error is AcceptanceFailure failure?failure.Category:update is not null?"EF_UPDATE_WITHOUT_SQL_DETAILS":"HARNESS_OR_PREFLIGHT":sql.Number switch{547=>"SQL_CONSTRAINT",2601 or 2627=>"SQL_UNIQUE",515=>"SQL_REQUIRED_COLUMN",2628 or 8152=>"SQL_TRUNCATION",1205=>"SQL_DEADLOCK",_=>"SQL_PROVIDER"}};
    }

    internal static (string[] tables,string[] constraints) ExtractKnownIdentifiers(string message,ISet<string> tables,ISet<string> constraints)
    {
        var foundTables=new HashSet<string>();var foundConstraints=new HashSet<string>();
        foreach(Match match in Regex.Matches(message,"(?:table|object)\\s+[\"'](?<name>[^\"']+)[\"']",RegexOptions.IgnoreCase))
        {
            var parts=match.Groups["name"].Value.Replace("[","").Replace("]","").Split('.');
            var name=parts.Length>=2?string.Join('.',parts.TakeLast(2)):"dbo."+parts[0];if(tables.Contains(name))foundTables.Add(name);
        }
        foreach(Match match in Regex.Matches(message,"(?:constraint|index)\\s+[\"'](?<name>[^\"']+)[\"']",RegexOptions.IgnoreCase))
            if(constraints.Contains(match.Groups["name"].Value))foundConstraints.Add(match.Groups["name"].Value);
        return(foundTables.Order().ToArray(),foundConstraints.Order().ToArray());
    }
}

internal sealed class AcceptanceFailure(string category):Exception("Structured acceptance failure")
{
    internal string Category {get;}=category;
}
